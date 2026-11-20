// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace OrbHunt.Gameplay;

/// <summary>
/// The orbs of the game: where they float, when they come and fade, and the rings they leave when they pop. Both scenes
/// play the same swarm and only draw it differently.
/// </summary>
/// <remarks>
/// The swarm lives in a field <see cref="FieldHeight"/> units high, whatever the size of the window; its width follows
/// the window's ratio (<see cref="FieldWidth"/>).
/// </remarks>
public sealed class OrbSwarm
{
    public const float FieldHeight = 720;
    // Orbs stay clear of the HUD's top bar and of its hint at the bottom
    private const float TopMargin = 120;
    private const float BottomMargin = 70;
    private const int MaxOrbs = 14;

    /// <summary>
    /// The colors of the orbs worth points.
    /// </summary>
    public static readonly Color4[] OrbColors =
    [
        new(0.13f, 0.83f, 0.93f, 1), // cyan
        new(0.66f, 0.33f, 0.97f, 1), // violet
        new(0.98f, 0.75f, 0.14f, 1), // gold
        new(0.20f, 0.83f, 0.60f, 1), // green
    ];

    /// <summary>
    /// The color of the red orbs, which cost a life.
    /// </summary>
    public static readonly Color4 BombColor = new(0.96f, 0.25f, 0.37f, 1);

    private readonly List<Orb> orbs = [];
    private readonly List<PopRing> rings = [];
    private readonly Random random = new();
    private float spawnTimer;

    /// <summary>
    /// Gets or sets the width of the field, in the units of <see cref="FieldHeight"/>.
    /// </summary>
    public float FieldWidth { get; set; } = 1280;

    /// <summary>
    /// Gets the orbs, the one on top last.
    /// </summary>
    public IReadOnlyList<Orb> Orbs => orbs;

    /// <summary>
    /// Gets the rings of the orbs that just popped.
    /// </summary>
    public IReadOnlyList<PopRing> Rings => rings;

    /// <summary>
    /// Empties the field, for a new round.
    /// </summary>
    public void Clear()
    {
        orbs.Clear();
        rings.Clear();
        spawnTimer = 0;
    }

    /// <summary>
    /// Moves the orbs, brings new ones and ages the rings.
    /// </summary>
    /// <param name="elapsed">The seconds since the last update.</param>
    /// <param name="speed">How fast the orbs move and come, from the main menu.</param>
    /// <param name="playing">Whether a round is being played; between rounds, fewer orbs come and none is red.</param>
    /// <param name="bombs">Whether red orbs come during a round.</param>
    public void Update(float elapsed, float speed, bool playing, bool bombs)
    {
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
            if (orb.Position.X < orb.Radius && orb.Velocity.X < 0 || orb.Position.X > FieldWidth - orb.Radius && orb.Velocity.X > 0)
                orb.Velocity.X = -orb.Velocity.X;
            if (orb.Position.Y < TopMargin && orb.Velocity.Y < 0 || orb.Position.Y > FieldHeight - BottomMargin && orb.Velocity.Y > 0)
                orb.Velocity.Y = -orb.Velocity.Y;
        }

        rings.RemoveAll(ring => (ring.Age += elapsed) > PopRing.Duration);

        spawnTimer -= elapsed * speed;
        if (spawnTimer > 0 || orbs.Count >= MaxOrbs)
            return;

        spawnTimer = playing ? 0.35f + random.NextSingle() * 0.4f : 0.8f;
        var radius = 18 + random.NextSingle() * 26;
        var angle = random.NextSingle() * MathF.Tau;
        orbs.Add(new Orb
        {
            Position = new Vector2(radius + random.NextSingle() * (FieldWidth - 2 * radius), TopMargin + random.NextSingle() * (FieldHeight - TopMargin - BottomMargin)),
            Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (40 + random.NextSingle() * 80),
            Radius = radius,
            ColorIndex = random.Next(OrbColors.Length),
            IsBomb = playing && bombs && random.NextSingle() < 0.2f,
            Lifetime = 4.5f + random.NextSingle() * 2.5f,
        });
    }

    /// <summary>
    /// Finds the orb under a point of the field.
    /// </summary>
    /// <returns>The orb on top under the point, or <c>null</c>.</returns>
    public Orb? FindOrbAt(Vector2 position)
    {
        for (var i = orbs.Count - 1; i >= 0; i--)
        {
            var orb = orbs[i];
            var radius = orb.Radius * orb.Scale;
            if (Vector2.DistanceSquared(orb.Position, position) <= radius * radius)
                return orb;
        }
        return null;
    }

    /// <summary>
    /// Pops an orb: it leaves a ring where it was.
    /// </summary>
    /// <returns><c>false</c> if the orb was already gone.</returns>
    public bool Pop(Orb orb)
    {
        if (!orbs.Remove(orb))
            return false;

        rings.Add(new PopRing(orb));
        return true;
    }
}

/// <summary>
/// An orb of the <see cref="OrbSwarm"/>.
/// </summary>
public sealed class Orb
{
    public Vector2 Position;
    public Vector2 Velocity;

    public float Radius { get; init; }

    public int ColorIndex { get; init; }

    public bool IsBomb { get; init; }

    public float Lifetime { get; init; }

    public float Age { get; set; }

    public Color4 Color => IsBomb ? OrbSwarm.BombColor : OrbSwarm.OrbColors[ColorIndex];

    /// <summary>
    /// Gets what the orb is worth: the small ones are worth more.
    /// </summary>
    public int Points => Radius < 26 ? 30 : Radius < 36 ? 20 : 10;

    /// <summary>
    /// Gets the size of the orb, from 0 to 1: it grows in, and shrinks at the end of its life.
    /// </summary>
    public float Scale => Math.Min(1, Age / 0.25f) * (1 - 0.3f * FadeOut);

    /// <summary>
    /// Gets the opacity of the orb, from 0 to 1: it fades in, and out at the end of its life.
    /// </summary>
    public float Opacity => Math.Min(1, Age / 0.25f) * (1 - FadeOut);

    private float FadeOut => Math.Clamp((Age - (Lifetime - 0.8f)) / 0.8f, 0, 1);
}

/// <summary>
/// The ring an orb leaves when it pops, which grows and fades.
/// </summary>
public sealed class PopRing(Orb orb)
{
    public const float Duration = 0.35f;

    /// <summary>
    /// Gets the orb that popped, which stays where it was.
    /// </summary>
    public Orb Orb { get; } = orb;

    public Vector2 Position => Orb.Position;

    public float Radius => Orb.Radius * Orb.Scale;

    public Color4 Color => Orb.Color;

    public float Age { get; set; }

    /// <summary>
    /// Gets how far the ring is in its life, from 0 to 1.
    /// </summary>
    public float Progress => Math.Min(1, Age / Duration);
}
