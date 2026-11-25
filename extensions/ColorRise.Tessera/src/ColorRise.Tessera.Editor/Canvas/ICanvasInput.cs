using Stride.Core.Mathematics;

namespace ColorRise.Tessera.Editor.Canvas;

/// <summary>
/// A button of the pointer the canvas tells its input about; the middle button pans the canvas.
/// </summary>
public enum CanvasButton
{
    /// <summary>
    /// The left button.
    /// </summary>
    Left,

    /// <summary>
    /// The right button.
    /// </summary>
    Right,
}

/// <summary>
/// The modifier keys held down.
/// </summary>
[Flags]
public enum CanvasModifiers
{
    /// <summary>
    /// None.
    /// </summary>
    None = 0,

    /// <summary>
    /// Shift.
    /// </summary>
    Shift = 1,

    /// <summary>
    /// Control.
    /// </summary>
    Control = 2,

    /// <summary>
    /// Alt.
    /// </summary>
    Alt = 4,
}

/// <summary>
/// The keys the canvas tells its input about, as the keyboard layout names them.
/// </summary>
public enum CanvasKey
{
    /// <summary>B.</summary>
    B,

    /// <summary>P.</summary>
    P,

    /// <summary>F.</summary>
    F,

    /// <summary>E.</summary>
    E,

    /// <summary>I.</summary>
    I,

    /// <summary>R.</summary>
    R,

    /// <summary>X.</summary>
    X,

    /// <summary>Y.</summary>
    Y,

    /// <summary>Z.</summary>
    Z,

    /// <summary>Escape.</summary>
    Escape,

    /// <summary>Delete.</summary>
    Delete,

    /// <summary>Enter.</summary>
    Enter,
}

/// <summary>
/// What a canvas tells about the pointer and the keys. Positions are in canvas units.
/// </summary>
public interface ICanvasInput
{
    /// <summary>
    /// A button went down over the canvas.
    /// </summary>
    void PointerPressed(Vector2 position, CanvasButton button, CanvasModifiers modifiers);

    /// <summary>
    /// The pointer moved over the canvas, or anywhere while a button pressed over it is held.
    /// </summary>
    void PointerMoved(Vector2 position, CanvasModifiers modifiers);

    /// <summary>
    /// A button pressed over the canvas went up.
    /// </summary>
    void PointerReleased(Vector2 position, CanvasButton button, CanvasModifiers modifiers);

    /// <summary>
    /// The pointer left the canvas.
    /// </summary>
    void PointerLeft();

    /// <summary>
    /// A key was pressed while the canvas has the focus; true when the key did something.
    /// </summary>
    bool KeyPressed(CanvasKey key, CanvasModifiers modifiers);
}
