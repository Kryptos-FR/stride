// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Stride.AvaloniaUI.Platform;
using Stride.Core;
using Stride.Games;
using Stride.Input;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The Avalonia UI system of a game with simulated input and no graphics device: frames render on the CPU as in a game,
/// and only the upload to a texture and the drawing are left out.
/// </summary>
internal sealed class HeadlessAvaloniaGame : IDisposable
{
    private readonly GameTime gameTime = new();
    private readonly List<AvaloniaUIView> views = [];

    public HeadlessAvaloniaGame(int width = 400, int height = 300)
    {
        Width = width;
        Height = height;

        Input = new InputManager();
        Input.Initialize(new GameContextHeadless());
        var source = new InputSourceSimulated();
        Input.Sources.Add(source);
        Keyboard = source.AddKeyboard();
        Mouse = source.AddMouse();

        var services = new ServiceRegistry();
        services.AddService(Input);
        System = new AvaloniaUISystem(services);
        System.Initialize();
    }

    /// <summary>
    /// Gets the size of the back buffer the views cover.
    /// </summary>
    public int Width { get; }

    public int Height { get; }

    public InputManager Input { get; }

    public KeyboardSimulated Keyboard { get; }

    public MouseSimulated Mouse { get; }

    public AvaloniaUISystem System { get; }

    /// <summary>
    /// Shows a view, as the processor does when an entity with the component enters the scene, and runs frames until
    /// it is rendered (the first frames lay it out at the back buffer's size).
    /// </summary>
    public AvaloniaUIView Show(AvaloniaUIComponent component)
    {
        var view = new AvaloniaUIView(component);
        System.AddView(view);
        views.Add(view);
        for (var frame = 0; frame < MaxFramesToShow && !view.IsShown; frame++)
            Frame();
        if (!view.IsShown)
            throw new InvalidOperationException($"The view was not rendered in {MaxFramesToShow} frames.");
        return view;
    }

    /// <summary>
    /// The most frames a view may take to be rendered once shown.
    /// </summary>
    public const int MaxFramesToShow = 5;

    public AvaloniaUIView Show(Control content, int drawOrder = 0)
        => Show(new AvaloniaUIComponent { Content = content, Resolution = new Vector2(Width, Height), DrawOrder = drawOrder });

    /// <summary>
    /// Runs a game frame: input, update, then the CPU part of the draw.
    /// </summary>
    public void Frame()
    {
        Input.Update(gameTime);
        System.Update(gameTime);

        foreach (var view in views)
        {
            view.SyncContent();
            view.Layout(Width, Height);
        }
        StrideAvaloniaPlatform.Pump();
        // No graphics device: the renderer draws on the CPU, without the game's graphics context
        StrideAvaloniaPlatform.Render(null);
    }

    /// <summary>
    /// Moves the mouse over the center of a control.
    /// </summary>
    public void MoveTo(Control control)
    {
        var root = TopLevel.GetTopLevel(control) ?? throw new InvalidOperationException("The control is not shown.");
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root)
            ?? throw new InvalidOperationException("The control is not in its top level.");
        MoveTo(new Vector2((float)(center.X / root.ClientSize.Width), (float)(center.Y / root.ClientSize.Height)));
    }

    /// <summary>
    /// Moves the mouse to a position normalized over the back buffer, and runs a frame.
    /// </summary>
    public void MoveTo(Vector2 position)
    {
        Mouse.SetPosition(position);
        Frame();
    }

    public void Click(MouseButton button = MouseButton.Left)
    {
        Mouse.SimulateMouseDown(button);
        Frame();
        Mouse.SimulateMouseUp(button);
        Frame();
    }

    public void Press(Keys key)
    {
        Keyboard.SimulateDown(key);
        Frame();
        Keyboard.SimulateUp(key);
        Frame();
    }

    public void Dispose()
    {
        foreach (var view in views)
        {
            System.RemoveView(view);
            view.Dispose();
        }
        System.Dispose();
        Input.Dispose();
    }
}
