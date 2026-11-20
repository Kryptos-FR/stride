// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Buffer = Stride.Graphics.Buffer;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// What the native canvas keeps on one graphics device: the canvas shader and its pipeline states, the buffer of the quads,
/// and the textures the fallback runs are uploaded to.
/// </summary>
internal sealed class NativeGraphics : IDisposable
{
    private static readonly Logger Log = GlobalLogger.GetLogger("AvaloniaUI");

    // A free texture of the pool unused for this many draws goes
    private const int PoolLifetime = 120;
    // The textures of the pool have sizes in steps of this many pixels, so runs of close sizes share them
    private const int PoolGranularity = 64;

    private readonly EffectInstance effect;
    private readonly MutablePipelineState blended;
    private readonly MutablePipelineState opaque;
    private readonly Texture white;
    private readonly List<(Texture Texture, int LastUse)> freeTextures = [];
    private readonly HashSet<NativeTextureRenderTarget> renderTargets = [];
    private readonly HashSet<Type> reportedFailures = [];
    private Buffer? instanceBuffer;
    private int draws;

    /// <summary>
    /// Gets whether the canvas can draw on a device of a profile: it needs Direct3D 10 features, and the canvas shader
    /// compiled for the device's graphics API.
    /// </summary>
    public static bool CanDraw(GraphicsProfile profile, bool hasBytecode) => profile >= GraphicsProfile.Level_10_0 && hasBytecode;

    /// <summary>
    /// Gets whether the canvas can draw on a device.
    /// </summary>
    public static bool CanDraw(GraphicsDevice device) => CanDraw(device.Features.CurrentProfile, AvaloniaCanvasEffect.Bytecode != null);

    /// <exception cref="InvalidOperationException">The device's graphics API has no bytecode of the canvas shader.</exception>
    public NativeGraphics(GraphicsDevice device)
    {
        Device = device;
        var bytecode = AvaloniaCanvasEffect.Bytecode
            ?? throw new InvalidOperationException($"The canvas shader of the Avalonia UI has no bytecode for {GraphicsDevice.Platform}.");
        effect = new EffectInstance(new Effect(device, bytecode) { Name = "AvaloniaCanvasEffect" });
        effect.UpdateEffect(device);
        blended = CreatePipeline(BlendStates.AlphaBlend);
        opaque = CreatePipeline(BlendStates.Opaque);
        white = Texture.New2D(device, 1, 1, PixelFormat.R8G8B8A8_UNorm, new[] { 0xFFFFFFFFu });
    }

    public GraphicsDevice Device { get; }

