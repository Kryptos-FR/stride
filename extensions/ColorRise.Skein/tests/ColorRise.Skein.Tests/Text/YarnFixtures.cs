namespace ColorRise.Skein.Tests.Text;

/// <summary>
/// One script written the ways files come: with either line break, with and without a byte order mark. It has tabs,
/// trailing spaces, comments between nodes and after values, and no blank line before its <c>===</c>.
/// </summary>
internal static class YarnFixtures
{
    public const char ByteOrderMark = (char)0xFEFF;

    public const string Script =
        "// The lighthouse, act one\n" +
        "title: Start\n" +
        "tags: intro  act1   \n" +
        "position: 0,0\n" +
        "---\n" +
        "Odile: The lamp's out again. #line:lamp   \n" +
        "-> I'll look.\n" +
        "\tOdile: Mind the stairs.\n" +
        "\t<<jump Lighthouse>>\n" +
        "-> Not my problem.\n" +
        "\t<<detour Odile_Key>>\n" +
        "===\n" +
        "\n" +
        "// Between two nodes\n" +
        "title: Lighthouse\n" +
        "color: blue // a comment after a value\n" +
        "---\n" +
        "Narrator: The stairs wind up into the dark. // <<jump Odile_Key>>\n" +
        "Odile: say Odile_Key\n" +
        "<<if visited(\"Odile_Key\")>>\n" +
        "    <<jump Odile_Key>>\n" +
        "<<endif>>\n" +
        "===\n" +
        "title: Odile_Key\n" +
        "when: once\n" +
        "when: visited_count(\"Start\") > 1\n" +
        "---\n" +
        "Odile: Take the key. #line:key #mood:kind\n" +
        "===\n" +
        "title: Odile_Key\n" +
        "when: always\n" +
        "---\n" +
        "Odile: You have it already.\n" +
        "===\n";

    /// <summary>
    /// The names of the ways a file is written, for theories.
    /// </summary>
    public static TheoryData<string> Variants => ["LF", "CRLF", "BOM+LF", "BOM+CRLF"];

    /// <summary>
    /// A text written with <c>\n</c>, as a file of the variant has it.
    /// </summary>
    public static string As(string variant, string text)
    {
        if (variant.EndsWith("CRLF", StringComparison.Ordinal))
            text = text.Replace("\n", "\r\n");
        return variant.StartsWith("BOM", StringComparison.Ordinal) ? ByteOrderMark + text : text;
    }

    /// <summary>
    /// Compiles a text of any variant, and fails the test on an error.
    /// </summary>
    public static void AssertCompiles(string text) => Scripts.Compile(text.TrimStart(ByteOrderMark));
}
