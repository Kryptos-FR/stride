namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// A place on the graph of a script: the top left corner of a card, as a <c>position:</c> header writes it.
/// </summary>
public readonly record struct YarnGraphPoint(int X, int Y);
