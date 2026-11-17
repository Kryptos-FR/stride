// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Stride.Core;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Core.Yaml;
using Stride.Core.Yaml.Serialization;
using Stride.Engine;

namespace Stride.PlayMode.Spike;

/// <summary>
/// Decodes snapshots as the editor would: into real entities and components that are never added to a scene, with
/// references to other entities and components resolved to stand-ins built from the tree.
/// </summary>
internal sealed class SnapshotDecoder
{
    private readonly SerializerSelector selector = new(reuseReferences: true, externalIdentifiableAsGuid: true, "Default", "Content");
    private readonly Dictionary<Guid, IIdentifiable> standIns = [];
    private readonly Dictionary<Guid, string> names = [];
    private readonly Dictionary<string, List<double>> decodeMs = [];
    private readonly Dictionary<string, int> failures = [];
    private readonly Dictionary<string, int> failureMessages = [];
    private readonly List<string> loadNotes = [];

    /// <summary>The children of the last transform decoded, before the mirror dropped them.</summary>
    private string LastChildren { get; set; } = "";

    public SnapshotDecoder(Options options)
    {
        // The editor has the engine and the game's assemblies loaded: their module initializers register the
        // serializers and the types for YAML.
        foreach (var name in new[] { "Stride.Engine", "Stride.Physics", "Stride.Particles", "Stride.UI", "Stride.Navigation", "Stride.Audio", "Stride.Rendering", "Stride.Graphics", "Stride.Games", "Stride.Assets" })
        {
            try
            {
                var assembly = Assembly.Load(name);
                RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
            }
            catch (Exception e)
            {
                loadNotes.Add($"{name}: {e.Message}");
            }
        }
        if (options.LoadGameAssemblies)
        {
            var directory = Path.GetDirectoryName(options.Game)!;
            var gameAssemblies = Directory.GetFiles(directory, "*.dll")
                .Where(f => !Path.GetFileName(f).StartsWith("Stride.", StringComparison.Ordinal) && !Path.GetFileName(f).StartsWith("System.", StringComparison.Ordinal) && !Path.GetFileName(f).StartsWith("Microsoft.", StringComparison.Ordinal))
                .Where(f => AssemblyName.GetAssemblyName(f).Name is { } n && (n.EndsWith(".Game", StringComparison.Ordinal) || IsPack(directory, n)));
            foreach (var file in gameAssemblies)
            {
                var assembly = Assembly.LoadFrom(file);
                RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
                loadNotes.Add($"loaded game assembly {assembly.GetName().Name}");
            }
        }
        else
        {
            loadNotes.Add("game assemblies NOT loaded: the game's script types are unknown to this side");
        }
    }

    private static bool IsPack(string directory, string name)
        => name is "VFXPackage" or "PrototypingBlocks" or "mannequinModel" or "SamplesAssetPackage" or "MaterialPackage";

    public void SetTree(JsonDocument tree)
    {
        foreach (var entity in TreeTools.Flatten(tree.RootElement))
        {
            names[entity.Id] = entity.Name;
            var standIn = new Entity(entity.Name) { Id = entity.Id };
            standIn.Components.Clear();
            for (var i = 0; i < entity.ComponentTypes.Length; i++)
            {
                names[entity.ComponentIds[i]] = $"{entity.Name}/{entity.ComponentTypes[i].Split('.')[^1]}";
                var type = FindType(entity.ComponentTypes[i]);
                if (type is null || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
                    continue;
                try
                {
                    var component = (EntityComponent)Activator.CreateInstance(type)!;
                    component.Id = entity.ComponentIds[i];
                    standIn.Components.Add(component);
                    standIns[component.Id] = component;
                }
                catch (Exception)
                {
                    // A component that cannot be built without a game: references to it decode to null.
                }
            }
            standIns[entity.Id] = standIn;
        }
    }

    private static readonly Dictionary<string, Type?> TypeCache = [];

    private static Type? FindType(string fullName)
    {
        if (TypeCache.TryGetValue(fullName, out var cached))
            return cached;
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t is not null);
        TypeCache[fullName] = type;
        return type;
    }

