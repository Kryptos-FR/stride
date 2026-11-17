// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// How an assembly of the played game compares with the editor's.
/// </summary>
[Experimental("STREXP012")]
public enum PlayAssemblyStatus
{
    /// <summary>The editor has the same build, or another build of the same types (the editor's own build of the game's code):
    /// its types read the game's snapshots.</summary>
    Same,

    /// <summary>The editor has another build whose types serialize other members (the game was rebuilt after a code change):
    /// binary snapshots of its types cannot be read safely, since a member added or changed would be misread.</summary>
    Stale,

    /// <summary>The editor does not have the assembly (a plugin without its editor part, or game code the editor did not load).</summary>
    Missing,
}

/// <summary>
/// Compares the assemblies the game reports (<see cref="PlayHello"/>, <see cref="PlayAssemblies"/>) with the editor's, by the
/// identifier of their build (MVID), then by the shape of the data their types serialize (<see cref="PlayAssemblyShape"/>),
/// so that a stale type is known before its payload is read.
/// </summary>
/// <remarks>
/// Stride's own assemblies are compared by version (the session checks the engine's version): the editor and the game can
/// load the same version from different builds. Thread-safe.
/// </remarks>
[Experimental("STREXP012")]
public sealed class PlayAssemblyCheck
{
    private readonly object @lock = new();
    private readonly Dictionary<string, PlayAssemblyInfo> gameAssemblies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayAssemblyStatus> statuses = new(StringComparer.Ordinal);
    private readonly Func<string, Assembly?> findEditorAssembly;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAssemblyCheck"/> class.
    /// </summary>
    /// <param name="findEditorAssembly">Finds the editor's assembly of a simple name, or <c>null</c>; by default, among the
    /// assemblies loaded in the editor.</param>
    public PlayAssemblyCheck(Func<string, Assembly?>? findEditorAssembly = null)
    {
        this.findEditorAssembly = findEditorAssembly ?? FindLoadedAssembly;
    }

    /// <summary>
    /// The game's assemblies the editor has another build of.
    /// </summary>
    public IReadOnlyList<string> StaleAssemblies
    {
        get
        {
            lock (@lock)
                return gameAssemblies.Keys.Where(x => GetStatusCore(x) == PlayAssemblyStatus.Stale).Order(StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>
    /// Records assemblies the game reported.
    /// </summary>
    public void Add(IEnumerable<PlayAssemblyInfo> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        lock (@lock)
        {
            foreach (var assembly in assemblies)
            {
                gameAssemblies[assembly.Name] = assembly;
                statuses.Remove(assembly.Name);
            }
        }
    }

    /// <summary>
    /// Compares one of the game's assemblies with the editor's.
    /// </summary>
    /// <param name="assemblyName">The simple name of the assembly.</param>
    public PlayAssemblyStatus GetStatus(string assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);
        lock (@lock)
            return GetStatusCore(assemblyName);
    }

    private PlayAssemblyStatus GetStatusCore(string assemblyName)
    {
        if (statuses.TryGetValue(assemblyName, out var status))
            return status;
        var editorAssembly = findEditorAssembly(assemblyName);
        if (editorAssembly is null)
            status = PlayAssemblyStatus.Missing;
        else if (!gameAssemblies.TryGetValue(assemblyName, out var gameAssembly))
            status = PlayAssemblyStatus.Same; // not reported: nothing to compare
        else if (assemblyName.StartsWith("Stride.", StringComparison.Ordinal) && editorAssembly.GetName().Version?.ToString() == gameAssembly.Version)
            status = PlayAssemblyStatus.Same;
        else if (editorAssembly.ManifestModule.ModuleVersionId == gameAssembly.Mvid)
            status = PlayAssemblyStatus.Same;
        else
        {
            // Another build: the editor's is built for itself and the game's for its platform, so they always differ; the
            // types still read the game's snapshots while they serialize the same members
            status = gameAssembly.Shape != Guid.Empty && PlayAssemblyShape.Compute(editorAssembly) == gameAssembly.Shape ? PlayAssemblyStatus.Same : PlayAssemblyStatus.Stale;
        }
        statuses[assemblyName] = status;
        return status;
    }

    private static Assembly? FindLoadedAssembly(string name)
    {
        // The last one loaded wins, as the editor's type lookups do after a reload of the game's code
        Assembly? found = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && assembly.GetName().Name == name)
                found = assembly;
        }
        return found;
    }
}
