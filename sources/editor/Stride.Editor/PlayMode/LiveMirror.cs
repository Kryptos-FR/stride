// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Engine;
using Stride.PlayMode.Snapshots;

namespace Stride.Editor.PlayMode;

/// <summary>
/// What became of a component of a snapshot in its mirror.
/// </summary>
[Experimental("STREXP012")]
public enum LiveComponentStatus
{
    /// <summary>Read: the component is a real object of its type with the game's values.</summary>
    Read,

    /// <summary>The editor does not have the component's type: a placeholder stands for it.</summary>
    UnknownType,

    /// <summary>The editor has another build of the component's assembly: a placeholder stands for it until the game's code is
    /// reloaded.</summary>
    StaleType,

    /// <summary>The game could not write the component (<see cref="LiveComponent.Error"/>): a placeholder stands for it.</summary>
    NotWritten,

    /// <summary>The editor could not read what the game wrote (<see cref="LiveComponent.Error"/>): a placeholder stands for it.</summary>
    ReadFailed,
}

/// <summary>
/// A component of a mirror.
/// </summary>
/// <param name="Id">The component's id.</param>
/// <param name="TypeName">Its type in the game: the full name, a comma and the assembly's simple name.</param>
/// <param name="Status">Whether it was read.</param>
/// <param name="Component">The component in the mirror's entity: a real one, or a placeholder (an
/// <see cref="Stride.Core.Yaml.IUnloadable"/> with the type's name) when it was not read.</param>
/// <param name="Error">Why it was not read.</param>
[Experimental("STREXP012")]
public sealed record LiveComponent(Guid Id, string TypeName, LiveComponentStatus Status, EntityComponent Component, string? Error);

/// <summary>
/// The copy of a live entity read from a snapshot: a real <see cref="Entity"/> with real components, never added to a scene,
/// which the property grid can show.
/// </summary>
/// <remarks>
/// References to other entities and components are stand-ins (an entity with its id and name, a component of the right type
/// with its id); <see cref="StandIns"/> gives the live entity each stands for. References to the mirror's own entity and
/// components are the mirror's. Content is a proxy with its URL. The transform keeps no children (the tree gives them).
/// </remarks>
/// <param name="Handle">The agent's handle of the entity.</param>
/// <param name="Id">The entity's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Entity">The mirror entity.</param>
/// <param name="Components">Its components, in order, with what became of each.</param>
/// <param name="StandIns">For each stand-in the mirror refers to (an entity or a component), the reference it stands for,
/// with the live entity's handle.</param>
[Experimental("STREXP012")]
public sealed record LiveEntityMirror(int Handle, Guid Id, string? Name, Entity Entity, IReadOnlyList<LiveComponent> Components, IReadOnlyDictionary<object, PlayMirrorReference> StandIns);

/// <summary>
/// A snapshot read into mirrors.
/// </summary>
/// <param name="Frame">The game's frame.</param>
/// <param name="GameTime">The game's total time, in seconds.</param>
/// <param name="Entities">The mirrors of the subscribed entities still in the game.</param>
[Experimental("STREXP012")]
public sealed record LiveSnapshot(long Frame, double GameTime, IReadOnlyList<LiveEntityMirror> Entities);
