using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Text;

namespace ColorRise.Skein.Editor.Tests.Player;

/// <summary>
/// What an open script editor shows of the run of the dialogue player: the node the run is in, the nodes it went
/// through, and the line it is at.
/// </summary>
public sealed class PlayMarksTests
{
    private const string Morning = """
        title: Start
        ---
        Narrator: Dawn.
        <<jump Odile>>
        ===
        title: Odile
        ---
        Odile: Morning.
        <<jump Tea>>
        ===
        title: Tea
        when: $never
        ---
        Odile: No tea.
        ===
        title: Tea
        when: always
        ---
        Odile: Tea?
        ===
        title: Unused
        ---
        <<declare $never = false>>
        Nobody: Never.
        ===

        """;

    private const string Evening = "title: Evening\n---\nNarrator: Dusk.\n===\n";

    [Fact]
    public async Task The_cards_and_the_line_of_an_open_editor_follow_the_run()
    {
        await using var session = await SkeinSession.OpenAsync(
            SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Morning), SkeinSession.Script("Dialogue/Act2", Evening));
        var editor = session.Edit("Dialogue/Act1");
        var other = session.Edit("Dialogue/Act2");
        var player = session.Player;
        Assert.All(editor.Cards, c => Assert.Equal(YarnPlayMark.None, c.PlayMark));
        Assert.Null(editor.PlayLine);

        await player.Start(session.ProjectAt("Dialogue/Story"), "Start");
        await player.Continue();

        // The run is in Odile, after Start
        Assert.Equal("Odile: Morning.", player.CurrentStep!.Text);
        Assert.Equal(
            [("Start", YarnPlayMark.Visited), ("Odile", YarnPlayMark.Current), ("Tea", YarnPlayMark.None), ("Tea", YarnPlayMark.None), ("Unused", YarnPlayMark.None)],
            editor.Cards.Select(c => (c.Title, c.PlayMark)));
        Assert.Equal(8, editor.PlayLine);

        // The editor of another script of the project shows nothing: the run is not in it
        Assert.All(other.Cards, c => Assert.Equal(YarnPlayMark.None, c.PlayMark));
        Assert.Null(other.PlayLine);

        // Of the nodes of a group, the one that was picked
        await player.Continue();
        Assert.Equal("Odile: Tea?", player.CurrentStep.Text);
        Assert.Equal([YarnPlayMark.Visited, YarnPlayMark.Visited, YarnPlayMark.None, YarnPlayMark.Current, YarnPlayMark.None], editor.Cards.Select(c => c.PlayMark));
        Assert.Equal(19, editor.PlayLine);

        // The marks follow the cursor
        await player.GoTo(2);
        Assert.Equal([YarnPlayMark.Current, YarnPlayMark.None, YarnPlayMark.None, YarnPlayMark.None, YarnPlayMark.None], editor.Cards.Select(c => c.PlayMark));
        Assert.Equal(3, editor.PlayLine);

        // An editor that opens during the run shows it too
        Assert.True(await session.Session.EditorCollection.CloseAssetEditorAsync(session.ScriptAt("Dialogue/Act1"), save: false));
        editor = session.Edit("Dialogue/Act1");
        Assert.Equal(YarnPlayMark.Current, editor.Cards[0].PlayMark);
        Assert.Equal(3, editor.PlayLine);

        // Once the text changed, the line of the run is of another text: the nodes stay marked, the line does not
        editor.TextBuffer.Replace(0, 0, "// A new first line\n", YarnTextBuffer.TypingActionName);
        await session.Analysis.WhenIdle();
        Assert.True(player.IsStale);
        Assert.Equal(YarnPlayMark.Current, editor.Cards[0].PlayMark);
        Assert.Null(editor.PlayLine);
        await player.Replay();
        Assert.Equal(4, editor.PlayLine);

        player.Reset();
        Assert.All(editor.Cards, c => Assert.Equal(YarnPlayMark.None, c.PlayMark));
        Assert.Null(editor.PlayLine);
    }
}
