using System.Text;

namespace ColorRise.Skein;

/// <summary>
/// Reads the text of a command.
/// </summary>
public static class YarnCommandText
{
    /// <summary>
    /// Splits the text of a command into its words. Double quotes group words into one (<c>say "two words"</c>), and inside
    /// them <c>\"</c> is a quote and <c>\\</c> a backslash.
    /// </summary>
    public static IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var words = new List<string>();
        var word = new StringBuilder();
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index++];
            if (char.IsWhiteSpace(character))
            {
                Flush();
            }
            else if (character == '"')
            {
                // A quote ends the word before it and makes a word of its own, even an empty one
                Flush();
                while (index < text.Length && text[index] != '"')
                {
                    character = text[index++];
                    if (character == '\\' && index < text.Length && text[index] is '"' or '\\')
                        character = text[index++];
                    word.Append(character);
                }

                index++;
                words.Add(word.ToString());
                word.Clear();
            }
            else
            {
                word.Append(character);
            }
        }

        Flush();
        return words;

        void Flush()
        {
            if (word.Length == 0)
                return;

            words.Add(word.ToString());
            word.Clear();
        }
    }
}
