using System.Globalization;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Services;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.Commands;

namespace ColorRise.Skein.Editor.ViewModels;

/// <summary>
/// A script of a project in the project's overview.
/// </summary>
/// <param name="Script">The script.</param>
/// <param name="Name">The file name the compiler knows the script by.</param>
/// <param name="NodeCount">The nodes the script has, notes included.</param>
/// <param name="ErrorCount">The errors the compiler found in the script.</param>
public sealed record YarnProjectScriptItem(YarnScriptViewModel Script, string Name, int NodeCount, int ErrorCount);

/// <summary>
/// A variable of a project in the project's overview.
/// </summary>
/// <param name="Name">The name, with its <c>$</c>.</param>
/// <param name="Type">The type, as Yarn names it.</param>
/// <param name="Default">The value the variable starts with, as a script writes it; empty for a smart variable.</param>
/// <param name="Description">What the <c>///</c> comment of the declaration says.</param>
/// <param name="File">The script that declares the variable, or first uses it.</param>
/// <param name="Line">The line of the declaration, from 1; 0 when it is not known.</param>
/// <param name="IsSmart">Whether the variable is an expression the story computes.</param>
/// <param name="IsDeclared">Whether a script declares the variable; the compiler guesses the type of one that is only used.</param>
public sealed record YarnProjectVariableItem(string Name, string Type, string Default, string Description, string File, int Line, bool IsSmart, bool IsDeclared);

/// <summary>
/// A language of a project in the project's overview.
/// </summary>
/// <param name="Language">The language code.</param>
/// <param name="IsBase">Whether it is the language the scripts are written in.</param>
/// <param name="File">The strings file, as a path from the folder of the project; empty when the language has none.</param>
/// <param name="Lines">The lines of the scripts that have an id.</param>
/// <param name="Translated">The lines with a translation made from the text the scripts have now.</param>
/// <param name="ToUpdate">The lines whose translation was made from a text that changed since.</param>
/// <param name="Missing">The lines without a translation.</param>
/// <param name="Problem">Why the strings file could not be read, or null.</param>
/// <param name="CanExport">Whether "Export strings" writes a file for the language: it never writes over translations.</param>
public sealed record YarnProjectLanguageItem(string Language, bool IsBase, string File, int Lines, int Translated, int ToUpdate, int Missing, string? Problem, bool CanExport);

/// <summary>
/// The overview of a Yarn project: its scripts, its variables, what the compiler says of it, and its languages with the
/// commands on line ids and strings files.
/// </summary>
[AssetEditorViewModel<YarnProjectViewModel>]
public sealed class YarnProjectEditorViewModel : AssetEditorViewModel<YarnProjectViewModel>
{
    private readonly YarnAnalysisService? analysisService;
    private readonly YarnLocalisationService? localisation;
    private YarnProjectAnalysis? analysis;
    private IReadOnlyList<YarnProjectScriptItem> scripts = [];
    private IReadOnlyList<YarnProjectVariableItem> variables = [];
    private IReadOnlyList<YarnDiagnostic> diagnostics = [];
    private IReadOnlyList<YarnProjectLanguageItem> languages = [];
    private IReadOnlyList<string> warnings = [];
    private int nodeCount;
    private int lineCount;
    private int linesWithoutId;
    private string? message;
    private bool isBusy;
    private bool destroyed;
    private Task refreshing = Task.CompletedTask;

    public YarnProjectEditorViewModel(YarnProjectViewModel project)
        : base(project)
    {
        analysisService = ServiceProvider.TryGet<YarnAnalysisService>();
        localisation = ServiceProvider.TryGet<YarnLocalisationService>();

        AddLineIdsCommand = new AnonymousTaskCommand(ServiceProvider, AddLineIds);
        ExportStringsCommand = new AnonymousTaskCommand<string>(ServiceProvider, ExportStrings);
        UpdateStringsCommand = new AnonymousTaskCommand(ServiceProvider, UpdateStrings);
        RefreshCommand = new AnonymousCommand(ServiceProvider, RefreshLanguages);
        GoToCommand = new AnonymousTaskCommand<object>(ServiceProvider, GoTo);
    }

