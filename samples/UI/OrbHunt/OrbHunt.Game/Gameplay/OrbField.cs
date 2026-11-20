// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using OrbHunt.ViewModels;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering;

namespace OrbHunt.Gameplay;

/// <summary>
/// The game of the orb field scene: the orbs float over a field of stars, drawn with a sprite batch by the graphics
/// compositor (<see cref="OrbFieldRenderer"/>) over what the camera sees, and the player clicks them for points before
/// they fade.
/// </summary>
/// <remarks>
/// The script reads the mouse as any game script does. While the UI holds the mouse (the pointer is over one of its
/// controls, or a menu covers the screen), the reads report nothing, so the game never sees a click meant for a button.
/// </remarks>
public sealed class OrbField : OrbHuntScript
{
    private readonly Star[] stars = new Star[160];
    private SpriteBatch? spriteBatch;
    private OrbTextures? textures;
    private float time;

    public override void Start()
    {
        base.Start();
        // The graphics compositor's orb field renderer finds the field here
        Services.AddService(this);

        spriteBatch = new SpriteBatch(GraphicsDevice);
        textures = new OrbTextures(GraphicsDevice);
        var random = new Random();
        for (var i = 0; i < stars.Length; i++)
            stars[i] = new Star(random.NextSingle(), random.NextSingle(), 0.3f + random.NextSingle() * 0.7f, random.NextSingle() * MathF.Tau);
    }

    public override void Cancel()
    {
        Services.RemoveService<OrbField>(this);
        spriteBatch?.Dispose();
        textures?.Dispose();
    }

    public override void Update()
    {
        base.Update();
        // The stars stand still while the game is paused
        if (ViewModel.State != GameState.Paused)
            time += (float)Game.UpdateTime.Elapsed.TotalSeconds;
    }

    protected override void Play()
    {
        if (Input.IsMouseButtonPressed(MouseButton.Left) && Swarm.FindOrbAt(Input.MousePosition * new Vector2(Swarm.FieldWidth, OrbSwarm.FieldHeight)) is { } orb)
            PopOrb(orb);
    }

    /// <summary>
    /// Draws the field, before the views are drawn over it. The graphics compositor calls it (<see cref="OrbFieldRenderer"/>).
    /// </summary>
    public void Draw(RenderDrawContext context)
    {
        if (spriteBatch == null || textures == null)
            return;

        var backBuffer = GraphicsDevice.Presenter.BackBuffer;
        var scale = backBuffer.Height / OrbSwarm.FieldHeight;
        var fieldWidth = Swarm.FieldWidth;
        spriteBatch.Begin(context.GraphicsContext, Matrix.Scaling(scale, scale, 1));

        foreach (var star in stars)
        {
            var twinkle = 0.6f + 0.4f * MathF.Sin(time * 2 * star.Depth + star.Phase);
            var position = new Vector2(
                (star.X * fieldWidth - time * 12 * star.Depth) % fieldWidth,
                star.Y * OrbSwarm.FieldHeight);
            if (position.X < 0)
                position.X += fieldWidth;
            var size = 2 + 4 * star.Depth;
            // Alpha 0 adds the color to what is under it (the sprite batch blends premultiplied colors)
            DrawCentered(textures.Glow, position, size, new Color4(0.7f, 0.8f, 1, 0) * (twinkle * star.Depth));
        }

        foreach (var orb in Swarm.Orbs)
        {
            var radius = orb.Radius * orb.Scale;
            var color = orb.Color;
            var glow = orb.IsBomb ? 0.7f + 0.3f * MathF.Sin(time * 8) : 0.45f;
            DrawCentered(textures.Glow, orb.Position, radius * 4, new Color4(color.R, color.G, color.B, 0) * (glow * orb.Opacity));
            DrawCentered(textures.Disc, orb.Position, radius * 2, Premultiply(orb.IsBomb ? new Color4(0.45f, 0.04f, 0.11f, 1) : color, orb.Opacity));
            if (orb.IsBomb)
                DrawCentered(textures.Ring, orb.Position, radius * 2, Premultiply(color, orb.Opacity));
            else
                DrawCentered(textures.Glow, orb.Position - new Vector2(radius * 0.3f), radius * 0.9f, new Color4(1, 1, 1, 0) * (0.5f * orb.Opacity));
        }

        foreach (var ring in Swarm.Rings)
        {
            var fade = 1 - ring.Progress;
            DrawCentered(textures.Ring, ring.Position, ring.Radius * 2 * (1 + ring.Progress * 1.5f), new Color4(ring.Color.R, ring.Color.G, ring.Color.B, 0) * fade);
        }

        spriteBatch.End();
    }

    private void DrawCentered(Texture texture, Vector2 center, float size, Color4 color)
        => spriteBatch!.Draw(texture, new RectangleF(center.X - size / 2, center.Y - size / 2, size, size), color);

    private static Color4 Premultiply(Color4 color, float opacity) => new(color.R * opacity, color.G * opacity, color.B * opacity, opacity);

    private readonly record struct Star(float X, float Y, float Depth, float Phase);
}
