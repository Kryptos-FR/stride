namespace ColorRise.Tessera.Editor.Canvas;

/// <summary>
/// A scene that time changes. The canvas that shows it gives it the time while it runs.
/// </summary>
public interface ICanvasClock
{
    /// <summary>
    /// Whether the scene wants the time now.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Raised when <see cref="IsRunning"/> changed.
    /// </summary>
    event EventHandler? IsRunningChanged;

    /// <summary>
    /// Moves the scene's time. The scene raises its <see cref="ICanvasScene.Changed"/> only when what it shows is different.
    /// </summary>
    void Advance(double seconds);
}
