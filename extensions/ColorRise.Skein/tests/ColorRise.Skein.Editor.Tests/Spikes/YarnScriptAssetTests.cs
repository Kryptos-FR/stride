using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Assets.Editor.Testing;
using Stride.Core.Assets;
using Stride.Core.Presentation.Dirtiables;
using Stride.Core.Presentation.Services;

namespace ColorRise.Skein.Editor.Tests.Spikes;

/// <summary>
/// A <c>.yarn</c> file of an asset folder is an asset of the session, and stays a plain Yarn file.
/// </summary>
public sealed class YarnScriptAssetTests
{
    private const string Hello = "title: Start\n---\nOdile: Hello.\n===\n";

    private static StrideEditorTestSessionOptions Options => new() { Plugins = [new SkeinEditorPlugin()] };

    [Fact]
    public async Task A_yarn_file_loads_as_a_script_with_its_text()
    {
        await using var session = await StrideEditorTestSession.OpenAsync(Options, ("Dialogue/Hello", new YarnScriptAsset { Text = Hello }));

        var path = Path.Combine(session.Directory, session.PackageName, "Assets", "Dialogue", "Hello.yarn");
        Assert.Equal(Hello, File.ReadAllText(path));

        var script = session.Asset<YarnScriptViewModel>("Dialogue/Hello");
        Assert.Equal(Hello, script.Asset.Text);
        Assert.Equal("/TestGame/Dialogue/Hello", script.Url);
        Assert.False(script.IsDirty);
    }

    [Fact]
    public async Task Its_id_comes_from_its_location()
    {
        await using var session = await StrideEditorTestSession.OpenAsync(Options, ("Dialogue/Hello", new YarnScriptAsset { Text = Hello }));

        var script = session.Asset<YarnScriptViewModel>("Dialogue/Hello");
        // The location the session gives it, with the package's asset namespace: a rename gives the script another id, so
        // nothing may refer to a script by id (a Yarn project names its scripts by pattern)
        Assert.Equal(SourceCodeAsset.GenerateIdFromLocation(session.PackageName, "/TestGame/Dialogue/Hello"), script.Id);
    }

    [Fact]
    public async Task A_change_of_its_text_is_an_undo_step_that_dirties_it_and_the_session_saves_plain_text()
    {
        await using var session = await StrideEditorTestSession.OpenAsync(Options, ("Dialogue/Hello", new YarnScriptAsset { Text = Hello }));
        var path = Path.Combine(session.Directory, session.PackageName, "Assets", "Dialogue", "Hello.yarn");
        var script = session.Asset<YarnScriptViewModel>("Dialogue/Hello");
        var undoRedo = session.Session.ServiceProvider.Get<IUndoRedoService>();
        const string Changed = "title: Start\n---\nOdile: Hello, courier.\n===\n";

        using (var transaction = undoRedo.CreateTransaction())
        {
            script.Asset.Text = Changed;
            undoRedo.PushOperation(new AnonymousDirtyingOperation(script.Dirtiables, () => script.Asset.Text = Hello, () => script.Asset.Text = Changed));
            undoRedo.SetName(transaction, "Typing");
        }

        Assert.True(script.IsDirty);
        Assert.Equal(Hello, File.ReadAllText(path));

        var log = session.Session.SaveSessionWithoutUI();
        Assert.False(log.HasErrors, log.ToText());
        Assert.Equal(Changed, File.ReadAllText(path));
        // What the session's own save does once the files are written (the save without windows leaves the view models alone)
        undoRedo.NotifySave();
        Assert.False(script.IsDirty);

        undoRedo.Undo();
        Assert.Equal(Hello, script.Asset.Text);
        Assert.True(script.IsDirty);
        undoRedo.Redo();
        Assert.Equal(Changed, script.Asset.Text);
    }

    [Fact]
    public async Task Renaming_it_moves_the_file_when_the_session_is_saved()
    {
        await using var session = await StrideEditorTestSession.OpenAsync(Options, ("Dialogue/Hello", new YarnScriptAsset { Text = Hello }));
        var folder = Path.Combine(session.Directory, session.PackageName, "Assets", "Dialogue");
        var script = session.Asset<YarnScriptViewModel>("Dialogue/Hello");

        script.Name = "Arrival";

        Assert.Equal("/TestGame/Dialogue/Arrival", script.Url);
        Assert.Equal(Hello, script.Asset.Text);
        var log = session.Session.SaveSessionWithoutUI();
        Assert.False(log.HasErrors, log.ToText());
        Assert.True(File.Exists(Path.Combine(folder, "Arrival.yarn")));
        Assert.False(File.Exists(Path.Combine(folder, "Hello.yarn")));
        Assert.Equal(Hello, File.ReadAllText(Path.Combine(folder, "Arrival.yarn")));
    }
}
