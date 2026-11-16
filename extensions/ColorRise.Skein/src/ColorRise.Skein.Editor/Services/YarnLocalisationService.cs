using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Interop;
using ColorRise.Skein.Assets.Localisation;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Presentation.Services;
using Stride.Core.Reflection;

namespace ColorRise.Skein.Editor.Services;

/// <summary>
/// What "Add line ids" did to the scripts of a project.
/// </summary>
/// <param name="Added">The ids that were added, over all the scripts.</param>
/// <param name="Problems">Why scripts were left as they are.</param>
public sealed record YarnLineIdsResult(int Added, IReadOnlyList<string> Problems);

/// <summary>
/// What "Export strings" or "Update strings files" did to the strings file of a language.
/// </summary>
public sealed record YarnStringsFileResult
{
    /// <summary>
    /// The language of the file.
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// The file, or null when the language has none.
    /// </summary>
    public UFile? File { get; init; }

    /// <summary>
    /// Whether the file was written.
    /// </summary>
    public bool Written { get; init; }

    /// <summary>
    /// The rows the file has now.
    /// </summary>
    public int Rows { get; init; }

    /// <summary>
    /// What an update changed; null for an export, and when nothing was done.
    /// </summary>
    public YarnStringsUpdateResult? Update { get; init; }

    /// <summary>
    /// Why nothing was done, or null.
    /// </summary>
    public string? Problem { get; init; }
}

/// <summary>
/// A language of a project with the state of its strings file.
/// </summary>
/// <param name="Language">The language code.</param>
/// <param name="IsBase">Whether it is the language the scripts are written in.</param>
/// <param name="File">The strings file of the language, or null when it has none.</param>
/// <param name="Exists">Whether the strings file is there.</param>
/// <param name="Status">How far the file is from the scripts; for the base language, every line counts as translated.</param>
/// <param name="Problem">Why the file could not be read, or null.</param>
public sealed record YarnLanguageStatus(string Language, bool IsBase, UFile? File, bool Exists, YarnStringsStatus Status, string? Problem);

/// <summary>
/// The commands of Game Studio on the lines of a Yarn project: line ids, the strings files of the languages, and the
/// files Yarn Spinner's own tools read.
/// </summary>
/// <remarks>
/// A service of the session: <see cref="Of"/>. Its members are for the UI thread.
/// </remarks>
public sealed class YarnLocalisationService
{
    /// <summary>
    /// The name of the undo step of "Add line ids".
    /// </summary>
    public const string AddLineIdsActionName = "Add line ids";

    /// <summary>
    /// The name of the undo step that sets the strings file of a language when its strings are exported.
    /// </summary>
    public const string ExportStringsActionName = "Export strings";

    private static readonly Logger Log = GlobalLogger.GetLogger("ColorRise.Skein");

    private readonly ISessionViewModel session;

    public YarnLocalisationService(ISessionViewModel session)
    {
        this.session = session;
    }

    /// <summary>
    /// The localisation service of a session.
    /// </summary>
    public static YarnLocalisationService Of(ISessionViewModel session) => session.ServiceProvider.Get<YarnLocalisationService>();

    /// <summary>
    /// Where "Export strings" writes the strings of a language that has no file yet: <c>Strings/{project}.{language}.csv</c>
    /// in the folder of the project.
    /// </summary>
    public static UFile DefaultStringsFile(YarnProjectViewModel project, string language)
    {
        ArgumentNullException.ThrowIfNull(project);
        return UPath.Combine(project.AssetItem.FullPath.GetFullDirectory(), new UFile($"Strings/{project.Name}.{language}.csv"));
    }

    /// <summary>
    /// Adds a <c>#line:</c> id to each line of the scripts of a project that has none, as one step of the undo history.
    /// </summary>
    public Task<YarnLineIdsResult> AddLineIds(YarnProjectViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return session.Dispatcher.InvokeTask(() => AddLineIdsAsync(project));
    }

