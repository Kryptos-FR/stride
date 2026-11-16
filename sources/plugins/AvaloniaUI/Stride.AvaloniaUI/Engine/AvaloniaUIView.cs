// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Embedding;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Stride.AvaloniaUI.Platform;
using Stride.Core.Diagnostics;
using Stride.Core.Reflection;
using Stride.Graphics;
using Stride.Rendering;
using PixelFormat = Stride.Graphics.PixelFormat;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace Stride.AvaloniaUI;

/// <summary>
/// The Avalonia top level of an <see cref="AvaloniaUIComponent"/>, and the texture its frames are uploaded to.
/// </summary>
internal sealed class AvaloniaUIView : IDisposable
{
    private static readonly Logger Log = GlobalLogger.GetLogger("AvaloniaUI");

    private readonly StrideTopLevelImpl impl = new();
    private readonly EmbeddableControlRoot root;
    private Texture? texture;
    private int uploadedVersion;
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
        root.Prepare();
        root.StartRendering();
        component.TopLevel = root;
    }

    public AvaloniaUIComponent Component { get; }

    public StrideTopLevelImpl Impl => impl;

    /// <summary>
    /// Gets the last frame of the view, once uploaded.
    /// </summary>
    public Texture? Texture => texture;

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
    /// Uploads the last frame to the texture if it changed since the last upload (game thread, during the draw).
    /// </summary>
    public unsafe void Upload(GraphicsDevice device, CommandList commandList, PixelFormat format)
    {
        if (!impl.TryGetFrame(out var data, out var size, out var rowPitch))
            return;

        var stale = texture == null || texture.Width != size.Width || texture.Height != size.Height || texture.Format != format;
        if (!stale && impl.FrameVersion == uploadedVersion)
            return;

        if (stale)
        {
            texture?.Dispose();
            texture = Texture.New2D(device, size.Width, size.Height, format);
        }

        texture!.SetData(commandList, new ReadOnlySpan<byte>((void*)data, rowPitch * size.Height));
        commandList.ResourceBarrierTransition(texture, BarrierLayout.ShaderResource);
        uploadedVersion = impl.FrameVersion;
    }

    /// <summary>
    /// Releases the texture (graphics content unloaded); the next upload creates it again.
    /// </summary>
    public void ReleaseTexture()
    {
        texture?.Dispose();
        texture = null;
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
