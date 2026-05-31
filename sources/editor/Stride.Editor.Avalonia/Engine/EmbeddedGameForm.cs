// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.InteropServices;
using Stride.Core.Presentation.Avalonia.Controls;
using Stride.Games;

namespace Stride.Editor.Avalonia.Engine;

/// <summary>
/// A specialization of <see cref="GameFormSDL"/> for embedding the game window inside an Avalonia
/// <see cref="GameEngineHost"/>. On Windows, the SDL window is immediately converted to a child
/// window (WS_CHILD) so that SDL cannot show it as a top-level window. Avalonia's
/// NativeControlHost will later call SetParent to embed it and manage its visibility.
/// </summary>
[System.ComponentModel.DesignerCategory("")]
public partial class EmbeddedGameForm : GameFormSDL
{
    public EmbeddedGameForm()
    {
        IsFullScreen = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Immediately convert to WS_CHILD so the game engine cannot show this as a
            // top-level window. A child window with no parent is inherently invisible,
            // preventing the race where SDL_ShowWindow fires before Avalonia reparents.
            var style = GetWindowLongPtr(Handle, GWL_STYLE);
            style = (nint)((long)style & ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME) | WS_CHILD);
            SetWindowLongPtr(Handle, GWL_STYLE, style);
            // SWP_FRAMECHANGED is required for the style change to take effect.
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    /// <summary>
    /// Gets or sets the <see cref="GameEngineHost"/> associated to this form.
    /// </summary>
    public GameEngineHost Host { get; set; }

    #region Windows Native Interop

    private const int GWL_STYLE = -16;
    private const long WS_CHILD = 0x40000000L;
    private const long WS_POPUP = 0x80000000L;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static nint GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex);
        return GetWindowLong32(hWnd, nIndex);
    }

    private static nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return SetWindowLong32(hWnd, nIndex, (int)dwNewLong);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong32(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr64(IntPtr hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    #endregion
}
