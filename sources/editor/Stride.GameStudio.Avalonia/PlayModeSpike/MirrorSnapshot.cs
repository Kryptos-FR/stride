// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using Stride.Core.Assets;
using Stride.Core.Extensions;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Engine.Design;

namespace Stride.GameStudio.Avalonia.PlayModeSpike;

/// <summary>
/// Play mode spike: the snapshot of one entity as the play agent would send it. The entity and its components are in
/// <see cref="Payload"/> (Stride's binary serialization); every object outside of them (other entities and their components,
/// content, strings) is cut and travels as a descriptor in <see cref="Shared"/>, by index.
/// </summary>
public sealed record MirrorSnapshot(Guid EntityId, byte[] Payload, IReadOnlyList<MirrorSharedObject> Shared)
{
    /// <summary>
    /// The size of the snapshot on the wire, roughly: the payload plus the descriptors.
    /// </summary>
    public int ApproximateSize => Payload.Length + Shared.Sum(x => x.ApproximateSize);
}

/// <summary>
/// An object outside of the snapshot's entity.
/// </summary>
public abstract record MirrorSharedObject
{
    public virtual int ApproximateSize => 20;
}

/// <summary>
/// An entity that is not the snapshot's: the mirror shows it as a stand-in with its id and name, and no component.
/// </summary>
public sealed record MirrorEntityStandIn(Guid Id, string? Name) : MirrorSharedObject
{
    public override int ApproximateSize => 16 + (Name?.Length ?? 0) * 2;
}

/// <summary>
/// A component of an entity that is not the snapshot's: a stand-in component of that type on the entity's stand-in.
/// </summary>
public sealed record MirrorComponentStandIn(Type Type, Guid Id, Guid EntityId, string? EntityName) : MirrorSharedObject
{
    public override int ApproximateSize => 40 + (EntityName?.Length ?? 0) * 2 + (Type.FullName?.Length ?? 0);
}

/// <summary>
/// Content (a model, a texture...), by URL. A runtime game knows the URL its content manager loaded it from, not the asset id.
/// </summary>
public sealed record MirrorContentReference(Type Type, AssetId Id, string? Url) : MirrorSharedObject
{
    public override int ApproximateSize => 20 + (Url?.Length ?? 0) * 2 + (Type.FullName?.Length ?? 0);
}

public sealed record MirrorString(string Value) : MirrorSharedObject
{
    public override int ApproximateSize => Value.Length * 2;
}

/// <summary>
/// An object the snapshot cannot describe (a graphics resource made at run time, for instance): the mirror gets null.
/// </summary>
public sealed record MirrorUnknownObject(Type Type) : MirrorSharedObject;

/// <summary>
/// Play mode spike: writes and reads <see cref="MirrorSnapshot"/>, with the clone machinery of <see cref="EntityCloner"/>:
/// the entity and its components are serialized, every other entity, component and content goes to the shared list.
/// </summary>
/// <remarks>
/// <para>
/// The engine's "Clone" profile cuts entities and content (it shares them); the "PlayMirror" profile this class registers adds
/// the components (<see cref="MirrorComponentSerializer{T}"/> for each component type), so that a reference to another entity's
/// component does not pull that entity in.
/// </para>
/// <para>
/// Spike code: the registration at run time of a serializer profile is a shortcut; the agent would declare it with
/// <c>DataSerializerGlobal</c> attributes in an assembly the assembly processor runs on.
/// </para>
/// </remarks>
public static class MirrorSnapshotSerializer
{
    private const string MirrorProfile = "PlayMirror";
    private static readonly object RegistrationLock = new();
    private static readonly HashSet<Type> RegisteredComponentTypes = [];
    private static SerializerSelector? mirrorSelector;
    private static SerializerSelector? defaultSelector;

