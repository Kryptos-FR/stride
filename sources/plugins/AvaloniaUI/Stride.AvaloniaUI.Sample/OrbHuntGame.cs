// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.AvaloniaUI.Sample.Gameplay;
using Stride.AvaloniaUI.Sample.ViewModels;
using Stride.AvaloniaUI.Sample.Views;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering.Compositing;

namespace Stride.AvaloniaUI.Sample;

/// <summary>
/// Orb Hunt: a small game whose menus and HUD are Avalonia views written in XAML, drawn by Stride over the game.
/// </summary>
/// <remarks>
/// The scene is built in code, so the sample needs no assets. A game made in Game Studio would have the same entities
/// in its scene asset, with the views named by their type.
/// </remarks>
public sealed class OrbHuntGame : Game
{
    public OrbHuntGame(AvaloniaUIRenderer renderer)
    {
        Renderer = renderer;
        GraphicsDeviceManager.PreferredBackBufferWidth = 1280;
        GraphicsDeviceManager.PreferredBackBufferHeight = 720;
    }

    /// <summary>
    /// Gets the renderer Avalonia was set up with, which draws the views.
    /// </summary>
    public AvaloniaUIRenderer Renderer { get; }

    protected override void BeginRun()
    {
        base.BeginRun();

        Window.Title = "Orb Hunt · Avalonia UI on Stride";
        Window.AllowUserResizing = true;

        var viewModel = new GameViewModel();
        viewModel.QuitRequested += Exit;

        // The HUD names its view by type, as a scene asset does (Game Studio's "View type" property); the game gives it
        // its view model once it is created
        var hud = new AvaloniaUIComponent
        {
            ViewType = "Stride.AvaloniaUI.Sample.Views.HudView, Stride.AvaloniaUI.Sample",
            Enabled = false,
        };

        // The menus are a view made in code, with its view model; drawn over the HUD and given the input first
        var menus = new AvaloniaUIComponent
        {
            Content = new MenuView { DataContext = viewModel },
            DrawOrder = 1,
        };

        var field = new OrbField { ViewModel = viewModel, Hud = hud, Menus = menus };

        var scene = new Scene();
        scene.Entities.Add(new Entity("Orb field") { field });
        scene.Entities.Add(new Entity("HUD") { hud });
        scene.Entities.Add(new Entity("Menus") { menus });
        SceneSystem.SceneInstance = new SceneInstance(Services, scene);

        // The game draws its field after clearing the screen; the Avalonia views are drawn over it
        SceneSystem.GraphicsCompositor = new GraphicsCompositor
        {
            Game = new SceneRendererCollection
            {
                new ClearRenderer { Color = new Color(8, 12, 26) },
                new DelegateSceneRenderer(field.Draw),
            },
        };
    }
}
