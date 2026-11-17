using ColorRise.Skein.Assets.Voice;

namespace ColorRise.Skein.Tests.Voice;

/// <summary>
/// The recording script gives the actors each line with its file, its speaker and the line before it.
/// </summary>
public sealed class RecordingScriptTests
{
    private const string Harbour = """
        title: Harbour
        ---
        <<declare $letters = 3>>
        Odile: The post is [wave]late[/wave]. #line:post #tired
        Odile: {$letters} letters, and none for you. #line:letters
        The gulls cry. #line:gulls
        Odile: The post is [wave]late[/wave]. #shadow:post
        No id here.
        ===
        title: Pier
        ---
        Fisher: Mind the nets. #line:Pier_0100_Fisher #shout
        ===
        title: CutGulls
        style: note
        ---
        Cut the gulls if the scene runs long. #line:cut
        ===
        """;

    [Fact]
    public void Each_line_with_an_id_out_of_the_notes_has_its_file_its_speaker_its_text_its_context_and_its_tags()
    {
        var compilation = Scripts.Compile(Harbour);
        var rows = YarnRecordingScript.RowsOf(compilation, "en", null, new Dictionary<string, string> { ["post"] = "Dialogue/Voice/en/post" });

        Assert.Equal(
            [
                new YarnRecordingRow("post.wav", "line:post", "Odile", "The post is late.", "Harbour", "", "#tired", true),
                new YarnRecordingRow("letters.wav", "line:letters", "Odile", "{0} letters, and none for you.", "Harbour", "Odile: The post is late.", "", false),
                new YarnRecordingRow("gulls.wav", "line:gulls", "", "The gulls cry.", "Harbour", "Odile: {0} letters, and none for you.", "", false),
                new YarnRecordingRow("Pier_0100_Fisher.wav", "line:Pier_0100_Fisher", "Fisher", "Mind the nets.", "Pier", "", "#shout", false),
            ],
            rows);
    }

    [Fact]
    public void A_language_has_the_lines_it_translates_with_the_context_of_the_line_before_even_untranslated()
    {
        var compilation = Scripts.Compile(Harbour);
        var french = new Dictionary<string, string>
        {
            ["line:post"] = "Odile : Le courrier est en retard.",
            ["line:gulls"] = "Les mouettes crient.",
        };

        var rows = YarnRecordingScript.RowsOf(compilation, "fr", french, new Dictionary<string, string>());

        Assert.Equal(["Odile|Le courrier est en retard.|", "|Les mouettes crient.|Odile: {0} letters, and none for you."], rows.Select(r => $"{r.Character}|{r.Text}|{r.Previous}"));
        Assert.Equal(
            "file,id,character,text,node,previous,tags,recorded\n"
            + "post.wav,line:post,Odile,Le courrier est en retard.,Harbour,,#tired,\n"
            + "gulls.wav,line:gulls,,Les mouettes crient.,Harbour,\"Odile: {0} letters, and none for you.\",,\n",
            YarnRecordingScript.Write(rows).ReplaceLineEndings("\n"));
    }
}
