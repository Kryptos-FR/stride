using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Localisation;
using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Skein.Assets;

/// <summary>
/// Compiles a Yarn project asset: one <see cref="YarnProject"/> at the URL of the asset, and the lines of each language
/// at <see cref="YarnProject.StringTableUrl"/>. The text of every script the project includes, and of the strings file
/// of each language, is part of what it compiles: a change to one of them compiles the project again.
/// </summary>
[AssetCompiler(typeof(YarnProjectAsset), typeof(AssetCompilationContext))]
public sealed class YarnProjectAssetCompiler : AssetCompilerBase
{
    /// <inheritdoc />
    protected override void Prepare(AssetCompilerContext context, AssetItem assetItem, string targetUrlInStorage, AssetCompilerResult result)
    {
        var project = (YarnProjectAsset)assetItem.Asset;
        var scripts = YarnProjectFiles.ScriptsOf(assetItem);

        // Said once for a package, by the first of its projects
        if (assetItem.Package is { } package && package.Assets.Where(a => a.Asset is YarnProjectAsset).MinBy(a => a.Location.FullPath, StringComparer.Ordinal) == assetItem)
        {
            foreach (var script in YarnProjectFiles.ScriptsInNoProject(package))
            {
                result.Warning($"{YarnProjectFiles.FileNameOf(script)} is in no Yarn project: the game does not have its nodes. Add it to the source files of a Yarn project.");
            }
        }

        var files = scripts.Select(s => (YarnProjectFiles.FileNameOf(s), ((YarnScriptAsset)s.Asset).Text ?? "")).ToArray();

        // The log of a build names a script by its file on the disk, which an IDE opens at the line
        var paths = scripts.ToDictionary(YarnProjectFiles.FileNameOf, s => s.FullPath.ToOSPath());

        // A strings file that is not there fails the command, not this method: Stride turns an error said here
        // into a failed step only for some of the ways an asset is reached
        var strings = new List<(string Language, string Path, string? Csv)>();
        foreach (var localisation in project.Localisations)
        {
            if (localisation.Strings is not { } file || localisation.Language.Length == 0)
                continue;

            var path = GetAbsolutePath(assetItem, file).ToOSPath();
            strings.Add((localisation.Language, path, File.Exists(path) ? File.ReadAllText(path) : null));
        }

        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new YarnProjectCommand(targetUrlInStorage, project, assetItem.Location.FullPath, files, paths, [.. strings], [], assetItem.Package!));
    }

    // The build engine serializes a command to run it in another process, which this one never asks for
#pragma warning disable STRDIAG010
    private sealed class YarnProjectCommand(
        string url,
        YarnProjectAsset parameters,
        string projectName,
        (string FileName, string Text)[] scripts,
        Dictionary<string, string> paths,
        (string Language, string Path, string? Csv)[] strings,
        YarnFunctionDeclaration[] functions,
        IAssetFinder assetFinder)
        : AssetCommand<YarnProjectAsset>(url, parameters, assetFinder)
#pragma warning restore STRDIAG010
    {
        // To raise when the content of a project changes for the same asset and scripts
        private const int ContentVersion = 2;

        protected override void ComputeParameterHash(BinarySerializationWriter writer)
        {
            base.ComputeParameterHash(writer);
            writer.Write(ContentVersion);
            writer.Write(scripts.Length);
            foreach (var (fileName, text) in scripts)
            {
                writer.Write(fileName);
                writer.Write(text);
            }

            writer.Write(strings.Length);
            foreach (var (language, _, csv) in strings)
            {
                writer.Write(language);
                writer.Write(csv is not null);
                writer.Write(csv ?? "");
            }

            writer.Write(functions.Length);
            foreach (var function in functions)
            {
                writer.Write(function.Name);
                writer.Write(function.ReturnType);
                writer.Write(string.Join(",", function.ParameterTypes));
                writer.Write(function.VariadicType ?? "");
            }
        }

        protected override Task<ResultStatus> DoCommandOverride(ICommandContext commandContext)
        {
            var translations = new Dictionary<string, IReadOnlyList<YarnStringRow>>();
            foreach (var (language, path, csv) in strings)
            {
                if (csv is null)
                {
                    commandContext.Logger.Error($"{projectName}: the strings file of '{language}' is not there: {path}");
                    return Task.FromResult(ResultStatus.Failed);
                }

                try
                {
                    translations[language] = YarnStringsCsv.Read(csv);
                }
                catch (FormatException exception)
                {
                    commandContext.Logger.Error($"{path}: {exception.Message}");
                    return Task.FromResult(ResultStatus.Failed);
                }
            }

            var build = YarnProjectBuilder.Build(Parameters, projectName, scripts, functions, translations);
            foreach (var diagnostic in build.Compilation.Diagnostics)
            {
                var message = (paths.TryGetValue(diagnostic.File, out var path) ? diagnostic with { File = path } : diagnostic).ToString();
                switch (diagnostic.Severity)
                {
                    case YarnSeverity.Error:
                        commandContext.Logger.Error(message);
                        break;
                    case YarnSeverity.Warning:
                        commandContext.Logger.Warning(message);
                        break;
                    case YarnSeverity.Info:
                        commandContext.Logger.Info(message);
                        break;
                }
            }

            foreach (var warning in build.Warnings)
            {
                commandContext.Logger.Warning(warning);
            }

            if (build.Project is null || build.BaseStrings is null)
                return Task.FromResult(ResultStatus.Failed);

            var content = new ContentManager(MicrothreadLocalDatabases.ProviderService);
            content.Save(Url, build.Project);
            foreach (var table in build.Translations.Prepend(build.BaseStrings))
            {
                content.Save(YarnProject.StringTableUrl(Url, table.Language), table);
            }

            return Task.FromResult(ResultStatus.Successful);
        }
    }
}
