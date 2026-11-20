// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Stride.AvaloniaUI.Sample.ViewModels;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering;

namespace Stride.AvaloniaUI.Sample.Gameplay;

/// <summary>
/// The game itself: orbs float over a field of stars, and the player clicks them for points before they fade.
/// </summary>
/// <remarks>
/// The script reads the mouse and keyboard as any game script does. While the UI holds the mouse (the pointer is over
/// one of its controls, or a menu covers the screen) or the keyboard (a text box has the focus), the reads report
/// nothing, so the game never sees a click meant for a button.
/// </remarks>
public sealed class OrbField : SyncScript
{
    /// <summary>
    /// The height of the field in its own units, whatever the size of the window; its width follows the window's ratio.
    /// </summary>
    private const float FieldHeight = 720;
    // Orbs stay clear of the HUD's top bar and of its hint at the bottom
    private const float TopMargin = 120;
    private const float BottomMargin = 70;
    private const int MaxOrbs = 14;

    private static readonly Color4[] OrbColors =
    [
        new(0.13f, 0.83f, 0.93f, 1), // cyan
        new(0.66f, 0.33f, 0.97f, 1), // violet
        new(0.98f, 0.75f, 0.14f, 1), // gold
        new(0.20f, 0.83f, 0.60f, 1), // green
    ];
    private static readonly Color4 BombColor = new(0.96f, 0.25f, 0.37f, 1);

    private readonly List<Orb> orbs = [];
    private readonly List<Pop> pops = [];
    private readonly Star[] stars = new Star[160];
    private readonly Random random = new();
    private SpriteBatch? spriteBatch;
    private OrbTextures? textures;
    private float fieldWidth = 1280;
    private float spawnTimer;
    private float hurtFlash;
    private float time;
    private bool? renderingOnGpu;

    /// <summary>
    /// Gets the state of the game the views bind to.
    /// </summary>
    [DataMemberIgnore]
    public required GameViewModel ViewModel { get; init; }

    /// <summary>
    /// Gets the HUD, shown during a round.
    /// </summary>
    [DataMemberIgnore]
    public required AvaloniaUIComponent Hud { get; init; }

    /// <summary>
    /// Gets the menus, shown when no round is being played.
    /// </summary>
    [DataMemberIgnore]
    public required AvaloniaUIComponent Menus { get; init; }

    public override void Start()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        textures = new OrbTextures(GraphicsDevice);
        for (var i = 0; i < stars.Length; i++)
            stars[i] = new Star(random.NextSingle(), random.NextSingle(), 0.3f + random.NextSingle() * 0.7f, random.NextSingle() * MathF.Tau);

