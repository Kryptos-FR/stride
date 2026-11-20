// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.Graphics.Font;

namespace Stride.AvaloniaUI.Rendering.Text;

/// <summary>
/// The FreeType face of a typeface, opened from the typeface's font data, which it keeps pinned for the face's life.
/// </summary>
/// <remarks>
/// The face is touched on the game thread only, which is Avalonia's UI and render thread.
/// </remarks>
internal sealed unsafe class NativeFont : IDisposable
{
    private static readonly uint[] ColorTables = [Tag("COLR"), Tag("CBDT"), Tag("sbix"), Tag("SVG ")];
    private static readonly uint VariationTable = Tag("fvar");
    private static readonly uint HeadTable = Tag("head");
    private static readonly uint NameTable = Tag("name");

    // The font file, on the pinned object heap: FreeType reads it for as long as the face lives
    private readonly byte[] data;

    private NativeFont(FT_FaceRec* face, byte[] data, string familyName, FontSimulations simulations)
    {
        Face = face;
        this.data = data;
        FamilyName = familyName;
        Simulations = simulations;
    }

    /// <summary>
    /// Gets the family name of the typeface.
    /// </summary>
    public string FamilyName { get; }

    /// <summary>
    /// Gets the simulations of the typeface: oblique only, which skews its glyphs.
    /// </summary>
    public FontSimulations Simulations { get; }

    /// <summary>
    /// Gets the FreeType face, or <c>null</c> once disposed.
    /// </summary>
    internal FT_FaceRec* Face { get; private set; }

    /// <summary>
    /// Opens the face of a typeface with FreeType.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with the reason, when the core can't draw the typeface natively: a simulated bold
    /// (<see cref="FallbackReason.TextSimulation"/>), a colour font (<see cref="FallbackReason.ColorFont"/>), a variable font,
    /// unreadable data, data FreeType can't open or whose faces aren't the typeface's (<see cref="FallbackReason.FontData"/>).
    /// </returns>
    public static bool TryOpen(nint library, GlyphTypeface typeface, [NotNullWhen(true)] out NativeFont? font, out FallbackReason reason)
    {
        font = null;
        if ((typeface.FontSimulations & FontSimulations.Bold) != 0)
        {
            reason = FallbackReason.TextSimulation;
            return false;
        }

        var platformTypeface = typeface.PlatformTypeface;
        if (Classify(tag => platformTypeface.TryGetTable(tag, out _)) is { } classified)
        {
            reason = classified;
            return false;
        }

        reason = FallbackReason.FontData;
        if (!platformTypeface.TryGetStream(out var stream))
            return false;
        byte[] file;
        using (stream)
            file = ReadPinned(stream);

        var index = FindFaceIndex(library, file, tag => platformTypeface.TryGetTable(tag, out var table) ? table : null);
        if (index < 0)
            return false;
        FT_FaceRec* face;
        fixed (byte* address = file)
        {
            if (FreeTypeNative.FT_New_Memory_Face(library, address, new CLong(file.Length), new CLong(index), out face) != 0)
                return false;
        }

        font = new NativeFont(face, file, typeface.FamilyName, typeface.FontSimulations);
        reason = default;
        return true;
    }

    /// <summary>
    /// Finds the face of a font file whose tables equal the typeface's: the only face when its <c>head</c> table does, or
    /// the face of a collection whose <c>head</c> and <c>name</c> tables do.
    /// </summary>
    /// <param name="typefaceTable">The typeface's table of a tag, or <c>null</c> when it has none.</param>
    /// <returns>The face's index, or -1 when no face is the typeface's, or FreeType can't open the file.</returns>
    internal static int FindFaceIndex(nint library, ReadOnlySpan<byte> file, Func<uint, ReadOnlyMemory<byte>?> typefaceTable)
    {
        fixed (byte* address = file)
        {
            // Face -1 gives the number of faces
            if (FreeTypeNative.FT_New_Memory_Face(library, address, new CLong(file.Length), new CLong(-1), out var probe) != 0)
                return -1;
            var count = (int)probe->num_faces.Value;
            FreeTypeNative.FT_Done_Face(probe);

            var head = typefaceTable(HeadTable);
            var name = count > 1 ? typefaceTable(NameTable) : null;
            if (head == null || (count > 1 && name == null))
                return -1;
            for (var index = 0; index < count; index++)
            {
                if (FreeTypeNative.FT_New_Memory_Face(library, address, new CLong(file.Length), new CLong(index), out var face) != 0)
                    continue;
                try
                {
                    if (TableEquals(face, HeadTable, head.Value.Span) && (count == 1 || TableEquals(face, NameTable, name!.Value.Span)))
                        return index;
                }
                finally
                {
                    FreeTypeNative.FT_Done_Face(face);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Gets why a typeface goes to the fallback from the tables it has, or <c>null</c> when the core may draw it.
    /// </summary>
    /// <returns><see cref="FallbackReason.ColorFont"/> for colour glyph tables, <see cref="FallbackReason.FontData"/> for a variable font.</returns>
    internal static FallbackReason? Classify(Func<uint, bool> hasTable)
    {
        foreach (var table in ColorTables)
        {
            if (hasTable(table))
                return FallbackReason.ColorFont;
        }
        return hasTable(VariationTable) ? FallbackReason.FontData : null;
    }

    public void Dispose()
    {
        if (Face == null)
            return;
        FreeTypeNative.FT_Done_Face(Face);
        Face = null;
        GC.KeepAlive(data);
    }

    private static uint Tag(string tag) => OpenTypeTag.Parse(tag);

    private static byte[] ReadPinned(Stream stream)
    {
        if (stream.CanSeek)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)(stream.Length - stream.Position)), pinned: true);
            stream.ReadExactly(bytes);
            return bytes;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var copy = GC.AllocateUninitializedArray<byte>((int)memory.Length, pinned: true);
        memory.GetBuffer().AsSpan(0, copy.Length).CopyTo(copy);
        return copy;
    }

    private static bool TableEquals(FT_FaceRec* face, uint tag, ReadOnlySpan<byte> expected)
    {
        CULong length = default;
        if (FreeTypeNative.FT_Load_Sfnt_Table(face, new CULong(tag), new CLong(0), null, &length) != 0 || (long)length.Value != expected.Length)
            return false;
        var table = expected.Length <= 1024 ? stackalloc byte[expected.Length] : new byte[expected.Length];
        fixed (byte* buffer = table)
        {
            if (FreeTypeNative.FT_Load_Sfnt_Table(face, new CULong(tag), new CLong(0), buffer, &length) != 0)
                return false;
        }
        return table.SequenceEqual(expected);
    }
}
