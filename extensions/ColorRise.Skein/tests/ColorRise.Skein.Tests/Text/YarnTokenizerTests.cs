using ColorRise.Skein.Assets.Text;

namespace ColorRise.Skein.Tests.Text;

/// <summary>
/// The pieces of a script an editor colours are the ones Yarn Spinner's lexer sees.
/// </summary>
public sealed class YarnTokenizerTests
{
    private const string Script =
        "title: Start // the first node\n"
        + "tags: quest light\n"
        + "when: $act == 2 and visited(\"Pier\")\n"
        + "---\n"
        + "<<declare $oil = 3>>\n"
        + "Odile: The [wave]lamp[/wave] is out, {$name}. #line:0a1b2c\n"
        + "-> I'll look. <<if $oil > 1>>\n"
        + "    <<jump Lighthouse>>\n"
        + "=> A gull cries.\n"
        + "<<fade_out 2 slow>>\n"
        + "===\n";

    private static List<(string Text, YarnTokenKind Kind)> Pieces(string text) => [.. YarnTokenizer.Tokenize(text).Select(t => (text.Substring(t.Offset, t.Length), t.Kind))];

    [Fact]
    public void The_pieces_of_a_script_have_their_kinds()
    {
        var pieces = Pieces(Script);

        Assert.Equal(
        [
            ("title", YarnTokenKind.HeaderKey), ("Start", YarnTokenKind.HeaderValue), ("// the first node", YarnTokenKind.Comment),
            ("tags", YarnTokenKind.HeaderKey), ("quest light", YarnTokenKind.HeaderValue),
            ("when", YarnTokenKind.HeaderKey), ("$act", YarnTokenKind.Variable), ("==", YarnTokenKind.Operator), ("2", YarnTokenKind.Number), ("and", YarnTokenKind.Operator),
            ("visited", YarnTokenKind.Function), ("(", YarnTokenKind.Operator), ("\"Pier\"", YarnTokenKind.String), (")", YarnTokenKind.Operator),
            ("---", YarnTokenKind.Delimiter),
            ("<<", YarnTokenKind.Command), ("declare", YarnTokenKind.Keyword), ("$oil", YarnTokenKind.Variable), ("=", YarnTokenKind.Operator), ("3", YarnTokenKind.Number), (">>", YarnTokenKind.Command),
            ("Odile:", YarnTokenKind.Speaker), ("[wave]", YarnTokenKind.Markup), ("[/wave]", YarnTokenKind.Markup),
            ("{", YarnTokenKind.Operator), ("$name", YarnTokenKind.Variable), ("}", YarnTokenKind.Operator), ("#line:0a1b2c", YarnTokenKind.Tag),
            ("->", YarnTokenKind.Option), ("<<", YarnTokenKind.Command), ("if", YarnTokenKind.Keyword), ("$oil", YarnTokenKind.Variable), (">", YarnTokenKind.Operator), ("1", YarnTokenKind.Number), (">>", YarnTokenKind.Command),
            ("<<", YarnTokenKind.Command), ("jump", YarnTokenKind.Keyword), ("Lighthouse", YarnTokenKind.NodeName), (">>", YarnTokenKind.Command),
            ("=>", YarnTokenKind.Option),
            ("<<", YarnTokenKind.Command), ("fade_out 2 slow", YarnTokenKind.CommandText), (">>", YarnTokenKind.Command),
            ("===", YarnTokenKind.Delimiter),
        ], pieces);
    }

    [Theory]
    [InlineData("LF")]
    [InlineData("CRLF")]
    [InlineData("BOM+LF")]
    [InlineData("BOM+CRLF")]
    public void The_places_are_those_of_the_text_whatever_its_line_breaks(string variant)
    {
        var text = YarnFixtures.As(variant, Script);

        Assert.Equal(Pieces(Script), Pieces(text));
        var tokens = YarnTokenizer.Tokenize(text);
        Assert.All(tokens.Zip(tokens.Skip(1)), pair => Assert.True(pair.First.Offset + pair.First.Length <= pair.Second.Offset));
    }

    [Theory]
    [InlineData("")]
    [InlineData("title")]
    [InlineData("title: Start\n---\n<<if $oil ==\nOdile: [wave\n-> {")]
    [InlineData("---\n===\n===\n<<")]
    [InlineData("title: A\n---\n\\")]
    public void A_text_being_typed_gives_its_pieces_without_failing(string text)
    {
        var tokens = YarnTokenizer.Tokenize(text);

        Assert.All(tokens, t => Assert.True(t.Offset >= 0 && t.Length > 0 && t.Offset + t.Length <= text.Length));
    }
}
