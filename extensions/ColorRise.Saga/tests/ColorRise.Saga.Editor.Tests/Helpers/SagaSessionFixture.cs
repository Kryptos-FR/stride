using System.Reflection;
using ColorRise.Saga.Assets;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Assets.Editor;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Editors;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Editor.Components.Status;
using Stride.Core.Presentation.Views;
using Stride.Core;
using Stride.Core.IO;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Reflection;

namespace ColorRise.Saga.Editor.Tests.Helpers;

/// <summary>
/// A Game Studio session over a small package of Saga assets, opened the way the editor opens one, with Stride's default assets
/// plugin and Saga's editor plugin.
/// </summary>
/// <remarks>
/// Stride ships no editor test kit, so this is a reduced copy of the fixtures of Stride's own editor tests, written against the public
/// API (see docs/plugin-findings.md, finding 5). The session and the test run in one exclusive context that stands for the UI thread:
/// <see cref="RunAsync"/> opens the session there, runs the test there and destroys the session there.
/// </remarks>
public sealed class SagaSessionFixture
{
    public const string PackageName = "SagaTest";

    static SagaSessionFixture()
    {
        // What the session does for the assemblies of a game's packages
        AssemblyRegistry.Register(typeof(SagaSystem).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(StoryAsset).Assembly, AssemblyCommonCategories.Assets);
        PackageSessionPublicHelper.FindAndSetMSBuildVersion();
    }

    private SagaSessionFixture(string directory, IViewModelServiceProvider serviceProvider)
    {
        Directory = directory;
        ServiceProvider = serviceProvider;
    }

    public string Directory { get; }

    public string PackageDirectory => Path.Combine(Directory, PackageName);

    public UFile PackagePath => new(Path.Combine(PackageDirectory, PackageName + Package.PackageFileExtension));

    public IViewModelServiceProvider ServiceProvider { get; }

    public PackageSessionResult Result { get; } = new();

    public SessionViewModel Session { get; private set; } = null!;

    /// <summary>
    /// The messages of the loading, for assertion failures.
    /// </summary>
    public string Log => string.Join(Environment.NewLine, Result.Messages.Select(x => x.ToString()));

    /// <summary>
    /// The asset at a location of the package (the session prefixes it with the package name).
    /// </summary>
    public T Asset<T>(string location) where T : AssetViewModel
        => Assert.IsType<T>(Session.GetAssetByUrl("/" + PackageName + "/" + location) ?? throw new InvalidOperationException($"No asset at {location}."), exactMatch: false);

    /// <summary>
    /// Waits for the property grid of the selection: it builds its nodes on another thread, which must not run while the test
    /// changes the asset, as in Stride's own editor tests.
    /// </summary>
    public async Task WaitForPropertiesAsync()
    {
        var properties = Session.AssetViewProperties;
        for (var i = 0; i < 250 && !properties.CanDisplayProperties && properties.FallbackMessage == "Loading properties..."; ++i)
            await Task.Delay(20);
    }

    /// <summary>
    /// Sets a member of a card or asset through the property graph, in one step of the undo history, as the property grid does.
    /// </summary>
    public void Edit(object owner, string member, object? value)
    {
        var undoRedo = Session.UndoRedoService;
        using var transaction = undoRedo.CreateTransaction();
        (Session.AssetNodeContainer.GetNode(owner) ?? throw new InvalidOperationException($"{owner} is not in an asset."))[member].Update(value);
        undoRedo.SetName(transaction, $"Edit {member}");
    }

