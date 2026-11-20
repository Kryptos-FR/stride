// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Platform;

/// <summary>
/// The cursors Avalonia asks for: the game reads the requested type from <see cref="StrideTopLevelImpl.Cursor"/>.
/// </summary>
internal sealed class StrideCursorFactory : ICursorFactory
{
    public ICursorImpl GetCursor(StandardCursorType cursorType) => new CursorImpl(cursorType);

    // Bitmap cursors are not supported yet: the arrow stands in for them
    public ICursorImpl CreateCursor(Bitmap cursor, PixelPoint hotSpot) => new CursorImpl(StandardCursorType.Arrow);

    internal sealed class CursorImpl(StandardCursorType type) : ICursorImpl
    {
        public StandardCursorType Type { get; } = type;

        public void Dispose()
        {
        }
    }
}
