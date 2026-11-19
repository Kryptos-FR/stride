using ColorRise.Skein;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Interop;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.Reflection;

namespace Brinemoor.Tests;

/// <summary>
/// Yarn Spinner's own tools read the story as it is in the assets of the game: the project file and the definitions
/// file beside the Yarn project are the ones Skein writes, and Yarn Spinner's compiler, given nothing but that project
/// file, compiles the scripts.
/// </summary>
public sealed class StockYarnToolsTests
{
    private const string Name = "Brinemoor";

    static StockYarnToolsTests()
    {
        AssemblyRegistry.Register(typeof(YarnProject).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(YarnProjectAsset).Assembly, AssemblyCommonCategories.Assets);
    }

    private static string ProjectFile => Path.Combine(StoryFiles.Folder, Name + YarnProjectFileWriter.FileExtension);

    private static string DefinitionsFile => Path.Combine(StoryFiles.Folder, Name + YarnDefinitionsWriter.FileExtension);

    /// <summary>
    /// The text of the definitions file of the game's commands and functions, as Skein writes it.
    /// </summary>
    internal static string Definitions() => YarnDefinitionsWriter.Write(GameActionScanner.Scan([typeof(StoryCommands).Assembly]).Actions);

    /// <summary>
    /// The text of the project file, as Skein writes it over the one that is there.
    /// </summary>
    internal static string Project(string? existing)
    {
        var asset = (YarnProjectAsset)AssetFileSerializer.Load<Asset>(Path.Combine(StoryFiles.Folder, Name + YarnProjectAsset.FileExtension)).Asset;
        return YarnProjectFileWriter.Merge(existing, asset, StoryFiles.Folder, Name + YarnDefinitionsWriter.FileExtension);
    }

    [Fact]
    public void The_files_for_Yarn_Spinners_tools_are_the_ones_Skein_writes()
    {
        // Whatever line endings the checkout gave the files
        Assert.Equal(Definitions().ReplaceLineEndings("\n"), File.ReadAllText(DefinitionsFile).ReplaceLineEndings("\n"));

        var existing = File.ReadAllText(ProjectFile);
        Assert.Equal(Project(existing).ReplaceLineEndings("\n"), existing.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Yarn_Spinners_compiler_compiles_the_story_from_its_project_file()
    {
        var project = Yarn.Compiler.Project.LoadFromFile(ProjectFile);
        var files = project.SourceFiles.ToList();
        Assert.Equal(StoryFiles.Scripts().Count, files.Count);

        // As Yarn Spinner's command line and editor extension do: the game's functions come from the definitions file the
        // project file names
        var job = Yarn.Compiler.CompilationJob.CreateFromProject(project);
        var result = Yarn.Compiler.Compiler.Compile(job);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error).Select(d => $"{Path.GetFileName(d.FileName)} {d}"));
        Assert.NotNull(result.Program);
        Assert.Contains("Events", result.Program.Nodes.Keys);
    }
}
