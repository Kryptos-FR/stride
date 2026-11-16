using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Collections;
using Stride.Core.Reflection;

namespace ColorRise.Skein.Editor.Services;

/// <summary>
/// Compiles the Yarn projects of a session in the background, from the scripts as the session has them, a moment after
/// the last change: the editors show its errors and take their links from it.
/// </summary>
/// <remarks>
/// A service of the session: <see cref="Of"/>. Its members are for the UI thread, and <see cref="Changed"/> is raised there.
/// It analyses the projects it was asked about, one at a time.
/// </remarks>
public sealed class YarnAnalysisService
{
    private readonly ISessionViewModel session;
    private readonly Dictionary<AssetId, Unit> units = [];
    // Asked for from the threads that build the property grid
    private readonly Dictionary<AssetId, ObservableList<string>> nodeNames = [];
    private readonly CancellationTokenSource closing = new();
    private CancellationTokenSource delay = new();
    private Task pump = Task.CompletedTask;
    private bool dirty;
    private bool flushing;

    // What the game's code gives to scripts, read again after the game's assemblies change
    private GameActions actions = GameActions.None;
    private int actionsVersion;
    private int scannedVersion = -1;

    public YarnAnalysisService(ISessionViewModel session)
    {
        this.session = session;
        session.AssetPropertiesChanged += OnAssetPropertiesChanged;
        session.DeletedAssetsChanged += OnDeletedAssetsChanged;
        AssemblyRegistry.AssemblyRegistered += OnAssembliesChanged;
        AssemblyRegistry.AssemblyUnregistered += OnAssembliesChanged;
    }

    /// <summary>
    /// How long the service waits after a change before it compiles: changes in a row are compiled once.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Raised after a project, or a script alone, was compiled again.
    /// </summary>
    public event Action<YarnProjectAnalysis>? Changed;

    /// <summary>
    /// The analysis service of a session.
    /// </summary>
    public static YarnAnalysisService Of(ISessionViewModel session) => session.ServiceProvider.Get<YarnAnalysisService>();

    /// <summary>
    /// The projects that include a script, by URL. The editors use the first.
    /// </summary>
    public IReadOnlyList<YarnProjectViewModel> ProjectsOf(YarnScriptViewModel script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return [.. YarnProjectFiles.ProjectsOf(script.AssetItem).Select(p => session.GetAssetById(p.Id)).OfType<YarnProjectViewModel>()];
    }

    /// <summary>
    /// The scripts a project includes that the session has, each with the file name the compiler knows it by.
    /// </summary>
    public IReadOnlyList<(string FileName, YarnScriptViewModel Script)> ScriptsOf(YarnProjectViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var scripts = new List<(string, YarnScriptViewModel)>();
        foreach (var item in YarnProjectFiles.ScriptsOf(project.AssetItem))
        {
            if (session.GetAssetById(item.Id) is YarnScriptViewModel script)
            {
                scripts.Add((YarnProjectFiles.FileNameOf(item), script));
            }
        }

        return scripts;
    }

    /// <summary>
    /// The last analysis of a project, or of the project of a script (of the script alone when no project includes it).
    /// Null until the first one is there: <see cref="Changed"/> says when.
    /// </summary>
    public YarnProjectAnalysis? AnalysisOf(AssetViewModel scriptOrProject)
    {
        ArgumentNullException.ThrowIfNull(scriptOrProject);

        var owner = scriptOrProject is YarnScriptViewModel script ? (AssetViewModel?)ProjectsOf(script).FirstOrDefault() ?? script : scriptOrProject;
        if (owner is not (YarnProjectViewModel or YarnScriptViewModel))
            throw new ArgumentException("Not a Yarn script or a Yarn project.", nameof(scriptOrProject));

        if (!units.TryGetValue(owner.Id, out var unit))
        {
            units.Add(owner.Id, unit = new Unit(owner));
            Invalidate();
        }

        return unit.Latest;
    }

