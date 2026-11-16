using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Assets.Editor.Testing;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.Services;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// A session as Game Studio has it once Skein is loaded: Skein's session services are there, a script opens in its
/// editor, and closing the session stops the services as the editor does.
/// </summary>
internal sealed class SkeinSession : IAsyncDisposable
{
    private readonly EditorTestSession kit;

    private SkeinSession(EditorTestSession kit)
    {
        this.kit = kit;
        UndoRedo.Done += (_, e) =>
        {
            // A transaction that made no change completes too, and is no step of the history
            if (e.Transaction.Operations.Count > 0)
            {
                Steps.Add(UndoRedo.GetName(e.Transaction));
            }
        };
    }

    public SessionViewModel Session => kit.Session;

    public string Directory => kit.Directory;

    public string PackageName => kit.PackageName;

    public YarnAnalysisService Analysis => YarnAnalysisService.Of(kit.Session);

    public YarnExternalChangeService ExternalChanges => YarnExternalChangeService.Of(kit.Session);

    public IUndoRedoService UndoRedo => kit.Session.ServiceProvider.Get<IUndoRedoService>();

    /// <summary>
    /// The names of the steps of the undo history made since the session opened.
    /// </summary>
    public List<string?> Steps { get; } = [];

    public static async DispatcherTask<SkeinSession> OpenAsync(params (string Location, Asset Asset)[] assets)
        => await OpenAsync([], assets);

    public static async DispatcherTask<SkeinSession> OpenAsync(object[] services, params (string Location, Asset Asset)[] assets)
    {
        var options = new StrideEditorTestSessionOptions
        {
            Plugins = [new SkeinEditorPlugin()],
            InitializeSession = true,
            StandInEditors = [typeof(YarnScriptEditorViewModel)],
            Services = services,
        };
        return new SkeinSession(await StrideEditorTestSession.OpenAsync(options, assets));
    }

    public static (string Location, Asset Asset) Project(string location) => (location, new YarnProjectAssetFactory().New());

    public static (string Location, Asset Asset) Script(string location, string text) => (location, new YarnScriptAsset { Text = text });

    public YarnScriptViewModel ScriptAt(string location) => kit.Asset<YarnScriptViewModel>(location);

    public YarnProjectViewModel ProjectAt(string location) => kit.Asset<YarnProjectViewModel>(location);

    /// <summary>
    /// Opens the editor of a script, as a double click in the asset view does.
    /// </summary>
    public YarnScriptEditorViewModel Edit(string location)
    {
        var script = ScriptAt(location);
        kit.Session.EditorCollection.OpenAssetEditor(script);
        Assert.True(kit.Session.EditorCollection.TryGetAssetEditor<YarnScriptEditorViewModel>(script, out var editor));
        return editor;
    }

    /// <summary>
    /// The path of the file of an asset of the package.
    /// </summary>
    public string FileOf(string location, string extension) => Path.Combine(kit.Directory, kit.PackageName, "Assets", location.Replace('/', Path.DirectorySeparatorChar) + extension);

    public async ValueTask DisposeAsync()
    {
        // The editor disposes its plugin host with the session, which runs what the plugins registered for the close;
        // the session of the test kit leaves its host as it is
        kit.OnUiThread(Analysis.Close);
        kit.OnUiThread(ExternalChanges.Close);
        await kit.DisposeAsync();
    }
}