    /// <summary>
    /// The scripts the project includes.
    /// </summary>
    public IReadOnlyList<YarnProjectScriptItem> Scripts { get => scripts; private set => SetValue(ref scripts, value); }

    /// <summary>
    /// The variables of the project, by name: the declared ones, and the ones the scripts only use.
    /// </summary>
    public IReadOnlyList<YarnProjectVariableItem> Variables { get => variables; private set => SetValue(ref variables, value); }

    /// <summary>
    /// The errors, warnings and remarks of the compiler for the whole project.
    /// </summary>
    public IReadOnlyList<YarnDiagnostic> Diagnostics { get => diagnostics; private set => SetValue(ref diagnostics, value); }

    /// <summary>
    /// What the build of the game would warn of besides the compiler's messages: lines without an id, a language without
    /// a strings file.
    /// </summary>
    public IReadOnlyList<string> Warnings { get => warnings; private set => SetValue(ref warnings, value); }

    /// <summary>
    /// The languages of the project, the base one first.
    /// </summary>
    public IReadOnlyList<YarnProjectLanguageItem> Languages { get => languages; private set => SetValue(ref languages, value); }

    /// <summary>
    /// The nodes of the project.
    /// </summary>
    public int NodeCount { get => nodeCount; private set => SetValue(ref nodeCount, value); }

    /// <summary>
    /// The lines and options of the project.
    /// </summary>
    public int LineCount { get => lineCount; private set => SetValue(ref lineCount, value); }

    /// <summary>
    /// The lines and options without a <c>#line:</c> id: they cannot be translated.
    /// </summary>
    public int LinesWithoutId { get => linesWithoutId; private set => SetValue(ref linesWithoutId, value); }

    /// <summary>
    /// What the last command did, or why it did nothing.
    /// </summary>
    public string? Message { get => message; private set => SetValue(ref message, value); }

    /// <summary>
    /// Whether a command is running.
    /// </summary>
    public bool IsBusy { get => isBusy; private set => SetValue(ref isBusy, value); }

    /// <summary>
    /// Adds a line id to each line that has none.
    /// </summary>
    public ICommandBase AddLineIdsCommand { get; }

    /// <summary>
    /// Writes the strings file of a language; the parameter is the language code.
    /// </summary>
    public ICommandBase ExportStringsCommand { get; }

    /// <summary>
    /// Brings the strings files of the languages up to date with the scripts.
    /// </summary>
    public ICommandBase UpdateStringsCommand { get; }

    /// <summary>
    /// Reads the strings files again: a translator changes them outside Game Studio.
    /// </summary>
    public ICommandBase RefreshCommand { get; }

    /// <summary>
    /// Shows a script, the declaration of a variable or the place of a message; the parameter is the item.
    /// </summary>
    public ICommandBase GoToCommand { get; }

    /// <inheritdoc/>
    public override Task<bool> Initialize()
    {
        if (analysisService is not null)
        {
            analysisService.Changed += OnAnalysisChanged;
            analysis = analysisService.AnalysisOf(Asset);
        }

        Refresh();
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        destroyed = true;
        if (analysisService is not null)
        {
            analysisService.Changed -= OnAnalysisChanged;
        }

        base.Destroy();
    }

    /// <summary>
    /// Completes when the overview shows the scripts as they are now, with the strings files as they are read.
    /// </summary>
    public async Task WhenRefreshed()
    {
        if (analysisService is not null)
        {
            await analysisService.WhenIdle();
        }

        await refreshing;
    }

    /// <summary>
    /// Adds a line id to each line of the project's scripts that has none, in one step of the undo history.
    /// </summary>
    public Task AddLineIds() => Run(async service =>
    {
        var result = await service.AddLineIds(Asset);
        var done = result.Added == 0 ? "Every line that can have an id has one." : $"Added {Count(result.Added, "line id")}.";
        return string.Join("\n", result.Problems.Prepend(done));
    });

    /// <summary>
    /// Writes the strings file of a language.
    /// </summary>
    public Task ExportStrings(string language) => Run(async service =>
    {
        var result = await service.ExportStrings(Asset, language);
        return result.Problem ?? $"Exported {Count(result.Rows, "line")} to {result.File?.ToOSPath()}.";
    });

