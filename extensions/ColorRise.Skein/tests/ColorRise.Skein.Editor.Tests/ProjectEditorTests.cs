using System.Reflection;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Localisation;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.Text;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.Testing;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The overview of a Yarn project, and the commands on its line ids, its strings files and Yarn Spinner's own project file.
/// </summary>
public sealed class ProjectEditorTests
{
    private const string Act1 = "title: Arrival\n---\n/// The purse\n<<declare $gold = 5 as number>>\nNarrator: Here. #line:here\nNarrator: No id.\n-> Go on\n    <<jump Pier>>\n===\ntitle: Pier\n---\nNarrator: The pier.\n===\n";
    private const string Act2 = "title: Lighthouse\n---\n<<set $lit = true>>\nKeeper: Up. #line:up\n<<jump Nowhere>>\n===\n";

    private static async DispatcherTask<SkeinSession> OpenAsync(params string[] languages)
    {
        var project = new YarnProjectAssetFactory().New();
        project.Localisations.AddRange(languages.Select(l => new YarnLocalisation { Language = l }));
        var (dialogs, _) = ExternalChangeTests.Dialogs.Answering(1);
        return await SkeinSession.OpenAsync(
            [dialogs, DispatchProxy.Create<IProgressWindowService, Silent>()],
            ("Dialogue/Story", project),
            SkeinSession.Script("Dialogue/Act1", Act1),
            SkeinSession.Script("Dialogue/Act2", Act2));
    }

    [Fact]
    public async Task The_overview_lists_the_scripts_the_variables_and_the_problems_of_the_project()
    {
        await using var session = await OpenAsync();
        var editor = session.EditProject("Dialogue/Story");
        await editor.WhenRefreshed();

        Assert.Equal([("Dialogue/Act1.yarn", 2, 0), ("Dialogue/Act2.yarn", 1, 1)], editor.Scripts.Select(s => (s.Name, s.NodeCount, s.ErrorCount)));
        Assert.Equal(3, editor.NodeCount);
        Assert.Equal(["$gold", "$lit"], editor.Variables.Select(v => v.Name));
        Assert.Equal(new YarnProjectVariableItem("$gold", "number", "5", "The purse", "Dialogue/Act1.yarn", 4, IsSmart: false, IsDeclared: true), editor.Variables[0]);
        Assert.Equal(("bool", false), (editor.Variables[1].Type, editor.Variables[1].IsDeclared));
        var missing = Assert.Single(editor.Diagnostics, d => d.Code == "YS0012");
        Assert.Equal(("Dialogue/Act2.yarn", 5), (missing.File, missing.Line));
        Assert.Equal((5, 3), (editor.LineCount, editor.LinesWithoutId));
        Assert.Equal("2 scripts  ·  3 nodes  ·  5 lines  ·  2 variables", editor.Summary);
        Assert.Equal("3 lines have no id and cannot be translated.", editor.LineIdNotice);
        var english = Assert.Single(editor.Languages);
        Assert.Equal(("en", true, 2, 2), (english.Language, english.IsBase, english.Lines, english.Translated));

        // The overview follows the scripts as they are typed
        session.ScriptAt("Dialogue/Act2").TextBuffer.Replace(0, 0, "title: Dawn\n---\nEarly. #line:early\n===\n", YarnTextBuffer.TypingActionName);
        await editor.WhenRefreshed();
        Assert.Equal(2, editor.Scripts[1].NodeCount);
        Assert.Equal(3, editor.Languages[0].Lines);

        // A variable, a message and a script open the script's editor, at their place
        await editor.GoTo(editor.Variables[0]);
        Assert.True(session.Session.EditorCollection.TryGetAssetEditor<YarnScriptEditorViewModel>(session.ScriptAt("Dialogue/Act1"), out var act1));
        Assert.Equal(new YarnTextPlace(4, 1), act1.TakeRequestedPlace());
        await editor.GoTo(editor.Diagnostics.Single(d => d.Code == "YS0012"));
        Assert.True(session.Session.EditorCollection.TryGetAssetEditor<YarnScriptEditorViewModel>(session.ScriptAt("Dialogue/Act2"), out var act2));
        Assert.Equal(9, act2.TakeRequestedPlace()?.Line);
    }

