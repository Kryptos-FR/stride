using Stride.Engine.Processors;
using Stride.Games;

namespace ColorRise.Skein;

/// <summary>
/// The time of the game: a <c>&lt;&lt;wait&gt;&gt;</c> counts the frames' time, so it slows and pauses with the game
/// (<see cref="GameTime.WarpElapsed"/>).
/// </summary>
public sealed class GameDialogueClock : IDialogueClock
{
    private readonly ScriptSystem scripts;
    private readonly IGame game;

    /// <summary>
    /// Makes a clock.
    /// </summary>
    /// <param name="scripts">The script system whose frames are counted; a wait is awaited from one of its micro-threads.</param>
    /// <param name="game">The game whose update time is counted.</param>
    public GameDialogueClock(ScriptSystem scripts, IGame game)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(game);
        this.scripts = scripts;
        this.game = game;
    }

    /// <inheritdoc />
    public async ValueTask Delay(TimeSpan duration, CancellationToken cancellation)
    {
        var remaining = duration;
        while (remaining > TimeSpan.Zero)
        {
            cancellation.ThrowIfCancellationRequested();
            await scripts.NextFrame();
            remaining -= game.UpdateTime.WarpElapsed;
        }

        cancellation.ThrowIfCancellationRequested();
    }
}
