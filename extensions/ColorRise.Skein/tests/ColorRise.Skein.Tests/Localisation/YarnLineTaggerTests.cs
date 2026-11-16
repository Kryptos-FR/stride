using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;

namespace ColorRise.Skein.Tests.Localisation;

/// <summary>
/// "Add line ids" gives an id to each line that has none, and changes nothing else of a script.
/// </summary>
public sealed class YarnLineTaggerTests
{
    private const string Script = "title: Start\r\ntags: act1\r\n---\r\n// The lamp scene\r\nOdile: The lamp's out again. #line:lamp\r\n-> I'll look. #mood:brave\r\n\tOdile: Mind the stairs.   \r\n<<jump Lighthouse>>\r\n===\r\n";

    [Fact]
    public void Each_line_without_an_id_gets_one_and_every_other_character_stays()
    {
        var existing = new HashSet<string> { "line:lamp" };

        var tagged = YarnLineTagger.AddLineIds("Dialogue/Hello.yarn", Script, existing, YarnLineIdStyle.Random);

        Assert.Empty(tagged.Problems);
        Assert.Equal(2, tagged.AddedIds.Count);
        Assert.Equal(2, tagged.AddedIds.Distinct().Count());
        Assert.All(tagged.AddedIds, id => Assert.StartsWith("line:", id));
        Assert.DoesNotContain("line:lamp", tagged.AddedIds);
        Assert.Equal(3, existing.Count);

        // Without the tags that were added, the script is the one that came in
        var stripped = tagged.AddedIds.Aggregate(tagged.Text, (text, id) => text.Replace(" #" + id, ""));
        Assert.Equal(Script, stripped);
        Assert.Contains($"-> I'll look. #mood:brave #{tagged.AddedIds[0]}", tagged.Text);

        // The compiler reads the ids
        var lines = YarnCompilerBridge.Compile(new YarnCompilationRequest { Files = [("Hello.yarn", tagged.Text + "title: Lighthouse\n---\n===\n")] }).Lines;
        Assert.All(tagged.AddedIds, id => Assert.False(lines[id].IsImplicitId));
    }

    [Fact]
    public void A_script_whose_lines_all_have_an_id_does_not_change()
    {
        var existing = new HashSet<string>();
        var once = YarnLineTagger.AddLineIds("Hello.yarn", Script, existing, YarnLineIdStyle.Random);

        var twice = YarnLineTagger.AddLineIds("Hello.yarn", once.Text, existing, YarnLineIdStyle.Random);

        Assert.Equal(once.Text, twice.Text);
        Assert.Empty(twice.AddedIds);
        Assert.Empty(twice.Problems);
    }

    [Fact]
    public void An_id_another_script_has_is_not_given_again()
    {
        var first = new HashSet<string>();
        var a = YarnLineTagger.AddLineIds("A.yarn", "title: Tavern\n---\nOdile: Hello.\n===\n", first, YarnLineIdStyle.Descriptive);

        // The same node, speaker and place in another script would get the same descriptive id
        var b = YarnLineTagger.AddLineIds("Dialogue/B.yarn", "title: Tavern\n---\nOdile: Hello.\n===\n", first, YarnLineIdStyle.Descriptive);

        var idA = Assert.Single(a.AddedIds);
        Assert.Equal("line:Tavern_0100_Odile", idA);
        Assert.Empty(a.Problems);
        Assert.Equal($"title: Tavern\n---\nOdile: Hello. #{idA}\n===\n", a.Text);

        // The id takes the name of its script, and stays descriptive
        var idB = Assert.Single(b.AddedIds);
        Assert.Equal("line:Tavern_B_0100_Odile", idB);
        Assert.Equal($"title: Tavern\n---\nOdile: Hello. #{idB}\n===\n", b.Text);
        Assert.Empty(b.Problems);
    }

