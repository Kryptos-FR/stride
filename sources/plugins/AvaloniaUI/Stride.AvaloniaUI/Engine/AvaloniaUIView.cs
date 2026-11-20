// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Embedding;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Stride.AvaloniaUI.Platform;
using Stride.Core.Diagnostics;
using Stride.Core.Reflection;
using Stride.Graphics;
using Stride.Rendering;
using PixelFormat = Stride.Graphics.PixelFormat;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace Stride.AvaloniaUI;

/// <summary>
/// The Avalonia top level of an <see cref="AvaloniaUIComponent"/>, and the texture the game draws its frames from.
/// </summary>
internal sealed class AvaloniaUIView : IDisposable
{
    private static readonly Logger Log = GlobalLogger.GetLogger("AvaloniaUI");

    private readonly StrideTopLevelImpl impl = new();
    private readonly EmbeddableControlRoot root;
    // The texture the CPU frames are uploaded to, and the sRGB view or copy of the texture of the GPU frames
    private Texture? uploadTexture;
    private Texture? gpuFrameView;
    private Texture? gpuFrameCopy;
    private int uploadedVersion;
    private int copiedVersion;
    private string? failedViewType;
    private bool disposed;

    public AvaloniaUIView(AvaloniaUIComponent component)
    {
        Component = component;
        root = new EmbeddableControlRoot(impl)
        {
            // Nothing drawn and nothing hit where no control is, so the game shows through and gets the input there
            Background = null,
            TransparencyBackgroundFallback = null!,
        };
        // The view is drawn over the game: subpixel text needs an opaque background
        TextOptions.SetTextRenderingMode(root, TextRenderingMode.Antialias);
        root.Prepare();
        root.StartRendering();
        component.TopLevel = root;
    }

    public AvaloniaUIComponent Component { get; }

    public StrideTopLevelImpl Impl => impl;

    /// <summary>
    /// Gets the texture of the last frame of the view, once the renderer drew it on the GPU or it was uploaded.
    /// </summary>
    public Texture? Texture { get; private set; }

    /// <summary>
    /// Gets whether the renderer drew the last frame on the game's graphics device rather than on the CPU.
    /// </summary>
    public bool IsRenderedOnGpu => impl.GpuFrame != null;

    /// <summary>
    /// Gets whether the view is shown and gets the input: its component is enabled and Avalonia rendered a frame of it.
    /// </summary>
    public bool IsShown => Component.Enabled && impl.FrameVersion > 0 && !disposed;

    /// <summary>
    /// Gets the control of this view that has the keyboard focus (Avalonia has one focused control in the process).
    /// </summary>
    public IInputElement? FocusedElement
        => root.FocusManager?.GetFocusedElement() is Visual focused && ReferenceEquals(TopLevel.GetTopLevel(focused), root) ? (IInputElement)focused : null;

    /// <summary>
    /// Shows the content the component asks for, creating it from the view type if needed.
    /// </summary>
    public void SyncContent()
    {
        var component = Component;
        if (component.Content == null && !string.IsNullOrEmpty(component.ViewType) && component.ViewType != failedViewType)
            component.Content = CreateView(component.ViewType);

        if (!ReferenceEquals(root.Content, component.Content))
            root.Content = component.Content;

        // A hidden view keeps no focus, so it doesn't keep the keyboard either
        if (!component.Enabled && FocusedElement != null)
            ClearFocus();
    }

    /// <summary>
    /// Sizes the view for a back buffer: its virtual resolution, scaled the way the component says.
    /// </summary>
    public void Layout(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        var resolution = Component.Resolution;
        var pixelWidth = width;
        var pixelHeight = height;
        double scaling;
        if (resolution.X <= 0 || resolution.Y <= 0)
        {
            scaling = 1;
        }
        else
        {
            switch (Component.ResolutionStretch)
            {
                case ResolutionStretch.FixedHeightAdaptableWidth:
                    scaling = height / resolution.Y;
                    break;
                case ResolutionStretch.FixedWidthFixedHeight:
                    // Avalonia scales the same way on both axes: render at the resolution's ratio and stretch the frame
                    scaling = Math.Min(width / resolution.X, height / resolution.Y);
                    pixelWidth = Math.Max(1, (int)Math.Round(resolution.X * scaling));
                    pixelHeight = Math.Max(1, (int)Math.Round(resolution.Y * scaling));
                    break;
                default:
                    scaling = width / resolution.X;
                    break;
            }
        }

        impl.Resize(new PixelSize(pixelWidth, pixelHeight), scaling > 0 ? scaling : 1);
    }

    /// <summary>
    /// Converts a position normalized over the back buffer to the view's logical coordinates.
    /// </summary>
    public Point ToClient(Vector2 normalizedPosition)
        => new(normalizedPosition.X * impl.ClientSize.Width, normalizedPosition.Y * impl.ClientSize.Height);

    /// <summary>
    /// Gets whether a control of the view is under a position (normalized over the back buffer).
    /// </summary>
    public bool HitTest(Vector2 normalizedPosition)
        => IsShown && root.InputHitTest(ToClient(normalizedPosition)) != null;

    public void ClearFocus()
    {
        if (FocusedElement != null)
            root.FocusManager?.Focus(null);
    }

    public void RaiseLeave(RawInputModifiers modifiers) => impl.RaisePointer(RawPointerEventType.LeaveWindow, new Point(-1, -1), modifiers);