    /// <summary>
    /// Writes a package with the given assets (location in the package, asset), opens it, runs the test and destroys the session.
    /// </summary>
    public static Task RunAsync(Func<SagaSessionFixture, Task> test, params (string Location, Asset Asset)[] assets)
        => ExclusiveContext.Run(async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "ColorRise.Saga.Editor.Tests", Guid.NewGuid().ToString("N"));
            var plugins = new TestPluginService(new StrideDefaultAssetsPlugin(), new SagaEditorPlugin(), new TestEditorViewsPlugin());
            var serviceProvider = new ViewModelServiceProvider(
            [
                new ExclusiveDispatcherService(),
                NotSupportedProxy<IEditorDialogService>.Create(),
                NotSupportedProxy<IClipboardService>.Create(),
                plugins,
            ]);
            var fixture = new SagaSessionFixture(directory, serviceProvider);
            try
            {
                fixture.WritePackage(assets);
                var session = await SessionViewModel.OpenSessionAsync(fixture.PackagePath, loadParameters => LoadWithoutMSBuild(fixture.PackagePath, fixture.Result, loadParameters),
                    fixture.Result, new FakeMainViewModel(serviceProvider), serviceProvider);
                fixture.Session = session ?? throw new InvalidOperationException("The session could not be opened:" + Environment.NewLine + fixture.Log);
                // As the editor does once the session is open: the plugins add their services, commands and the like to it
                plugins.InitializeSession(session);
                await test(fixture);
            }
            finally
            {
                if (fixture.Session is not null)
                {
                    fixture.Session.Destroy();
                    // Nothing the test started may outlive it
                    await fixture.Session.BackgroundWork.WaitAsync(TimeSpan.FromSeconds(30));
                }
                try
                {
                    System.IO.Directory.Delete(directory, true);
                }
                catch (IOException)
                {
                    // Best effort
                }
            }
        });

    /// <summary>
    /// Opens a package without the NuGet restore and the MSBuild evaluation of its project, which have nothing to give here.
    /// </summary>
    private static void LoadWithoutMSBuild(UFile packagePath, PackageSessionResult result, PackageLoadParameters loadParameters)
    {
        var projectDirectory = packagePath.GetFullDirectory().ToOSPath();
        var projectFiles = System.IO.Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories).Select(x => new UFile(x)).ToList();
        PackageSession.LoadWithoutMSBuild(packagePath, ProjectType.Library, result, loadParameters, PlatformType.Shared, projectFiles);
    }

    private void WritePackage((string Location, Asset Asset)[] assets)
    {
        System.IO.Directory.CreateDirectory(Path.Combine(PackageDirectory, "Assets"));

        // A project next to the package makes it a project of the solution, hence editable. It holds no asset types, so the
        // session does not build it.
        File.WriteAllText(Path.Combine(PackageDirectory, PackageName + ".csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <StrideContainsAssetTypes>false</StrideContainsAssetTypes>
              </PropertyGroup>
            </Project>
            """);

        var package = new Package
        {
            FullPath = PackagePath,
            Meta = { Name = PackageName, Version = new PackageVersion("1.0.0") },
            AssetFolders = { new AssetFolder("Assets") },
            ResourceFolders = { "Resources" },
        };
        AssetFileSerializer.Save(PackagePath.ToOSPath(), package, null);

        foreach (var (location, asset) in assets)
        {
            var extension = AssetRegistry.GetDefaultExtension(asset.GetType()) ?? throw new InvalidOperationException($"{asset.GetType()} has no AssetDescription.");
            var path = Path.Combine(PackageDirectory, "Assets", location + extension);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AssetFileSerializer.Save(path, asset, null);
        }
    }

    private sealed class TestPluginService(params AssetsPlugin[] plugins) : SceneEditorPluginHost(plugins)
    {
        protected override Type AssetEditorViewType => typeof(IAssetEditorView);

        protected override bool AddTemplateProvider(ISessionViewModel session, ITemplateProvider provider) => false;

        protected override void RemoveTemplateProvider(ISessionViewModel session, ITemplateProvider provider)
        {
        }
    }

    /// <summary>
    /// Views for Saga's editors, which show nothing: the session opens an editor only when a view is registered for it.
    /// </summary>
    private sealed class TestEditorViewsPlugin : AssetsPlugin
    {
        public override IReadOnlyList<Assembly> ScannedAssemblies => [];

        public override void Initialize(IEditorPluginContext context)
        {
            context.AssetEditorViewTypes.Register(typeof(StoryEditorViewModel), typeof(TestEditorView));
            context.AssetEditorViewTypes.Register(typeof(DialogueEditorViewModel), typeof(TestEditorView));
        }

        private sealed class TestEditorView : IAssetEditorView;
    }

    private sealed class FakeMainViewModel(IViewModelServiceProvider serviceProvider) : IMainViewModel
    {
        public StatusViewModel Status { get; } = new(serviceProvider);

        public string EditorToolkitName => EditorToolkits.Avalonia;

        public IEnumerable<string> TextAssetTypes => [];

        public void AddRecentFile(UFile filePath)
        {
        }
    }
}

/// <summary>
/// A service the tests don't use: calls that return nothing (such as showing the progress of the session's loading) do
/// nothing, any other call fails.
/// </summary>
public class NotSupportedProxy<T> : DispatchProxy where T : class
{
    public static T Create() => Create<T, NotSupportedProxy<T>>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => targetMethod?.ReturnType == typeof(void) ? null : throw new NotSupportedException($"{typeof(T).Name}.{targetMethod?.Name} is not supported in these tests.");
}

/// <summary>
/// The dispatcher of the session: one exclusive context at a time, standing for the UI thread.
/// </summary>
public sealed class ExclusiveDispatcherService : IDispatcherService
{
    public bool CheckAccess() => ExclusiveContext.IsEntered;

    public void EnsureAccess(bool inDispatcherThread = true)
    {
        if (CheckAccess() != inDispatcherThread)
            throw new InvalidOperationException(inDispatcherThread ? "Not in the exclusive dispatcher context." : "In the exclusive dispatcher context.");
    }

    public void Invoke(Action callback) => ExclusiveContext.Run(() => { callback(); return true; });

    public TResult Invoke<TResult>(Func<TResult> callback) => ExclusiveContext.Run(callback);

    public Task InvokeAsync(Action callback, CancellationToken token = default)
    {
        Invoke(callback);
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TResult>(Func<TResult> callback, CancellationToken token = default) => Task.FromResult(ExclusiveContext.Run(callback));

    public Task InvokeTask(Func<Task> task, CancellationToken token = default) => ExclusiveContext.Run(task);

    public Task<TResult> InvokeTask<TResult>(Func<Task<TResult>> task, CancellationToken token = default) => ExclusiveContext.Run(task);

    public Task LowPriorityInvokeAsync(Action callback, CancellationToken token = default) => InvokeAsync(callback, token);
}

/// <summary>
/// A reentrant lock as a synchronization context: what runs in it runs alone, and the continuations of what it awaits come back
/// into it.
/// </summary>
internal sealed class ExclusiveContext : SynchronizationContext
{
    public static readonly ExclusiveContext Instance = new();

    private static readonly object Gate = new();

    public static bool IsEntered => Monitor.IsEntered(Gate);

    public static TResult Run<TResult>(Func<TResult> callback)
    {
        lock (Gate)
        {
            var context = Current;
            SetSynchronizationContext(Instance);
            try
            {
                return callback();
            }
            finally
            {
                SetSynchronizationContext(context);
            }
        }
    }

    public override void Post(SendOrPostCallback d, object? state) => ThreadPool.QueueUserWorkItem(_ => Run(() => { d(state); return true; }));

    public override void Send(SendOrPostCallback d, object? state) => Run(() => { d(state); return true; });

    public override SynchronizationContext CreateCopy() => this;
}
