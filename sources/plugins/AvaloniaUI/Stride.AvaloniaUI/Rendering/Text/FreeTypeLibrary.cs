// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Graphics.Font;

namespace Stride.AvaloniaUI.Rendering.Text;

/// <summary>
/// A FreeType library instance, which the faces opened in it and their glyphs are made with.
/// </summary>
internal sealed class FreeTypeLibrary : IDisposable
{
    /// <exception cref="InvalidOperationException">FreeType can't be initialized.</exception>
    public FreeTypeLibrary()
    {
        var error = FreeTypeNative.FT_Init_FreeType(out var handle);
        if (error != 0)
            throw new InvalidOperationException($"FreeType can't be initialized (error {error}).");
        Handle = handle;
    }

    /// <summary>
    /// Gets the library's <c>FT_Library</c>, or zero once disposed.
    /// </summary>
    public nint Handle { get; private set; }

    public void Dispose()
    {
        if (Handle == 0)
            return;
        FreeTypeNative.FT_Done_FreeType(Handle);
        Handle = 0;
    }
}
