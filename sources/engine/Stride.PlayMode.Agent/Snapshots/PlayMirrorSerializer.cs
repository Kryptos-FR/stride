// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Reflection;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Snapshots;

/// <summary>
/// Writes and reads one component of a live entity in Stride's binary serialization, cut at the entity boundary (the
/// <see cref="PlaySnapshotFormat.Binary"/> payload of a snapshot).
/// </summary>
/// <remarks>
/// <para>
/// The component is written in full. Every entity, and every component other than this one, is written as a
/// <see cref="PlayMirrorReference"/> (the "PlayMirror" serializer profile): a reference to another entity's camera does not pull
/// that entity in, and the reader decides what a reference becomes. Content is written by URL (the "Content" profile), as a
/// game's content manager knows it; the reader gets proxies with the URL.
/// </para>
/// <para>
/// Payload: the length of the serialized data (4 bytes), the data, then the references (count, then each one).
/// </para>
/// <para>
/// The profile's entity serializer is declared on this class, so the assembly processor registers it. The component serializers
/// are registered at run time for every component type of the loaded assemblies (<see cref="RegisterComponentTypes"/>): the
/// game's scripts are only known in the game, so no assembly built before it can declare them.
/// </para>
/// </remarks>
[DataSerializerGlobal(typeof(PlayMirrorEntitySerializer), Profile = Profile)]
public static class PlayMirrorSerializer
{
    /// <summary>
    /// The name of the serializer profile that cuts a component at its entity's boundary.
    /// </summary>
    public const string Profile = "PlayMirror";

    private static readonly object RegistrationLock = new();
    private static readonly ConcurrentDictionary<Type, bool> RegisteredTypes = [];
    private static readonly HashSet<Assembly> ScannedAssemblies = [];
    private static (SerializerSelector Mirror, SerializerSelector Inner)? selectors;
    private static volatile bool assembliesChanged = true;

    static PlayMirrorSerializer()
    {
        AssemblyRegistry.AssemblyRegistered += (_, _) => assembliesChanged = true;
    }