    /// <summary>
    /// Writes the strings file of a language of a project: the lines that have an id, in the base language, for a
    /// translator to translate in place. A language without a file gets one (<see cref="DefaultStringsFile"/>), set in
    /// the project's localisations. The file of another language than the base one is never written over.
    /// </summary>
    public Task<YarnStringsFileResult> ExportStrings(YarnProjectViewModel project, string language)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(language);
        return session.Dispatcher.InvokeTask(() => ExportStringsAsync(project, language));
    }

    /// <summary>
    /// Brings the strings file of each language of a project up to date with the scripts: new lines are added without a
    /// translation, the rows of lines that are gone are removed, and the translations of lines that changed are marked.
    /// </summary>
    public Task<IReadOnlyList<YarnStringsFileResult>> UpdateStrings(YarnProjectViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return session.Dispatcher.InvokeTask(() => UpdateStringsAsync(project));
    }

    /// <summary>
    /// The languages of a project, the base one first, with what their strings files hold of the lines of a compilation.
    /// </summary>
    public Task<IReadOnlyList<YarnLanguageStatus>> LanguagesOf(YarnProjectViewModel project, YarnCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(compilation);
        return session.Dispatcher.InvokeTask(() => LanguagesOfAsync(project, compilation));
    }

    /// <summary>
    /// Writes the <c>.yarnproject</c> file of a project and the definitions file of the game's commands and functions
    /// beside the asset, when the project asks for them and they would change.
    /// </summary>
    /// <returns>The files that were written.</returns>
    public IReadOnlyList<string> WriteProjectFiles(YarnProjectViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var asset = project.Asset;
        if (!asset.WriteYarnProjectFile)
            return [];

        var written = new List<string>();
        try
        {
            var directory = project.AssetItem.FullPath.GetFullDirectory().ToOSPath();
            var definitionsName = project.Name + YarnDefinitionsWriter.FileExtension;
            var definitionsPath = Path.Combine(directory, definitionsName);

            // Before the game is built Game Studio knows none of its commands: the file of the last build stays
            var actions = session.ServiceProvider.TryGet<YarnAnalysisService>()?.AnalysisOf(project)?.Actions
                ?? GameActionScanner.Scan(AssemblyRegistry.FindAll());
            if (actions.Actions.Count > 0 && WriteIfChanged(definitionsPath, YarnDefinitionsWriter.Write(actions.Actions)))
            {
                written.Add(definitionsPath);
            }

            var projectPath = Path.Combine(directory, project.Name + YarnProjectFileWriter.FileExtension);
            var existing = File.Exists(projectPath) ? File.ReadAllText(projectPath) : null;
            var text = YarnProjectFileWriter.Merge(existing, asset, directory, File.Exists(definitionsPath) ? definitionsName : "");
            if (text != existing)
            {
                File.WriteAllText(projectPath, text);
                written.Add(projectPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            Log.Warning($"{project.Url}: the Yarn project file was not written. {exception.Message}");
        }

        return written;
    }

    private async Task<YarnLineIdsResult> AddLineIdsAsync(YarnProjectViewModel project)
    {
        var scripts = ScriptsOf(project);
        var texts = scripts.Select(s => s.Script.TextBuffer.Text).ToList();
        var style = project.Asset.LineIdStyle;
        var tagged = await Task.Run(() =>
        {
            // The new ids of a script are kept away from those of the scripts before it too
            var ids = YarnLineTagger.ExistingIds(texts);
            return scripts.Select((s, i) => YarnLineTagger.AddLineIds(s.FileName, texts[i], ids, style)).ToList();
        });

        var problems = new List<string>();
        var added = 0;
        var undoRedo = session.ServiceProvider.Get<IUndoRedoService>();
        using (var transaction = undoRedo.CreateTransaction())
        {
            for (var i = 0; i < scripts.Count; i++)
            {
                problems.AddRange(tagged[i].Problems);
                if (tagged[i].AddedIds.Count == 0)
                    continue;

                var buffer = scripts[i].Script.TextBuffer;
                if (buffer.Text != texts[i])
                {
                    problems.Add($"{scripts[i].FileName}: the script changed while its line ids were being added. Add line ids again.");
                    continue;
                }

                buffer.Apply(ChangesOf(texts[i], tagged[i].Text), AddLineIdsActionName);
                added += tagged[i].AddedIds.Count;
            }

            undoRedo.SetName(transaction, AddLineIdsActionName);
        }

        return new YarnLineIdsResult(added, problems);
    }

    private async Task<YarnStringsFileResult> ExportStringsAsync(YarnProjectViewModel project, string language)
    {
        var asset = project.Asset;
        var isBase = language == asset.BaseLanguage;
        var index = asset.Localisations.FindIndex(l => l.Language == language);
        if (language.Length == 0 || (!isBase && index < 0))
            return new YarnStringsFileResult { Language = language, Problem = $"The project has no language '{language}': add it to its localisations first." };

        var file = index >= 0 && asset.Localisations[index].Strings is { } set ? Absolute(project, set) : DefaultStringsFile(project, language);
        var path = file.ToOSPath();
        if (!isBase && File.Exists(path))
            return new YarnStringsFileResult { Language = language, File = file, Problem = $"The strings file of '{language}' is there already: {path}. \"Update strings files\" brings it up to date and keeps its translations." };

        var (compilation, problem) = await CompileStringsAsync(project);
        if (compilation is null)
            return new YarnStringsFileResult { Language = language, File = file, Problem = problem };

        var rows = YarnStringsCsv.RowsOf(compilation, language);
        if (await WriteAsync(path, YarnStringsCsv.Write(rows)) is { } failure)
            return new YarnStringsFileResult { Language = language, File = file, Problem = failure };

        // The asset may have changed while the file was written
        index = project.Asset.Localisations.FindIndex(l => l.Language == language);
        if (index >= 0 && project.Asset.Localisations[index].Strings is null)
        {
            var undoRedo = session.ServiceProvider.Get<IUndoRedoService>();
            using var transaction = undoRedo.CreateTransaction();
            project.SetStringsFile(index, file);
            undoRedo.SetName(transaction, ExportStringsActionName);
        }

        return new YarnStringsFileResult { Language = language, File = file, Written = true, Rows = rows.Count };
    }

    private async Task<IReadOnlyList<YarnStringsFileResult>> UpdateStringsAsync(YarnProjectViewModel project)
    {
        var asset = project.Asset;
        var baseLanguage = asset.BaseLanguage;
        var languages = asset.Localisations
            .Where(l => l.Language.Length > 0 && l.Language != baseLanguage)
            .Select(l => (l.Language, File: l.Strings is { } file ? Absolute(project, file) : null))
            .ToList();
        if (languages.Count == 0)
            return [];

        var (compilation, problem) = await CompileStringsAsync(project);
        if (compilation is null)
            return [.. languages.Select(l => new YarnStringsFileResult { Language = l.Language, File = l.File, Problem = problem })];

        var baseRows = YarnStringsCsv.RowsOf(compilation, baseLanguage);
        return await Task.Run(() => (IReadOnlyList<YarnStringsFileResult>)[.. languages.Select(l => Update(l.Language, l.File, baseRows))]);
    }

    private static YarnStringsFileResult Update(string language, UFile? file, IReadOnlyList<YarnStringRow> baseRows)
    {
        if (file is null)
            return new YarnStringsFileResult { Language = language, Problem = $"'{language}' has no strings file: export its strings first." };

        var path = file.ToOSPath();
        try
        {
            if (!File.Exists(path))
                return new YarnStringsFileResult { Language = language, File = file, Problem = $"The strings file of '{language}' is not there: {path}" };

            var update = YarnStringsUpdate.Update(YarnStringsCsv.Read(File.ReadAllText(path)), baseRows, language);
            if (update.Changed)
            {
                File.WriteAllText(path, YarnStringsCsv.Write(update.Rows));
            }

            return new YarnStringsFileResult { Language = language, File = file, Written = update.Changed, Rows = update.Rows.Count, Update = update };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return new YarnStringsFileResult { Language = language, File = file, Problem = $"{path}: {exception.Message}" };
        }
    }

    private async Task<IReadOnlyList<YarnLanguageStatus>> LanguagesOfAsync(YarnProjectViewModel project, YarnCompilation compilation)
    {
        var asset = project.Asset;
        var baseLanguage = asset.BaseLanguage;
        var others = asset.Localisations
            .Where(l => l.Language.Length > 0 && l.Language != baseLanguage)
            .DistinctBy(l => l.Language)
            .Select(l => (l.Language, File: l.Strings is { } file ? Absolute(project, file) : null))
            .ToList();

        return await Task.Run(() =>
        {
            var baseRows = YarnStringsCsv.RowsOf(compilation, baseLanguage);
            var languages = new List<YarnLanguageStatus> { new(baseLanguage, true, null, false, new YarnStringsStatus(baseRows.Count, baseRows.Count, 0, 0), null) };
            foreach (var (language, file) in others)
            {
                var none = new YarnStringsStatus(baseRows.Count, 0, 0, 0);
                if (file is null)
                {
                    languages.Add(new YarnLanguageStatus(language, false, null, false, none, null));
                    continue;
                }

                var path = file.ToOSPath();
                try
                {
                    languages.Add(File.Exists(path)
                        ? new YarnLanguageStatus(language, false, file, true, YarnStringsStatus.Of(baseRows, YarnStringsCsv.Read(File.ReadAllText(path))), null)
                        : new YarnLanguageStatus(language, false, file, false, none, $"The file is not there: {path}"));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
                {
                    languages.Add(new YarnLanguageStatus(language, false, file, true, none, exception.Message));
                }
            }

            return (IReadOnlyList<YarnLanguageStatus>)languages;
        });
    }

    /// <summary>
    /// The lines of the scripts of a project as they are now. The severities of the project do not count, and neither do
    /// the types: only a script that cannot be read stops, because the lines of a script that is not read would look
    /// removed, and their translations would go.
    /// </summary>
    private async Task<(YarnCompilation? Compilation, string? Problem)> CompileStringsAsync(YarnProjectViewModel project)
    {
        var files = ScriptsOf(project).Select(s => (s.FileName, s.Script.TextBuffer.Text)).ToList();
        var compilation = await Task.Run(() => YarnCompilerBridge.Compile(new YarnCompilationRequest { Files = files, StringsOnly = true }));
        return compilation.Diagnostics.FirstOrDefault(d => d.Severity == YarnSeverity.Error) is { } error
            ? (null, $"The scripts have errors: fix them first, so that no translation is lost. {error.File}({error.Line}): {error.Message}")
            : (compilation, null);
    }

    private IReadOnlyList<(string FileName, YarnScriptViewModel Script)> ScriptsOf(YarnProjectViewModel project)
        => session.ServiceProvider.Get<YarnAnalysisService>().ScriptsOf(project);

    // A strings file is set from the folder of the project in the asset's file, and as a full path once the session has it
    private static UFile Absolute(YarnProjectViewModel project, UFile file)
        => file.IsAbsolute ? file : UPath.Combine(project.AssetItem.FullPath.GetFullDirectory(), file);

    private static async Task<string?> WriteAsync(string path, string text)
    {
        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            });
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"{path}: {exception.Message}";
        }
    }

    private static bool WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text)
            return false;

        File.WriteAllText(path, text);
        return true;
    }

    /// <summary>
    /// The edits that make a text another one, line by line: the tagger only adds to the end of lines, and an edit of one
    /// line keeps the place of what the text pane shows around it.
    /// </summary>
    private static IReadOnlyList<YarnTextEdit> ChangesOf(string before, string after)
    {
        var edits = new List<YarnTextEdit>();
        var (b, a) = (0, 0);
        while (b <= before.Length && a <= after.Length)
        {
            var bEnd = before.IndexOf('\n', b) is var bn and >= 0 ? bn : before.Length;
            var aEnd = after.IndexOf('\n', a) is var an and >= 0 ? an : after.Length;
            var was = before.AsSpan(b, bEnd - b);
            var now = after.AsSpan(a, aEnd - a);
            if (!was.SequenceEqual(now))
            {
                var head = was.CommonPrefixLength(now);
                var tail = 0;
                while (tail < was.Length - head && tail < now.Length - head && was[^(tail + 1)] == now[^(tail + 1)])
                {
                    tail++;
                }

                edits.Add(new YarnTextEdit(b + head, was.Length - head - tail, now[head..^tail].ToString()));
            }

            // Not the same lines: the whole text is one change
            if ((bEnd == before.Length) != (aEnd == after.Length))
                return [new YarnTextEdit(0, before.Length, after)];

            (b, a) = (bEnd + 1, aEnd + 1);
        }

        return edits;
    }
}
