// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Rendering.Fallback;

/// <summary>
/// Memory a fallback canvas draws into: premultiplied pixels of 4 bytes, and whether it still holds the previous frame.
/// </summary>
internal readonly record struct FallbackTarget(IntPtr Address, PixelSize Size, int RowBytes, PixelFormat Format, Vector Dpi, bool PreviousFrameRetained);