    /// <summary>Decodes a snapshot and describes what each member became.</summary>
    public void Decode(byte[] payload, string outputPath)
    {
        var text = new StringBuilder();
        foreach (var note in loadNotes)
            text.AppendLine($"# {note}");
        foreach (var (entity, components, ticks, allocated) in Read(payload, out var format))
        {
            text.AppendLine();
            text.AppendLine($"== {entity.Name} {entity.Id} (agent: {ticks * 1000.0 / Stopwatch.Frequency:F3} ms, {allocated} B allocated)");
            foreach (var chunk in components)
            {
                text.AppendLine($"-- {chunk.TypeName} {chunk.Id} ({chunk.Bytes?.Length ?? 0} bytes)");
                if (chunk.Bytes is null)
                {
                    text.AppendLine($"   AGENT FAILED: {chunk.Error}");
                    continue;
                }
                if (format == SnapshotFormat.Yaml)
                {
                    text.AppendLine("   yaml:");
                    foreach (var line in Encoding.UTF8.GetString(chunk.Bytes).Split('\n').Take(60))
                        text.AppendLine("   | " + line.TrimEnd('\r'));
                }
                try
                {
                    var component = DecodeComponent(format, chunk.Bytes, entity);
                    text.AppendLine($"   decoded as {component.GetType().FullName}{(component is IUnloadable u ? $" (IUnloadable of {u.TypeName}, {u.AssemblyName}: {u.Error})" : "")}");
                    Describe(text, component, "   ", 0);
                    if (component is TransformComponent)
                        text.AppendLine($"   (children before the mirror dropped them: {LastChildren})");
                }
                catch (Exception e)
                {
                    text.AppendLine($"   DECODE FAILED: {e.GetType().Name}: {e.Message}");
                }
            }
        }
        File.WriteAllText(outputPath, text.ToString());
    }

