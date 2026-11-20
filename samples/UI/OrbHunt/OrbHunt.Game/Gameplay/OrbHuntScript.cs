// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using OrbHunt.ViewModels;
using Stride.AvaloniaUI;
using Stride.Core;
using Stride.Engine;
using Stride.Graphics;
using Stride.Input;

namespace OrbHunt.Gameplay;

/// <summary>
/// The game, as both scenes play it: the rounds, the views and the orbs (<see cref="OrbSwarm"/>). Each scene has a script
/// that derives from it and draws the orbs its own way: <see cref="OrbField"/> with the graphics compositor, over a field of
/// stars, and <see cref="HudOrbs"/> as controls of the HUD, over a planet.
/// </summary>
/// <remarks>
/// The script reads the keyboard as any game script does. While the UI holds the keyboard (a text box has the focus), the
/// reads report nothing, so the game never sees the keys typed in a name.
/// </remarks>
public abstract class OrbHuntScript : SyncScript
{
    private bool? renderingOnGpu;

    /// <summary>
    /// Gets the state of the game the views bind to.
    /// </summary>
    [DataMemberIgnore]
    public GameViewModel ViewModel { get; } = new();

    /// <summary>
    /// Gets or sets the HUD, shown during a round.
    /// </summary>
    /// <userdoc>The view of the HUD, shown during a round.</userdoc>
    public AvaloniaUIComponent? Hud { get; set; }

    /// <summary>
    /// Gets or sets the menus, shown when no round is being played.
    /// </summary>
    /// <userdoc>The view of the menus, shown when no round is being played.</userdoc>
    public AvaloniaUIComponent? Menus { get; set; }

    /// <summary>
    /// Gets the orbs.
    /// </summary>
    protected OrbSwarm Swarm { get; } = new();

    public override void Start()
    {
        Game.Window.Title = "Orb Hunt · Avalonia UI on Stride";
        Game.Window.AllowUserResizing = true;

        ViewModel.RoundStarted += Swarm.Clear;
        ViewModel.QuitRequested += ((Game)Game).Exit;
    }

    public override void Update()
    {
        // The views are created from their view type when their entity enters the scene: give them the view model
        // once they exist. Each view is an entity component, which the game shows and hides.
        if (Hud != null)
        {
            if (Hud.Content is { DataContext: null } hud)
                hud.DataContext = ViewModel;
            Hud.Enabled = ViewModel.State != GameState.MainMenu;
        }
        if (Menus != null)
        {
            if (Menus.Content is { DataContext: null } menus)
                menus.DataContext = ViewModel;
            Menus.Enabled = ViewModel.State != GameState.Playing;
        }
        ShowRenderer();

        var backBuffer = GraphicsDevice.Presenter.BackBuffer;
        Swarm.FieldWidth = OrbSwarm.FieldHeight * backBuffer.Width / Math.Max(1, backBuffer.Height);

        if (Input.IsKeyPressed(Keys.Escape) || Input.IsKeyPressed(Keys.P))
            ViewModel.TogglePause();
        if (ViewModel.State == GameState.Paused)
            return;

        var elapsed = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        var playing = ViewModel.State == GameState.Playing;
        ViewModel.Hurt = Math.Max(0, ViewModel.Hurt - elapsed / 0.6);
        Swarm.Update(elapsed, (float)ViewModel.OrbSpeed, playing, ViewModel.BombsEnabled);

        if (!playing)
            return;

        Play();
        ViewModel.TimeLeft = Math.Max(0, ViewModel.TimeLeft - elapsed);
        if (ViewModel.TimeLeft <= 0 || ViewModel.Lives <= 0)
            ViewModel.EndRound();
    }

    /// <summary>
    /// Takes the input of the player during a round, once the orbs have moved.
    /// </summary>
    protected virtual void Play()
    {
    }

    /// <summary>
    /// Pops an orb the player hit: a red one costs a life, the others are worth points.
    /// </summary>
    protected void PopOrb(Orb orb)
    {
        if (ViewModel.State != GameState.Playing || !Swarm.Pop(orb))
            return;

        if (orb.IsBomb)
        {
            ViewModel.Lives--;
            ViewModel.Hurt = 1;
        }
        else
        {
            ViewModel.Score += orb.Points;
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
}