    [Fact]
    public void A_node_group_split_over_scripts_gets_descriptive_ids_in_each_and_its_new_lines_follow()
    {
        const string Events = "title: Events\nwhen: always\n---\nOdile: Rain again.\n===\n";
        var ids = new HashSet<string>();
        YarnLineTagger.AddLineIds("Village.yarn", Events, ids, YarnLineIdStyle.Descriptive);
        var mill = YarnLineTagger.AddLineIds("Mill.yarn", Events.Replace("Rain again.", "The wheel turns.\nOdile: Still turning."), ids, YarnLineIdStyle.Descriptive);

        Assert.Empty(mill.Problems);
        Assert.Equal(["line:Events.cb2a808c_Mill_0100_Odile", "line:Events.cb2a808c_Mill_0200_Odile"], mill.AddedIds);

        // A line written later between two of them keeps the script's name, though its own id would be free
        var added = mill.Text.Replace("Odile: Still turning.", "Odile: Slower now.\nOdile: Still turning.");
        var again = YarnLineTagger.AddLineIds("Mill.yarn", added, ids, YarnLineIdStyle.Descriptive);
        Assert.Equal(["line:Events.cb2a808c_Mill_0150_Odile"], again.AddedIds);

        var lines = YarnCompilerBridge.Compile(new YarnCompilationRequest { Files = [("Village.yarn", Events), ("Mill.yarn", again.Text)] });
        Assert.DoesNotContain(lines.Diagnostics, d => d.Severity == YarnSeverity.Error);
    }

    [Theory]
    [InlineData("A mason's mark cut in the wall: a circle.", "line:Mark_0100")]
    [InlineData("Old Tom: Mind the step.", "line:Mark_0100_OldTom")]
    [InlineData("Maren: Hello there.", "line:Mark_0100_Maren")]
    [InlineData("the note says: come alone.", "line:Mark_0100")]
    [InlineData("Plain narration.", "line:Mark_0100")]
    public void A_descriptive_id_names_a_speaker_only_when_the_words_before_the_colon_read_as_a_name(string line, string id)
    {
        var script = $"title: Mark\n---\n{line}\n===\n";

        var tagged = YarnLineTagger.AddLineIds("Mark.yarn", script, new HashSet<string>(), YarnLineIdStyle.Descriptive);

        Assert.Empty(tagged.Problems);
        Assert.Equal([id], tagged.AddedIds);
        var compiled = YarnCompilerBridge.Compile(new YarnCompilationRequest { Files = [("Mark.yarn", tagged.Text)] });
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Severity == YarnSeverity.Error);
        Assert.False(compiled.Lines[id].IsImplicitId);
    }

    [Fact]
    public void A_script_with_an_error_is_left_as_it_is_with_the_reason()
    {
        var broken = "title: Start\n---\n<<if $x >>\nOdile: Hm.\n";

        var tagged = YarnLineTagger.AddLineIds("Dialogue/Broken.yarn", broken, new HashSet<string>(), YarnLineIdStyle.Random);

        Assert.Equal(broken, tagged.Text);
        Assert.Empty(tagged.AddedIds);
        Assert.Contains("Dialogue/Broken.yarn: the script has errors", Assert.Single(tagged.Problems));
    }

    [Fact]
    public void The_ids_of_a_compilation_are_the_ones_written_by_hand()
    {
        var compilation = Scripts.Compile("title: Start\n---\nOdile: One. #line:one\nOdile: Two.\n===\n");

        Assert.Equal(["line:one"], YarnLineTagger.ExistingIds(compilation));
    }

    [Fact]
    public void The_ids_of_scripts_are_read_from_their_text_even_when_they_do_not_compile()
    {
        var ids = YarnLineTagger.ExistingIds(["title: A\n---\nOne. #line:one #mood:calm\n-> Go #line:go\n<<if>>\n===\n", "Two. #line:two", "#line: \n#line:"]);

        Assert.Equal(["line:go", "line:one", "line:two"], ids.Order());
    }
}
