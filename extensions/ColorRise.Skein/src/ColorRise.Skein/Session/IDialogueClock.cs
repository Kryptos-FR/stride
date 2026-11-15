namespace ColorRise.Skein;

/// <summary>
/// The time a dialogue waits in: a <c>&lt;&lt;wait 2&gt;&gt;</c> asks it for two seconds. A game gives its own time, which
/// pauses with the game; a test gives one that does not wait.
/// </summary>
public interface IDialogueClock
{
    /// <summary>
    /// Completes after a duration, or throws <see cref="OperationCanceledException"/> when cancelled.
    /// </summary>
    ValueTask Delay(TimeSpan duration, CancellationToken cancellation);
}

/// <summary>
/// The time of the computer.
/// </summary>
public sealed class SystemDialogueClock : IDialogueClock
{
    /// <summary>
    /// The one instance.
    /// </summary>
    public static SystemDialogueClock Instance { get; } = new();

    private SystemDialogueClock()
    {
    }

    /// <inheritdoc />
    public async ValueTask Delay(TimeSpan duration, CancellationToken cancellation)
    {
        if (duration > TimeSpan.Zero)
            await Task.Delay(duration, cancellation);
    }
}