    /// <summary>
    /// Writes a snapshot of <paramref name="entity"/> and its components.
    /// </summary>
    public static MirrorSnapshot Capture(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var (selector, inner) = EnsureSelector(entity.Components.Select(x => x.GetType()));
        var context = new EntityCloner.CloneContext
        {
            EntitySerializerSelector = inner,
            ClonedObjects = [entity, .. entity.Components],
        };
        var stream = new MemoryStream(4096);
        var writer = new BinarySerializationWriter(stream);
        writer.Context.SerializerSelector = selector;
        writer.Context.Set(EntityCloner.CloneContextProperty, context);
        writer.Context.Set(ContentSerializerContext.SerializeAttachedReferenceProperty, ContentSerializerContext.AttachedReferenceSerialization.AsSerializableVersion);
        writer.SerializeExtended(entity, ArchiveMode.Serialize);

        var shared = context.SharedObjects.Select(Describe).ToList();
        return new MirrorSnapshot(entity.Id, stream.ToArray(), shared);
    }

    /// <summary>
    /// Reads a snapshot into new objects: the mirror entity, with stand-ins for the objects outside of it.
    /// </summary>
    public static Entity Restore(MirrorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var (selector, inner) = EnsureSelector([]);
        var standIns = new Dictionary<Guid, Entity>();
        var context = new EntityCloner.CloneContext { EntitySerializerSelector = inner };
        foreach (var shared in snapshot.Shared)
            context.SharedObjects.Add(Materialize(shared, standIns)!);

        var reader = new BinarySerializationReader(new MemoryStream(snapshot.Payload, false));
        reader.Context.SerializerSelector = selector;
        reader.Context.Set(EntityCloner.CloneContextProperty, context);
        reader.Context.Set(ContentSerializerContext.SerializeAttachedReferenceProperty, ContentSerializerContext.AttachedReferenceSerialization.AsSerializableVersion);
        Entity? entity = null;
        reader.SerializeExtended(ref entity, ArchiveMode.Deserialize);
        return entity ?? throw new InvalidOperationException("The snapshot holds no entity.");
    }

    /// <summary>
    /// The serializer a snapshot uses for <paramref name="type"/> (a diagnostic of the spike).
    /// </summary>
    public static string SerializerOf(Type type) => $"{EnsureSelector([type]).Mirror.GetSerializer(type)?.GetType().Name ?? "none"} registered={RegisteredComponentTypes.Contains(type)} count={RegisteredComponentTypes.Count} entry={DataSerializerFactory.GetSerializer(MirrorProfile, type).SerializerType?.Name} default={DataSerializerFactory.GetSerializer("Default", type).SerializerType?.Name} profiles={string.Join('/', mirrorSelector!.Profiles)}";

    private static MirrorSharedObject Describe(object? shared)
    {
        switch (shared)
        {
            case Entity entity:
                return new MirrorEntityStandIn(entity.Id, entity.Name);
            case EntityComponent component:
                return new MirrorComponentStandIn(component.GetType(), component.Id, component.Entity?.Id ?? Guid.Empty, component.Entity?.Name);
            case string text:
                return new MirrorString(text);
            case null:
                return new MirrorUnknownObject(typeof(object));
        }
        var reference = AttachedReferenceManager.GetAttachedReference(shared);
        return reference?.Url is { } url ? new MirrorContentReference(shared.GetType(), reference.Id, url) : new MirrorUnknownObject(shared.GetType());
    }

    private static object? Materialize(MirrorSharedObject shared, Dictionary<Guid, Entity> standIns)
    {
        switch (shared)
        {
            case MirrorEntityStandIn entity:
                return GetStandIn(entity.Id, entity.Name);
            case MirrorComponentStandIn component:
            {
                var owner = GetStandIn(component.EntityId, component.EntityName);
                if (component.Type == typeof(TransformComponent))
                    return owner.Transform;
                var instance = (EntityComponent)Activator.CreateInstance(component.Type)!;
                instance.Id = component.Id;
                owner.Components.Add(instance);
                return instance;
            }
            case MirrorContentReference content:
                return AttachedReferenceManager.CreateProxyObject(content.Type, content.Id, content.Url);
            case MirrorString text:
                return text.Value;
            default:
                return null;
        }

