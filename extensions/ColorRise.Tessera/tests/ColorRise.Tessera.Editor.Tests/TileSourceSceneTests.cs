using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// A source on a canvas that time changes: the animated tiles, the frame each one shows while the scene plays, and the text
/// written on a tile.
/// </summary>
public sealed class TileSourceSceneTests
{
    private static readonly TileId Animated = TileId.Parse("1:0,0");
    private static readonly TileId Second = TileId.Parse("1:1,0");
    private static readonly RectangleF Everything = new(0, 0, 64, 32);

    [Fact]
    public async Task An_animated_tile_has_a_corner_mark()
    {
        await using var session = await OpenTownAsync();
        var scene = Scene(session);
        scene.IsAnimated = tile => tile == Animated;

        foreach (var plays in new[] { false, true })
        {
            scene.Plays = plays;
            var corners = Collect(scene).Shapes.Where(shape => shape.Mark == CanvasMark.Corner).ToList();

            var corner = Assert.Single(corners);
            Assert.Equal(new RectangleF(0, 0, 16, 16), corner.Bounds);
        }
    }

    [Fact]
    public async Task A_playing_scene_shows_the_frame_of_the_time_over_the_tile()
    {
        await using var session = await OpenTownAsync();
        var scene = Scene(session);
        Animate(scene);
        scene.Plays = true;

        Assert.DoesNotContain(Collect(scene).Sprites, sprite => sprite.Replace);
        scene.Advance(0.25);

        var replaced = Assert.Single(Collect(scene).Sprites, sprite => sprite.Replace);
        Assert.Equal(new Rectangle(16, 0, 16, 16), replaced.Source);
        Assert.Equal(new RectangleF(0, 0, 16, 16), replaced.Destination);
        Assert.Equal(0.25, scene.Time, 6);
    }

    [Fact]
    public async Task Advance_asks_a_redraw_only_when_a_frame_changed()
    {
        await using var session = await OpenTownAsync();
        var scene = Scene(session);
        Animate(scene);
        scene.Plays = true;
        var changes = 0;
        scene.Changed += (_, _) => changes++;

        scene.Advance(0.05);
        Assert.Equal(0, changes);
        scene.Advance(0.2);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task A_scene_that_does_not_play_shows_the_source_as_it_is()
    {
        await using var session = await OpenTownAsync();
        var scene = Scene(session);
        Animate(scene);
        var changes = 0;
        scene.Changed += (_, _) => changes++;

        scene.Advance(0.25);

        Assert.False(scene.IsRunning);
        Assert.Equal(0, changes);
        Assert.DoesNotContain(Collect(scene).Sprites, sprite => sprite.Replace);
    }

    [Fact]
    public async Task The_scene_runs_while_it_plays_and_has_an_animated_tile()
    {
        await using var session = await OpenTownAsync();
        var scene = Scene(session);
        var running = new List<bool>();
        scene.IsRunningChanged += (_, _) => running.Add(scene.IsRunning);
        var animates = true;
        scene.IsAnimated = tile => animates && tile == Animated;
        Assert.Empty(running);

        scene.Plays = true;
        Assert.Equal([true], running);
        scene.IsAnimated = _ => false;
        Assert.Equal([true, false], running);
        animates = false;
        scene.IsAnimated = tile => animates && tile == Animated;
        Assert.Equal([true, false], running);
        animates = true;
        scene.Refresh();
        Assert.Equal([true, false, true], running);
        scene.Plays = false;
        Assert.Equal([true, false, true, false], running);
    }

    [Fact]
    public async Task A_frame_without_a_picture_shows_the_tile_itself()
    {
        await using var session = await OpenTownAsync();
        var scene = Scene(session);
        var slot = scene.Layout!.SlotOf(Second)!;
        scene.IsAnimated = tile => tile == Animated;
        scene.FrameOf = (_, _) => slot with { Picture = null };
        scene.Plays = true;

        scene.Advance(0.25);

        Assert.DoesNotContain(Collect(scene).Sprites, sprite => sprite.Replace);
    }

    [Fact]
    public async Task A_tile_carries_the_text_of_LabelOf()
    {
        await using var session = await OpenTownAsync();
        var scene = Scene(session);
        scene.LabelOf = tile => tile == Second ? "2, 4" : null;

        var label = Assert.Single(Collect(scene).Labels);

        Assert.Equal("2, 4", label.Text);
        Assert.Equal(new RectangleF(16, 0, 16, 16), label.Bounds);
    }

    [Fact]
    public void A_key_goes_to_the_handler_and_is_not_used_without_one()
    {
        var scene = new TileSourceScene();
        Assert.False(scene.KeyPressed(CanvasKey.Delete, CanvasModifiers.None));

        var heard = new List<(CanvasKey, CanvasModifiers)>();
        scene.KeyHandler = (key, modifiers) =>
        {
            heard.Add((key, modifiers));
            return key == CanvasKey.Delete;
        };

        Assert.True(scene.KeyPressed(CanvasKey.Delete, CanvasModifiers.Shift));
        Assert.False(scene.KeyPressed(CanvasKey.B, CanvasModifiers.None));
        Assert.Equal([(CanvasKey.Delete, CanvasModifiers.Shift), (CanvasKey.B, CanvasModifiers.None)], heard);
    }

    // The tile 1:0,0 is animated: it shows itself, then the tile 1:1,0 from 0.2 s on
    private static void Animate(TileSourceScene scene)
    {
        var layout = scene.Layout!;
        scene.IsAnimated = tile => tile == Animated;
        scene.FrameOf = (_, time) => time >= 0.2 ? layout.SlotOf(Second) : null;
    }

    private static CanvasFrame Collect(TileSourceScene scene)
    {
        var frame = new CanvasFrame();
        scene.Collect(Everything, 1, frame);
        return frame;
    }

    private static TileSourceScene Scene(TesseraSession session)
    {
        var pictures = TilePictureService.Of(session.Session).Get(session.TileSetAt("Tiles/Town"));
        return new TileSourceScene { Layout = pictures.Sources[0] };
    }

    private static async DispatcherTask<TesseraSession> OpenTownAsync()
    {
        var session = await TesseraSession.OpenAsync(("Tiles/Town", TestTown.TileSet()));
        session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel);
        return session;
    }
}
