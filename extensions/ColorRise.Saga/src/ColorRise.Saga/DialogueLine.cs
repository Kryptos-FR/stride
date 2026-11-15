namespace ColorRise.Saga;

/// <summary>
/// A line of a dialogue, as the game shows it: who says it, and the text with its <c>{variable}</c> parts filled in.
/// </summary>
public sealed record DialogueLine(string SpeakerId, string Text);