    [Fact]
    public async Task Add_line_ids_tags_every_line_without_an_id_in_one_step()
    {
        await using var session = await OpenAsync();
        var editor = session.EditProject("Dialogue/Story");
        var (act1, act2) = (session.ScriptAt("Dialogue/Act1"), session.ScriptAt("Dialogue/Act2"));

        var result = await session.Localisation.AddLineIds(session.ProjectAt("Dialogue/Story"));

        Assert.Equal(3, result.Added);
        Assert.Empty(result.Problems);
        Assert.Equal(["Add line ids"], session.Steps);
        var lines = act1.TextBuffer.Text.Split('\n');
        Assert.Equal("Narrator: Here. #line:here", lines[4]);
        Assert.StartsWith("Narrator: No id. #line:", lines[5]);
        Assert.StartsWith("-> Go on #line:", lines[6]);
        Assert.StartsWith("Narrator: The pier. #line:", lines[11]);
        // Nothing else of the text changed, and a script with nothing to tag is left alone
        Assert.Equal(Act1, string.Join("\n", lines.Select(l => l.IndexOf(" #line:", StringComparison.Ordinal) is var at and >= 0 && !l.EndsWith("#line:here", StringComparison.Ordinal) ? l[..at] : l)));
        Assert.Equal(Act2, act2.TextBuffer.Text);
        await editor.WhenRefreshed();
        Assert.Equal(0, editor.LinesWithoutId);

        // Once more: nothing to add, and no step
        await editor.AddLineIds();
        Assert.Equal("Every line that can have an id has one.", editor.Message);
        Assert.Single(session.Steps);

        // One undo takes every id back
        session.UndoRedo.Undo();
        Assert.Equal(Act1, act1.TextBuffer.Text);

        // A script that cannot be read is left as it is, with the reason; the others get their ids
        act2.TextBuffer.Replace(act2.TextBuffer.Text.Length, 0, "title: Broken\n---\n<<if>>\nLost.\n", YarnTextBuffer.TypingActionName);
        await editor.AddLineIds();
        Assert.StartsWith("Added 3 line ids.\nDialogue/Act2.yarn: the script has errors", editor.Message);
        Assert.DoesNotContain("Lost. #line:", act2.TextBuffer.Text);
    }

