using System.Text;

namespace ColorRise.Skein.Assets.Text;

/// <summary>
/// One change of a script's text: <paramref name="Length"/> characters at <paramref name="Offset"/> give way to
/// <paramref name="Text"/>.
/// </summary>
/// <remarks>
/// The edits of one operation are all places of the text before the operation, never overlap, and come in the order of
/// the text. Apply them from the last to the first, so that each place is still right when its edit is applied.
/// </remarks>
public readonly record struct YarnTextEdit(int Offset, int Length, string Text)
{
    /// <summary>
    /// Gives a text with the edits of one operation applied.
    /// </summary>
    public static string Apply(string text, IReadOnlyList<YarnTextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edits);

        var result = new StringBuilder(text);
        for (var i = edits.Count - 1; i >= 0; i--)
        {
            var edit = edits[i];
            result.Remove(edit.Offset, edit.Length).Insert(edit.Offset, edit.Text);
        }

        return result.ToString();
    }
}
