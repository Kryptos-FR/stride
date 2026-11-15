using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ColorRise.Skein.Assets.Compilation;

/// <summary>
/// The commands and the functions of a game, as its assemblies declare them.
/// </summary>
/// <param name="Actions">Every command and function that can be one: commands first, each kind by name.</param>
/// <param name="Problems">What is wrong with each method that carries an attribute and cannot be a command or a function; the message names it.</param>
public sealed record GameActions(IReadOnlyList<YarnActionDescription> Actions, IReadOnlyList<string> Problems)
{
    /// <summary>
    /// No command and no function.
    /// </summary>
    public static GameActions None { get; } = new([], []);

    /// <summary>
    /// The functions, as the compiler needs them to check their calls.
    /// </summary>
    public IReadOnlyList<YarnFunctionDeclaration> Functions { get; } =
        [.. Actions.Where(a => a.Kind == YarnActionKind.Function).Select(YarnFunctionDeclaration.From)];
}

/// <summary>
/// Finds the commands and the functions of a game in its assemblies, for the build, Game Studio and the definitions
/// file of Yarn Spinner's tools.
/// </summary>
public static class GameActionScanner
{
    private static readonly string SkeinName = typeof(YarnCommandAttribute).Assembly.GetName().Name!;

    /// <summary>
    /// Reads the methods that carry <see cref="YarnCommandAttribute"/> or <see cref="YarnFunctionAttribute"/>, the
    /// way a running game registers them. No method runs.
    /// </summary>
    /// <param name="assemblies">The assemblies of the game, in any order; one that does not reference Skein is skipped.</param>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Runs in the build and in Game Studio, which are not trimmed.")]
    public static GameActions Scan(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var registry = new YarnActionRegistry();
        var problems = new List<string>();

        // In a fixed order, so that of two methods of one name the same one wins at each build
        foreach (var assembly in assemblies.Distinct().OrderBy(a => a.FullName, StringComparer.Ordinal))
        {
            // Only an assembly that references Skein can carry its attributes
            if (assembly.GetReferencedAssemblies().Any(r => r.Name == SkeinName))
            {
                problems.AddRange(registry.RegisterWhatFits(assembly));
            }
        }

        return new GameActions(registry.Describe(), problems);
    }
}
