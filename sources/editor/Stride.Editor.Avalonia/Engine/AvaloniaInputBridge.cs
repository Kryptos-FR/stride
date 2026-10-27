// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using Avalonia.Input;
using Stride.Core.Mathematics;
using Stride.Input;
using MouseButton = Stride.Input.MouseButton;

namespace Stride.Editor.Avalonia.Engine;

/// <summary>
/// Feeds the keyboard and pointer input of an <see cref="OffscreenGameView"/> to the game (viewport spike, options B and B').
/// </summary>
/// <remarks>
/// A window-less game has no input source of its own (<c>InputSourceFactory</c> gives none to a headless context). The
/// bridge adds an <see cref="InputSourceSimulated"/> to the game: the view queues its Avalonia events on the UI thread,
/// and the game applies them on its own thread before its update. Avalonia's <see cref="Key"/> has the values of Stride's
/// <see cref="Keys"/> (both follow WPF), so a key converts with a cast. A real implementation would be a dedicated input
/// source (<c>InputSourceAvalonia</c>, like <c>InputSourceWinforms</c>), with text input and the mouse lock of the
/// camera controls.
/// </remarks>
internal sealed class AvaloniaInputBridge
{
    private readonly ConcurrentQueue<Action<MouseSimulated, KeyboardSimulated>> pending = new();
    private InputSourceSimulated? source;
    private MouseSimulated? mouse;
    private KeyboardSimulated? keyboard;
    private int applied;

    public AvaloniaInputBridge(OffscreenGameView view)
    {
        view.PointerMoved += (_, e) => QueuePosition(view, e);
        view.PointerPressed += (_, e) =>
        {
            view.Focus();
            QueuePosition(view, e);
            var button = ToMouseButton(e.GetCurrentPoint(view).Properties.PointerUpdateKind);
            pending.Enqueue((m, _) => m.SimulateMouseDown(button));
        };
        view.PointerReleased += (_, e) =>
        {
            QueuePosition(view, e);
            var button = ToMouseButton(e.GetCurrentPoint(view).Properties.PointerUpdateKind);
            pending.Enqueue((m, _) => m.SimulateMouseUp(button));
        };
        view.PointerWheelChanged += (_, e) =>
        {
            var delta = (float)e.Delta.Y;
            pending.Enqueue((m, _) => m.SimulateMouseWheel(delta));
        };
        view.KeyDown += (_, e) =>
        {
            var key = (Keys)(int)e.Key;
            pending.Enqueue((_, k) => k.SimulateDown(key));
        };
        view.KeyUp += (_, e) =>
        {
            var key = (Keys)(int)e.Key;
            pending.Enqueue((_, k) => k.SimulateUp(key));
        };
    }

    /// <summary>
    /// Applies the queued input to the game (game thread, before the update of its input manager).
    /// </summary>
    public void Apply(InputManager input)
    {
        if (source == null)
        {
            source = new InputSourceSimulated();
            input.Sources.Add(source);
            mouse = source.AddMouse();
            keyboard = source.AddKeyboard();
        }
        while (pending.TryDequeue(out var action))
        {
            action(mouse!, keyboard!);
            applied++;
        }
    }

    /// <summary>
    /// Describes the input state the game sees, for the viewport stats.
    /// </summary>
    public string Describe(InputManager input)
    {
        var description = $"input: applied={applied} stride-mouse=({input.MousePosition.X:F2},{input.MousePosition.Y:F2}) left-down={input.IsMouseButtonDown(MouseButton.Left)} keys-down={input.DownKeys.Count}";
        applied = 0;
        return description;
    }

    private void QueuePosition(OffscreenGameView view, PointerEventArgs e)
    {
        // Stride's mouse positions are normalized to the surface
        var point = e.GetPosition(view);
        var size = view.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;
        var position = new Vector2((float)(point.X / size.Width), (float)(point.Y / size.Height));
        pending.Enqueue((m, _) => m.SetPosition(position));
    }

    private static MouseButton ToMouseButton(PointerUpdateKind kind)
    {
        return kind switch
        {
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => MouseButton.Right,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => MouseButton.Middle,
            _ => MouseButton.Left,
        };
    }
}
