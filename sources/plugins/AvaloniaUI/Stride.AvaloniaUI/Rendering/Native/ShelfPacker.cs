// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// Packs rectangles into a square in shelves: rows whose heights are those of their rectangles rounded up to 8 pixels.
/// </summary>
/// <remarks>
/// A rectangle goes on the first shelf of its rounded height with room left, else on a new shelf under the others.
/// </remarks>
internal sealed class ShelfPacker(int size)
{
    private const int ShelfStep = 8;

    private readonly List<Shelf> shelves = [];

    // The top of the space under the shelves
    private int bottom;

    /// <summary>
    /// Takes room for a rectangle.
    /// </summary>
    /// <returns><c>false</c> when no shelf has room for it and no new shelf fits under the others.</returns>
    public bool TryAdd(int width, int height, out Int2 position)
    {
        position = default;
        if (width <= 0 || height <= 0 || width > size || height > size)
            return false;

        var shelfHeight = (height + ShelfStep - 1) / ShelfStep * ShelfStep;
        for (var i = 0; i < shelves.Count; i++)
        {
            var shelf = shelves[i];
            if (shelf.Height == shelfHeight && shelf.Right + width <= size)
            {
                position = new Int2(shelf.Right, shelf.Top);
                shelves[i] = shelf with { Right = shelf.Right + width };
                return true;
            }
        }

        if (bottom + shelfHeight > size)
            return false;
        shelves.Add(new Shelf(bottom, shelfHeight, width));
        position = new Int2(0, bottom);
        bottom += shelfHeight;
        return true;
    }

    /// <summary>
    /// Empties the square.
    /// </summary>
    public void Clear()
    {
        shelves.Clear();
        bottom = 0;
    }

    // A shelf: its top, its height, and the right of its last rectangle
    private readonly record struct Shelf(int Top, int Height, int Right);
}