    /// <summary>
    /// The titles of the nodes of a project that a dialogue can start, sorted. The list is kept current: it changes, on
    /// the UI thread, after each analysis of the project. Safe from any thread.
    /// </summary>
    public ObservableList<string> NodeNamesOf(YarnProjectViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        ObservableList<string>? names;
        lock (nodeNames)
        {
            if (!nodeNames.TryGetValue(project.Id, out names))
            {
                nodeNames.Add(project.Id, names = []);
            }
        }

        if (session.Dispatcher.CheckAccess())
        {
            Fill();
        }
        else if (!closing.IsCancellationRequested)
        {
            session.Dispatcher.InvokeAsync(Fill);
        }

        return names;

        // Asking for the analysis makes the service compile the project when it did not yet
        void Fill()
        {
            if (!closing.IsCancellationRequested && !project.IsDeleted && AnalysisOf(project) is { } analysis)
            {
                ShowNodeNames(names, analysis);
            }
        }
    }

    /// <summary>
    /// Says that a script, a project or the place of one of them may have changed: the service compiles what did, after
    /// <see cref="Delay"/>. Safe from any thread.
    /// </summary>
    public void Invalidate()
    {
        if (closing.IsCancellationRequested)
            return;

        if (!session.Dispatcher.CheckAccess())
        {
            session.Dispatcher.InvokeAsync(Invalidate);
            return;
        }

        dirty = true;
        if (pump.IsCompleted)
        {
            pump = session.TrackBackgroundWork(session.Dispatcher.InvokeTask(PumpAsync));
        }
        else
        {
            // A change during the wait starts the wait again
            delay.Cancel();
        }
    }

    /// <summary>
    /// Compiles what is waiting for its delay now, and completes when nothing is left to compile.
    /// </summary>
    public async Task WhenIdle()
    {
        flushing = true;
        try
        {
            while (!pump.IsCompleted)
            {
                delay.Cancel();
                await pump;
            }
        }
        finally
        {
            flushing = false;
        }
    }

    /// <summary>
    /// Stops the service when its session closes: what is compiling is cancelled, and the session waits for it.
    /// </summary>
    public void Close()
    {
        if (closing.IsCancellationRequested)
            return;

        closing.Cancel();
        delay.Cancel();
        session.AssetPropertiesChanged -= OnAssetPropertiesChanged;
        session.DeletedAssetsChanged -= OnDeletedAssetsChanged;
        AssemblyRegistry.AssemblyRegistered -= OnAssembliesChanged;
        AssemblyRegistry.AssemblyUnregistered -= OnAssembliesChanged;
    }