        ViewModel.RoundStarted += () =>
        {
            orbs.Clear();
            pops.Clear();
            spawnTimer = 0;
        };
    }

    public override void Cancel()
    {
        spriteBatch?.Dispose();
        textures?.Dispose();
    }

    public override void Update()
    {
        // The HUD is created from its view type, as a view of a scene asset is: give it the view model once it exists
        if (Hud.Content is { DataContext: null } hud)
            hud.DataContext = ViewModel;

        // Each view is an entity component: the game shows and hides it
        Hud.Enabled = ViewModel.State != GameState.MainMenu;
        Menus.Enabled = ViewModel.State != GameState.Playing;
        ShowRenderer();

        var backBuffer = GraphicsDevice.Presenter.BackBuffer;
        fieldWidth = FieldHeight * backBuffer.Width / Math.Max(1, backBuffer.Height);

        if (Input.IsKeyPressed(Keys.Escape) || Input.IsKeyPressed(Keys.P))
            ViewModel.TogglePause();
        if (ViewModel.State == GameState.Paused)
            return;

        var elapsed = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        var playing = ViewModel.State == GameState.Playing;
        time += elapsed;
        hurtFlash = Math.Max(0, hurtFlash - elapsed);

        MoveOrbs(elapsed);
        SpawnOrbs(elapsed, playing);
        pops.RemoveAll(pop => (pop.Age += elapsed) > Pop.Duration);

        if (!playing)
            return;

        if (Input.IsMouseButtonPressed(MouseButton.Left))
            Click(Input.MousePosition * new Vector2(fieldWidth, FieldHeight));

        ViewModel.TimeLeft = Math.Max(0, ViewModel.TimeLeft - elapsed);
        if (ViewModel.TimeLeft <= 0 || ViewModel.Lives <= 0)
            ViewModel.EndRound();
    }

    /// <summary>
    /// Draws the field, before the views are drawn over it. The graphics compositor calls it.
    /// </summary>
    public void Draw(RenderDrawContext context)
    {
        if (spriteBatch == null || textures == null)
            return;

        var backBuffer = GraphicsDevice.Presenter.BackBuffer;
        var scale = backBuffer.Height / FieldHeight;
        spriteBatch.Begin(context.GraphicsContext, Matrix.Scaling(scale, scale, 1));

        foreach (var star in stars)
        {
            var twinkle = 0.6f + 0.4f * MathF.Sin(time * 2 * star.Depth + star.Phase);
            var position = new Vector2(
                (star.X * fieldWidth - time * 12 * star.Depth) % fieldWidth,
                star.Y * FieldHeight);
            if (position.X < 0)
                position.X += fieldWidth;
            var size = 2 + 4 * star.Depth;
            // Alpha 0 adds the color to what is under it (the sprite batch blends premultiplied colors)
            DrawCentered(textures.Glow, position, size, new Color4(0.7f, 0.8f, 1, 0) * (twinkle * star.Depth));
        }

        foreach (var orb in orbs)
        {
            var radius = orb.Radius * orb.Scale;
            var color = orb.IsBomb ? BombColor : orb.Color;
            var glow = orb.IsBomb ? 0.7f + 0.3f * MathF.Sin(time * 8) : 0.45f;
            DrawCentered(textures.Glow, orb.Position, radius * 4, new Color4(color.R, color.G, color.B, 0) * (glow * orb.Opacity));
            DrawCentered(textures.Disc, orb.Position, radius * 2, Premultiply(orb.IsBomb ? new Color4(0.45f, 0.04f, 0.11f, 1) : color, orb.Opacity));
            if (orb.IsBomb)
                DrawCentered(textures.Ring, orb.Position, radius * 2, Premultiply(BombColor, orb.Opacity));
            else
                DrawCentered(textures.Glow, orb.Position - new Vector2(radius * 0.3f), radius * 0.9f, new Color4(1, 1, 1, 0) * (0.5f * orb.Opacity));
        }

        foreach (var pop in pops)
        {
            var progress = pop.Age / Pop.Duration;
            var fade = 1 - progress;
            DrawCentered(textures.Ring, pop.Position, pop.Radius * 2 * (1 + progress * 1.5f), new Color4(pop.Color.R, pop.Color.G, pop.Color.B, 0) * fade);
        }

        if (hurtFlash > 0)
            spriteBatch.Draw(textures.Glow, new RectangleF(-fieldWidth * 0.25f, -FieldHeight * 0.25f, fieldWidth * 1.5f, FieldHeight * 1.5f), new Color4(0.8f, 0, 0.1f, 0) * hurtFlash);

        spriteBatch.End();
    }

    private void DrawCentered(Texture texture, Vector2 center, float size, Color4 color)
        => spriteBatch!.Draw(texture, new RectangleF(center.X - size / 2, center.Y - size / 2, size, size), color);

    private static Color4 Premultiply(Color4 color, float opacity) => new(color.R * opacity, color.G * opacity, color.B * opacity, opacity);

    private void MoveOrbs(float elapsed)
    {
        var speed = (float)ViewModel.OrbSpeed;
        for (var i = orbs.Count - 1; i >= 0; i--)
        {
            var orb = orbs[i];
            orb.Age += elapsed;
            if (orb.Age > orb.Lifetime)
            {
                orbs.RemoveAt(i);
                continue;
            }

            orb.Position += orb.Velocity * speed * elapsed;
            // Bounce on the edges of the field
            if (orb.Position.X < orb.Radius && orb.Velocity.X < 0 || orb.Position.X > fieldWidth - orb.Radius && orb.Velocity.X > 0)
                orb.Velocity.X = -orb.Velocity.X;
            if (orb.Position.Y < TopMargin && orb.Velocity.Y < 0 || orb.Position.Y > FieldHeight - BottomMargin && orb.Velocity.Y > 0)
                orb.Velocity.Y = -orb.Velocity.Y;
        }
    }

    private void SpawnOrbs(float elapsed, bool playing)
    {
        spawnTimer -= elapsed * (float)ViewModel.OrbSpeed;
        if (spawnTimer > 0 || orbs.Count >= MaxOrbs)
            return;

        spawnTimer = playing ? 0.35f + random.NextSingle() * 0.4f : 0.8f;
        var radius = 18 + random.NextSingle() * 26;
        var angle = random.NextSingle() * MathF.Tau;
        orbs.Add(new Orb
        {
            Position = new Vector2(radius + random.NextSingle() * (fieldWidth - 2 * radius), TopMargin + random.NextSingle() * (FieldHeight - TopMargin - BottomMargin)),
            Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (40 + random.NextSingle() * 80),
            Radius = radius,
            Color = OrbColors[random.Next(OrbColors.Length)],
            IsBomb = playing && ViewModel.BombsEnabled && random.NextSingle() < 0.2f,
            Lifetime = 4.5f + random.NextSingle() * 2.5f,
        });
    }

    private void Click(Vector2 position)
    {
        // The orb on top is the last one drawn
        for (var i = orbs.Count - 1; i >= 0; i--)
        {
            var orb = orbs[i];
            var radius = orb.Radius * orb.Scale;
            if (Vector2.DistanceSquared(orb.Position, position) > radius * radius)
                continue;

            orbs.RemoveAt(i);
            pops.Add(new Pop(orb.Position, radius, orb.IsBomb ? BombColor : orb.Color));
            if (orb.IsBomb)
            {
                ViewModel.Lives--;
                hurtFlash = 0.6f;
            }
            else
            {
                // The small orbs are worth more
                ViewModel.Score += orb.Radius < 26 ? 30 : orb.Radius < 36 ? 20 : 10;
            }
            return;
        }
    }

    private void ShowRenderer()
    {
        var onGpu = Services.GetService<AvaloniaUISystem>()?.IsRenderingOnGpu == true;
        if (onGpu == renderingOnGpu)
            return;

        renderingOnGpu = onGpu;
        var renderer = ((OrbHuntGame)Game).Renderer == AvaloniaUIRenderer.Native ? "Stride's native renderer" : "Skia";
        ViewModel.RendererName = $"UI drawn by {renderer} on the {(onGpu ? "GPU" : "CPU")} · {GraphicsDevice.Platform}";
    }

    private sealed class Orb
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public Color4 Color;
        public bool IsBomb;
        public float Age;
        public float Lifetime;

        // Grows in, shrinks and fades out at the end of its life
        public float Scale => Math.Min(1, Age / 0.25f) * (1 - 0.3f * FadeOut);

        public float Opacity => Math.Min(1, Age / 0.25f) * (1 - FadeOut);

        private float FadeOut => Math.Clamp((Age - (Lifetime - 0.8f)) / 0.8f, 0, 1);
    }

    private sealed class Pop(Vector2 position, float radius, Color4 color)
    {
        public const float Duration = 0.35f;

        public Vector2 Position { get; } = position;

        public float Radius { get; } = radius;

        public Color4 Color { get; } = color;

        public float Age { get; set; }
    }

    private readonly record struct Star(float X, float Y, float Depth, float Phase);
}
