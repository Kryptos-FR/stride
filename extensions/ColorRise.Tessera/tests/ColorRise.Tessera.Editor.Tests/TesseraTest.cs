using ColorRise.Tessera.Editor.FlipbookEditor;
using ColorRise.Tessera.Editor.TileMapEditor;
using ColorRise.Tessera.Editor.TileSetEditor;
using ColorRise.Tessera.Editor.ViewModels;
using ColorRise.Tessera.Tests.Assets;
using Stride.Assets.Editor.Templates;
using Stride.Assets.Editor.Testing;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Components.TemplateDescriptions;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Templates;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Services;
using Stride.Core.Yaml;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// Opens a session of the editor test kit as Game Studio opens one once Tessera is loaded.
/// </summary>
internal static class TesseraTest
{
    public static DispatcherTask<EditorTestSession> OpenAsync(params (string Location, Asset Asset)[] assets)
    {
        var options = new StrideEditorTestSessionOptions
        {
            Plugins = [new TesseraEditorPlugin()],
            InitializeSession = true,
        };
        return StrideEditorTestSession.OpenAsync(options, assets);
    }
}

/// <summary>
/// A session as Game Studio has it once Tessera is loaded, with what the editor tests ask of it: the undo steps made, the
/// files of the package, images written beside the assets, and the templates of Tessera.
/// </summary>
internal sealed class TesseraSession : IAsyncDisposable
{
    // The editors the test made, destroyed before the session closes
    private readonly List<AssetEditorViewModel> editors = [];

    private TesseraSession(EditorTestSession kit, TestEditorDialogService dialogs)
    {
        Kit = kit;
        Dialogs = dialogs;
        UndoRedo.Done += (_, e) =>
        {
            // A transaction that made no change completes too, and is no step of the history
            if (e.Transaction.Operations.Count > 0)
            {
                Steps.Add(UndoRedo.GetName(e.Transaction));
            }
        };
    }

    public EditorTestSession Kit { get; }

    public SessionViewModel Session => Kit.Session;

    public IUndoRedoService UndoRedo => Kit.Session.ServiceProvider.Get<IUndoRedoService>();

    /// <summary>
    /// The dialogs of the session: the answers they give, and the questions they were asked.
    /// </summary>
    public TestEditorDialogService Dialogs { get; }

    /// <summary>
    /// The names of the undo steps made since the session opened.
    /// </summary>
    public List<string?> Steps { get; } = [];

    public static DispatcherTask<TesseraSession> OpenAsync(params (string Location, Asset Asset)[] assets)
        => OpenAsync(new StrideEditorTestSessionOptions(), assets);

    /// <summary>
    /// Opens a session with Tessera's plugin, initialized, and what the test gives: other plugins (the views'), services (the
    /// dialogs), stand-in editors or the dispatcher of a UI thread.
    /// </summary>
    public static async DispatcherTask<TesseraSession> OpenAsync(StrideEditorTestSessionOptions options, params (string Location, Asset Asset)[] assets)
    {
        // The dialogs the test gives, or a dialog service that answers OK and picks nothing
        var dialogs = options.Services.OfType<TestEditorDialogService>().FirstOrDefault() ?? new TestEditorDialogService();
        var withTessera = new StrideEditorTestSessionOptions
        {
            Plugins = [new TesseraEditorPlugin(), .. options.Plugins],
            InitializeSession = true,
            StandInEditors = options.StandInEditors,
            Services = [dialogs, .. options.Services.Where(service => service != dialogs)],
            Dispatcher = options.Dispatcher,
        };
        return new TesseraSession(await StrideEditorTestSession.OpenAsync(withTessera, assets), dialogs);
    }

    /// <summary>
    /// Makes the editor of a tile set as Game Studio does, and initializes it; the session destroys it when it closes.
    /// </summary>
    public async Task<TileSetEditorViewModel> EditTileSetAsync(string location)
    {
        var editor = new TileSetEditorViewModel(TileSetAt(location));
        editors.Add(editor);
        Assert.True(await editor.Initialize());
        return editor;
    }

    /// <summary>
    /// Makes the editor of a map as Game Studio does, and initializes it; the session destroys it when it closes.
    /// </summary>
    public async Task<TileMapEditorViewModel> EditMapAsync(string location)
    {
        var editor = new TileMapEditorViewModel(MapAt(location));
        editors.Add(editor);
        Assert.True(await editor.Initialize());
        return editor;
    }

