namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// What an input of a played dialogue does.
/// </summary>
internal enum PlayerInputKind
{
    /// <summary>
    /// Starts the dialogue at the node <see cref="PlayerInput.Name"/>.
    /// </summary>
    Start,

    /// <summary>
    /// Goes on after a line.
    /// </summary>
    Advance,

    /// <summary>
    /// Chooses an option.
    /// </summary>
    Choose,

    /// <summary>
    /// Sets the variable <see cref="PlayerInput.Name"/> to <see cref="PlayerInput.Value"/>.
    /// </summary>
    SetVariable,

    /// <summary>
    /// Answers <see cref="PlayerInput.Value"/> for the call <see cref="PlayerInput.Name"/> of a function of the game, from there on.
    /// </summary>
    Answer,
}

/// <summary>
/// One thing the user did in a played dialogue. A run is its inputs in order: playing them again gives the run again.
/// </summary>
/// <param name="Kind">What the input does.</param>
/// <param name="Name">The node, the variable or the call.</param>
/// <param name="Value">The value of the variable or of the answer.</param>
/// <param name="Option">The index of the option chosen, as it was offered.</param>
/// <param name="LineId">The id of the line of the option chosen.</param>
/// <param name="Text">The text of the option chosen.</param>
internal sealed record PlayerInput(PlayerInputKind Kind, string Name = "", object? Value = null, int Option = -1, string LineId = "", string Text = "");
