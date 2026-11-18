// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

/// <summary>
/// The entry point the .NET host calls before the game's <c>Main</c> when <c>DOTNET_STARTUP_HOOKS</c> names this assembly
/// (the editor's play mode starts a game that way).
/// </summary>
/// <remarks>
/// <para>
/// The host requires a class with no namespace and a <c>public static void Initialize()</c>. This assembly references no
/// Stride assembly: it checks that the game runs the Stride version the agent was built against, installs the resolver of
/// the agent's folder, and only then loads the agent, whose module initializer needs the engine.
/// </para>
/// <para>
/// The variables of the play mode (those that start with <c>STRIDE_PLAY_</c>) are removed from the game's environment first,
/// so that a process the game starts does not load an agent, and handed to the agent. Nothing here may end the game: every failure is written to the standard error, and the game runs
/// without an agent.
/// </para>
/// </remarks>
internal static class StartupHook
{
    private const string StartupHooksVariable = "DOTNET_STARTUP_HOOKS";
    private const string PipeVariable = "STRIDE_PLAY_PIPE";
    private const string EngineVersionVariable = "STRIDE_PLAY_ENGINE_VERSION";
    private const string VariablePrefix = "STRIDE_PLAY_";
    private const string AgentAssemblyName = "Stride.PlayMode.Agent";
    private const string AgentBootstrapType = "Stride.PlayMode.Agent.PlayAgentBootstrap";

    /// <summary>
    /// Called by the .NET host before <c>Main</c>.
    /// </summary>
    public static void Initialize()
    {
        try
        {
            var pipeName = Environment.GetEnvironmentVariable(PipeVariable);
            var expectedVersion = Environment.GetEnvironmentVariable(EngineVersionVariable);
            var hookPath = typeof(StartupHook).Assembly.Location;
            // The play mode's options (start paused, the key of the game window...), for the agent
            var variables = ClearVariables(hookPath);
            if (string.IsNullOrEmpty(pipeName))
                return;

            var gameVersion = FindEngineVersion();
            if (gameVersion is null)
            {
                Console.Error.WriteLine("Play mode: the game does not use the Stride engine; the live connection is off.");
                return;
            }
            if (!string.IsNullOrEmpty(expectedVersion) && !string.Equals(WithoutMetadata(gameVersion), WithoutMetadata(expectedVersion), StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Play mode: the game runs Stride {gameVersion} and the editor's agent was built for {expectedVersion}; the live connection is off.");
                return;
            }

            // Only the assemblies of the agent's folder, which ships with the editor and holds nothing else: what the game
            // has wins, since this handler runs only when the game's own probing fails.
            var agentDirectory = Path.GetDirectoryName(hookPath)!;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                var candidate = Path.Combine(agentDirectory, name.Name + ".dll");
                return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
            };
            var agent = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(AgentAssemblyName));
            agent.GetType(AgentBootstrapType, throwOnError: true)!
                .GetMethod("Start", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(IReadOnlyDictionary<string, string>)])!
                .Invoke(null, [pipeName, variables]);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Play mode: the agent could not start; the live connection is off. {e.GetBaseException().Message}");
        }
    }

    /// <summary>
    /// Removes the play mode's variables from the game's environment, and returns those that start with
    /// <see cref="VariablePrefix"/>.
    /// </summary>
    private static Dictionary<string, string> ClearVariables(string hookPath)
    {
        // Keep the other startup hooks of the variable, if the user set some
        var hooks = Environment.GetEnvironmentVariable(StartupHooksVariable);
        if (hooks is not null)
        {
            var others = hooks.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !string.Equals(Path.GetFullPath(x), Path.GetFullPath(hookPath), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Environment.SetEnvironmentVariable(StartupHooksVariable, others.Length > 0 ? string.Join(Path.PathSeparator, others) : null);
        }
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>())
        {
            if (entry.Key is string name && name.StartsWith(VariablePrefix, StringComparison.Ordinal))
            {
                variables[name] = entry.Value as string ?? string.Empty;
                Environment.SetEnvironmentVariable(name, null);
            }
        }
        return variables;
    }

    private static string? FindEngineVersion()
    {
        // The game's own assemblies, as its deps.json lists them
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string assemblies)
            return null;
        foreach (var path in assemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(Path.GetFileName(path), "Stride.Engine.dll", StringComparison.OrdinalIgnoreCase))
                return FileVersionInfo.GetVersionInfo(path).ProductVersion;
        }
        return null;
    }

    private static string WithoutMetadata(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}