        Entity GetStandIn(Guid id, string? name)
        {
            if (!standIns.TryGetValue(id, out var standIn))
                standIns.Add(id, standIn = new Entity(name) { Id = id });
            return standIn;
        }
    }

    private static (SerializerSelector Mirror, SerializerSelector Inner) EnsureSelector(IEnumerable<Type> componentTypes)
    {
        lock (RegistrationLock)
        {
            var missing = typeof(EntityComponent).GetInheritedInstantiableTypes().Concat(componentTypes)
                .Where(x => !x.IsAbstract && x.GetConstructor(Type.EmptyTypes) is not null && !RegisteredComponentTypes.Contains(x))
                .Distinct().ToList();
            if (missing.Count > 0)
            {
                // A new registration each time types appear (a game assembly loaded later): the factory merges the profiles
                var serializers = new AssemblySerializers(typeof(MirrorSnapshotSerializer).Assembly);
                var profile = new AssemblySerializersPerProfile();
                foreach (var type in missing)
                {
                    RegisteredComponentTypes.Add(type);
                    var existing = DataSerializerFactory.GetSerializer("Default", type);
                    if (existing.SerializerType is not null)
                        profile.Add(new AssemblySerializerEntry(existing.Id, type, typeof(MirrorComponentSerializer<>).MakeGenericType(type)));
                }
                serializers.Profiles[MirrorProfile] = profile;
                DataSerializerFactory.RegisterSerializationAssembly(serializers);
                // A selector keeps the serializers it made for the types it knew: new selectors pick the new profile entries up
                mirrorSelector = null;
                defaultSelector = null;
            }
            if (mirrorSelector is null || defaultSelector is null)
            {
                mirrorSelector = new SerializerSelector(true, false, "Default", "Clone", MirrorProfile);
                // The serializers of the snapshot's own entity and components come from the default profile, and the serializers
                // of their members from the mirror's, so that a member referencing another entity or component is cut too
                defaultSelector = new SerializerSelector(true, false, "Default") { SelectorOverride = mirrorSelector };
            }
            return (mirrorSelector, defaultSelector);
        }
    }
}

/// <summary>
/// Play mode spike: the serializer of a component in a snapshot. A component of the snapshot's entity is serialized; any other
/// is shared (cut), as <see cref="CloneSerializer{T}"/> does for entities.
/// </summary>
/// <remarks>
/// <see cref="CloneSerializer{T}"/> cannot be used for components: it attaches a deserialized component to its entity, which
/// the entity's component collection does again.
/// </remarks>
public sealed class MirrorComponentSerializer<T> : DataSerializer<T> where T : EntityComponent
{
    public override void PreSerialize(ref T obj, ArchiveMode mode, SerializationStream stream)
    {
        var context = stream.Context.Get(EntityCloner.CloneContextProperty);
        bool isShared;
        if (mode == ArchiveMode.Serialize)
        {
            isShared = context.ClonedObjects?.Contains(obj) == false;
            stream.Write(isShared);
            if (isShared)
            {
                stream.Write(context.SharedObjects.Count);
                context.SharedObjects.Add(obj);
                return;
            }
        }
        else
        {
            isShared = stream.ReadBoolean();
            if (isShared)
            {
                obj = (T)context.SharedObjects[stream.ReadInt32()];
                return;
            }
        }
        context.EntitySerializerSelector.GetSerializer<T>()!.PreSerialize(ref obj, mode, stream);
        context.SerializedObjects.Add(obj);
    }

    public override void Serialize(ref T obj, ArchiveMode mode, SerializationStream stream)
    {
        var context = stream.Context.Get(EntityCloner.CloneContextProperty);
        if (context.SerializedObjects.Contains(obj))
            context.EntitySerializerSelector.GetSerializer<T>()!.Serialize(ref obj, mode, stream);
    }
}
