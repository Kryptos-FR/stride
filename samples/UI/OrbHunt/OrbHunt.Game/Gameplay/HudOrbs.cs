// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using OrbHunt.ViewModels;
using Stride.Core.Mathematics;
using Color = Avalonia.Media.Color;

namespace OrbHunt.Gameplay;

/// <summary>
/// The game of the planet scene: the orbs are controls of the HUD, drawn by Avalonia over the 3D scene, and a press on
/// one pops it.
/// </summary>
/// <remarks>
/// Every frame, the script gives each orb of the swarm a view model in <see cref="GameViewModel.Orbs"/>, which the HUD
/// lays out on a canvas. The HUD is laid out <see cref="OrbSwarm.FieldHeight"/> units high (its resolution stretch keeps
/// the height), so the units of the field are the units of the canvas. The press on an orb runs its command: the game
/// gets no click to hit test.
/// </remarks>
public sealed class HudOrbs : OrbHuntScript
{
    // The view model of each orb and ring of the swarm
    private readonly Dictionary<object, OrbViewModel> views = [];
    private readonly HashSet<object> current = [];
    // The brushes of each color of orb, and of the red orbs, made once the game runs Avalonia
    private OrbLook[] looks = [];
    private OrbLook? bombLook;

    public override void Start()
    {
        base.Start();
        looks = [.. OrbSwarm.OrbColors.Select(color => new OrbLook(CreateOrbBrush(ToColor(color)), ToColor(color)))];
        bombLook = new OrbLook(CreateOrbBrush(Color.FromRgb(140, 14, 36)), ToColor(OrbSwarm.BombColor));
    }

    public override void Update()
    {
        base.Update();
        ShowOrbs();
    }

    private void ShowOrbs()
    {
        current.Clear();
        var pulse = 0.7 + 0.3 * Math.Sin(Game.UpdateTime.Total.TotalSeconds * 8);

        foreach (var orb in Swarm.Orbs)
        {
            if (!views.TryGetValue(orb, out var view))
            {
                var look = LookOf(orb);
                Add(orb, view = new OrbViewModel(look.Fill, look.Glow, orb.IsBomb, new RelayCommand(() => PopOrb(orb))));
            }

            // The square holds the orb and its light, twice as large
            var radius = orb.Radius * orb.Scale;
            Place(view, orb.Position, radius * 4, orb.Opacity);
            view.GlowOpacity = orb.IsBomb ? pulse : 0.6;
            current.Add(orb);
        }

        foreach (var ring in Swarm.Rings)
        {
            if (!views.TryGetValue(ring, out var view))
            {
                var look = LookOf(ring.Orb);
                Add(ring, view = new OrbViewModel(look.Ring, look.Glow, ring.Orb.IsBomb, popCommand: null));
            }

            Place(view, ring.Position, ring.Radius * 2 * (1 + ring.Progress * 1.5f), 1 - ring.Progress);
            current.Add(ring);
        }

        // The orbs that popped or faded, and the rings that ended
        foreach (var (item, view) in views.Where(x => !current.Contains(x.Key)).ToList())
        {
            views.Remove(item);
            ViewModel.Orbs.Remove(view);
        }
    }

    private OrbLook LookOf(Orb orb) => orb.IsBomb ? bombLook! : looks[orb.ColorIndex];

    private void Add(object item, OrbViewModel view)
    {
        views.Add(item, view);
        // Last on top, as the swarm orders its orbs
        ViewModel.Orbs.Add(view);
    }

    private static void Place(OrbViewModel view, Vector2 center, float size, float opacity)
    {
        view.Left = center.X - size / 2;
        view.Top = center.Y - size / 2;
        view.Size = size;
        view.Opacity = opacity;
    }

    private static Color ToColor(Color4 color) => Color.FromRgb((byte)(color.R * 255), (byte)(color.G * 255), (byte)(color.B * 255));

    // A ball lit from the top left: light at the top left, the color in the middle, darker at the edge
    private static IBrush CreateOrbBrush(Color color) => new RadialGradientBrush
    {
        GradientOrigin = new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Mix(color, Colors.White, 0.6), 0),
            new GradientStop(color, 0.55),
            new GradientStop(Mix(color, Colors.Black, 0.45), 1),
        },
    }.ToImmutable();

    // A light that fades out from the edge of the orb, which covers the middle half of the square
    private static IBrush CreateGlowBrush(Color color) => new RadialGradientBrush
    {
        GradientStops =
        {
            new GradientStop(Color.FromArgb(150, color.R, color.G, color.B), 0.45),
            new GradientStop(Color.FromArgb(60, color.R, color.G, color.B), 0.65),
            new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
        },
    }.ToImmutable();

    /// <summary>
    /// The brushes of a color of orb: the orb, its light, and the ring it leaves.
    /// </summary>
    private sealed class OrbLook(IBrush fill, Color color)
    {
        public IBrush Fill { get; } = fill;

        public IBrush Glow { get; } = CreateGlowBrush(color);

        public IBrush Ring { get; } = new SolidColorBrush(color).ToImmutable();
    }

    private static Color Mix(Color color, Color other, double amount) => Color.FromRgb(
        (byte)(color.R + (other.R - color.R) * amount),
        (byte)(color.G + (other.G - color.G) * amount),
        (byte)(color.B + (other.B - color.B) * amount));
}
