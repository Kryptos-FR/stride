using Stride.Core.Diagnostics;

namespace ColorRise.Skein.Tests.Runtime;

/// <summary>
/// From a line id to the text of the current language, with its markup read.
/// </summary>
public sealed class LineProviderTests
{
    private static YarnStringTable Table(string language, params (string Id, string Text)[] strings)
    {
        var table = new YarnStringTable { Language = language };
        foreach (var (id, text) in strings)
        {
            table.Strings[id] = text;
        }

        return table;
    }

    private static readonly YarnStringTable English = Table(
        "en",
        ("line:letters", "Odile: You have {0} letter[plural value={0} one=\"\" other=\"s\"/]."),
        ("line:tide", "The tide is [wave]out[/wave]."),
        ("line:lamp", "Kye: Take the lamp."));

    private static readonly YarnStringTable French = Table("fr", ("line:lamp", "Kye : Prends la lampe."), ("line:tide", "La marée est [wave]basse[/wave]."));

    private static TextLineProvider Provider(ILogger? log = null, List<string>? loads = null) => new(
        language =>
        {
            loads?.Add(language);
            return language switch
            {
                "en" => English,
                "fr" => French,
                _ => null,
            };
        },
        "en",
        log);

    [Fact]
    public void A_line_gets_its_values_its_plural_and_its_speaker()
    {
        var line = Provider().GetLine("line:letters", ["3"]);

        Assert.Equal("line:letters", line.Id);
        Assert.Equal("Odile: You have 3 letters.", line.Text.Text);
        Assert.Equal("Odile", line.CharacterName);
        Assert.Equal("You have 3 letters.", line.TextWithoutCharacterName.Text);
        Assert.Equal("en", line.Language);

        Assert.Equal("Odile: You have 1 letter.", Provider().GetLine("line:letters", ["1"]).Text.Text);
    }

    [Fact]
    public void A_line_without_a_speaker_has_no_name_and_keeps_its_markup()
    {
        var line = Provider().GetLine("line:tide", []);

        Assert.Null(line.CharacterName);
        Assert.Equal("The tide is out.", line.Text.Text);
        Assert.Equal("The tide is out.", line.TextWithoutCharacterName.Text);
        Assert.True(line.Text.TryGetAttributeWithName("wave", out var wave));
        Assert.Equal("out", line.Text.TextForAttribute(wave));
    }

    [Fact]
    public void The_language_of_the_provider_gives_the_text()
    {
        var loads = new List<string>();
        var provider = Provider(loads: loads);
        provider.Language = "fr";

        var line = provider.GetLine("line:lamp", []);
        provider.GetLine("line:tide", []);

        Assert.Equal("Kye : Prends la lampe.", line.Text.Text);
        Assert.Equal("Kye", line.CharacterName);
        Assert.Equal("Prends la lampe.", line.TextWithoutCharacterName.Text);
        Assert.Equal("fr", line.Language);
        // A language is loaded once
        Assert.Equal(["fr"], loads);
    }

    [Theory]
    [InlineData("Odile : Oui.")]
    [InlineData("Odile\u00A0: Oui.")]
    [InlineData("Odile\u202F: Oui.")]
    public void A_space_before_the_colon_is_not_part_of_the_name(string text)
    {
        var provider = new TextLineProvider(_ => Table("fr", ("line:oui", text)), "fr");

        var line = provider.GetLine("line:oui", []);

        Assert.Equal("Odile", line.CharacterName);
        Assert.Equal("Oui.", line.TextWithoutCharacterName.Text);
        Assert.Equal(text, line.Text.Text);
    }

    [Fact]
    public void A_line_the_language_lacks_falls_back_to_the_base_language_with_one_warning()
    {
        var log = new LoggerResult();
        var provider = Provider(log);
        provider.Language = "fr";

        var first = provider.GetLine("line:letters", ["3"]);
        var second = provider.GetLine("line:letters", ["2"]);

        Assert.Equal("Odile: You have 3 letters.", first.Text.Text);
        Assert.Equal("en", first.Language);
        Assert.Equal("Odile: You have 2 letters.", second.Text.Text);
        var warning = Assert.Single(log.Messages);
        Assert.Equal(LogMessageType.Warning, warning.Type);
        Assert.Contains("line:letters", warning.Text);
        Assert.Contains("fr", warning.Text);
    }

    [Fact]
    public void A_language_without_a_table_falls_back_whole_with_one_warning()
    {
        var log = new LoggerResult();
        var provider = Provider(log);
        provider.Language = "de";

        provider.GetLine("line:lamp", []);
        var line = provider.GetLine("line:tide", []);

        Assert.Equal("The tide is out.", line.Text.Text);
        var warning = Assert.Single(log.Messages);
        Assert.Contains("de", warning.Text);
    }

    [Fact]
    public void A_line_no_table_has_shows_its_id_and_logs_an_error_once()
    {
        var log = new LoggerResult();
        var provider = Provider(log);

        var line = provider.GetLine("line:gone", []);
        provider.GetLine("line:gone", []);

        Assert.Equal("line:gone", line.Text.Text);
        var error = Assert.Single(log.Messages);
        Assert.Equal(LogMessageType.Error, error.Type);
        Assert.Contains("line:gone", error.Text);
    }

    [Fact]
    public void Markup_that_does_not_parse_shows_the_text_as_written_and_logs_an_error_once()
    {
        var log = new LoggerResult();
        var provider = new TextLineProvider(_ => Table("en", ("line:bad", "Odile: This [b is broken.")), "en", log);

        var line = provider.GetLine("line:bad", []);
        provider.GetLine("line:bad", []);

        Assert.Equal("Odile: This [b is broken.", line.Text.Text);
        var error = Assert.Single(log.Messages);
        Assert.Equal(LogMessageType.Error, error.Type);
        Assert.Contains("line:bad", error.Text);
    }

    [Fact]
    public void A_line_carries_the_metadata_of_the_project_and_a_shadow_line_the_text_of_its_source()
    {
        var provider = new TextLineProvider(_ => English, "en")
        {
            LineMetadata = new Dictionary<string, List<string>> { ["line:lamp"] = ["shout"], ["line:echo"] = ["whisper"] },
            ShadowLines = new Dictionary<string, string> { ["line:echo"] = "line:lamp" },
        };

        Assert.Equal(["shout"], provider.GetLine("line:lamp", []).Metadata);
        Assert.Empty(provider.GetLine("line:tide", []).Metadata);

        var echo = provider.GetLine("line:echo", []);
        Assert.Equal("line:echo", echo.Id);
        Assert.Equal("Kye: Take the lamp.", echo.Text.Text);
        Assert.Equal(["whisper"], echo.Metadata);
    }

    [Fact]
    public void A_game_adds_its_own_replacement_markup()
    {
        var provider = new TextLineProvider(_ => Table("en", ("line:key", "Press [key name=\"jump\"/] to climb.")), "en");
        provider.Markup.RegisterMarkerProcessor("key", new KeyNames());

        Assert.Equal("Press Space to climb.", provider.GetLine("line:key", []).Text.Text);
    }

    private sealed class KeyNames : Yarn.Markup.IAttributeMarkerProcessor
    {
        public Yarn.Markup.ReplacementMarkerResult ProcessReplacementMarker(
            Yarn.Markup.MarkupAttribute marker, System.Text.StringBuilder childBuilder, List<Yarn.Markup.MarkupAttribute> childAttributes, string localeCode)
        {
            childBuilder.Append("Space");
            return new Yarn.Markup.ReplacementMarkerResult([], 0);
        }
    }
}
