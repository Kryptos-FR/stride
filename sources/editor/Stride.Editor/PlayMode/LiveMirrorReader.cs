// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stride.Core.Yaml;
using Stride.Engine;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.Editor.PlayMode;

/// <summary>
/// Reads the snapshot of a live entity into a mirror (<see cref="LiveEntityMirror"/>).
/// </summary>
/// <remarks>
/// A component is read only when the editor has its type in the same build as the game (<see cref="PlayAssemblyCheck"/>):
/// binary payloads have no member names, so another build could misread values silently. Any component not read becomes a
/// placeholder, and the entity's other components are read. Thread-safe; each call makes new objects.
/// </remarks>
[Experimental("STREXP012")]
public sealed class LiveMirrorReader
{
    private readonly PlayAssemblyCheck assemblies;
    private readonly Func<string, string, Type?> findType;
    private readonly ConcurrentDictionary<string, Type?> types = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveMirrorReader"/> class.
    /// </summary>
    /// <param name="assemblies">The comparison of the game's assemblies with the editor's.</param>
    /// <param name="findType">Finds the editor's type of a full name in an assembly of a simple name; by default, among the
    /// loaded assemblies.</param>
    public LiveMirrorReader(PlayAssemblyCheck assemblies, Func<string, string, Type?>? findType = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        this.assemblies = assemblies;
        this.findType = findType ?? FindLoadedType;
    }

    /// <summary>
    /// Reads a snapshot.
    /// </summary>
    public LiveSnapshot Read(PlaySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new LiveSnapshot(snapshot.Frame, snapshot.GameTime, snapshot.Entities.Select(Read).ToList());
    }

    /// <summary>
    /// Reads one entity of a snapshot.
    /// </summary>
    public LiveEntityMirror Read(PlayEntitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var mirror = new Entity(snapshot.Name) { Id = snapshot.Id };
        mirror.Components.Clear();

        // First an instance of each component, so that a reference from one component to another of the entity finds it
        var prepared = new (EntityComponent Component, LiveComponentStatus Status, string? Error)[snapshot.Components.Count];
        for (var i = 0; i < snapshot.Components.Count; i++)
        {
            prepared[i] = Prepare(snapshot.Components[i]);
            mirror.Components.Add(prepared[i].Component);
        }

        var standIns = new StandIns(this, snapshot.Handle, mirror);
        var components = new List<LiveComponent>(prepared.Length);
        for (var i = 0; i < snapshot.Components.Count; i++)
        {
            var chunk = snapshot.Components[i];
            var (component, status, error) = prepared[i];
            if (status == LiveComponentStatus.Read)
            {
                try
                {
                    PlayMirrorSerializer.Read(chunk.Payload, component, standIns.Resolve);
                }
                catch (Exception e)
                {
                    status = LiveComponentStatus.ReadFailed;
                    error = $"{e.GetType().Name}: {e.Message}";
                    var placeholder = CreatePlaceholder(chunk, error);
                    mirror.Components[i] = placeholder;
                    component = placeholder;
                }
            }
            components.Add(new LiveComponent(chunk.Id, chunk.TypeName, status, component, error));
        }

        // The tree gives the hierarchy: the stand-ins of the children are not the mirror's
        mirror.Transform?.Children.Clear();
        return new LiveEntityMirror(snapshot.Handle, snapshot.Id, snapshot.Name, mirror, components, standIns.Map);
    }

    private (EntityComponent Component, LiveComponentStatus Status, string? Error) Prepare(PlayComponentSnapshot chunk)
    {
        if (chunk.Format != PlaySnapshotFormat.Binary)
        {
            var error = chunk.Error ?? $"The game sent the component in format {(byte)chunk.Format}, which the editor cannot read.";
            return (CreatePlaceholder(chunk, error), chunk.Format == PlaySnapshotFormat.None ? LiveComponentStatus.NotWritten : LiveComponentStatus.ReadFailed, error);
        }
        var (typeName, assemblyName) = Split(chunk.TypeName);
        switch (assemblies.GetStatus(assemblyName))
        {
            case PlayAssemblyStatus.Stale:
            {
                var error = $"The game runs another build of {assemblyName}; reload the game's code to see this component.";
                return (CreatePlaceholder(chunk, error), LiveComponentStatus.StaleType, error);
            }
            case PlayAssemblyStatus.Missing:
            {
                var error = $"The editor does not have {assemblyName}.";
                return (CreatePlaceholder(chunk, error), LiveComponentStatus.UnknownType, error);
            }
        }
        var type = GetType(typeName, assemblyName);
        if (type is null || type.IsAbstract || !typeof(EntityComponent).IsAssignableFrom(type))
        {
            var error = $"The editor does not have the type {typeName}.";
            return (CreatePlaceholder(chunk, error), LiveComponentStatus.UnknownType, error);
        }
        try
        {
            var component = (EntityComponent)Activator.CreateInstance(type)!;
            component.Id = chunk.Id;
            return (component, LiveComponentStatus.Read, null);
        }
        catch (Exception e)
        {
            var error = $"The editor cannot make a {typeName}: {e.GetBaseException().Message}";
            return (CreatePlaceholder(chunk, error), LiveComponentStatus.ReadFailed, error);
        }
    }

