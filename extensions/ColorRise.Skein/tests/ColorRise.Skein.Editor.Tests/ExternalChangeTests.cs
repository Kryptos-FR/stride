using System.Reflection;
using System.Text;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.Windows;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The session takes what another program writes to a Yarn script, and asks before it gives up unsaved changes for it.
/// </summary>
public sealed class ExternalChangeTests
{
    private const string Act1 = "title: Start\n---\nOdile: The lamp's out again.\n===\n";
    private const string Changed = Act1 + "title: Pier\n---\nFisher: Mind the nets.\n===\n";

    [Fact]
    public async Task A_script_without_unsaved_changes_takes_the_text_of_its_file()
    {
        var (dialogs, questions) = Dialogs.Answering(1);
        await using var session = await SkeinSession.OpenAsync([dialogs], SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var editor = session.Edit("Dialogue/Act1");
        var script = session.ScriptAt("Dialogue/Act1");
        var path = session.FileOf("Dialogue/Act1", ".yarn");
        Assert.Equal(Act1, File.ReadAllText(path));

        // Nothing changed: nothing to take
        await session.ExternalChanges.CheckAsync();
        Assert.Empty(session.Steps);

        File.WriteAllText(path, Changed);
        await session.ExternalChanges.CheckAsync();

        Assert.Equal(Changed, script.TextBuffer.Text);
        Assert.Equal(["Start", "Pier"], editor.Cards.Select(c => c.Title));
        Assert.False(script.IsDirty);
        Assert.Equal(["Reload from disk"], session.Steps);
        Assert.Empty(questions.Asked);

        // The same file again is no change
        await session.ExternalChanges.CheckAsync();
        Assert.Single(session.Steps);

        // The text of before is one undo away, and is then an unsaved change
        session.UndoRedo.Undo();
        Assert.Equal(Act1, script.TextBuffer.Text);
        Assert.Single(editor.Cards);
        Assert.True(script.IsDirty);
    }

    [Theory]
    [InlineData("utf-16")]
    [InlineData("latin-1")]
    public async Task A_file_that_is_not_utf8_is_not_taken_and_says_so(string encoding)
    {
        var (dialogs, questions) = Dialogs.Answering(1);
        await using var session = await SkeinSession.OpenAsync([dialogs], SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var script = session.ScriptAt("Dialogue/Act1");
        var path = session.FileOf("Dialogue/Act1", ".yarn");
        // The text is read: an asset whose text was never read takes its file as it is when it is first read
        Assert.Equal(Act1, script.TextBuffer.Text);
        var warnings = new List<string>();
        void OnLogged(ILogMessage message)
        {
            if (message.Module == "ColorRise.Skein" && message.Type == LogMessageType.Warning)
                warnings.Add(message.Text);
        }

        GlobalLogger.GlobalMessageLogged += OnLogged;
        try
        {
            File.WriteAllText(path, Changed.Replace("Mind", "Gare à"), encoding == "utf-16" ? Encoding.Unicode : Encoding.Latin1);
            await session.ExternalChanges.CheckAsync();

            Assert.Equal(Act1, script.TextBuffer.Text);
            Assert.Empty(session.Steps);
            Assert.Empty(questions.Asked);
            var warning = Assert.Single(warnings);
            Assert.Contains("Dialogue/Act1", warning);
            Assert.Contains("not UTF-8", warning);
        }
        finally
        {
            GlobalLogger.GlobalMessageLogged -= OnLogged;
        }
    }

    [Fact]
    public async Task A_script_with_unsaved_changes_takes_the_text_of_its_file_when_the_user_says_so()
    {
        var (dialogs, questions) = Dialogs.Answering(2);
        await using var session = await SkeinSession.OpenAsync([dialogs], SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var script = session.ScriptAt("Dialogue/Act1");
        var path = session.FileOf("Dialogue/Act1", ".yarn");
        script.TextBuffer.Replace(0, 0, "// mine\n", "Typing");

        // 'Keep my changes'
        File.WriteAllText(path, Changed);
        await session.ExternalChanges.CheckAsync();

        var question = Assert.Single(questions.Asked);
        Assert.StartsWith("/TestGame/Dialogue/Act1 has been changed outside of Game Studio, and has unsaved changes here.", question.Message);
        Assert.Equal(["Reload", "Keep my changes"], question.Buttons);
        Assert.Equal("// mine\n" + Act1, script.TextBuffer.Text);
        Assert.True(script.IsDirty);

        // The answer holds for that content of the file
        await session.ExternalChanges.CheckAsync();
        Assert.Single(questions.Asked);

        // Another content is another question: 'Reload'
        questions.Answer = 1;
        File.WriteAllText(path, Changed + "// theirs\n");
        await session.ExternalChanges.CheckAsync();

        Assert.Equal(2, questions.Asked.Count);
        Assert.Equal(Changed + "// theirs\n", script.TextBuffer.Text);
        Assert.False(script.IsDirty);
        Assert.Equal(["Typing", "Reload from disk"], session.Steps);
    }

    [Fact]
    public async Task What_the_session_saved_is_not_a_change_of_another_program()
    {
        var (dialogs, questions) = Dialogs.Answering(1);
        await using var session = await SkeinSession.OpenAsync([dialogs], SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1));
        var script = session.ScriptAt("Dialogue/Act1");
        var path = session.FileOf("Dialogue/Act1", ".yarn");

        script.TextBuffer.Replace(0, 0, "// one\n", "Typing");
        var log = session.Session.SaveSessionWithoutUI();
        Assert.False(log.HasErrors, log.ToText());
        session.UndoRedo.NotifySave();
        Assert.Equal("// one\n" + Act1, File.ReadAllText(path));

        // Typed after the save, before the service looks at the file
        script.TextBuffer.Replace(0, 0, "// two\n", "Typing");
        await session.ExternalChanges.CheckAsync();

        Assert.Empty(questions.Asked);
        Assert.Equal("// two\n// one\n" + Act1, script.TextBuffer.Text);
        Assert.Equal(["Typing", "Typing"], session.Steps);
    }

    [Fact]
    public async Task The_files_are_watched()
    {
        var (dialogs, _) = Dialogs.Answering(1);
        await using var session = await SkeinSession.OpenAsync([dialogs], SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", Act1));
        var (act1, act2) = (session.ScriptAt("Dialogue/Act1"), session.ScriptAt("Dialogue/Act2"));
        session.ExternalChanges.Delay = TimeSpan.FromMilliseconds(50);
        Assert.Equal(Act1, act1.TextBuffer.Text);
        Assert.False(act1.Asset.HasByteOrderMark);
        Assert.False(act2.Asset.HasByteOrderMark);

        // Written with a byte order mark, which the text does not have and the next save keeps
        var withMark = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        File.WriteAllText(session.FileOf("Dialogue/Act2", ".yarn"), Changed, withMark);
        File.WriteAllText(session.FileOf("Dialogue/Act1", ".yarn"), Changed, withMark);
        await ExclusiveDispatcherService.WaitUntilAsync(() => act1.TextBuffer.Text != Act1 && act2.Asset.HasByteOrderMark, "the scripts to take what their files have");

        Assert.Equal(Changed, act1.TextBuffer.Text);
        Assert.True(act1.Asset.HasByteOrderMark);
        Assert.False(act1.IsDirty);

        // A script the session had not read yet reads the file as it is now: nothing to undo
        Assert.Equal(Changed, act2.TextBuffer.Text);
        Assert.True(act2.Asset.HasByteOrderMark);
        Assert.Equal(["Reload from disk"], session.Steps);
    }

    /// <summary>
    /// The dialogs of the editor, which answer the questions with a button and keep them.
    /// </summary>
    public class Dialogs : DispatchProxy
    {
        /// <summary>
        /// The questions asked, each with the captions of its buttons.
        /// </summary>
        public List<(string Message, string[] Buttons)> Asked { get; } = [];

        /// <summary>
        /// The result of the button that answers the questions.
        /// </summary>
        public int Answer { get; set; }

        public static (IEditorDialogService Service, Dialogs Questions) Answering(int answer)
        {
            var service = Create<IEditorDialogService, Dialogs>();
            var dialogs = (Dialogs)(object)service;
            dialogs.Answer = answer;
            return (service, dialogs);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IDialogService.MessageBoxAsync) && args is [string message, IReadOnlyCollection<DialogButtonInfo> buttons, ..])
            {
                Asked.Add((message, [.. buttons.Select(b => b.Content?.ToString() ?? "")]));
                return Task.FromResult(Answer);
            }

            // What the session shows without a question (its progress) is nothing to answer
            return targetMethod?.ReturnType == typeof(void) ? null : throw new NotSupportedException($"The test does not expect {targetMethod?.Name}.");
        }
    }
}
