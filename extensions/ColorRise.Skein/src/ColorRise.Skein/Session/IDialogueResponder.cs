namespace ColorRise.Skein;

/// <summary>
/// What a dialogue asks of who shows it: each call is awaited before the dialogue goes on. The Stride runner answers with
/// its presenters, Game Studio's player with its panel, a test with a recording. It has the shape of Yarn Spinner 3.3's
/// responder, in Skein's types.
/// </summary>
public interface IDialogueResponder
{
    /// <summary>
    /// The dialogue enters a node that may run these lines: the time to load what they need (voice-over, portraits).
    /// </summary>
    ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds);

    /// <summary>
    /// Shows a line; the dialogue goes on when the task completes.
    /// </summary>
    /// <param name="line">The line, in the current language.</param>
    /// <param name="cancellation">The two requests of the player: hurry up, and go to the next line.</param>
    ValueTask HandleLineAsync(LocalizedLine line, LineCancellation cancellation);

    /// <summary>
    /// Offers options and waits for the choice.
    /// </summary>
    /// <param name="options">The options, the ones whose condition fails included (<see cref="DialogueOption.IsAvailable"/>).</param>
    /// <param name="cancellation">Cancelled when the dialogue stops.</param>
    /// <returns>The <see cref="DialogueOption.Index"/> of the choice, or null for no choice: the dialogue goes on after the options.</returns>
    ValueTask<int?> HandleOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation);

    /// <summary>
    /// A command no handler of the game has: the responder's to run, or to ignore.
    /// </summary>
    /// <param name="commandText">The text of the command, the script's values in it.</param>
    /// <param name="cancellation">Cancelled when the dialogue stops.</param>
    ValueTask HandleCommandAsync(string commandText, CancellationToken cancellation);

    /// <summary>
    /// The dialogue enters a node. For a node group, the group's name comes first, then the name of the node picked.
    /// </summary>
    ValueTask HandleNodeStartAsync(string node);

    /// <summary>
    /// The dialogue leaves a node.
    /// </summary>
    ValueTask HandleNodeCompleteAsync(string node);

    /// <summary>
    /// The dialogue is over: it reached its end, or it was stopped. Called once per run, whatever ended it.
    /// </summary>
    ValueTask HandleDialogueCompleteAsync();
}