    private Type? GetType(string typeName, string assemblyName)
        => types.GetOrAdd($"{typeName}, {assemblyName}", _ => findType(typeName, assemblyName));

    private static EntityComponent CreatePlaceholder(PlayComponentSnapshot chunk, string error)
    {
        var (typeName, assemblyName) = Split(chunk.TypeName);
        var placeholder = (EntityComponent)UnloadableObjectInstantiator.CreateUnloadableObject(typeof(EntityComponent), typeName, assemblyName, error, []);
        placeholder.Id = chunk.Id;
        return placeholder;
    }

    private static (string TypeName, string AssemblyName) Split(string typeName)
    {
        var comma = typeName.LastIndexOf(',');
        return comma < 0 ? (typeName, string.Empty) : (typeName[..comma].Trim(), typeName[(comma + 1)..].Trim());
    }

    private static Type? FindLoadedType(string typeName, string assemblyName)
    {
        Type? found = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && assembly.GetName().Name == assemblyName && assembly.GetType(typeName) is { } type)
                found = type;
        }
        return found;
    }

    /// <summary>
    /// What the references of one entity's components stand for: the mirror and its components, or stand-ins made once per
    /// snapshot.
    /// </summary>
    private sealed class StandIns(LiveMirrorReader reader, int handle, Entity mirror)
    {
        private readonly Dictionary<(int Handle, Guid Id), Entity> entities = [];
        private readonly Dictionary<(int Handle, Guid Id), EntityComponent?> components = [];

        public Dictionary<object, PlayMirrorReference> Map { get; } = new(ReferenceEqualityComparer.Instance);

        public object? Resolve(PlayMirrorReference reference)
        {
            var isMirror = reference.EntityHandle != 0 ? reference.EntityHandle == handle : reference.EntityId == mirror.Id;
            switch (reference.Kind)
            {
                case PlayMirrorReferenceKind.Entity:
                    return isMirror ? mirror : GetEntity(reference);
                case PlayMirrorReferenceKind.Component when isMirror:
                    return mirror.Components.FirstOrDefault(x => x.Id == reference.ComponentId);
                case PlayMirrorReferenceKind.Component:
                    return GetComponent(reference);
                default:
                    return null;
            }
        }

        private Entity GetEntity(PlayMirrorReference reference)
        {
            var key = (reference.EntityHandle, reference.EntityId);
            if (!entities.TryGetValue(key, out var standIn))
            {
                standIn = new Entity(reference.EntityName) { Id = reference.EntityId };
                entities.Add(key, standIn);
                Map[standIn] = reference with { Kind = PlayMirrorReferenceKind.Entity, ComponentId = Guid.Empty, ComponentTypeName = null };
            }
            return standIn;
        }

        private EntityComponent? GetComponent(PlayMirrorReference reference)
        {
            var key = (reference.EntityHandle, reference.ComponentId);
            if (components.TryGetValue(key, out var standIn))
                return standIn;
            var owner = GetEntity(reference);
            var (typeName, assemblyName) = Split(reference.ComponentTypeName ?? string.Empty);
            var type = reader.assemblies.GetStatus(assemblyName) == PlayAssemblyStatus.Same ? reader.GetType(typeName, assemblyName) : null;
            try
            {
                if (type == typeof(TransformComponent))
                {
                    standIn = owner.Transform;
                }
                else if (type is not null && !type.IsAbstract && typeof(EntityComponent).IsAssignableFrom(type))
                {
                    standIn = (EntityComponent)Activator.CreateInstance(type)!;
                    owner.Components.Add(standIn);
                }
            }
            catch (Exception)
            {
                // A component that cannot be made outside of a game: the reference reads as null
                standIn = null;
            }
            if (standIn is not null)
            {
                standIn.Id = reference.ComponentId;
                Map[standIn] = reference;
            }
            components.Add(key, standIn);
            return standIn;
        }
    }
}
