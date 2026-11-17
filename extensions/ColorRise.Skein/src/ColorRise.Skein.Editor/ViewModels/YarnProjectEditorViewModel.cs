using System.Globalization;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Services;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Services;
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
/// A command or a function of the game in the project's overview.
/// </summary>
/// <param name="Name">The name scripts write.</param>
/// <param name="Usage">How a script calls it: <c>&lt;&lt;walk who: string [running: bool = false]&gt;&gt;</c>, <c>has_item(item: string) → bool</c>.</param>
/// <param name="Description">What its attribute says it does.</param>
/// <param name="Method">The C# method, as <c>Type.Method</c>.</param>
/// <param name="IsCommand">Whether it is a command; a function otherwise.</param>
/// <param name="Waits">Whether the dialogue waits for the command to end.</param>
public sealed record YarnProjectActionItem(string Name, string Usage, string Description, string Method, bool IsCommand, bool Waits)
{
    /// <summary>
    /// The item of a command or a function the game's code declares.
    /// </summary>
    public static YarnProjectActionItem From(YarnActionDescription action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var parameters = action.Parameters.Select(ParameterText).ToList();
        if (action.Kind != YarnActionKind.Command)
            return new YarnProjectActionItem(action.Name, $"{action.Name}({string.Join(", ", parameters)}) → {action.ReturnType}", action.Documentation ?? "", action.Method, IsCommand: false, Waits: false);

        // The first word of a command on an object names the object
        if (action.TargetType is { Length: > 0 } target)
        {
            parameters.Insert(0, $"{char.ToLowerInvariant(target[0])}{target[1..]}: {target}");
        }

        return new YarnProjectActionItem(action.Name, $"<<{string.Join(' ', [action.Name, .. parameters])}>>", action.Documentation ?? "", action.Method, IsCommand: true, action.IsAsync);
    }

