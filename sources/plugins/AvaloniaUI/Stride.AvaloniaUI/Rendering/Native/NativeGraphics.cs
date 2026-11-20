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
/// What a native canvas keeps on one graphics device: the device's shared resources of the canvas, the buffer of the quads,
/// and the textures the fallback runs are uploaded to.
/// </summary>
internal sealed class NativeGraphics : IDisposable
{
    private static readonly Logger Log = GlobalLogger.GetLogger("AvaloniaUI");

    // A free texture of the pool unused for this many draws goes
    private const int PoolLifetime = 120;
    // The textures of the pool have sizes in steps of this many pixels, so runs of close sizes share them
    private const int PoolGranularity = 64;

    private readonly List<(Texture Texture, int LastUse)> freeTextures = [];
    private readonly HashSet<NativeTextureRenderTarget> renderTargets = [];
    private readonly HashSet<Type> reportedFailures = [];
    private Buffer? instanceBuffer;
    private int draws;

    private Texture[] savedRenderTargets = [];
    private Viewport[] savedViewports = [];
    private Rectangle[] savedScissors = [];

    /// <summary>
    /// Gets why the canvas can't draw on a device of a profile, or <c>null</c> when it can: it needs Direct3D 10 features,
    /// and the canvas shader compiled for the device's graphics API.
    /// </summary>
    public static string? GetReasonNotToDraw(GraphicsProfile profile, bool hasBytecode)
    {
        if (profile < GraphicsProfile.Level_10_0)
            return $"its profile is {profile}, below {GraphicsProfile.Level_10_0}";
        if (!hasBytecode)
            return $"the canvas shader has no bytecode for {GraphicsDevice.Platform}";
        return null;
    }

    /// <summary>
    /// Gets why the canvas can't draw on a device, or <c>null</c> when it can.
    /// </summary>
    public static string? GetReasonNotToDraw(GraphicsDevice device)
        => GetReasonNotToDraw(device.Features.CurrentProfile, AvaloniaCanvasEffect.Bytecode != null);

    /// <summary>
    /// Gets whether the canvas can draw on a device of a profile (<see cref="GetReasonNotToDraw(GraphicsProfile, bool)"/>).
    /// </summary>
    public static bool CanDraw(GraphicsProfile profile, bool hasBytecode) => GetReasonNotToDraw(profile, hasBytecode) == null;

    /// <exception cref="InvalidOperationException">The device's graphics API has no bytecode of the canvas shader.</exception>
    public NativeGraphics(GraphicsDevice device)
    {
        Device = device;
        Resources = CanvasDeviceResources.Get(device);
    }

    public GraphicsDevice Device { get; }

    /// <summary>
    /// Gets what the canvases of the device share, disposed with the device.
    /// </summary>
    internal CanvasDeviceResources Resources { get; }

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
        var renderTargetCount = Save(commandList.RenderTargets, ref savedRenderTargets);
        var viewportCount = Save(commandList.Viewports, ref savedViewports);
        var scissorCount = Save(commandList.Scissors, ref savedScissors);

        commandList.ResourceBarrierTransition(target, BarrierLayout.RenderTarget);
        commandList.SetRenderTargetAndViewport(null, target);
        Resources.Effect.Parameters.Set(AvaloniaCanvasShaderKeys.TargetSize, new Vector2(target.Width, target.Height));
        foreach (var segment in segments)
        {
            commandList.SetPipelineState(segment.Opaque ? Resources.Opaque : Resources.Blended);
            commandList.SetScissorRectangle(segment.Scissor);
            Resources.Effect.Parameters.Set(TexturingKeys.Texture0, segment.Texture ?? Resources.White);
            var clip = CanvasBatch.GetClipConstants(segment.Clip);
            Resources.Effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipX, clip.X);
            Resources.Effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipY, clip.Y);
            Resources.Effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipRect, clip.Rect);
            Resources.Effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipRadiiTop, clip.RadiiTop);
            Resources.Effect.Parameters.Set(AvaloniaCanvasShaderKeys.ClipRadiiBottom, clip.RadiiBottom);
            Resources.Effect.Apply(context);
            commandList.SetVertexBuffer(0, instanceBuffer, 0, CanvasInstance.Size);
            commandList.DrawInstanced(4, segment.Count, 0, segment.Start);
        }
        // The textures of the runs stay referenced by nothing
        Resources.Effect.Parameters.Set(TexturingKeys.Texture0, Resources.White);

        if (renderTargetCount == 0 && depthStencil == null)
            commandList.ResetTargets();
        else
            commandList.SetRenderTargets(depthStencil, savedRenderTargets.AsSpan(0, renderTargetCount));
        if (viewportCount > 0)
            commandList.SetViewports(savedViewports.AsSpan(0, viewportCount));
        if (scissorCount > 0)
            commandList.SetScissorRectangles(savedScissors.AsSpan(0, scissorCount));
        // The game's targets stay referenced by nothing
        savedRenderTargets.AsSpan(0, renderTargetCount).Clear();
        draws++;
    }

    // Copies a part of the command list's state into an array kept from draw to draw, grown when needed; gives its length
    private static int Save<T>(ReadOnlySpan<T> state, ref T[] saved)
    {
        if (saved.Length < state.Length)
            saved = new T[state.Length];
        state.CopyTo(saved);
        return state.Length;
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
    /// Releases what this canvas made on the device, the textures of the render targets included: they report themselves
    /// corrupted. The device's shared resources stay, for the next canvas of the device.
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
    }
}
