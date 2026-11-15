using Stride.Core;
using Stride.Engine;

namespace ColorRise.Skein;

/// <summary>
/// Shows a dialogue to the player: a game derives from it to show lines its own way (a text box, a speech bubble, a
/// voice), and adds its presenters to a <see cref="DialogueRunnerComponent"/>. Every method does nothing by default:
/// override the ones the presenter cares about.
/// </summary>
/// <remarks>
/// The runner calls every presenter at once and waits for all of them. Everything runs on the game's thread, in the
/// runner's micro-thread: a method can await <c>Script.NextFrame()</c>.
/// </remarks>
[ComponentCategory("Dialogue")]
public abstract class DialoguePresenter : StartupScript
{
    /// <summary>
    /// The runner whose dialogue the presenter shows; null before its first dialogue.
    /// </summary>
    [DataMemberIgnore]
    public DialogueRunnerComponent? Runner { get; internal set; }

    /// <summary>
    /// A dialogue starts.
    /// </summary>
    public virtual Task OnDialogueStartedAsync() => Task.CompletedTask;

    /// <summary>
    /// The dialogue is over: it reached its end, or it was stopped.
    /// </summary>
    public virtual Task OnDialogueCompletedAsync() => Task.CompletedTask;

    /// <summary>
    /// The dialogue enters a node.
    /// </summary>
    /// <param name="node">The name of the node in the program. For a node group, the group's name comes first, then the name of the node picked.</param>
    public virtual Task OnNodeEnterAsync(string node) => Task.CompletedTask;

    /// <summary>
    /// The dialogue leaves a node.
    /// </summary>
    public virtual Task OnNodeExitAsync(string node) => Task.CompletedTask;

    /// <summary>
    /// The dialogue enters a node that may run these lines: the time to load what they need (voice-over, portraits).
    /// </summary>
    public virtual Task PrepareForLinesAsync(IReadOnlyList<string> lineIds) => Task.CompletedTask;

    /// <summary>
    /// Shows a line. The dialogue goes on when the tasks of all the presenters complete.
    /// </summary>
    /// <param name="line">The line, in the current language.</param>
    /// <param name="cancellation">The two requests of the player: hurry up (finish what is animating), and go to the next
    /// line (leave now). Both are cancelled when the dialogue stops.</param>
    public virtual Task RunLineAsync(LocalizedLine line, LineCancellation cancellation) => Task.CompletedTask;

    /// <summary>
    /// Offers options and waits for the choice.
    /// </summary>
    /// <param name="options">The options, the ones whose condition fails included (<see cref="DialogueOption.IsAvailable"/>).</param>
    /// <param name="cancellation">Cancelled when another presenter gave the choice, or when the dialogue stops.</param>
    /// <returns>The <see cref="DialogueOption.Index"/> of the choice, or null to leave the choice to another presenter.</returns>
    public virtual Task<int?> RunOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation) => Task.FromResult<int?>(null);
}