    // An optional parameter in brackets with its default, the cases of an enum, the C# type of an object
    private static string ParameterText(YarnActionParameter parameter)
    {
        var text = $"{parameter.Name}: {(parameter.Choices.Count > 0 ? string.Join(" | ", parameter.Choices) : parameter.TypeName ?? parameter.Type)}";
        if (parameter.IsParams)
            return text + "...";

        return parameter.IsOptional ? $"[{text}{(parameter.DefaultValue is { } value ? " = " + value : "")}]" : text;
    }
}

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
/// <param name="Voiced">The lines that have a voice-over clip in the language.</param>
/// <param name="Problem">Why the strings file could not be read, or null.</param>
/// <param name="CanExport">Whether "Export strings" writes a file for the language: it never writes over translations.</param>
public sealed record YarnProjectLanguageItem(string Language, bool IsBase, string File, int Lines, int Translated, int ToUpdate, int Missing, int Voiced, string? Problem, bool CanExport);

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
    private string summary = "";
    private string? lineIdNotice;
    private int nodeCount;
    private int lineCount;
    private int linesWithoutId;
    private string? message;
    private bool isBusy;
    private bool destroyed;
    private Task refreshing = Task.CompletedTask;

    /// <summary>
    /// Makes the overview of a Yarn project.
    /// </summary>
    public YarnProjectEditorViewModel(YarnProjectViewModel project)
        : base(project)
    {
        analysisService = ServiceProvider.TryGet<YarnAnalysisService>();
        localisation = ServiceProvider.TryGet<YarnLocalisationService>();

        AddLineIdsCommand = new AnonymousTaskCommand(ServiceProvider, AddLineIds);
        ExportStringsCommand = new AnonymousTaskCommand<string>(ServiceProvider, ExportStrings);
        UpdateStringsCommand = new AnonymousTaskCommand(ServiceProvider, UpdateStrings);
        ExportRecordingScriptsCommand = new AnonymousTaskCommand(ServiceProvider, ExportRecordingScripts);
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
    /// The commands and the functions of the game, commands first, each kind by name.
    /// </summary>
    public IReadOnlyList<YarnProjectActionItem> Actions { get; private set => SetValue(ref field, value); } = [];

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
    /// The project in a few numbers: its scripts, nodes, lines and variables.
    /// </summary>
    public string Summary { get => summary; private set => SetValue(ref summary, value); }

    /// <summary>
    /// How many lines have no id, in words; null when every line has one.
    /// </summary>
    public string? LineIdNotice { get => lineIdNotice; private set => SetValue(ref lineIdNotice, value); }

    /// <summary>
    /// The nodes of the project.
    /// </summary>
    public int NodeCount { get => nodeCount; private set => SetValue(ref nodeCount, value); }

    /// <summary>
    /// The lines and options of the project.
    /// </summary>
    public int LineCount { get => lineCount; private set => SetValue(ref lineCount, value); }

    /// <summary>
    /// The lines and options without a <c>#line:</c> id: they cannot be translated. A line that repeats another
    /// (<c>#shadow:</c>) is not one of them.
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
    /// Writes the recording script of each language, for the actors.
    /// </summary>
    public ICommandBase ExportRecordingScriptsCommand { get; }

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
    public Task ExportStrings(string language) => Run(async service => Exported(await service.ExportStrings(Asset, language)));

    /// <summary>
    /// Writes the strings file of each language of the project that has none.
    /// </summary>
    public async Task ExportMissingStrings()
    {
        await WhenRefreshed();
        var missing = Languages.Where(l => !l.IsBase && l.CanExport).Select(l => l.Language).ToList();
        await Run(async service =>
        {
            if (missing.Count == 0)
            {
                return Languages.Count > 1
                    ? "Every language has its strings file: \"Update strings files\" brings them up to date."
                    : "The project has no other language: add one to its localisations.";
            }

            var done = new List<string>();
            foreach (var language in missing)
            {
                done.Add(Exported(await service.ExportStrings(Asset, language)));
            }

            return string.Join("\n", done);
        });
    }

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
    /// Writes the recording script of each language of the project.
    /// </summary>
    public Task ExportRecordingScripts() => Run(async service =>
        {
            var folder = Asset.AssetItem.FullPath.GetFullDirectory();
            var results = await service.ExportRecordingScripts(Asset);
            return string.Join("\n", results.Select(r => r.Problem is { } problem
                ? $"{r.Language}: {problem}"
                : $"{r.Language}: {r.File!.MakeRelative(folder)}, {Count(r.Rows, "line")}, {r.Recorded} recorded"
                    + (r.Untranslated > 0 ? $"; {Count(r.Untranslated, "line")} without a translation left out." : ".")));
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

    /// <summary>
    /// Opens the overview of a project, or shows the one that is open.
    /// </summary>
    /// <returns>The overview, or null when the session cannot open one.</returns>
    public static async Task<YarnProjectEditorViewModel?> OpenEditor(YarnProjectViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.ServiceProvider.TryGet<IAssetEditorsManager>() is not { } editors)
            return null;

        await editors.OpenAssetEditorAsync(project);
        return editors.TryGetAssetEditor<YarnProjectEditorViewModel>(project, out var editor) ? editor : null;
    }

    private string Exported(YarnStringsFileResult result)
        => result.Problem ?? $"Exported {Count(result.Rows, "line")} to {result.File?.MakeRelative(Asset.AssetItem.FullPath.GetFullDirectory())}, from the folder of the project.";

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
        Actions = [.. current.Actions.Actions.Select(YarnProjectActionItem.From)];
        Diagnostics = compilation.Diagnostics;
        Warnings = current.Build.Warnings;
        NodeCount = current.Nodes.Count;
        LineCount = compilation.Lines.Count;
        LinesWithoutId = compilation.Lines.Count(l => l.Value.IsImplicitId && l.Value.ShadowOf is null);
        Summary = string.Join("  ·  ", Count(Scripts.Count, "script"), Count(NodeCount, "node"), Count(LineCount, "line"), Count(Variables.Count, "variable"));
        LineIdNotice = LinesWithoutId switch
        {
            0 => null,
            1 => "1 line has no id and cannot be translated.",
            var count => $"{count} lines have no id and cannot be translated.",
        };
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
            l.Voiced,
            l.Problem,
            l.IsBase || !l.Exists))];
    }

    // As a script writes the value
    private static string DefaultOf(YarnVariableData variable) => variable.Default switch
    {
        null => "",
        var value when variable.CaseOf(value) is { } named => $"{variable.Type}.{named.Name}",
        bool flag => flag ? "true" : "false",
        string text => $"\"{text}\"",
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        var other => other.ToString() ?? "",
    };

    private static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";
}