    /// <summary>
    /// The name of a type as snapshots and trees write it: the full name, a comma and the assembly's simple name.
    /// </summary>
    public static string TypeNameOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return $"{type.FullName}, {type.Assembly.GetName().Name}";
    }

    /// <summary>
    /// Registers the profile's serializer of every component type of the assemblies registered so far (the engine's, the
    /// plugins', the game's), and of <paramref name="moreTypes"/>. Only new types cost anything; the first call scans the
    /// assemblies and takes a few milliseconds.
    /// </summary>
    /// <param name="moreTypes">Component types that may not be in a registered assembly.</param>
    public static void RegisterComponentTypes(IEnumerable<Type>? moreTypes = null)
    {
        if (!assembliesChanged && (moreTypes is null || moreTypes.All(RegisteredTypes.ContainsKey)))
            return;
        lock (RegistrationLock)
        {
            var missing = new List<Type>();
            if (assembliesChanged)
            {
                assembliesChanged = false;
                foreach (var assembly in AssemblyRegistry.FindAll())
                {
                    if (!ScannedAssemblies.Add(assembly))
                        continue;
                    foreach (var type in GetLoadableTypes(assembly))
                        AddIfMissing(type, missing);
                }
            }
            if (moreTypes is not null)
            {
                foreach (var type in moreTypes)
                    AddIfMissing(type, missing);
            }
            if (missing.Count == 0)
                return;

            // One more registration for the assembly; the factory merges the profiles of all of them
            var serializers = new AssemblySerializers(typeof(PlayMirrorSerializer).Assembly);
            var profile = new AssemblySerializersPerProfile();
            foreach (var type in missing)
            {
                RegisteredTypes.TryAdd(type, true);
                // The same type id as the default serializer: a component written with its type reads back with this one
                var existing = DataSerializerFactory.GetSerializer("Default", type);
                if (existing.SerializerType is not null)
                    profile.Add(new AssemblySerializerEntry(existing.Id, type, typeof(PlayMirrorComponentSerializer<>).MakeGenericType(type)));
            }
            if (profile.Count == 0)
                return;
            serializers.Profiles[Profile] = profile;
            DataSerializerFactory.RegisterSerializationAssembly(serializers);
            // A selector keeps the serializers it already made: new ones pick the new entries up
            selectors = null;
        }
    }

    /// <summary>
    /// Creates the serializers of these component types ahead of their first snapshot (5 to 15 ms the first time), on any
    /// thread.
    /// </summary>
    public static void Prepare(IEnumerable<Type> componentTypes)
    {
        ArgumentNullException.ThrowIfNull(componentTypes);
        var types = componentTypes.ToList();
        RegisterComponentTypes(types);
        var (mirror, inner) = GetSelectors();
        foreach (var type in types)
        {
            mirror.GetSerializer(type);
            inner.GetSerializer(type);
        }
    }

    /// <summary>
    /// Writes a component.
    /// </summary>
    /// <param name="component">The component, on the game thread.</param>
    /// <param name="handleOf">Gives the agent's handle of an entity, or 0 for an entity it does not track.</param>
    /// <returns>The payload.</returns>
    /// <exception cref="Exception">The component holds something the binary serializers cannot write (content made at run
    /// time, which has no URL, for instance). Nothing of it is written.</exception>
    public static byte[] Write(EntityComponent component, Func<Entity, int> handleOf)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(handleOf);
        RegisterComponentTypes([component.GetType()]);

        var (mirror, inner) = GetSelectors();
        var context = PlayMirrorContext.ForWrite(inner, component, handleOf);
        var data = new MemoryStream(1024);
        var writer = new BinarySerializationWriter(data);
        writer.Context.SerializerSelector = mirror;
        writer.Context.Set(PlayMirrorContext.Key, context);
        writer.Context.Set(ContentSerializerContext.SerializeAttachedReferenceProperty, ContentSerializerContext.AttachedReferenceSerialization.AsSerializableVersion);
        writer.SerializeExtended(component, ArchiveMode.Serialize);
        writer.Flush();

        var payload = new PlayWriter((int)data.Length + 64 * context.References.Count + 8);
        payload.WriteBytes(data.GetBuffer().AsSpan(0, (int)data.Length));
        payload.WriteList(context.References, PlayMirrorReference.Write);
        return payload.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Reads the references of a payload, before reading it, to prepare what they stand for.
    /// </summary>
    public static IReadOnlyList<PlayMirrorReference> ReadReferences(ReadOnlyMemory<byte> payload)
    {
        var reader = new PlayReader(payload);
        reader.ReadBytes();
        return reader.ReadList(PlayMirrorReference.Read, 43);
    }

    /// <summary>
    /// Reads a component.
    /// </summary>
    /// <param name="payload">A payload written by <see cref="Write"/>.</param>
    /// <param name="target">An instance of the written type to read into (the mirror's component), or <c>null</c> to make one.</param>
    /// <param name="resolve">Gives what a reference stands for: the mirror's entity or one of its components, a stand-in,
    /// or <c>null</c>. It is called once per reference.</param>
    /// <returns>The component read.</returns>
    /// <exception cref="Exception">The reader does not have the written type, or has another build of it.</exception>
    public static EntityComponent Read(ReadOnlyMemory<byte> payload, EntityComponent? target, Func<PlayMirrorReference, object?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var reader = new PlayReader(payload);
        var data = reader.ReadBytes();
        var references = reader.ReadList(PlayMirrorReference.Read, 43);
        RegisterComponentTypes(target is not null ? [target.GetType()] : null);

        var (mirror, inner) = GetSelectors();
        var context = PlayMirrorContext.ForRead(inner, target, references, resolve);
        var stream = new BinarySerializationReader(new MemoryStream(data.ToArray(), writable: false));
        stream.Context.SerializerSelector = mirror;
        stream.Context.Set(PlayMirrorContext.Key, context);
        stream.Context.Set(ContentSerializerContext.SerializeAttachedReferenceProperty, ContentSerializerContext.AttachedReferenceSerialization.AsSerializableVersion);
        var component = target;
        stream.SerializeExtended(ref component, ArchiveMode.Deserialize);
        return component ?? throw new InvalidOperationException("The payload holds no component.");
    }

    private static (SerializerSelector Mirror, SerializerSelector Inner) GetSelectors()
    {
        lock (RegistrationLock)
        {
            if (selectors is { } existing)
                return existing;
            var mirror = new SerializerSelector(reuseReferences: true, externalIdentifiableAsGuid: false, "Default", "Content", Profile);
            // The root component's own serializer comes from the default profile; its members, from the mirror's
            var inner = new SerializerSelector(reuseReferences: true, externalIdentifiableAsGuid: false, "Default", "Content") { SelectorOverride = mirror };
            selectors = (mirror, inner);
            return (mirror, inner);
        }
    }

    private static void AddIfMissing(Type type, List<Type> missing)
    {
        if (typeof(EntityComponent).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters && !RegisteredTypes.ContainsKey(type) && !missing.Contains(type))
            missing.Add(type);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(x => x is not null)!;
        }
    }
}
