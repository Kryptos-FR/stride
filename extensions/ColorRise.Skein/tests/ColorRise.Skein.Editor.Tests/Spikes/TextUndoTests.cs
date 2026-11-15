using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Text;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Assets.Editor.Testing;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Presentation.Services;

namespace ColorRise.Skein.Editor.Tests.Spikes;

/// <summary>
/// Changes of a script's text are steps of the session's undo history, typing included.
/// </summary>
public sealed class TextUndoTests
{
    private const string Hello = "title: Start\n---\nOdile: Hello.\n===\n";
    private const string Typing = YarnTextBuffer.TypingActionName;

    private static StrideEditorTestSessionOptions Options => new() { Plugins = [new SkeinEditorPlugin()] };

    [Fact]
    public async Task Each_replace_is_a_step_that_undo_takes_back()
    {
        await using var test = await ScriptTest.OpenAsync();
        var buffer = test.Buffer;

        buffer.Replace(Hello.IndexOf("Hello", StringComparison.Ordinal), 5, "Good evening", "Change line");
        buffer.Replace(7, 5, "Arrival", "Rename node");

        Assert.Equal("title: Arrival\n---\nOdile: Good evening.\n===\n", buffer.Text);
        Assert.Equal(["Change line", "Rename node"], test.Steps);
        Assert.True(test.Script.IsDirty);

        test.UndoRedo.Undo();
        Assert.Equal("title: Start\n---\nOdile: Good evening.\n===\n", buffer.Text);
        test.UndoRedo.Undo();
        Assert.Equal(Hello, buffer.Text);
        Assert.False(test.Script.IsDirty);

        test.UndoRedo.Redo();
        test.UndoRedo.Redo();
        Assert.Equal("title: Arrival\n---\nOdile: Good evening.\n===\n", buffer.Text);
        Assert.True(test.Script.IsDirty);
    }

    [Fact]
    public async Task Typing_in_a_row_is_one_step()
    {
        await using var test = await ScriptTest.OpenAsync();
        var buffer = test.Buffer;
        var end = Hello.IndexOf('.', StringComparison.Ordinal);

        buffer.Replace(end, 0, "a", Typing);
        test.Time.Advance(TimeSpan.FromMilliseconds(300));
        buffer.Replace(end + 1, 0, "b", Typing);
        test.Time.Advance(TimeSpan.FromMilliseconds(300));
        buffer.Replace(end + 2, 0, "c", Typing);
        // Backspace is typing too
        buffer.Replace(end + 2, 1, "", Typing);

        Assert.Equal(Hello.Replace("Hello.", "Helloab."), buffer.Text);
        Assert.Equal([Typing], test.Steps);
        test.UndoRedo.Undo();
        Assert.Equal(Hello, buffer.Text);
        Assert.False(test.Script.IsDirty);
        test.UndoRedo.Redo();
        Assert.Equal(Hello.Replace("Hello.", "Helloab."), buffer.Text);
    }

    [Fact]
    public async Task Typing_after_a_pause_is_a_new_step()
    {
        await using var test = await ScriptTest.OpenAsync();
        var buffer = test.Buffer;

        buffer.Replace(0, 0, "a", Typing);
        test.Time.Advance(YarnTextBuffer.TypingMergeWindow + TimeSpan.FromMilliseconds(1));
        buffer.Replace(1, 0, "b", Typing);

        Assert.Equal([Typing, Typing], test.Steps);
        test.UndoRedo.Undo();
        Assert.Equal("a" + Hello, buffer.Text);
    }

    [Fact]
    public async Task Another_step_between_typing_keeps_its_place_in_the_history()
    {
        await using var test = await ScriptTest.OpenAsync();
        var buffer = test.Buffer;

        buffer.Replace(0, 0, "a", Typing);
        buffer.Replace(8, 5, "Arrival", "Rename node");
        buffer.Replace(1, 0, "b", Typing);

        Assert.Equal([Typing, "Rename node", Typing], test.Steps);
        test.UndoRedo.Undo();
        Assert.Equal("atitle: Arrival\n---\nOdile: Hello.\n===\n", buffer.Text);
        test.UndoRedo.Undo();
        Assert.Equal("a" + Hello, buffer.Text);
        test.UndoRedo.Undo();
        Assert.Equal(Hello, buffer.Text);
    }

