namespace ColorRise.Skein.Editor.Text;

/// <summary>
/// One change of a script's text: <paramref name="RemovedLength"/> characters at <paramref name="Offset"/> gave way to
/// <paramref name="InsertedText"/>.
/// </summary>
public readonly record struct YarnTextChange(int Offset, int RemovedLength, string InsertedText);
