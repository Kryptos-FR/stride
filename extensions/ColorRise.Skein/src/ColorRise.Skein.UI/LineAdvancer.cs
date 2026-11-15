using Stride.Core;
using Stride.Engine;
using Stride.Input;

namespace ColorRise.Skein.UI;

/// <summary>
/// Lets the player move a dialogue on: a first press asks the presenters to hurry the line
/// (<see cref="DialogueRunnerComponent.RequestHurryUp"/>), the next asks to leave it
/// (<see cref="DialogueRunnerComponent.RequestNextLine"/>). With a <see cref="LinePresenter"/>, one press finishes the
/// typing and the next goes on, and a line already all shown goes on at the first press.
/// </summary>
/// <remarks>
/// It is a presenter that shows nothing: put it in the runner's presenters. A game with its own input calls
/// <see cref="Advance"/>.
/// </remarks>
[DataContract("SkeinLineAdvancer")]
[Display("Dialogue line advancer")]
[ComponentCategory("Dialogue")]
public sealed class LineAdvancer : DialoguePresenter
{
    private int presses;
    private int listening;

    /// <summary>
    /// The key that moves the dialogue on.
    /// </summary>
    /// <userdoc>The key that hurries the line, then goes to the next.</userdoc>
    [DataMember(10)]
    public Keys AdvanceKey { get; set; } = Keys.Space;

    /// <summary>
    /// The gamepad button that moves the dialogue on.
    /// </summary>
    /// <userdoc>The gamepad button that hurries the line, then goes to the next.</userdoc>
    [DataMember(20)]
    public GamePadButton AdvanceButton { get; set; } = GamePadButton.A;

    /// <summary>
    /// Whether a click or a touch anywhere moves the dialogue on too.
    /// </summary>
    /// <userdoc>A click or a touch anywhere also hurries the line, then goes to the next.</userdoc>
    [DataMember(30)]
    public bool AdvanceWithPointer { get; set; } = true;

    /// <summary>
    /// Moves the dialogue on, as a press of the key does.
    /// </summary>
    public void Advance()
    {
        if (Runner is not { } runner)
            return;

        if (presses++ == 0)
            runner.RequestHurryUp();
        else
            runner.RequestNextLine();
    }

    /// <inheritdoc />
    public override Task OnDialogueStartedAsync()
    {
        // One listener per dialogue: the one of a dialogue before ends when its number is not the current one
        var mine = ++listening;
        Script.AddTask(() => ListenAsync(mine));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnDialogueCompletedAsync()
    {
        listening++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        presses = 0;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        listening++;
        base.Cancel();
    }

    private async Task ListenAsync(int mine)
    {
        while (listening == mine)
        {
            await Script.NextFrame();
            if (listening == mine && IsPressed())
                Advance();
        }
    }

    private bool IsPressed()
    {
        var input = Input;
        if (input.IsKeyPressed(AdvanceKey) || input.DefaultGamePad?.IsButtonPressed(AdvanceButton) == true)
            return true;

        if (!AdvanceWithPointer)
            return false;

        foreach (var pointer in input.PointerEvents)
        {
            if (pointer.EventType == PointerEventType.Pressed)
                return true;
        }

        return false;
    }
}
