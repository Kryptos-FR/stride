using ColorRise.Skein.UI;
using Yarn.Markup;

namespace ColorRise.Skein.Tests.UI;

/// <summary>
/// A typewriter shows a line little by little, pauses where the markup says, and tells the markers it passes.
/// </summary>
public sealed class TypewriterTests
{
    private static MarkupParseResult Parse(string text) => new LineParser().ParseString(text, "en", addImplicitCharacterAttribute: false);

    private static string After(Typewriter typewriter, double seconds) => typewriter.Advance(TimeSpan.FromSeconds(seconds));

    [Fact]
    public void Letters_come_at_the_speed_asked()
    {
        var typewriter = new Typewriter(Parse("Hello"), TypewriterMode.Letter, 10);

        Assert.Equal("", typewriter.VisibleText);
        Assert.Equal("He", After(typewriter, 0.2));
        Assert.False(typewriter.IsComplete);
        Assert.Equal("Hello", After(typewriter, 0.3));
        Assert.True(typewriter.IsComplete);
        Assert.Equal("Hello", After(typewriter, 1));
    }

    [Fact]
    public void Small_steps_add_up_as_one_big_step()
    {
        var typewriter = new Typewriter(Parse("Hello"), TypewriterMode.Letter, 10);
        var frame = TimeSpan.FromSeconds(1 / 60.0);

        for (var i = 0; i < 29; i++)
        {
            typewriter.Advance(frame);
        }

        Assert.Equal("Hell", typewriter.VisibleText);

        typewriter.Advance(frame);
        typewriter.Advance(frame);
        Assert.Equal("Hello", typewriter.VisibleText);
        Assert.True(typewriter.IsComplete);
    }

    [Fact]
    public void A_pause_holds_the_text_for_its_time()
    {
        // As Yarn Spinner reads it: a marker that closes itself takes the space that follows it
        var typewriter = new Typewriter(Parse("Wait [pause=500/] now"), TypewriterMode.Letter, 10);

        Assert.Equal("Wait ", After(typewriter, 0.5));
        Assert.Equal("Wait ", After(typewriter, 0.25));
        Assert.Equal("Wait ", After(typewriter, 0.25));
        Assert.Equal("Wait n", After(typewriter, 0.1));
        Assert.Equal("Wait now", After(typewriter, 0.2));
        Assert.True(typewriter.IsComplete);
    }

    [Fact]
    public void A_pause_at_the_end_holds_the_end()
    {
        var typewriter = new Typewriter(Parse("Well[pause=500/]"), TypewriterMode.Letter, 10);

        Assert.Equal("Well", After(typewriter, 0.6));
        Assert.False(typewriter.IsComplete);
        After(typewriter, 0.3);
        Assert.True(typewriter.IsComplete);
    }

    [Fact]
    public void Words_come_whole()
    {
        var typewriter = new Typewriter(Parse("one two  three"), TypewriterMode.Word, 2);

        Assert.Equal("", After(typewriter, 0.4));
        Assert.Equal("one", After(typewriter, 0.1));
        Assert.Equal("one two", After(typewriter, 0.5));
        Assert.Equal("one two  three", After(typewriter, 0.5));
        Assert.True(typewriter.IsComplete);
    }

    [Fact]
    public void Complete_shows_all_and_skips_the_pauses()
    {
        var typewriter = new Typewriter(Parse("Wait [pause=500/] now"), TypewriterMode.Letter, 10);
        After(typewriter, 0.1);

        typewriter.Complete();

        Assert.Equal("Wait now", typewriter.VisibleText);
        Assert.True(typewriter.IsComplete);
    }

    [Theory]
    [InlineData(TypewriterMode.Instant, 10f)]
    [InlineData(TypewriterMode.Letter, 0f)]
    [InlineData(TypewriterMode.Word, -1f)]
    public void Instant_or_no_speed_shows_all_at_once(TypewriterMode mode, float speed)
    {
        var typewriter = new Typewriter(Parse("Wait [pause=500/] now"), mode, speed);

        Assert.Equal("Wait now", After(typewriter, 0));
        Assert.True(typewriter.IsComplete);
    }

    [Fact]
    public void The_markers_the_text_passes_are_told_once_in_order_and_pauses_are_not()
    {
        var typewriter = new Typewriter(Parse("[shake/]Go[wave] on[/wave][pause=100/][bell/]!"), TypewriterMode.Letter, 10);
        var reached = new List<string>();
        typewriter.MarkerReached += marker => reached.Add($"{marker.Name}@{marker.Position}");

        After(typewriter, 0);
        Assert.Equal(["shake@0"], reached);

        After(typewriter, 0.2);
        Assert.Equal(["shake@0", "wave@2"], reached);

        typewriter.Complete();
        Assert.Equal(["shake@0", "wave@2", "bell@5"], reached);
        typewriter.Complete();
        After(typewriter, 1);
        Assert.Equal(3, reached.Count);
    }

    [Fact]
    public void A_letter_made_of_several_characters_comes_whole()
    {
        // An emoji is two characters; an accent that has no letter of its own follows its letter
        var typewriter = new Typewriter(Parse("a\U0001F600x\u0301z"), TypewriterMode.Letter, 10);

        Assert.Equal("a", After(typewriter, 0.1));
        Assert.Equal("a\U0001F600", After(typewriter, 0.1));
        Assert.Equal("a\U0001F600x\u0301", After(typewriter, 0.1));
        Assert.Equal("a\U0001F600x\u0301z", After(typewriter, 0.1));
    }

    [Fact]
    public void An_empty_line_is_complete_at_once()
    {
        var typewriter = new Typewriter(Parse(""), TypewriterMode.Letter, 10);

        Assert.Equal("", After(typewriter, 0));
        Assert.True(typewriter.IsComplete);
    }
}
