// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Media;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Text;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// A glyph in the atlas: its page, the texel of its top-left corner there, and its image; no page when it has no ink.
/// </summary>
internal readonly record struct AtlasGlyph(Texture? Page, Int2 Texel, GlyphImage Image);

/// <summary>
/// The glyph masks of a device's canvases, in pages of 8-bit coverage, with the FreeType faces they are rasterized from.
/// </summary>
/// <remarks>
/// <para>
/// Each page keeps a copy of its texels on the CPU and the rectangle changed since its last upload. Glyphs touch, without
/// padding between them: the shader loads texels one to one and never filters.
/// </para>
/// <para>
/// When a glyph has no room, the atlas adds a page while it has fewer than its most, else empties the page used least
/// recently, if that was before the current frame; else the glyph is refused.
/// </para>
/// </remarks>
internal sealed class GlyphAtlas : IDisposable
{
    /// <summary>
    /// The default width and height of a page, in texels.
    /// </summary>
    public const int DefaultPageSize = 1024;

    /// <summary>
    /// The default most pages of an atlas.
    /// </summary>
    public const int DefaultMaxPages = 8;

    private readonly GraphicsDevice device;
    private readonly int pageSize;
    private readonly int maxPages;
    private readonly FreeTypeLibrary library = new();
    private readonly List<Page> pages = [];
    private readonly Dictionary<GlyphKey, (Page? Page, Int2 Texel, GlyphImage Image)> glyphs = [];
    private readonly Dictionary<GlyphTypeface, (NativeFont? Font, FallbackReason Reason)> fonts = [];
    private readonly ArrayBufferWriter<byte> coverage = new();
    private byte[] upload = [];
    private int frame;