    /// <summary>Decodes a snapshot and records the time it took.</summary>
    public void MeasureDecode(byte[] payload)
    {
        var start = Stopwatch.GetTimestamp();
        var key = "";
        foreach (var (entity, components, _, _) in Read(payload, out var format))
        {
            key = format.ToString();
            foreach (var chunk in components)
            {
                if (chunk.Bytes is null)
                    continue;
                try
                {
                    DecodeComponent(format, chunk.Bytes, entity);
                }
                catch (Exception e)
                {
                    failures[key] = failures.GetValueOrDefault(key) + 1;
                    var message = $"{key} {chunk.TypeName.Split(',')[0]}: {e.GetType().Name}: {e.Message}";
                    failureMessages[message] = failureMessages.GetValueOrDefault(message) + 1;
                }
            }
        }
        if (!decodeMs.TryGetValue(key, out var list))
            decodeMs[key] = list = [];
        list.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    public void WriteStats(Utf8JsonWriter json)
    {
        foreach (var (key, values) in decodeMs)
        {
            json.WriteStartObject(key);
            var sorted = values.OrderBy(x => x).ToArray();
            json.WriteNumber("snapshots", sorted.Length);
            json.WriteNumber("meanMs", Math.Round(sorted.Average(), 3));
            json.WriteNumber("p95Ms", Math.Round(sorted[Math.Max(0, (int)Math.Ceiling(0.95 * sorted.Length) - 1)], 3));
            json.WriteNumber("failedComponents", failures.GetValueOrDefault(key));
            json.WriteEndObject();
        }
        json.WriteStartObject("failures");
        foreach (var (message, count) in failureMessages)
            json.WriteNumber(message, count);
        json.WriteEndObject();
    }

    private sealed record Chunk(Guid Id, string TypeName, byte[]? Bytes, string? Error);

    private IEnumerable<(Entity Entity, List<Chunk> Components, long Ticks, long Allocated)> Read(byte[] payload, out SnapshotFormat format)
    {
        var reader = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
        reader.ReadInt64();
        reader.ReadDouble();
        format = (SnapshotFormat)reader.ReadByte();
        var count = reader.ReadInt32();
        var result = new List<(Entity, List<Chunk>, long, long)>();
        for (var i = 0; i < count; i++)
        {
            var id = new Guid(reader.ReadBytes(16));
            var name = reader.ReadString();
            var componentCount = reader.ReadInt32();
            var chunks = new List<Chunk>();
            for (var j = 0; j < componentCount; j++)
            {
                var componentId = new Guid(reader.ReadBytes(16));
                var typeName = reader.ReadString();
                var length = reader.ReadInt32();
                chunks.Add(length < 0 ? new Chunk(componentId, typeName, null, reader.ReadString()) : new Chunk(componentId, typeName, reader.ReadBytes(length), null));
            }
            var ticks = reader.ReadInt64();
            var allocated = reader.ReadInt64();
            // The mirror entity: a real entity, never added to a scene, that replaces its stand-in.
            var mirror = new Entity(name) { Id = id };
            mirror.Components.Clear();
            result.Add((mirror, chunks, ticks, allocated));
        }
        return result;
    }

    private EntityComponent DecodeComponent(SnapshotFormat format, byte[] bytes, Entity mirror)
    {
        if (format == SnapshotFormat.Binary)
        {
            // The component's own entity resolves to the mirror, any other entity or component to its stand-in.
            standIns.TryGetValue(mirror.Id, out var standIn);
            standIns[mirror.Id] = mirror;
            try
            {
                var reader = new BinarySerializationReader(new MemoryStream(bytes));
                reader.Context.SerializerSelector = selector;
                reader.Context.Set(MemberSerializer.ExternalIdentifiables, standIns);
                reader.Context.Set(ContentSerializerContext.SerializeAttachedReferenceProperty, ContentSerializerContext.AttachedReferenceSerialization.AsSerializableVersion);
                EntityComponent? component = null;
                reader.SerializeExtended(ref component, ArchiveMode.Deserialize);
                // The children resolve to the stand-ins of the child transforms, which the mirror would re-parent (and the
                // next mirror would fail on): the tree gives the hierarchy, so the mirror keeps no children.
                if (component is TransformComponent transform)
                {
                    LastChildren = string.Join(", ", transform.Children.Select(c => $"{c.Entity?.Name ?? "?"} {c.Id}"));
                    transform.Children.Clear();
                }
                return component!;
            }
            finally
            {
                if (standIn is not null)
                    standIns[mirror.Id] = standIn;
            }
        }
        var settings = new SerializerContextSettings();
        return (EntityComponent)AssetYamlSerializer.Default.Deserialize(new MemoryStream(bytes), typeof(EntityComponent), settings, out _, out _);
    }

    private void Describe(StringBuilder text, object instance, string indent, int depth)
    {
        if (TypeDescriptorFactory.Default.Find(instance.GetType()) is not ObjectDescriptor descriptor)
            return;
        foreach (var member in descriptor.Members)
        {
            object? value;
            try
            {
                value = member.Get(instance);
            }
            catch (Exception e)
            {
                value = $"<get failed: {e.Message}>";
            }
            text.AppendLine($"{indent}{member.Name}: {Classify(value)}");
            if (depth < 1 && value is not null && value is not string && !value.GetType().IsPrimitive && !value.GetType().IsEnum && Classify(value).StartsWith("object", StringComparison.Ordinal))
                Describe(text, value, indent + "  ", depth + 1);
        }
    }

    private string Classify(object? value)
    {
        switch (value)
        {
            case null:
                return "null";
            case IUnloadable unloadable:
                return $"IUnloadable {unloadable.TypeName}";
            case Entity entity:
                return standIns.TryGetValue(entity.Id, out var s) && ReferenceEquals(s, entity)
                    ? $"entity stand-in '{entity.Name}' {entity.Id}"
                    : $"entity object {entity.Id} (name in tree: {names.GetValueOrDefault(entity.Id, "unknown")})";
            case EntityComponent component:
                return standIns.TryGetValue(component.Id, out var c) && ReferenceEquals(c, component)
                    ? $"component stand-in {names.GetValueOrDefault(component.Id)} {component.Id}"
                    : $"component object {component.GetType().Name} {component.Id} (in tree: {names.GetValueOrDefault(component.Id, "unknown")})";
        }
        if (AttachedReferenceManager.GetAttachedReference(value) is { } reference)
            return $"content {value.GetType().Name} url='{reference.Url}' proxy={reference.IsProxy}";
        var type = value.GetType();
        if (value is string || type.IsPrimitive || type.IsEnum || type.Namespace == "Stride.Core.Mathematics" || type == typeof(Guid))
            return $"{value}";
        if (value is ICollection collection)
        {
            var first = collection.Cast<object?>().FirstOrDefault();
            return $"{type.Name} count={collection.Count}{(collection.Count > 0 ? $" first={Classify(first)}" : "")}";
        }
        return $"object {type.Name}";
    }
}
