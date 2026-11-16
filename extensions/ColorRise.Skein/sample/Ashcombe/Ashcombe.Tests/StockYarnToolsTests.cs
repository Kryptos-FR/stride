using ColorRise.Skein;
using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Interop;
using Stride.Core.Assets;
using Stride.Core.Assets.Serializers;
using Stride.Core.Reflection;

namespace Ashcombe.Tests;

/// <summary>
/// Yarn Spinner's own tools read the sample's two Yarn projects as they are in the assets of the game: the project files
/// and the definitions files beside them are the ones Skein writes, and Yarn Spinner's compiler, given nothing but a
/// project file, compiles its scripts. The story's project leaves the echo's folder out, as the asset does.
/// </summary>
public sealed class StockYarnToolsTests
{
    static StockYarnToolsTests()
    {
        AssemblyRegistry.Register(typeof(YarnProject).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(YarnProjectAsset).Assembly, AssemblyCommonCategories.Assets);
    }

    public static TheoryData<string> Projects => new() { "Ashcombe", "Echo" };

    private static string Folder(string name) => name == "Echo" ? StoryFiles.EchoFolder : StoryFiles.Folder;

    private static string ProjectFile(string name) => Path.Combine(Folder(name), name + YarnProjectFileWriter.FileExtension);

    [Theory]
    [MemberData(nameof(Projects))]
    public void The_files_for_Yarn_Spinners_tools_are_the_ones_Skein_writes(string name)
    {
        var definitions = YarnDefinitionsWriter.Write(GameActionScanner.Scan([typeof(StoryCommands).Assembly]).Actions);
        var definitionsFile = Path.Combine(Folder(name), name + YarnDefinitionsWriter.FileExtension);

        // Whatever line endings the checkout gave the files
        Assert.Equal(definitions.ReplaceLineEndings("\n"), File.ReadAllText(definitionsFile).ReplaceLineEndings("\n"));

        var asset = (YarnProjectAsset)AssetFileSerializer.Load<Asset>(Path.Combine(Folder(name), name + YarnProjectAsset.FileExtension)).Asset;
        var existing = File.ReadAllText(ProjectFile(name));
        var written = YarnProjectFileWriter.Merge(existing, asset, Folder(name), name + YarnDefinitionsWriter.FileExtension);
        Assert.Equal(written.ReplaceLineEndings("\n"), existing.ReplaceLineEndings("\n"));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Yarn_Spinners_compiler_compiles_each_project_from_its_project_file(string name)
    {
        var project = Yarn.Compiler.Project.LoadFromFile(ProjectFile(name));
        var files = project.SourceFiles.ToList();
        Assert.Equal((name == "Echo" ? StoryFiles.EchoScripts() : StoryFiles.Scripts()).Count, files.Count);

        // No declaration of the game's functions: the compiler has the scripts and the functions of every Yarn Spinner
        // dialogue, as its command line has, and finds the types of the game's functions from how the scripts use them
        var job = Yarn.Compiler.CompilationJob.CreateFromFiles(files, new Yarn.Dialogue(new Yarn.MemoryVariableStore()).Library);
        job.LanguageVersion = project.FileVersion;
        var result = Yarn.Compiler.Compiler.Compile(job);

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error).Select(d => $"{Path.GetFileName(d.FileName)} {d}"));
        Assert.Contains(name == "Echo" ? "Echo" : "Hearing", result.Program!.Nodes.Keys);
    }
}
