// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Raw;
using Stride.AvaloniaUI.Platform;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using AvaloniaKey = Avalonia.Input.Key;
using RawInputModifiers = Avalonia.Input.RawInputModifiers;
using AvaloniaVector = Avalonia.Vector;

namespace Stride.AvaloniaUI;

/// <summary>
/// Runs Avalonia in the game: pumps its dispatcher, renders the views of the <see cref="AvaloniaUIComponent"/>s, draws
/// them over the game, and gives them the input.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia runs on the game thread, which is its UI thread and its render thread. A game that doesn't set up Avalonia
/// before it runs gets the <see cref="StrideAvaloniaApplication"/>.
/// </para>
/// <para>
/// The input goes through the <see cref="InputManager"/>'s capture: the views capture the mouse with
/// <see cref="InputCapturePriority.Hover"/> while it is over one of their controls or a button pressed on them is held,
/// and the keyboard with <see cref="InputCapturePriority.Focus"/> while a text box has the focus. Scripts then don't see
/// the input the UI takes. A click where no control is gives the focus back to the game.
/// </para>
/// </remarks>
[GameSystem]
public sealed class AvaloniaUISystem : GameSystemBase,
    IInputEventListener<PointerEvent>, IInputEventListener<MouseButtonEvent>, IInputEventListener<MouseWheelEvent>,
    IInputEventListener<KeyEvent>, IInputEventListener<TextInputEvent>
{
    /// <summary>
    /// The draw order of the system: after the scene, under the profiler's overlay.
    /// </summary>
    public const int DefaultDrawOrder = 0xfffff0;

    private readonly List<AvaloniaUIView> views = [];
    private readonly List<AvaloniaUIView> sortedViews = [];
    private readonly List<PendingInput> pendingInput = [];
    private readonly HashSet<Keys> downKeys = [];
    private readonly HashSet<MouseButton> downButtons = [];

    private InputManager? input;
    private SpriteBatch? spriteBatch;
    private GraphicsDevice? attachedDevice;
    private bool renderOnGpu = true;

    // The view under the pointer, the one that got a button press (it keeps the pointer until all buttons are released),
    // and the one that gets the keyboard
    private AvaloniaUIView? hoverView;
    private AvaloniaUIView? pointerView;
    private AvaloniaUIView? focusView;
    private int touchPointerId = -1;
    private Vector2? lastPointerPosition;
    private bool textInputEnabled;

    public AvaloniaUISystem(IServiceRegistry registry)
        : base(registry)
    {
        Enabled = true;
        Visible = true;
        DrawOrder = DefaultDrawOrder;
    }

    /// <summary>
    /// Sets up Avalonia on the Stride platform with <see cref="StrideAvaloniaApplication"/>, unless the game already did.
    /// </summary>
    /// <exception cref="InvalidOperationException">Avalonia runs on another platform, or on another thread.</exception>
    public static void EnsureAvalonia()
    {
        if (!StrideAvaloniaPlatform.IsInitialized)
        {
            if (Application.Current != null)
                throw new InvalidOperationException("Avalonia is already set up on another platform in this process. Set it up with UseStride() to show Avalonia views in a game.");

            AppBuilder.Configure<StrideAvaloniaApplication>()
                .UseStride()
                .WithInterFont()
                .SetupWithoutStarting();
        }

        if (!StrideAvaloniaPlatform.IsUIThread)
            throw new InvalidOperationException("Avalonia runs on another thread. Avalonia views can only be shown by games that run on the thread Avalonia was set up on.");
    }

    public override void Initialize()
    {
        base.Initialize();

        EnsureAvalonia();

        input = Services.GetService<InputManager>();
        if (input != null)
        {
            input.AddListener(this);
            input.ResolvingCapture += OnResolvingCapture;
        }
    }

    /// <summary>
    /// Gets or sets whether the renderer draws the views on the game's graphics device when it can (Skia shares Vulkan
    /// devices), rather than on the CPU. A change takes effect at the next frame.
    /// </summary>
    public bool RenderOnGpu
    {
        get => renderOnGpu;
        set
        {
            if (renderOnGpu == value)
                return;
            renderOnGpu = value;
            // Applied now once the graphics content is loaded, otherwise when it loads
            if (spriteBatch != null)
                UpdateGraphicsDevice();
        }
    }

    /// <summary>
    /// Gets whether the renderer draws the views on the game's graphics device.
    /// </summary>
    public bool IsRenderingOnGpu => attachedDevice != null;

    protected override void LoadContent()
    {
        base.LoadContent();
        spriteBatch = new SpriteBatch(GraphicsDevice);
        UpdateGraphicsDevice();
    }

    protected override void UnloadContent()
    {
        ReleaseGraphics();
        base.UnloadContent();
    }

    protected override void Destroy()
    {
        if (input != null)
        {
            input.ResolvingCapture -= OnResolvingCapture;
            input.RemoveListener(this);
            input.ReleaseAll(this);
            if (textInputEnabled)
                input.TextInput?.DisableTextInput();
        }
        textInputEnabled = false;

        // The processor disposes the views; the textures of those still here go now
        ReleaseGraphics();
        views.Clear();

        base.Destroy();
    }

    // The renderer draws the views on the game's device, or on the CPU, the way RenderOnGpu says
    private void UpdateGraphicsDevice()
    {
        if (renderOnGpu && attachedDevice == null)
        {
            if (StrideAvaloniaPlatform.AttachGraphicsDevice(GraphicsDevice))
                attachedDevice = GraphicsDevice;
        }
        else if (!renderOnGpu && attachedDevice != null)
        {
            StrideAvaloniaPlatform.DetachGraphicsDevice(attachedDevice);
            attachedDevice = null;
        }
    }

    private void ReleaseGraphics()
    {
        foreach (var view in views)
            view.ReleaseTexture();
        // The renderer releases what it has on the device while the device is still there
        if (attachedDevice != null)
            StrideAvaloniaPlatform.DetachGraphicsDevice(attachedDevice);
        attachedDevice = null;
        spriteBatch?.Dispose();
        spriteBatch = null;
    }

    internal void AddView(AvaloniaUIView view) => views.Add(view);

    internal void RemoveView(AvaloniaUIView view)
    {
        views.Remove(view);
        if (ReferenceEquals(hoverView, view))
            hoverView = null;
        if (ReferenceEquals(pointerView, view))
            pointerView = null;
        if (ReferenceEquals(focusView, view))
            focusView = null;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        foreach (var view in views)
            view.SyncContent();

        // Avalonia's due timers and posted jobs
        StrideAvaloniaPlatform.Pump();
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);

        var device = GraphicsDevice;
        var backBuffer = device?.Presenter?.BackBuffer;
        if (views.Count == 0 || device == null || backBuffer == null || spriteBatch == null || Game == null)
            return;

        foreach (var view in views)
        {
            view.SyncContent();
            view.Layout(backBuffer.Width, backBuffer.Height);
        }

        // Layout and composition (dispatcher jobs), then rendering of the views that changed: on the GPU, the renderer
        // submits its commands now, ahead of the command list of this frame
        StrideAvaloniaPlatform.Pump();
        StrideAvaloniaPlatform.Render();

        var graphicsContext = Game.GraphicsContext;
        var commandList = graphicsContext.CommandList;

        // Avalonia renders sRGB colors: sampled as sRGB, they stay right on an sRGB back buffer (linear color space)
        var srgb = backBuffer.Format.IsSRgb;
        var anyShown = false;
        foreach (var view in SortedViews())
        {
            if (!view.Component.Enabled)
                continue;
            view.PrepareFrame(device, commandList, srgb);
            anyShown |= view.IsShown;
        }
        if (!anyShown)
            return;

        commandList.SetRenderTargetAndViewport(null, backBuffer);
        // Avalonia's frames are premultiplied, which is the alpha blending of the sprite batch
        spriteBatch.Begin(graphicsContext, SpriteSortMode.Deferred, BlendStates.AlphaBlend, device.SamplerStates.LinearClamp, DepthStencilStates.None);
        var destination = new RectangleF(0, 0, backBuffer.Width, backBuffer.Height);
        foreach (var view in sortedViews)
        {
            if (view is { IsShown: true, Texture: { } texture })
                spriteBatch.Draw(texture, destination, Color4.White);
        }
        spriteBatch.End();
    }

    // The views from the bottom one to the top one
    private List<AvaloniaUIView> SortedViews()
    {
        sortedViews.Clear();
        sortedViews.AddRange(views);
        sortedViews.Sort(static (a, b) => a.Component.DrawOrder.CompareTo(b.Component.DrawOrder));
        return sortedViews;
    }

    #region Input

    private enum InputKind
    {
        Move,
        ButtonDown,
        ButtonUp,
        Wheel,
        KeyDown,
        KeyUp,
        Text,
    }

    private readonly record struct PendingInput(InputKind Kind, Vector2 Position = default, MouseButton Button = default, float Wheel = 0, Keys Key = default, string? Text = null);

    // The events are kept in their order and given to Avalonia when the input manager resolves the captures, so the
    // captures of this frame follow what the UI did with them

    void IInputEventListener<PointerEvent>.ProcessEvent(PointerEvent inputEvent)
    {
        if (inputEvent.Device is IMouseDevice)
        {
            // The buttons come from the mouse button events
            if (inputEvent.EventType == PointerEventType.Moved)
                pendingInput.Add(new PendingInput(InputKind.Move, inputEvent.Position));
            return;
        }

        // A touch or a pen is the left button of the mouse for now; only the first one down is followed
        switch (inputEvent.EventType)
        {
            case PointerEventType.Pressed when touchPointerId < 0:
                touchPointerId = inputEvent.PointerId;
                pendingInput.Add(new PendingInput(InputKind.Move, inputEvent.Position));
                pendingInput.Add(new PendingInput(InputKind.ButtonDown, inputEvent.Position, MouseButton.Left));
                break;
            case PointerEventType.Moved when inputEvent.PointerId == touchPointerId:
                pendingInput.Add(new PendingInput(InputKind.Move, inputEvent.Position));
                break;
            case PointerEventType.Released or PointerEventType.Canceled when inputEvent.PointerId == touchPointerId:
                touchPointerId = -1;
                pendingInput.Add(new PendingInput(InputKind.ButtonUp, inputEvent.Position, MouseButton.Left));
                break;
        }
    }

    void IInputEventListener<MouseButtonEvent>.ProcessEvent(MouseButtonEvent inputEvent)
        => pendingInput.Add(new PendingInput(inputEvent.IsDown ? InputKind.ButtonDown : InputKind.ButtonUp, inputEvent.Mouse.Position, inputEvent.Button));

    void IInputEventListener<MouseWheelEvent>.ProcessEvent(MouseWheelEvent inputEvent)
        => pendingInput.Add(new PendingInput(InputKind.Wheel, inputEvent.Mouse.Position, Wheel: inputEvent.WheelDelta));

    void IInputEventListener<KeyEvent>.ProcessEvent(KeyEvent inputEvent)
        => pendingInput.Add(new PendingInput(inputEvent.IsDown ? InputKind.KeyDown : InputKind.KeyUp, Key: inputEvent.Key));

    void IInputEventListener<TextInputEvent>.ProcessEvent(TextInputEvent inputEvent)
    {
        // Compositions in progress are not shown yet: only the committed text
        if (inputEvent.Type == TextInputEventType.Input && !string.IsNullOrEmpty(inputEvent.Text))
            pendingInput.Add(new PendingInput(InputKind.Text, Text: inputEvent.Text));
    }

    private void OnResolvingCapture(object? sender, EventArgs e)
    {
        foreach (var pending in pendingInput)
        {
            switch (pending.Kind)
            {
                case InputKind.Move:
                case InputKind.ButtonDown:
                case InputKind.ButtonUp:
                case InputKind.Wheel:
                    DispatchPointer(pending);
                    break;
                default:
                    DispatchKeyboard(pending);
                    break;
            }
        }
        pendingInput.Clear();

        // The views change under a still pointer too (a view shown, hidden or moved): the hover follows them
        if (pointerView is { IsShown: false })
            pointerView = null;
        if (pointerView == null && lastPointerPosition is { } position)
            SetHoverView(HitTest(position));

        if (focusView != null && (!focusView.IsShown || focusView.FocusedElement == null))
            focusView = null;

        UpdateCaptures();
    }

    private void DispatchPointer(in PendingInput pending)
    {
        if (pending.Kind == InputKind.ButtonDown)
            downButtons.Add(pending.Button);
        else if (pending.Kind == InputKind.ButtonUp)
            downButtons.Remove(pending.Button);

        if (pointerView is { IsShown: false })
            pointerView = null;

        lastPointerPosition = pending.Position;
        var target = pointerView ?? HitTest(pending.Position);
        SetHoverView(target);

        if (target == null)
        {
            // A click in the game takes the focus from the UI
            if (pending.Kind == InputKind.ButtonDown)
            {
                focusView?.ClearFocus();
                focusView = null;
            }
            return;
        }

        var position = target.ToClient(pending.Position);
        var modifiers = GetModifiers();
        switch (pending.Kind)
        {
            case InputKind.Move:
                target.Impl.RaisePointer(RawPointerEventType.Move, position, modifiers);
                break;
            case InputKind.ButtonDown:
                target.Impl.RaisePointer(ToButtonDown(pending.Button), position, modifiers);
                pointerView = target;
                if (!ReferenceEquals(focusView, target))
                {
                    focusView?.ClearFocus();
                    focusView = target;
                }
                break;
            case InputKind.ButtonUp:
                target.Impl.RaisePointer(ToButtonUp(pending.Button), position, modifiers);
                if (downButtons.Count == 0)
                    pointerView = null;
                break;
            case InputKind.Wheel:
                target.Impl.RaiseWheel(position, new AvaloniaVector(0, pending.Wheel), modifiers);
                break;
        }
    }

    private void DispatchKeyboard(in PendingInput pending)
    {
        if (pending.Kind == InputKind.KeyDown)
            downKeys.Add(pending.Key);
        else if (pending.Kind == InputKind.KeyUp)
            downKeys.Remove(pending.Key);

        // The keyboard goes to the view that has a focused control
        if (focusView is not { IsShown: true } view || view.FocusedElement == null)
            return;

        switch (pending.Kind)
        {
            case InputKind.KeyDown:
                // Avalonia's keys have the values of Stride's
                view.Impl.RaiseKey(RawKeyEventType.KeyDown, (AvaloniaKey)(int)pending.Key, GetModifiers());
                break;
            case InputKind.KeyUp:
                view.Impl.RaiseKey(RawKeyEventType.KeyUp, (AvaloniaKey)(int)pending.Key, GetModifiers());
                break;
            case InputKind.Text:
                view.Impl.RaiseText(pending.Text!);
                break;
        }
    }

    private void SetHoverView(AvaloniaUIView? view)
    {
        if (ReferenceEquals(view, hoverView))
            return;
        if (hoverView is { IsShown: true })
            hoverView.RaiseLeave(GetModifiers());
        hoverView = view;
    }

    // The topmost shown view with a control under the position
    private AvaloniaUIView? HitTest(Vector2 position)
    {
        var sorted = SortedViews();
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            if (sorted[i].HitTest(position))
                return sorted[i];
        }
        return null;
    }

    private void UpdateCaptures()
    {
        if (input == null)
            return;

        if (input.HasMouse)
        {
            // Hover captures the mouse, except while a button pressed in the game is held, so a game drag that crosses
            // the UI goes on and ends. A locked mouse (mouse look) is never captured, because its position is not a cursor.
            var mouse = input.Mouse;
            var gameDragInProgress = pointerView == null && downButtons.Count > 0 && !ReferenceEquals(mouse.CaptureState.Owner, this);
            var wantsMouse = (pointerView != null || (hoverView != null && !gameDragInProgress)) && !mouse.IsPositionLocked;
            if (wantsMouse)
                input.TryCapture(mouse, this, InputCapturePriority.Hover);
            else
                input.Release(mouse, this);
        }

        // A text box with the focus takes the keyboard, so typing doesn't move the player
        var typing = focusView?.FocusedElement is TextBox;
        if (input.HasKeyboard)
        {
            if (typing)
                input.TryCapture(input.Keyboard, this, InputCapturePriority.Focus);
            else
                input.Release(input.Keyboard, this);
        }

        if (typing != textInputEnabled)
        {
            textInputEnabled = typing;
            if (typing)
                input.TextInput?.EnabledTextInput();
            else
                input.TextInput?.DisableTextInput();
        }
    }

    private RawInputModifiers GetModifiers()
    {
        var modifiers = RawInputModifiers.None;
        if (downKeys.Contains(Keys.LeftShift) || downKeys.Contains(Keys.RightShift))
            modifiers |= RawInputModifiers.Shift;
        if (downKeys.Contains(Keys.LeftCtrl) || downKeys.Contains(Keys.RightCtrl))
            modifiers |= RawInputModifiers.Control;
        if (downKeys.Contains(Keys.LeftAlt) || downKeys.Contains(Keys.RightAlt))
            modifiers |= RawInputModifiers.Alt;
        if (downKeys.Contains(Keys.LeftWin) || downKeys.Contains(Keys.RightWin))
            modifiers |= RawInputModifiers.Meta;
        foreach (var button in downButtons)
        {
            modifiers |= button switch
            {
                MouseButton.Left => RawInputModifiers.LeftMouseButton,
                MouseButton.Right => RawInputModifiers.RightMouseButton,
                MouseButton.Middle => RawInputModifiers.MiddleMouseButton,
                MouseButton.Extended1 => RawInputModifiers.XButton1MouseButton,
                MouseButton.Extended2 => RawInputModifiers.XButton2MouseButton,
                _ => RawInputModifiers.None,
            };
        }
        return modifiers;
    }

    private static RawPointerEventType ToButtonDown(MouseButton button) => button switch
    {
        MouseButton.Right => RawPointerEventType.RightButtonDown,
        MouseButton.Middle => RawPointerEventType.MiddleButtonDown,
        MouseButton.Extended1 => RawPointerEventType.XButton1Down,
        MouseButton.Extended2 => RawPointerEventType.XButton2Down,
        _ => RawPointerEventType.LeftButtonDown,
    };

    private static RawPointerEventType ToButtonUp(MouseButton button) => button switch
    {
        MouseButton.Right => RawPointerEventType.RightButtonUp,
        MouseButton.Middle => RawPointerEventType.MiddleButtonUp,
        MouseButton.Extended1 => RawPointerEventType.XButton1Up,
        MouseButton.Extended2 => RawPointerEventType.XButton2Up,
        _ => RawPointerEventType.LeftButtonUp,
    };

    #endregion
}
