// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// Memory a fallback canvas draws into: premultiplied pixels of 4 bytes, whether it still holds the previous frame, and how drawings reach it.
/// </summary>
/// <param name="ScaleDrawingToDpi">Whether drawings are in device-independent pixels, scaled to <paramref name="Dpi"/>, as in a render target bitmap.</param>
/// <param name="GrayscaleText">Whether text is drawn without subpixel antialiasing, as in a bitmap cache.</param>
internal readonly record struct FallbackTarget(
    IntPtr Address,
    PixelSize Size,
    int RowBytes,
    PixelFormat Format,
    Vector Dpi,
    bool PreviousFrameRetained,
    bool ScaleDrawingToDpi = false,
    bool GrayscaleText = false);
