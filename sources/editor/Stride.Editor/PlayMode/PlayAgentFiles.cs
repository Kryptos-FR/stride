// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Stride.Engine;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The play agent's files that ship next to the editor (the startup hook, the agent and the protocol, in a folder of their
/// own), and the variables that load them into a game.
/// </summary>
/// <remarks>
/// The folder holds only these assemblies: the hook resolves the agent's dependencies from it, and must never find another
/// build of an assembly the game has. The agent is built against the editor's engine, so it attaches only to a game that runs
/// the same Stride version.
/// </remarks>
[Experimental("STREXP012")]
public sealed class PlayAgentFiles
{
    /// <summary>
    /// The name of the folder of the agent's files, next to the editor's assemblies.
    /// </summary>
    public const string FolderName = "PlayMode";

    /// <summary>
    /// The file name of the startup hook.
    /// </summary>
    public const string HookFileName = "Stride.PlayMode.Hook.dll";

    /// <summary>
    /// The files the folder holds.
    /// </summary>
    public static IReadOnlyList<string> FileNames { get; } = [HookFileName, "Stride.PlayMode.Agent.dll", "Stride.PlayMode.Protocol.dll"];

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAgentFiles"/> class.
    /// </summary>
    /// <param name="directory">The folder of the files.</param>
    /// <param name="strideVersion">The Stride version the agent was built against.</param>
    public PlayAgentFiles(string directory, string strideVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentNullException.ThrowIfNull(strideVersion);
        Directory = directory;
        StrideVersion = strideVersion;
    }

    /// <summary>
    /// The agent's files next to this editor, built against its engine.
    /// </summary>
    public static PlayAgentFiles NextToEditor { get; } = new(Path.Combine(AppContext.BaseDirectory, FolderName), EditorStrideVersion);

    /// <summary>
    /// The informational version of the editor's engine.
    /// </summary>
    public static string EditorStrideVersion => typeof(Entity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

    /// <summary>
    /// The folder of the files.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// The Stride version the agent was built against.
    /// </summary>
    public string StrideVersion { get; }

    /// <summary>
    /// The path of the startup hook.
    /// </summary>
    public string HookPath => Path.Combine(Directory, HookFileName);

    /// <summary>
    /// Whether every file is there.
    /// </summary>
    public bool Exist => FileNames.All(x => File.Exists(Path.Combine(Directory, x)));

    /// <summary>
    /// Compares two Stride versions without their build metadata (what follows a <c>+</c>).
    /// </summary>
    public static bool AreSameVersion(string? first, string? second)
        => first is not null && second is not null && string.Equals(WithoutMetadata(first), WithoutMetadata(second), StringComparison.Ordinal);

    /// <summary>
    /// Reads the Stride version a built game runs: the informational version of the <c>Stride.Engine.dll</c> next to it.
    /// </summary>
    /// <param name="gameAssemblyPath">The assembly of the game's executable project.</param>
    /// <returns>The version, or <c>null</c> when the folder has no engine.</returns>
    public static string? GetGameStrideVersion(string gameAssemblyPath)
    {
        ArgumentNullException.ThrowIfNull(gameAssemblyPath);
        var engine = Path.Combine(Path.GetDirectoryName(gameAssemblyPath) ?? string.Empty, "Stride.Engine.dll");
        return File.Exists(engine) ? FileVersionInfo.GetVersionInfo(engine).ProductVersion : null;
    }

    /// <summary>
    /// The variables that load the agent into a game and connect it to a transport.
    /// </summary>
    /// <param name="transportEnvironment">The transport's variables (<see cref="IPlayTransport.GameEnvironment"/>).</param>
    /// <param name="existingStartupHooks">The startup hooks already in the environment the game inherits, kept before the agent's.</param>
    public IReadOnlyDictionary<string, string> CreateGameEnvironment(IReadOnlyDictionary<string, string> transportEnvironment, string? existingStartupHooks = null)
    {
        ArgumentNullException.ThrowIfNull(transportEnvironment);
        var environment = new Dictionary<string, string>(transportEnvironment)
        {
            [PlayProtocol.StartupHooksVariable] = string.IsNullOrEmpty(existingStartupHooks) ? HookPath : $"{existingStartupHooks}{Path.PathSeparator}{HookPath}",
            [PlayProtocol.EngineVersionVariable] = StrideVersion,
        };
        return environment;
    }

    private static string WithoutMetadata(string version)
    {
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}
