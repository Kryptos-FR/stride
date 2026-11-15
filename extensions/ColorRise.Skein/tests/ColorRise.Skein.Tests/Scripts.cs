using ColorRise.Skein.Assets.Compilation;

namespace ColorRise.Skein.Tests;

/// <summary>
/// Compiles the scripts the tests run.
/// </summary>
internal static class Scripts
{
    /// <summary>
    /// Compiles one script, and fails the test on any error.
    /// </summary>
    public static YarnCompilation Compile(string text, params YarnFunctionDeclaration[] functions)
    {
        var result = YarnCompilerBridge.Compile(new YarnCompilationRequest { Files = [("Test.yarn", text)], Functions = functions });
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == YarnSeverity.Error);
        Assert.NotNull(result.Program);
        return result;
    }

    /// <summary>
    /// The program of one script.
    /// </summary>
    public static Yarn.Program Program(string text, params YarnFunctionDeclaration[] functions) => Compile(text, functions).Program!;
}
