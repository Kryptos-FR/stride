namespace ColorRise.Saga;

/// <summary>
/// A choice of a dialogue, as the game shows it. <see cref="Index"/> is what to pass to <see cref="DialogueSession.Choose"/>.
/// </summary>
/// <param name="Index">The index of the choice in its node; it does not change when other choices are hidden.</param>
/// <param name="Text">The text, with its <c>{variable}</c> parts filled in.</param>
/// <param name="IsEnabled">Whether the choice can be taken. A disabled choice is shown so the player knows it exists.</param>
public sealed record DialogueChoiceView(int Index, string Text, bool IsEnabled);
