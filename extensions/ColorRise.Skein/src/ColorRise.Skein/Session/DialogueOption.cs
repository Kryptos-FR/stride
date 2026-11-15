namespace ColorRise.Skein;

/// <summary>
/// One of the options a dialogue offers.
/// </summary>
/// <param name="Index">What the responder answers to choose this option.</param>
/// <param name="Line">The text of the option, in the current language.</param>
/// <param name="IsAvailable">False when the option's condition fails: a game hides it or shows it disabled.</param>
public sealed record DialogueOption(int Index, LocalizedLine Line, bool IsAvailable);