    /// <summary>
    /// Makes the editor of a flipbook as Game Studio does, and initializes it; the session destroys it when it closes.
    /// </summary>
    public async Task<FlipbookEditorViewModel> EditFlipbookAsync(string location)
    {
        var editor = new FlipbookEditorViewModel(FlipbookAt(location));
        editors.Add(editor);
        Assert.True(await editor.Initialize());
        return editor;
    }

    /// <summary>
    /// The path of the file of an asset of the package.
    /// </summary>
    public string FileOf(string location, string extension) => Path.Combine(AssetsFolder, location.Replace('/', Path.DirectorySeparatorChar) + extension);

    /// <summary>
    /// Writes a PNG at <c>Assets/&lt;location&gt;</c> of the package, and gives its full path.
    /// </summary>
    public UFile WriteImage(string location, int width, int height, Func<int, int, Color> pixel)
    {
        var path = Path.Combine(AssetsFolder, location.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, TestImages.Png(width, height, pixel));
        return new UFile(path);
    }

    public TileSetViewModel TileSetAt(string location) => Kit.Asset<TileSetViewModel>(location);

    public TileMapViewModel MapAt(string location) => Kit.Asset<TileMapViewModel>(location);

    public FlipbookViewModel FlipbookAt(string location) => Kit.Asset<FlipbookViewModel>(location);

    /// <summary>
    /// A template of Tessera, read from its file in the sources: <c>src/ColorRise.Tessera.Assets/Templates/Assets/&lt;name&gt;.sdtpl</c>.
    /// </summary>
    public TemplateDescription Template(string name)
    {
        var path = SourceFile($"src/ColorRise.Tessera.Assets/Templates/Assets/{name}.sdtpl");
        var description = YamlSerializer.Load<TemplateDescription>(path);
        description.FullPath = path;
        return description;
    }

    /// <summary>
    /// Runs a template as "Add asset" does, in a folder of the package, made first if the package has none of that name.
    /// </summary>
    public async Task<List<AssetViewModel>> RunTemplateAsync(TemplateDescription template, string folder)
    {
        // Both Game Studios register the generators of Stride's asset templates when they start, and the kit does not (finding 11).
        // Tessera's templates use the generator of an asset factory; the prompts are those of scripts and models
        StrideAssetTemplates.Register(new NoPrompt(), new NoPrompt());
        var assets = Session.AssetCollection;
        var transactions = Session.ActionHistory.Transactions.Count;
        var created = await Session.Dispatcher.InvokeTask(() =>
        {
            assets.SelectedLocations.Clear();
            // As a person makes the folder in the solution explorer, then adds the asset in it; making it is no step of the history
            assets.SelectedLocations.Add(Session.LocalPackages.Single().AssetMountPoint.GetOrCreateDirectory(folder, canUndoRedoCreation: false));
            return assets.RunAssetTemplateAsync(new TemplateDescriptionViewModel(Session.ServiceProvider, template), null);
        });

        // The history records the creation once the session heard of the new assets: the test goes on after that
        if (created.Count > 0)
        {
            await ExclusiveDispatcherService.WaitUntilAsync(() => Session.ActionHistory.Transactions.Count > transactions, "the creation to be recorded");
        }
        return created;
    }

    /// <summary>
    /// A file of the extension's sources, found from the folder of the tests.
    /// </summary>
    public static string SourceFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                return path;
            }
        }
        throw new FileNotFoundException(relativePath);
    }

    public ValueTask DisposeAsync()
    {
        foreach (var editor in editors)
        {
            editor.Destroy();
        }
        return Kit.DisposeAsync();
    }

    private string AssetsFolder => Path.Combine(Kit.Directory, Kit.PackageName, "Assets");

    /// <summary>
    /// The dialogs of a new script and of a model import, which no template of Tessera shows.
    /// </summary>
    private sealed class NoPrompt : IScriptNamePrompt, IModelImportPrompt
    {
        public Task<bool> PromptAsync(ScriptNameViewModel viewModel) => throw new NotSupportedException("No template of Tessera asks the names of a script.");

        public Task<bool> PromptAsync(ImportModelFromFileViewModel viewModel) => throw new NotSupportedException("No template of Tessera imports a model.");
    }
}
