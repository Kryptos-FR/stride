// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.ExceptionServices;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Stride.AvaloniaUI.Rendering.Fallback;
using Stride.AvaloniaUI.Rendering.Native;
using Stride.AvaloniaUI.Rendering.Text;
using Stride.AvaloniaUI.Tests;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Graphics.Regression;
using Stride.Rendering.Compositing;
using Xunit;

namespace Stride.AvaloniaUI.Native.Tests;

/// <summary>
/// Tests the glyph atlas of the native canvas: its shelves, its cache, its eviction and its uploads.
/// </summary>
public sealed class GlyphAtlasTests : GameTestBase
{
    public GlyphAtlasTests()
    {
        GraphicsDeviceManager.PreferredBackBufferWidth = 64;
        GraphicsDeviceManager.PreferredBackBufferHeight = 64;
        GraphicsDeviceManager.PreferredGraphicsProfile = [GraphicsProfile.Level_11_0];
    }

    [Fact]
    public void PackerFillsShelvesAndRefusesWhatDoesNotFit()
    {
        var packer = new ShelfPacker(64);
        Assert.True(packer.TryAdd(30, 10, out var a));
        Assert.Equal(new Int2(0, 0), a);
        // 12 rounds up to 16 too: the same shelf
        Assert.True(packer.TryAdd(30, 12, out var b));
        Assert.Equal(new Int2(30, 0), b);
        // An 8 shelf under the 16 one
        Assert.True(packer.TryAdd(30, 7, out var c));
        Assert.Equal(new Int2(0, 16), c);
        // The 16 shelf is full: a new one under the 8 shelf
        Assert.True(packer.TryAdd(10, 16, out var d));
        Assert.Equal(new Int2(0, 24), d);
        // Back on the 8 shelf
        Assert.True(packer.TryAdd(30, 8, out var e));
        Assert.Equal(new Int2(30, 16), e);
        Assert.False(packer.TryAdd(65, 1, out _));
        // A 32 shelf would end at 72
        Assert.False(packer.TryAdd(10, 25, out _));
        packer.Clear();
        Assert.True(packer.TryAdd(64, 64, out var f));
        Assert.Equal(new Int2(0, 0), f);
    }

    [Fact]
    public void SameKeyGivesTheSameTexel() => RunWithAtlas(GlyphAtlas.DefaultPageSize, GlyphAtlas.DefaultMaxPages, (atlas, commandList) =>
    {
        var key = GetKey(atlas, "A", 24);
        Assert.True(atlas.TryGetFont(GetInter(), out var font, out _));
        Assert.Same(key.Font, font);

        Assert.True(atlas.TryGet(key, out var first, out _));
        Assert.True(atlas.TryGet(key, out var second, out _));
        Assert.Equal(first, second);
        Assert.NotNull(first.Page);
        Assert.True(atlas.TryGet(key with { Luminance = 7 }, out var white, out _));
        Assert.NotEqual(first.Texel, white.Texel);
        Assert.Equal(1, atlas.PageCount);
    });

    [Fact]
    public void GlyphWithoutInkTakesNoRoom() => RunWithAtlas(64, 1, (atlas, commandList) =>
    {
        Assert.True(atlas.TryGet(GetKey(atlas, " ", 24), out var space, out _));
        Assert.Null(space.Page);
        Assert.False(space.Image.HasInk);
        Assert.Equal(0, atlas.PageCount);
    });

    [Fact]
    public void FullAtlasRefusesThenEvictsTheNextFrame() => RunWithAtlas(64, 2, (atlas, commandList) =>
    {
        // At 48 px, no two of these glyphs share a page of 64 pixels
        var w = GetKey(atlas, "W", 48);
        var m = GetKey(atlas, "M", 48);
        var zhe = GetKey(atlas, "Ж", 48);

        atlas.BeginFrame();
        Assert.True(atlas.TryGet(w, out var first, out _));
        Assert.True(atlas.TryGet(m, out var second, out _));
        Assert.NotSame(first.Page, second.Page);
        Assert.False(atlas.TryGet(zhe, out _, out var reason));
        Assert.Equal(FallbackReason.GlyphAtlasFull, reason);

        // The page of W, the least recently used, is emptied for Ж
        atlas.BeginFrame();
        Assert.True(atlas.TryGet(m, out _, out _));
        Assert.True(atlas.TryGet(zhe, out var third, out _));
        Assert.Same(first.Page, third.Page);
        Assert.Equal(new Int2(0, 0), third.Texel);
        Assert.Equal(2, atlas.PageCount);

        // W was forgotten with its page, and both pages are used this frame
        Assert.False(atlas.TryGet(w, out _, out _));
    });

