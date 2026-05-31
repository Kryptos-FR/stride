// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Stride.Core.Presentation.Avalonia.Controls;

/// <summary>
/// A <see cref="NativeControlHost"/> that embeds a game engine SDL window.
/// Avalonia handles reparenting, positioning, and visibility automatically.
/// </summary>
public class GameEngineHost : NativeControlHost, IDisposable
{
    private bool isDisposed;

    static GameEngineHost()
    {
        FocusableProperty.OverrideMetadata(typeof(GameEngineHost), new StyledPropertyMetadata<bool>(true));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameEngineHost"/> class.
    /// </summary>
    /// <param name="childHandle">The native handle of the child (hosted) window.</param>
    public GameEngineHost(IntPtr childHandle)
    {
        Handle = childHandle;
        MinWidth = 32;
        MinHeight = 32;
    }

    /// <summary>
    /// Gets the native handle of the hosted window.
    /// </summary>
    public IntPtr Handle { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
    }

    /// <inheritdoc />
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (Handle == IntPtr.Zero)
            return base.CreateNativeControlCore(parent);

        return new PlatformHandle(Handle, GetHandleDescriptor());
    }

    /// <inheritdoc />
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        // Intentionally a no-op: SDL owns the hosted window and destroys it with the game,
        // so Avalonia must not destroy the native handle here.
    }

    /// <summary>
    /// Forwards a message from the hosted window. Currently a no-op placeholder for future input forwarding.
    /// </summary>
    /// <param name="hwnd">The native handle of the hosted window that produced the message.</param>
    /// <param name="msg">The message identifier.</param>
    /// <param name="wParam">The word parameter of the message.</param>
    /// <param name="lParam">The long parameter of the message.</param>
    public void ForwardMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        // TODO: Implement input forwarding for Avalonia (keyboard/mouse events from SDL)
    }

    private static string GetHandleDescriptor()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "HWND";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "XID";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "NSView";
        return "HWND";
    }
}