    /// <summary>
    /// Gets the last frame ready for the draw (game thread, during the draw): a frame rendered on the GPU is moved to the
    /// shader resource layout, or copied to be sampled as sRGB when it can't be viewed so; one rendered on the CPU is
    /// uploaded if it changed since the last upload.
    /// </summary>
    /// <param name="srgb">Whether the frame is sampled as sRGB, as the colors of a linear back buffer.</param>
    public unsafe void PrepareFrame(GraphicsDevice device, CommandList commandList, bool srgb)
    {
        if (impl.GpuFrame is { } gpuFrame)
        {
            ReleaseUploadTexture();
            if (srgb && !impl.GpuFrameCanViewAsSrgb)
            {
                ReleaseGpuFrameView();
                Texture = GetSrgbCopy(device, commandList, gpuFrame);
                return;
            }

            ReleaseGpuFrameCopy();
            commandList.ResourceBarrierTransition(gpuFrame, BarrierLayout.ShaderResource);
            Texture = srgb ? GetSrgbView(gpuFrame) : gpuFrame;
            return;
        }

        ReleaseGpuFrameView();
        ReleaseGpuFrameCopy();
        if (!impl.TryGetFrame(out var data, out var size, out var rowPitch))
        {
            Texture = null;
            return;
        }

        var format = srgb ? PixelFormat.R8G8B8A8_UNorm_SRgb : PixelFormat.R8G8B8A8_UNorm;
        var stale = uploadTexture == null || uploadTexture.Width != size.Width || uploadTexture.Height != size.Height || uploadTexture.Format != format;
        if (stale)
        {
            uploadTexture?.Dispose();
            uploadTexture = Texture.New2D(device, size.Width, size.Height, format);
        }

        if (stale || impl.FrameVersion != uploadedVersion)
        {
            uploadTexture!.SetData(commandList, new ReadOnlySpan<byte>((void*)data, rowPitch * size.Height));
            commandList.ResourceBarrierTransition(uploadTexture, BarrierLayout.ShaderResource);
            uploadedVersion = impl.FrameVersion;
        }
        Texture = uploadTexture;
    }

    /// <summary>
    /// Releases the textures of the view (graphics content unloaded); the next frame creates them again.
    /// </summary>
    public void ReleaseTexture()
    {
        ReleaseUploadTexture();
        ReleaseGpuFrameView();
        ReleaseGpuFrameCopy();
        Texture = null;
    }

    // Skia draws UNorm colors, which are sRGB: a view of them as sRGB gives the linear colors
    private Texture GetSrgbView(Texture gpuFrame)
    {
        if (gpuFrameView?.ParentTexture != gpuFrame)
        {
            ReleaseGpuFrameView();
            gpuFrameView = gpuFrame.ToTextureView(new TextureViewDescription
            {
                Type = ViewType.Full,
                Format = PixelFormat.R8G8B8A8_UNorm_SRgb,
                Flags = TextureFlags.ShaderResource,
            });
        }
        return gpuFrameView;
    }

    // The native canvas draws into UNorm textures, which Direct3D can't view as sRGB: a copy of the frame, made when it
    // changes, is sampled as sRGB instead
    private Texture GetSrgbCopy(GraphicsDevice device, CommandList commandList, Texture gpuFrame)
    {
        var stale = gpuFrameCopy == null || gpuFrameCopy.Width != gpuFrame.Width || gpuFrameCopy.Height != gpuFrame.Height;
        if (stale)
        {
            ReleaseGpuFrameCopy();
            gpuFrameCopy = Texture.New2D(device, gpuFrame.Width, gpuFrame.Height, PixelFormat.R8G8B8A8_UNorm_SRgb);
        }

        if (stale || impl.FrameVersion != copiedVersion)
        {
            commandList.ResourceBarrierTransition(gpuFrame, BarrierLayout.CopySource);
            commandList.ResourceBarrierTransition(gpuFrameCopy!, BarrierLayout.CopyDest);
            commandList.Copy(gpuFrame, gpuFrameCopy!);
            commandList.ResourceBarrierTransition(gpuFrameCopy!, BarrierLayout.ShaderResource);
            copiedVersion = impl.FrameVersion;
        }
        return gpuFrameCopy!;
    }

    private void ReleaseGpuFrameCopy()
    {
        gpuFrameCopy?.Dispose();
        gpuFrameCopy = null;
    }

    private void ReleaseUploadTexture()
    {
        uploadTexture?.Dispose();
        uploadTexture = null;
    }

    private void ReleaseGpuFrameView()
    {
        gpuFrameView?.Dispose();
        gpuFrameView = null;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        if (ReferenceEquals(Component.TopLevel, root))
            Component.TopLevel = null;
        root.Content = null;
        root.StopRendering();
        root.Dispose();
        ReleaseTexture();
    }

    private Control? CreateView(string typeName)
    {
        try
        {
            var type = FindType(typeName);
            if (type == null || !typeof(Control).IsAssignableFrom(type))
            {
                Log.Error($"The Avalonia view type [{typeName}] of entity [{Component.Entity?.Name}] is not a control type of the game's assemblies.");
                failedViewType = typeName;
                return null;
            }
            return (Control?)Activator.CreateInstance(type);
        }
        catch (Exception e)
        {
            Log.Error($"Could not create the Avalonia view [{typeName}] of entity [{Component.Entity?.Name}].", e);
            failedViewType = typeName;
            return null;
        }
    }

    private static Type? FindType(string typeName)
    {
        if (typeName.Contains(','))
            return AssemblyRegistry.GetType(typeName, throwOnError: false) ?? Type.GetType(typeName, throwOnError: false);

        return AssemblyRegistry.FindAll()
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type != null);
    }
}
