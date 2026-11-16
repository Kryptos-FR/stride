using ColorRise.Skein.Assets.Compilation;

namespace ColorRise.Skein.Tests.Guide;

/// <summary>
/// The guides say nothing the compilers refuse: their C# is the code of <c>DeveloperGuideSamples.cs</c>, and their Yarn
/// compiles.
/// </summary>
public sealed class GuideTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void The_code_of_the_developers_guide_is_compiled()
    {
        var samples = Lines(File.ReadAllText(Path.Combine(Root, "tests", "ColorRise.Skein.Tests", "Guide", "DeveloperGuideSamples.cs")));
        var blocks = Blocks("guide-developers.md", "csharp");
        Assert.NotEmpty(blocks);

        foreach (var block in blocks)
        {
            var lines = Lines(block);
            var found = Enumerable.Range(0, Math.Max(samples.Count - lines.Count + 1, 0)).Any(at => samples.Skip(at).Take(lines.Count).SequenceEqual(lines));
            Assert.True(found, $"This code of the guide is not in DeveloperGuideSamples.cs:\n{block}");
        }
    }

    [Theory]
    [InlineData("guide-writers.md")]
    [InlineData("guide-developers.md")]
    public void The_yarn_of_a_guide_compiles(string guide)
    {
        var registry = new YarnActionRegistry();
        registry.RegisterType(typeof(Purse));
        var functions = registry.Describe().Where(a => a.Kind == YarnActionKind.Function).Select(YarnFunctionDeclaration.From).ToList();
        functions.Add(new YarnFunctionDeclaration { Name = "has_item", ParameterTypes = [YarnFunctionDeclaration.String], ReturnType = YarnFunctionDeclaration.Bool });

        var blocks = Blocks(guide, "yarn");
        Assert.NotEmpty(blocks);

        foreach (var block in blocks)
        {
            // A block that is not a whole script is the body of a node
            var script = block.Contains("\n---\n", StringComparison.Ordinal) ? block : $"title: Sample\n---\n{block}\n===\n";
            var result = YarnCompilerBridge.Compile(new YarnCompilationRequest { Files = [("Guide.yarn", script)], Functions = functions });
            var errors = result.Diagnostics.Where(d => d.Severity == YarnSeverity.Error).Select(d => d.ToString()).ToList();
            Assert.True(errors.Count == 0, $"This Yarn of {guide} does not compile:\n{block}\n{string.Join("\n", errors)}");
        }
    }

    // The fenced blocks of a language in a guide
    private static List<string> Blocks(string guide, string language)
    {
        var blocks = new List<string>();
        List<string>? current = null;
        foreach (var line in File.ReadAllLines(Path.Combine(Root, "docs", guide)))
        {
            if (current is null)
            {
                if (line.Trim() == "```" + language)
                    current = [];
            }
            else if (line.Trim() == "```")
            {
                blocks.Add(string.Join("\n", current));
                current = null;
            }
            else
            {
                current.Add(line);
            }
        }

        return blocks;
    }

    // The lines of some code, without their indentation and without the empty ones
    private static List<string> Lines(string code) => [.. code.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)];

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "ColorRise.Skein.slnx")))
                return folder.FullName;
        }

        throw new InvalidOperationException("The folder of ColorRise.Skein was not found above the tests.");
    }
}
