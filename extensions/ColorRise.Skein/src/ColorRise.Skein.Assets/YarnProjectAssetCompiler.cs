using ColorRise.Skein.Assets.Compilation;
using Stride.Core.Assets;
using Stride.Core.Assets.Compiler;
using Stride.Core.BuildEngine;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Skein.Assets;

/// <summary>
/// Compiles a Yarn project asset: one <see cref="YarnProject"/> at the URL of the asset, and the lines of each language
/// at <see cref="YarnProject.StringTableUrl"/>. The text of every script the project includes is part of what it
/// compiles: a change to a script compiles the project again.
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
        result.BuildSteps = new AssetBuildStep(assetItem);
        result.BuildSteps.Add(new YarnProjectCommand(targetUrlInStorage, project, assetItem.Location.FullPath, files, paths, [], assetItem.Package!));
    }

    // The build engine serializes a command to run it in another process, which this one never asks for
#pragma warning disable STRDIAG010
    private sealed class YarnProjectCommand(
        string url,
        YarnProjectAsset parameters,
        string projectName,
        (string FileName, string Text)[] scripts,
        Dictionary<string, string> paths,
        YarnFunctionDeclaration[] functions,
        IAssetFinder assetFinder)
        : AssetCommand<YarnProjectAsset>(url, parameters, assetFinder)
#pragma warning restore STRDIAG010
    {
        // To raise when the content of a project changes for the same asset and scripts
        private const int ContentVersion = 1;

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
            var build = YarnProjectBuilder.Build(Parameters, projectName, scripts, functions);
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
            content.Save(YarnProject.StringTableUrl(Url, build.BaseStrings.Language), build.BaseStrings);
            return Task.FromResult(ResultStatus.Successful);
        }
    }
}