    /// <summary>
    /// Gets or sets the game's graphics context, which the canvas records its commands in; set while Avalonia renders.
    /// </summary>
    public GraphicsContext? Context { get; set; }

    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Draws quads into a texture, in order, with the scissor, the texture and the clip of each segment.
    /// </summary>
    internal void Draw(Texture target, ReadOnlySpan<CanvasInstance> instances, ReadOnlySpan<CanvasSegment> segments)
    {
        if (Context is not { } context || instances.IsEmpty)
            return;

        var commandList = context.CommandList;
        var size = instances.Length * CanvasInstance.Size;
        if (instanceBuffer == null || instanceBuffer.SizeInBytes < size)
        {
            instanceBuffer?.Dispose();
            instanceBuffer = Buffer.Vertex.New<CanvasInstance>(Device, Math.Max(instances.Length, 64) * 2, GraphicsResourceUsage.Default);
        }
        instanceBuffer.SetData<CanvasInstance>(commandList, instances);
        // The copy leaves the buffer as a copy's destination on Direct3D 12: the draws read it only after this barrier, and
        // the next view's copy into it waits for them
        commandList.ResourceBarrierTransition(instanceBuffer, BarrierLayout.Common);

        // The game's targets come back once the canvas has drawn
        var depthStencil = commandList.DepthStencilBuffer;
        var renderTargets = commandList.RenderTargets.ToArray();
        var viewports = commandList.Viewports.ToArray();
        var scissors = commandList.Scissors.ToArray();

        commandList.ResourceBarrierTransition(target, BarrierLayout.RenderTarget);
        commandList.SetRenderTargetAndViewport(null, target);
        effect.Parameters.Set(AvaloniaCanvasShaderKeys.TargetSize, new Vector2(target.Width, target.Height));
        foreach (var segment in segments)
        {
            commandList.SetPipelineState(segment.Opaque ? opaque.CurrentState : blended.CurrentState);
            commandList.SetScissorRectangle(segment.Scissor);
            effect.Parameters.Set(TexturingKeys.Texture0, segment.Texture ?? white);
            var clip = CanvasBatch.GetClipConstants(segment.Clip);
            effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipX, clip.X);
            effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipY, clip.Y);
            effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipRect, clip.Rect);
            effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipRadiiTop, clip.RadiiTop);
            effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipRadiiBottom, clip.RadiiBottom);
            effect.Apply(context);
            commandList.SetVertexBuffer(0, instanceBuffer, 0, CanvasInstance.Size);
            commandList.DrawInstanced(4, segment.Count, 0, segment.Start);
        }
        // The textures of the runs stay referenced by nothing
        effect.Parameters.Set(TexturingKeys.Texture0, white);

        if (renderTargets.Length == 0 && depthStencil == null)
            commandList.ResetTargets();
        else
            commandList.SetRenderTargets(depthStencil, renderTargets);
        if (viewports.Length > 0)
            commandList.SetViewports(viewports);
        if (scissors.Length > 0)
            commandList.SetScissorRectangles(scissors);
        draws++;
    }

    /// <summary>
    /// Takes a texture of the pool of at least a size, to upload the pixels of a run to.
    /// </summary>
    internal Texture RentTexture(int width, int height)
    {
        var best = -1;
        for (var i = 0; i < freeTextures.Count; i++)
        {
            var texture = freeTextures[i].Texture;
            if (texture.Width >= width && texture.Height >= height
                && (best < 0 || texture.Width * texture.Height < freeTextures[best].Texture.Width * freeTextures[best].Texture.Height))
                best = i;
        }
        if (best >= 0)
        {
            var texture = freeTextures[best].Texture;
            freeTextures.RemoveAt(best);
            return texture;
        }

        return Texture.New2D(Device, RoundUp(width), RoundUp(height), PixelFormat.R8G8B8A8_UNorm, TextureFlags.ShaderResource);

        static int RoundUp(int value) => (value + PoolGranularity - 1) / PoolGranularity * PoolGranularity;
    }

    /// <summary>
    /// Gives back the textures of a frame once its draws are recorded; those the pool kept too long unused go.
    /// </summary>
    internal void ReturnTextures(List<Texture> textures)
    {
        foreach (var texture in textures)
            freeTextures.Add((texture, draws));
        textures.Clear();

        for (var i = freeTextures.Count - 1; i >= 0; i--)
        {
            if (draws - freeTextures[i].LastUse > PoolLifetime)
            {
                freeTextures[i].Texture.Dispose();
                freeTextures.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Reports a fallback run that failed, once per kind of failure: its drawing is lost, the frame goes on.
    /// </summary>
    internal void ReportFailure(Exception exception)
    {
        if (reportedFailures.Add(exception.GetType()))
            Log.Error("The fallback of the Avalonia UI failed to draw a part of a frame; that part is not drawn.", exception);
    }

    internal void AddRenderTarget(NativeTextureRenderTarget renderTarget) => renderTargets.Add(renderTarget);

    internal void RemoveRenderTarget(NativeTextureRenderTarget renderTarget) => renderTargets.Remove(renderTarget);

    /// <summary>
    /// Releases everything on the device, the textures of the render targets included: they report themselves corrupted.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        Context = null;

        foreach (var renderTarget in renderTargets.ToArray())
            renderTarget.ReleaseTexture();
        renderTargets.Clear();
        foreach (var (texture, _) in freeTextures)
            texture.Dispose();
        freeTextures.Clear();
        instanceBuffer?.Dispose();
        instanceBuffer = null;
        white.Dispose();
        effect.Dispose();
    }

    private MutablePipelineState CreatePipeline(BlendStateDescription blendState)
    {
        var pipeline = new MutablePipelineState(Device);
        var state = pipeline.State;
        state.SetDefaults();
        state.RootSignature = effect.RootSignature;
        state.EffectBytecode = effect.Effect.Bytecode;
        state.BlendState = blendState;
        state.RasterizerState = RasterizerStates.CullNone with { ScissorTestEnable = true };
        state.DepthStencilState = DepthStencilStates.None;
        state.PrimitiveType = PrimitiveType.TriangleStrip;
        state.InputElements = CanvasInstance.InputElements;
        state.Output = new RenderOutputDescription(PixelFormat.R8G8B8A8_UNorm);
        pipeline.Update();
        return pipeline;
    }
}