    public GlyphAtlas(GraphicsDevice device, int pageSize = DefaultPageSize, int maxPages = DefaultMaxPages)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);
        this.device = device;
        this.pageSize = pageSize;
        this.maxPages = maxPages;
    }

    /// <summary>
    /// Gets the atlas's <c>FT_Library</c>, which its faces are opened in.
    /// </summary>
    public nint Library => library.Handle;

    /// <summary>
    /// Gets the number of pages the atlas has made.
    /// </summary>
    internal int PageCount => pages.Count;

    /// <summary>
    /// Gets the FreeType face of a typeface, opened the first time it is asked for.
    /// </summary>
    /// <returns><c>false</c>, with the reason, when the typeface goes to the fallback (<see cref="NativeFont.TryOpen"/>).</returns>
    public bool TryGetFont(GlyphTypeface typeface, [NotNullWhen(true)] out NativeFont? font, out FallbackReason reason)
    {
        if (!fonts.TryGetValue(typeface, out var entry))
        {
            NativeFont.TryOpen(Library, typeface, out var opened, out var openReason);
            entry = (opened, openReason);
            fonts.Add(typeface, entry);
        }
        font = entry.Font;
        reason = entry.Reason;
        return font != null;
    }

    /// <summary>
    /// Starts a frame of the game: the pages used before it may be emptied for new glyphs.
    /// </summary>
    public void BeginFrame() => frame++;

    /// <summary>
    /// Gets a glyph, rasterized and given room in a page the first time it is asked for.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with the reason: <see cref="FallbackReason.GlyphAtlasFull"/> when no page has room for the glyph,
    /// <see cref="FallbackReason.FontData"/> when FreeType fails on it.
    /// </returns>
    public bool TryGet(in GlyphKey key, out AtlasGlyph glyph, out FallbackReason reason)
    {
        reason = default;
        if (glyphs.TryGetValue(key, out var entry))
        {
            if (entry.Page != null)
                entry.Page.LastUse = frame;
            glyph = new AtlasGlyph(entry.Page?.Texture, entry.Texel, entry.Image);
            return true;
        }

        glyph = default;
        coverage.Clear();
        if (!GlyphRasterizer.TryRasterize(Library, key, coverage, out var image))
        {
            reason = FallbackReason.FontData;
            return false;
        }

        if (!image.HasInk)
        {
            glyphs.Add(key, (null, default, image));
            glyph = new AtlasGlyph(null, default, image);
            return true;
        }

        if (!TryFindRoom(image.Width, image.Height, out var page, out var texel))
        {
            reason = FallbackReason.GlyphAtlasFull;
            return false;
        }

        // The glyph's rows into the page's copy, and its rectangle into the page's changes
        var rows = coverage.WrittenSpan;
        for (var y = 0; y < image.Height; y++)
            rows.Slice(y * image.Width, image.Width).CopyTo(page.Pixels.AsSpan((texel.Y + y) * pageSize + texel.X, image.Width));
        var rectangle = new Rectangle(texel.X, texel.Y, image.Width, image.Height);
        page.Changed = page.Changed.IsEmpty ? rectangle : Rectangle.Union(page.Changed, rectangle);
        page.Glyphs.Add(key);
        page.LastUse = frame;

        glyphs.Add(key, (page, texel, image));
        glyph = new AtlasGlyph(page.Texture, texel, image);
        return true;
    }

    /// <summary>
    /// Copies the changed texels of each page to the GPU, and leaves the pages ready to be read by shaders.
    /// </summary>
    public void Upload(CommandList commandList)
    {
        foreach (var page in pages)
        {
            var changed = page.Changed;
            if (changed.IsEmpty)
                continue;

            var length = changed.Width * changed.Height;
            if (upload.Length < length)
                upload = new byte[Math.Max(length, upload.Length * 2)];
            for (var y = 0; y < changed.Height; y++)
                page.Pixels.AsSpan((changed.Y + y) * pageSize + changed.X, changed.Width).CopyTo(upload.AsSpan(y * changed.Width, changed.Width));
            page.Texture.SetData(commandList, upload.AsSpan(0, length), 0, 0, new ResourceRegion(changed.Left, changed.Top, 0, changed.Right, changed.Bottom, 1));
            commandList.ResourceBarrierTransition(page.Texture, BarrierLayout.ShaderResource);
            page.Changed = Rectangle.Empty;
        }
    }

    public void Dispose()
    {
        foreach (var page in pages)
            page.Texture.Dispose();
        pages.Clear();
        glyphs.Clear();
        foreach (var (font, _) in fonts.Values)
            font?.Dispose();
        fonts.Clear();
        library.Dispose();
    }

    // Finds room in a page: one with room, else a new page, else the page used least recently before this frame, emptied
    private bool TryFindRoom(int width, int height, [NotNullWhen(true)] out Page? page, out Int2 texel)
    {
        texel = default;
        page = null;
        if (width > pageSize || height > pageSize)
            return false;

        foreach (var candidate in pages)
        {
            if (candidate.Packer.TryAdd(width, height, out texel))
            {
                page = candidate;
                return true;
            }
        }

        if (pages.Count < maxPages)
        {
            page = new Page(Texture.New2D(device, pageSize, pageSize, PixelFormat.R8_UNorm, TextureFlags.ShaderResource), pageSize);
            pages.Add(page);
            return page.Packer.TryAdd(width, height, out texel);
        }

        Page? oldest = null;
        foreach (var candidate in pages)
        {
            if (candidate.LastUse < frame && (oldest == null || candidate.LastUse < oldest.LastUse))
                oldest = candidate;
        }
        if (oldest == null)
            return false;

        foreach (var key in oldest.Glyphs)
            glyphs.Remove(key);
        oldest.Glyphs.Clear();
        oldest.Packer.Clear();
        page = oldest;
        return page.Packer.TryAdd(width, height, out texel);
    }

    // A page: its texture, the copy of its texels, its shelves, its glyphs, the frame it was last used in, and the
    // rectangle changed since its last upload
    private sealed class Page(Texture texture, int size)
    {
        public Texture Texture { get; } = texture;

        public byte[] Pixels { get; } = new byte[size * size];

        public ShelfPacker Packer { get; } = new(size);

        public List<GlyphKey> Glyphs { get; } = [];

        public int LastUse { get; set; }

        public Rectangle Changed { get; set; }
    }
}
