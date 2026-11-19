using Yarn;
using Yarn.Compiler;

namespace ColorRise.Skein.Tests.Spikes;

/// <summary>
/// Yarn Spinner's packages from nuget.org compile and run a script beside Stride's assemblies.
/// </summary>
public sealed class YarnPackagesTests
{
    [Fact]
    public async Task Compiles_and_runs_with_the_nuget_packages()
    {
        var job = CompilationJob.CreateFromString("Hello.yarn", "title: Start\n---\nOdile: Hello.\n===\n");
        var result = Compiler.Compile(job);

        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Program);

        var lines = new LineRecorder();
        var dialogue = new Dialogue(new MemoryVariableStore()) { Responder = lines, Program = result.Program };
        await dialogue.StartDialogue("Start");

        Assert.Equal(["Odile: Hello."], lines.LineIds.Select(id => result.StringTable![id].text));
        Assert.False(dialogue.IsActive);
    }
}
