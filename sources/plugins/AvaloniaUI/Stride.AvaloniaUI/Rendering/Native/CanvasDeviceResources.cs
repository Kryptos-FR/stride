// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Graphics;
using Stride.Rendering;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// What the native canvas keeps once per graphics device: the canvas shader, its two pipeline states and a white texture.
/// </summary>
/// <remarks>
/// They are kept in the device's shared data, under the key of this type, and disposed with the device, so that the canvas
/// made again on each move between the GPU and the CPU adds nothing to the device.
/// </remarks>
internal sealed class CanvasDeviceResources : IDisposable
{
    private CanvasDeviceResources(GraphicsDevice device)
    {
        var bytecode = AvaloniaCanvasEffect.Bytecode
            ?? throw new InvalidOperationException($"The canvas shader of the Avalonia UI has no bytecode for {GraphicsDevice.Platform}.");
        try
        {
            Effect = new EffectInstance(new Effect(device, bytecode) { Name = "AvaloniaCanvasEffect" });
            Effect.UpdateEffect(device);
            Blended = CreatePipeline(device, Effect, BlendStates.AlphaBlend);
            Opaque = CreatePipeline(device, Effect, BlendStates.Opaque);
            White = Texture.New2D(device, 1, 1, PixelFormat.R8G8B8A8_UNorm, new[] { 0xFFFFFFFFu });
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the resources of a device, made at the first call.
    /// </summary>
    /// <exception cref="InvalidOperationException">The device's graphics API has no bytecode of the canvas shader.</exception>
    public static CanvasDeviceResources Get(GraphicsDevice device)
        => device.GetOrCreateSharedData(typeof(CanvasDeviceResources), static device => new CanvasDeviceResources(device));

    /// <summary>
    /// Gets the canvas shader, whose parameters each draw sets.
    /// </summary>
    public EffectInstance Effect { get; }

    /// <summary>
    /// Gets the pipeline state of the quads blended over the target with premultiplied alpha.
    /// </summary>
    public PipelineState Blended { get; }

    /// <summary>
    /// Gets the pipeline state of the quads that replace the target's pixels.
    /// </summary>
    public PipelineState Opaque { get; }

    /// <summary>
    /// Gets a texture of one white texel, sampled by the draws that have no texture.
    /// </summary>
    public Texture White { get; }

    public void Dispose()
    {
        // A constructor that throws leaves what it didn't make null
        White?.Dispose();
        Opaque?.Dispose();
        Blended?.Dispose();
        Effect?.Dispose();
    }

    private static PipelineState CreatePipeline(GraphicsDevice device, EffectInstance effect, BlendStateDescription blendState)
    {
        var state = new PipelineStateDescription();
        state.SetDefaults();
        state.RootSignature = effect.RootSignature;
        state.EffectBytecode = effect.Effect.Bytecode;
        state.BlendState = blendState;
        state.RasterizerState = RasterizerStates.CullNone with { ScissorTestEnable = true };
        state.DepthStencilState = DepthStencilStates.None;
        state.PrimitiveType = PrimitiveType.TriangleStrip;
        state.InputElements = CanvasInstance.InputElements;
        state.Output = new RenderOutputDescription(PixelFormat.R8G8B8A8_UNorm);
        return PipelineState.New(device, state);
    }
}