    /// <summary>
    /// Brings the strings files of the languages up to date with the scripts.
    /// </summary>
    public Task UpdateStrings() => Run(async service =>
    {
        var results = await service.UpdateStrings(Asset);
        return results.Count == 0
            ? "The project has no other language: add one to its localisations."
            : string.Join("\n", results.Select(r => r.Problem ?? (r.Update is { Changed: true } update
                ? $"{r.Language}: {update.Added} added, {update.Removed} removed, {update.NeedsUpdate} to update."
                : $"{r.Language}: up to date.")));
    });

    /// <summary>
    /// Shows a script of the overview, the declaration of a variable or the place of a message, in the script's editor.
    /// </summary>
    public async Task GoTo(object item)
    {
        var (script, line, column) = item switch
        {
            YarnProjectScriptItem shown => (shown.Script, 0, 0),
            YarnProjectVariableItem variable => (analysis?.ScriptOf(variable.File), variable.Line, 1),
            YarnDiagnostic diagnostic => (analysis?.ScriptOf(diagnostic.File), diagnostic.Line, diagnostic.Column),
            _ => (null, 0, 0),
        };
        if (script is not null && await YarnScriptEditorViewModel.OpenEditor(script) is { } editor && line > 0)
        {
            editor.RequestPlace(new YarnTextPlace(line, Math.Max(1, column)));
        }
    }

    private async Task Run(Func<YarnLocalisationService, Task<string>> command)
    {
        if (localisation is null || IsBusy)
            return;

        IsBusy = true;
        try
        {
            Message = await command(localisation);
        }
        finally
        {
            IsBusy = false;
        }

        // The strings files changed without the scripts
        RefreshLanguages();
    }

    private void OnAnalysisChanged(YarnProjectAnalysis changed)
    {
        if (changed.Project == Asset)
        {
            analysis = changed;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (analysis is not { } current)
            return;

        var compilation = current.Compilation;
        Scripts = [.. current.Scripts.Select(s => new YarnProjectScriptItem(
            s.Script,
            s.FileName,
            current.Nodes.Count(n => n.File == s.FileName),
            compilation.Diagnostics.Count(d => d.File == s.FileName && d.Severity == YarnSeverity.Error)))];
        Variables = [.. compilation.Variables
            .Where(v => !v.IsInternal)
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(v => new YarnProjectVariableItem(v.Name, v.Type, DefaultOf(v), v.Description ?? "", v.File ?? "", v.Line, v.IsSmart, !v.IsImplicit))];
        Diagnostics = compilation.Diagnostics;
        Warnings = current.Build.Warnings;
        NodeCount = current.Nodes.Count;
        LineCount = compilation.Lines.Count;
        LinesWithoutId = compilation.Lines.Count(l => l.Value.IsImplicitId);
        RefreshLanguages();
    }

    private void RefreshLanguages()
    {
        if (analysis is { } current && localisation is not null && !destroyed)
        {
            refreshing = Session.TrackBackgroundWork(Session.Dispatcher.InvokeTask(() => ShowLanguagesAsync(current, localisation)));
        }
    }

    private async Task ShowLanguagesAsync(YarnProjectAnalysis current, YarnLocalisationService service)
    {
        var read = await service.LanguagesOf(Asset, current.Compilation);

        // Another analysis came while the files were read: its own reading shows
        if (destroyed || analysis != current)
            return;

        var folder = Asset.AssetItem.FullPath.GetFullDirectory();
        Languages = [.. read.Select(l => new YarnProjectLanguageItem(
            l.Language,
            l.IsBase,
            l.File?.MakeRelative(folder).ToString() ?? "",
            l.Status.Lines,
            l.Status.Translated,
            l.Status.ToUpdate,
            l.Status.Missing,
            l.Problem,
            l.IsBase || !l.Exists))];
    }

    // As a script writes the value
    private static string DefaultOf(YarnVariableData variable) => variable.Default switch
    {
        null => "",
        bool flag => flag ? "true" : "false",
        string text => $"\"{text}\"",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        var other => other.ToString() ?? "",
    };

    private static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";
}
