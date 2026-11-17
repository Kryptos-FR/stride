// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Stride.PlayMode.Spike;

/// <summary>
/// An entity of the tree, flattened.
/// </summary>
internal sealed record TreeEntity(Guid Id, string Name, string[] ComponentTypes, Guid[] ComponentIds, int ChildCount);

/// <summary>
/// Reads the tree message.
/// </summary>
internal static class TreeTools
{
    public static IEnumerable<TreeEntity> Flatten(JsonElement root)
    {
        if (root.TryGetProperty("root", out var scene))
        {
            foreach (var entity in FlattenScene(scene))
                yield return entity;
        }
    }

    private static IEnumerable<TreeEntity> FlattenScene(JsonElement scene)
    {
        foreach (var entity in scene.GetProperty("entities").EnumerateArray())
        {
            foreach (var e in FlattenEntity(entity))
                yield return e;
        }
        foreach (var child in scene.GetProperty("children").EnumerateArray())
        {
            foreach (var e in FlattenScene(child.GetProperty("scene")))
                yield return e;
        }
    }

    private static IEnumerable<TreeEntity> FlattenEntity(JsonElement entity)
    {
        var components = entity.GetProperty("components").EnumerateArray().ToArray();
        var children = entity.TryGetProperty("children", out var c) ? c.EnumerateArray().ToArray() : [];
        yield return new TreeEntity(
            entity.GetProperty("id").GetGuid(),
            entity.GetProperty("name").GetString() ?? "",
            components.Select(x => x.GetProperty("type").GetString()!).ToArray(),
            components.Select(x => x.GetProperty("id").GetGuid()).ToArray(),
            children.Length);
        foreach (var child in children)
        {
            foreach (var e in FlattenEntity(child))
                yield return e;
        }
    }
}

/// <summary>
/// Writes the tree as indented text.
/// </summary>
internal static class TreeDump
{
    public static string Write(JsonElement root)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{root.GetProperty("entities").GetInt32()} entities in the scene instance, tree built in {root.GetProperty("buildMs").GetDouble():F2} ms");
        if (root.TryGetProperty("root", out var scene))
            WriteScene(builder, scene, 0);
        return builder.ToString();
    }

    private static void WriteScene(StringBuilder builder, JsonElement scene, int depth)
    {
        builder.Append(' ', depth * 2).AppendLine($"[scene] {scene.GetProperty("name").GetString()} url={scene.GetProperty("url").GetString()} id={scene.GetProperty("id").GetGuid()}");
        foreach (var entity in scene.GetProperty("entities").EnumerateArray())
            WriteEntity(builder, entity, depth + 1);
        foreach (var child in scene.GetProperty("children").EnumerateArray())
            WriteScene(builder, child.GetProperty("scene"), depth + 1);
    }

    private static void WriteEntity(StringBuilder builder, JsonElement entity, int depth)
    {
        var types = entity.GetProperty("components").EnumerateArray().Select(x => x.GetProperty("type").GetString()!.Split('.')[^1]);
        var enabled = entity.GetProperty("enabled").GetBoolean() ? "" : " (disabled)";
        builder.Append(' ', depth * 2).AppendLine($"{entity.GetProperty("name").GetString()}{enabled} [{string.Join(", ", types)}] {entity.GetProperty("id").GetGuid()}");
        if (entity.TryGetProperty("children", out var children))
        {
            foreach (var child in children.EnumerateArray())
                WriteEntity(builder, child, depth + 1);
        }
    }
}

/// <summary>
/// Chooses the entities to snapshot: a player with its scripts and physics, the entity with the scripts that reference
/// other objects, a light, a model and the biggest entity.
/// </summary>
internal static class EntityPicker
{
    public sealed record Picked(string Role, Guid Id, string Name, int ComponentCount);

    public static List<Picked> Pick(JsonElement tree, string[] names)
    {
        var entities = TreeTools.Flatten(tree).ToList();
        var result = new List<Picked>();
        void Add(string role, TreeEntity? entity)
        {
            if (entity is not null && result.All(x => x.Id != entity.Id))
                result.Add(new Picked(role, entity.Id, entity.Name, entity.ComponentTypes.Length));
        }

        foreach (var name in names)
            Add("asked", entities.FirstOrDefault(x => x.Name == name));
        if (names.Length > 0)
            return result;

        bool Has(TreeEntity e, string suffix) => e.ComponentTypes.Any(t => t.EndsWith(suffix, StringComparison.Ordinal));
        Add("player", entities.Where(e => Has(e, "PlayerController") || Has(e, "CharacterComponent")).OrderByDescending(e => e.Name.Contains("Player")).ThenByDescending(e => e.ComponentTypes.Length).FirstOrDefault());
        Add("references", entities.FirstOrDefault(e => Has(e, ".PlayerInput")));
        Add("light", entities.FirstOrDefault(e => Has(e, ".LightComponent")));
        Add("model", entities.FirstOrDefault(e => Has(e, ".ModelComponent") && e.ComponentTypes.Length <= 3));
        Add("big", entities.OrderByDescending(e => e.ComponentTypes.Length).ThenByDescending(e => e.ChildCount).FirstOrDefault(e => result.All(x => x.Id != e.Id)));
        return result;
    }
}