    [Fact]
    public async Task Typing_does_not_join_a_saved_step_or_one_that_was_undone()
    {
        await using var test = await ScriptTest.OpenAsync();
        var buffer = test.Buffer;

        buffer.Replace(0, 0, "a", Typing);
        var log = test.Session.Session.SaveSessionWithoutUI();
        Assert.False(log.HasErrors, log.ToText());
        test.UndoRedo.NotifySave();
        buffer.Replace(1, 0, "b", Typing);

        Assert.Equal([Typing, Typing], test.Steps);
        // Back to what was saved
        test.UndoRedo.Undo();
        Assert.Equal("a" + Hello, buffer.Text);
        Assert.False(test.Script.IsDirty);

        test.UndoRedo.Redo();
        buffer.Replace(2, 0, "c", Typing);
        Assert.Equal([Typing, Typing, Typing], test.Steps);
    }

    [Fact]
    public async Task Every_change_is_reported_also_by_undo_and_redo()
    {
        await using var test = await ScriptTest.OpenAsync();
        var buffer = test.Buffer;
        var changes = new List<YarnTextChange>();
        buffer.Changed += changes.Add;

        buffer.Replace(7, 5, "Arrival", "Rename node");
        test.UndoRedo.Undo();
        test.UndoRedo.Redo();

        Assert.Equal([new YarnTextChange(7, 5, "Arrival"), new YarnTextChange(7, 7, "Start"), new YarnTextChange(7, 5, "Arrival")], changes);
    }

    [Fact]
    public async Task A_replace_that_changes_nothing_is_no_step()
    {
        await using var test = await ScriptTest.OpenAsync();

        test.Buffer.Replace(7, 5, "Start", "Rename node");
        test.Buffer.Replace(3, 0, "", Typing);

        Assert.Empty(test.Steps);
        Assert.False(test.Script.IsDirty);
        Assert.Throws<ArgumentOutOfRangeException>(() => test.Buffer.Replace(Hello.Length, 1, "", Typing));
    }

    [Fact]
    public async Task A_reload_from_disk_is_a_step_that_leaves_the_script_clean()
    {
        await using var test = await ScriptTest.OpenAsync();
        var buffer = test.Buffer;
        var changes = new List<YarnTextChange>();
        buffer.Replace(7, 5, "Arrival", "Rename node");
        buffer.Changed += changes.Add;

        buffer.ReloadFromDisk("title: Arrival\n---\nOdile: Hello, courier.\n===\n");

        Assert.Equal("title: Arrival\n---\nOdile: Hello, courier.\n===\n", buffer.Text);
        Assert.Equal(["Rename node", YarnTextBuffer.ReloadActionName], test.Steps);
        Assert.False(test.Script.IsDirty);
        Assert.Equal([new YarnTextChange(31, 0, ", courier")], changes);

        test.UndoRedo.Undo();
        Assert.Equal("title: Arrival\n---\nOdile: Hello.\n===\n", buffer.Text);
        Assert.True(test.Script.IsDirty);
        test.UndoRedo.Undo();
        Assert.Equal(Hello, buffer.Text);
    }

    private sealed class ManualTime : TimeProvider
    {
        private long ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => ticks;

        public void Advance(TimeSpan time) => ticks += time.Ticks;
    }

    private sealed class ScriptTest : IAsyncDisposable
    {
        private ScriptTest(EditorTestSession session)
        {
            Session = session;
            Script = session.Asset<YarnScriptViewModel>("Dialogue/Hello");
            Script.TextBuffer.Time = Time;
            UndoRedo = session.Session.ServiceProvider.Get<IUndoRedoService>();
            UndoRedo.Done += (_, e) => Steps.Add(UndoRedo.GetName(e.Transaction));
        }

        public EditorTestSession Session { get; }

        public YarnScriptViewModel Script { get; }

        public YarnTextBuffer Buffer => Script.TextBuffer;

        public IUndoRedoService UndoRedo { get; }

        public ManualTime Time { get; } = new();

        public List<string?> Steps { get; } = [];

        public static async DispatcherTask<ScriptTest> OpenAsync()
        {
            return new ScriptTest(await StrideEditorTestSession.OpenAsync(Options, ("Dialogue/Hello", new YarnScriptAsset { Text = Hello })));
        }

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