    private async Task PumpAsync()
    {
        while (dirty && !closing.IsCancellationRequested)
        {
            dirty = false;
            if (!flushing)
            {
                delay = new CancellationTokenSource();
                try
                {
                    await Task.Delay(Delay, delay.Token);
                }
                catch (OperationCanceledException)
                {
                    // Another change, a flush or the close of the session
                }

                if (dirty && !flushing)
                    continue;
            }

            dirty = false;
            try
            {
                await AnalyseAsync();
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task AnalyseAsync()
    {
        foreach (var unit in units.Values.ToList())
        {
            if (Resolve(unit) is not { } input)
            {
                units.Remove(unit.Owner.Id);
                continue;
            }

            if (unit.Input is { } last && last.Matches(input))
                continue;

            unit.Input = input;
            var version = actionsVersion;
            var known = scannedVersion == version ? actions : null;
            var token = closing.Token;
            var (build, gameActions) = await Task.Run(
                () =>
                {
                    var found = known ?? GameActionScanner.Scan(AssemblyRegistry.FindAll());
                    return (YarnProjectBuilder.Build(input.Settings, input.Name, input.Files, found.Functions, translations: null, token), found);
                },
                token);

            if (closing.IsCancellationRequested)
                return;

            actions = gameActions;
            scannedVersion = version;
            if (!build.Compilation.HasErrors)
            {
                unit.GoodNodes = build.Compilation.Nodes;
            }

            unit.Latest = new YarnProjectAnalysis
            {
                Project = unit.Owner as YarnProjectViewModel,
                Scripts = input.Scripts,
                Texts = input.Files,
                Build = build,
                Nodes = unit.GoodNodes ?? build.Compilation.Nodes,
                Actions = gameActions,
            };
            ObservableList<string>? names;
            lock (nodeNames)
            {
                nodeNames.TryGetValue(unit.Owner.Id, out names);
            }

            if (names is not null)
            {
                ShowNodeNames(names, unit.Latest);
            }

            Changed?.Invoke(unit.Latest);
        }
    }

    private static void ShowNodeNames(ObservableList<string> names, YarnProjectAnalysis analysis)
    {
        var titles = analysis.StartNodes;
        if (!names.SequenceEqual(titles))
        {
            names.Clear();
            names.AddRange(titles);
        }
    }

    /// <summary>
    /// What to compile for a unit now; null when the unit is no more (deleted, or a lone script that a project now includes).
    /// </summary>
    private Input? Resolve(Unit unit)
    {
        if (unit.Owner.IsDeleted || session.GetAssetById(unit.Owner.Id) != unit.Owner)
            return null;

        if (unit.Owner is YarnScriptViewModel alone)
        {
            if (YarnProjectFiles.ProjectsOf(alone.AssetItem).Count > 0)
                return null;

            // Alone, with Yarn Spinner's own settings
            return new Input(new YarnProjectAsset(), alone.Url, [(YarnProjectFiles.FileNameOf(alone.AssetItem), alone)], actionsVersion);
        }

        var project = (YarnProjectViewModel)unit.Owner;
        var scripts = ScriptsOf(project);

        // A copy: the compilation reads it on another thread while the property grid may change the asset
        var asset = project.Asset;
        var settings = new YarnProjectAsset { BaseLanguage = asset.BaseLanguage, LineIdStyle = asset.LineIdStyle, Saliency = asset.Saliency };
        settings.SourceFiles.AddRange(asset.SourceFiles);
        settings.ExcludedFiles.AddRange(asset.ExcludedFiles);
        settings.Localisations.AddRange(asset.Localisations.Select(l => new YarnLocalisation { Language = l.Language, Strings = l.Strings }));
        foreach (var (code, severity) in asset.DiagnosticSeverities)
        {
            settings.DiagnosticSeverities[code] = severity;
        }

        return new Input(settings, project.Url, scripts, actionsVersion);
    }

    // Raised from the thread that changed the assets
    private void OnAssetPropertiesChanged(object? sender, AssetChangedEventArgs e)
    {
        if (e.Assets.Any(a => a is YarnProjectViewModel or YarnScriptViewModel))
        {
            Invalidate();
        }
    }

    private void OnDeletedAssetsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Invalidate();

    // Raised from the thread that loads or unloads the game's assemblies
    private void OnAssembliesChanged(object? sender, AssemblyRegisteredEventArgs e)
    {
        Interlocked.Increment(ref actionsVersion);
        Invalidate();
    }

    private sealed class Unit(AssetViewModel owner)
    {
        public AssetViewModel Owner { get; } = owner;

        public Input? Input { get; set; }

        public YarnProjectAnalysis? Latest { get; set; }

        public IReadOnlyList<YarnNodeData>? GoodNodes { get; set; }
    }

    private sealed class Input
    {
        public Input(YarnProjectAsset settings, string name, IReadOnlyList<(string FileName, YarnScriptViewModel Script)> scripts, int actionsVersion)
        {
            Settings = settings;
            Name = name;
            Scripts = scripts;
            ActionsVersion = actionsVersion;
            Files = [.. scripts.Select(s => (s.FileName, s.Script.Asset.Text ?? ""))];
        }

        public YarnProjectAsset Settings { get; }

        public string Name { get; }

        public IReadOnlyList<(string FileName, YarnScriptViewModel Script)> Scripts { get; }

        public IReadOnlyList<(string FileName, string Text)> Files { get; }

        public int ActionsVersion { get; }

        public bool Matches(Input other)
            => ActionsVersion == other.ActionsVersion
               && Name == other.Name
               && Files.SequenceEqual(other.Files)
               && Scripts.Select(s => s.Script).SequenceEqual(other.Scripts.Select(s => s.Script))
               && Settings.BaseLanguage == other.Settings.BaseLanguage
               && Settings.Saliency == other.Settings.Saliency
               && Settings.SourceFiles.SequenceEqual(other.Settings.SourceFiles)
               && Settings.ExcludedFiles.SequenceEqual(other.Settings.ExcludedFiles)
               && Settings.Localisations.Select(l => (l.Language, l.Strings)).SequenceEqual(other.Settings.Localisations.Select(l => (l.Language, l.Strings)))
               && Settings.DiagnosticSeverities.OrderBy(s => s.Key, StringComparer.Ordinal).SequenceEqual(other.Settings.DiagnosticSeverities.OrderBy(s => s.Key, StringComparer.Ordinal));
    }
}