    [Fact]
    public async Task Strings_are_exported_for_a_language_and_updated_when_the_scripts_change()
    {
        await using var session = await OpenAsync("fr");
        var project = session.ProjectAt("Dialogue/Story");
        var editor = session.EditProject("Dialogue/Story");
        var act1 = session.ScriptAt("Dialogue/Act1");
        var file = Path.Combine(Path.GetDirectoryName(session.FileOf("Dialogue/Story", ".sdyarn"))!, "Strings", "Story.fr.csv");
        await editor.WhenRefreshed();
        Assert.Equal([("en", true), ("fr", true)], editor.Languages.Select(l => (l.Language, l.CanExport)));
        Assert.Equal((2, 0, 2, ""), (editor.Languages[1].Lines, editor.Languages[1].Translated, editor.Languages[1].Missing, editor.Languages[1].File));
        // A language without its strings is a warning of the build, and so are lines without an id
        Assert.Contains(editor.Warnings, w => w.Contains("3 lines have no line id"));

        // Nothing to update yet
        var none = Assert.Single(await session.Localisation.UpdateStrings(project));
        Assert.Equal("'fr' has no strings file: export its strings first.", none.Problem);

        // The export writes the lines that have an id, in the base language, and sets the file of the language
        await editor.ExportStrings("fr");
        Assert.Equal("Exported 2 lines to Strings/Story.fr.csv, from the folder of the project.", editor.Message);
        var rows = YarnStringsCsv.Read(File.ReadAllText(file));
        Assert.Equal([("fr", "line:here", "Narrator: Here."), ("fr", "line:up", "Keeper: Up.")], rows.Select(r => (r.Language, r.Id, r.Text)));
        Assert.Equal(file, project.Asset.Localisations[0].Strings?.ToOSPath());
        Assert.True(project.IsDirty);
        Assert.Equal(["Export strings"], session.Steps);
        await editor.WhenRefreshed();
        Assert.Equal(("Strings/Story.fr.csv", 2, false), (editor.Languages[1].File, editor.Languages[1].Translated, editor.Languages[1].CanExport));

        // A file that is there is never written over
        await editor.ExportStrings("fr");
        Assert.Contains("is there already", editor.Message);
        Assert.Contains("no language 'de'", (await session.Localisation.ExportStrings(project, "de")).Problem);

        // The translator works, then the scripts change: a new line, a changed one
        File.WriteAllText(file, File.ReadAllText(file).Replace("Narrator: Here.", "Narrateur : Ici.").Replace("Keeper: Up.", "Gardien : En haut."));
        act1.TextBuffer.Replace(act1.TextBuffer.Text.IndexOf("Narrator: Here.", StringComparison.Ordinal), 0, "Narrator: New. #line:new\n", YarnTextBuffer.TypingActionName);
        var up = session.ScriptAt("Dialogue/Act2").TextBuffer;
        up.Replace(up.Text.IndexOf("Up.", StringComparison.Ordinal), 3, "Up there.", YarnTextBuffer.TypingActionName);

        await editor.UpdateStrings();

        Assert.Equal("fr: 1 added, 0 removed, 1 to update.", editor.Message);
        rows = YarnStringsCsv.Read(File.ReadAllText(file));
        Assert.Equal([("line:new", ""), ("line:here", "Narrateur : Ici."), ("line:up", "(NEEDS UPDATE) Gardien : En haut.")], rows.Select(r => (r.Id, r.Text)));
        await editor.WhenRefreshed();
        var french = editor.Languages[1];
        Assert.Equal((3, 1, 1, 1), (french.Lines, french.Translated, french.ToUpdate, french.Missing));
        await editor.UpdateStrings();
        Assert.Equal("fr: up to date.", editor.Message);

        // The base language is exported too, for the translators to read
        await editor.ExportStrings("en");
        var english = Path.Combine(Path.GetDirectoryName(file)!, "Story.en.csv");
        Assert.Equal(3, YarnStringsCsv.Read(File.ReadAllText(english)).Count);

        // Its file is written again from the scripts, but never over a file that is not a strings file
        File.WriteAllText(english, File.ReadAllText(english).Replace("Narrator: New.", "Edited by hand."));
        await editor.ExportStrings("en");
        var exported = File.ReadAllText(english);
        Assert.Contains("Narrator: New.", exported);
        File.WriteAllText(english, "Notes of the translator\n");
        await editor.ExportStrings("en");
        Assert.Contains("is not a strings file", editor.Message);
        Assert.Equal("Notes of the translator\n", File.ReadAllText(english));
        File.WriteAllText(english, exported);

        // A script that cannot be read stops the update: its lines would look removed
        act1.TextBuffer.Replace(act1.TextBuffer.Text.Length, 0, "title: Broken\n---\n<<if>>\n", YarnTextBuffer.TypingActionName);
        var before = File.ReadAllText(file);
        await editor.UpdateStrings();
        Assert.StartsWith("The scripts have errors: fix them first, so that no translation is lost. Dialogue/Act1.yarn(", editor.Message);
        Assert.Equal(before, File.ReadAllText(file));
    }

    [Fact]
    public async Task Saving_a_project_writes_the_project_file_of_Yarn_Spinners_tools()
    {
        await using var session = await OpenAsync("fr");
        var project = session.ProjectAt("Dialogue/Story");
        var path = session.FileOf("Dialogue/Story", ".yarnproject");
        await session.Localisation.ExportStrings(project, "fr");

        Assert.True(await session.Session.SaveSessionAsync());

        var read = Yarn.Compiler.Project.LoadFromFile(path);
        Assert.Equal(["**/*.yarn"], read.SourceFilePatterns);
        Assert.Equal("en", read.BaseLanguage);
        Assert.Equal("Strings/Story.fr.csv", read.Localisation["fr"].Strings);
        Assert.Equal(Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error, read.CompilerOptions.DiagnosticsSeverity["YS0012"]);
        // Yarn Spinner finds the scripts of the project from it
        Assert.Equal(["Act1.yarn", "Act2.yarn"], read.SourceFiles.Select(Path.GetFileName).Order());

        // What another tool keeps in the file stays, and a save that changes nothing does not write the file again
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"baseLanguage\"", "\"editorOptions\": { \"zoom\": 2 },\n    \"baseLanguage\""));
        Assert.True(await session.Session.SaveSessionAsync());
        var text = File.ReadAllText(path);
        Assert.Contains("\"zoom\": 2", text);
        var stamp = File.GetLastWriteTimeUtc(path);
        Assert.True(await session.Session.SaveSessionAsync());
        Assert.Equal(text, File.ReadAllText(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));

        // A project that does not ask for the file has none written
        File.Delete(path);
        project.Asset.WriteYarnProjectFile = false;
        Assert.Empty(session.Localisation.WriteProjectFiles(project));
        Assert.False(File.Exists(path));
    }

    /// <summary>
    /// The progress window of a save, which a test does not look at.
    /// </summary>
    public class Silent : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }
}