    [Fact]
    public void GlyphLargerThanAPageIsRefused() => RunWithAtlas(64, 2, (atlas, commandList) =>
    {
        Assert.False(atlas.TryGet(GetKey(atlas, "W", 120), out _, out var reason));
        Assert.Equal(FallbackReason.GlyphAtlasFull, reason);
        Assert.Equal(0, atlas.PageCount);
    });

    [Fact]
    public void UploadedGlyphsAreInThePage() => RunWithAtlas(128, 1, (atlas, commandList) =>
    {
        // A first glyph uploaded alone, then a second one: the second upload sends only what changed after the first
        var g = GetKey(atlas, "g", 36);
        var q = GetKey(atlas, "Q", 36) with { SubpixelX = 2, Luminance = 7 };
        Assert.True(atlas.TryGet(g, out var first, out _));
        atlas.Upload(commandList);
        Assert.True(atlas.TryGet(q, out var second, out _));
        atlas.Upload(commandList);

        Assert.Same(first.Page, second.Page);
        var page = first.Page!.GetData<byte>(commandList);
        AssertGlyphInPage(atlas, g, first, page, 128);
        AssertGlyphInPage(atlas, q, second, page, 128);
    });

    private static void AssertGlyphInPage(GlyphAtlas atlas, GlyphKey key, AtlasGlyph glyph, byte[] page, int pageSize)
    {
        var coverage = new ArrayBufferWriter<byte>();
        Assert.True(GlyphRasterizer.TryRasterize(atlas.Library, key, coverage, out var image));
        Assert.Equal(image, glyph.Image);
        Assert.True(image.HasInk);
        var expected = coverage.WrittenSpan;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var texel = page[(glyph.Texel.Y + y) * pageSize + glyph.Texel.X + x];
                Assert.True(expected[y * image.Width + x] == texel, $"Glyph {key.Glyph}: texel ({x}, {y}) is {texel}, not {expected[y * image.Width + x]}.");
            }
        }
    }

    private static GlyphTypeface GetInter() => new Typeface("fonts:Inter#Inter").GlyphTypeface;

    private static GlyphKey GetKey(GlyphAtlas atlas, string character, float size)
    {
        var typeface = GetInter();
        Assert.True(atlas.TryGetFont(typeface, out var font, out var reason), $"Inter can't be opened: {reason}.");
        using var shaped = TextShaper.Current.ShapeText(character, new TextShaperOptions(typeface, size));
        return new GlyphKey(font, shaped[0].GlyphIndex, GlyphRasterizer.ToSize26Dot6(size), GlyphRendering.Antialias, TextHintingMode.Strong, 0, 0);
    }

    // Runs a test with an atlas on the game's device, in a draw of the game
    private static void RunWithAtlas(int pageSize, int maxPages, Action<GlyphAtlas, CommandList> test) => TestAvalonia.Run(() =>
    {
        var game = new GlyphAtlasTests();
        game.SceneSystem.SceneInstance = new SceneInstance(game.Services, new Scene());
        game.SceneSystem.GraphicsCompositor = new GraphicsCompositor { Game = new ClearRenderer() };
        game.FrameGameSystem.IsUnitTestFeeding = true;
        ExceptionDispatchInfo? failure = null;
        var ran = false;
        game.FrameGameSystem.Draw(2, () =>
        {
            ran = true;
            try
            {
                using var atlas = new GlyphAtlas(game.GraphicsDevice, pageSize, maxPages);
                test(atlas, game.GraphicsContext.CommandList);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        RunGameTest(game);
        failure?.Throw();
        Assert.True(ran, "The game ended before the test's draw.");
    });
}
