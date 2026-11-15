using Yarn;
using Yarn.Compiler;

namespace ColorRise.Skein.Tests.Spikes;

/// <summary>
/// Yarn Spinner's packages from nuget.org compile and run a script beside Stride's assemblies.
/// </summary>
public sealed class YarnPackagesTests
{
    [Fact]
    public void Compiles_and_runs_with_the_nuget_packages()
    {
        var job = CompilationJob.CreateFromString("Hello.yarn", "title: Start\n---\nOdile: Hello.\n===\n");
        var result = Compiler.Compile(job);

        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Program);

        var lines = new List<string>();
        var dialogue = new Dialogue(new MemoryVariableStore());
        // Yarn 3.2 ignores a Continue() made inside a handler: the caller continues once the handler has returned
        dialogue.LineHandler = line => lines.Add(result.StringTable![line.ID].text!);
        dialogue.CommandHandler = _ => { };
        dialogue.OptionsHandler = _ => { };
        dialogue.NodeStartHandler = _ => { };
        dialogue.NodeCompleteHandler = _ => { };
        dialogue.DialogueCompleteHandler = () => { };
        dialogue.SetProgram(result.Program!);
        dialogue.SetNode("Start");
        var steps = 0;
        do
        {
            dialogue.Continue();
        }
        while (dialogue.IsActive && ++steps < 10);

        Assert.Equal(["Odile: Hello."], lines);
        Assert.False(dialogue.IsActive);
    }
}
