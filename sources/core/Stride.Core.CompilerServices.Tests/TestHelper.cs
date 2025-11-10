using Xunit;

namespace Stride.Core.CompilerServices.Tests;

/// <summary>
/// Provides helper methods for testing analyzer diagnostics.
/// </summary>
internal static class TestHelper
{
    /// <summary>
    /// Verifies that the provided source code does not produce any analyzer diagnostics.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    public static async Task ExpectNoDiagnosticsAsync(string sourceCode)
    {
        var diagnostics = await CompilerUtils.CompileAndGetAnalyzerDiagnosticsAsync(sourceCode, CompilerUtils.AllAnalyzers);
        
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Verifies that the provided source code produces at least one diagnostic with the specified ID.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <param name="expectedDiagnosticId">The expected diagnostic ID.</param>
    public static async Task ExpectDiagnosticAsync(string sourceCode, string expectedDiagnosticId)
    {
        var diagnostics = await CompilerUtils.CompileAndGetAnalyzerDiagnosticsAsync(sourceCode, CompilerUtils.AllAnalyzers);
        var matchingDiagnostic = diagnostics.FirstOrDefault(d => d.Id == expectedDiagnosticId);
        
        Assert.NotNull(matchingDiagnostic);
    }

    /// <summary>
    /// Verifies that the provided source code produces exactly the specified diagnostics.
    /// </summary>
    /// <param name="sourceCode">The C# source code to analyze.</param>
    /// <param name="expectedDiagnosticIds">The expected diagnostic IDs.</param>
    public static async Task ExpectDiagnosticsAsync(string sourceCode, params string[] expectedDiagnosticIds)
    {
        var diagnostics = await CompilerUtils.CompileAndGetAnalyzerDiagnosticsAsync(sourceCode, CompilerUtils.AllAnalyzers);
        var diagnosticIds = diagnostics.Select(d => d.Id).ToArray();
        
        Assert.Equal(expectedDiagnosticIds.OrderBy(x => x), diagnosticIds.OrderBy(x => x));
    }

    /// <summary>
    /// Legacy method for backwards compatibility. Use <see cref="ExpectNoDiagnosticsAsync"/> instead.
    /// </summary>
    [Obsolete("Use ExpectNoDiagnosticsAsync instead")]
    public static Task ExpectNoDiagnosticsErrorsAsync(string sourceCode) => ExpectNoDiagnosticsAsync(sourceCode);

    /// <summary>
    /// Legacy method for backwards compatibility. Use <see cref="ExpectDiagnosticAsync"/> instead.
    /// </summary>
    [Obsolete("Use ExpectDiagnosticAsync instead")]
    public static Task ExpectDiagnosticsErrorAsync(string sourceCode, string diagnosticID) => ExpectDiagnosticAsync(sourceCode, diagnosticID);
}
