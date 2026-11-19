// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.PlayMode.Agent;

/// <summary>
/// The game's content as the play agent sees it: whether a URL is built, loading and unloading, and the URLs of the build.
/// </summary>
internal interface IPlayContent
{
    /// <summary>Whether the game's built data has this URL.</summary>
    bool Exists(string url);

    /// <summary>Loads the content of a URL (one more reference to it, when the game already loaded it).</summary>
    object Load(Type type, string url);

    /// <summary>Gives back one reference to content loaded by <see cref="Load"/>.</summary>
    void Unload(object content);

    /// <summary>The URLs of the game's built data.</summary>
    IReadOnlyList<string> ListUrls();
}

/// <summary>
/// The game's own content manager.
/// </summary>
internal sealed class PlayGameContent(Func<ContentManager?> content) : IPlayContent
{
    private ContentManager Content => content() ?? throw new PlayEditException("The game has no content manager yet: content cannot be loaded.");

    public bool Exists(string url) => content() is { FileProvider: not null } manager && manager.Exists(url);

    public object Load(Type type, string url) => Content.Load(type, url);

    public void Unload(object loaded) => Content.Unload(loaded);

    public IReadOnlyList<string> ListUrls()
    {
        if (content()?.FileProvider?.ContentIndexMap is not { } index)
            return [];
        return index.GetMergedIdMap().Select(x => x.Key).Order(StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// The content that the edits of the subscribers loaded (a model, a material, a prefab set by URL), and when to give it back.
/// </summary>
/// <remarks>
/// <para>
/// Each piece of content an edit sets is loaded once for that edit, through the game's content manager, which counts the
/// references: content the game already loaded is the same object, with one more reference. A load is kept under the member
/// or the collection the edit changed (its "slot": the entity, the component and the path). After each edit of a slot, the
/// loads of the slot whose content is no longer there (the reference changed again, the item was removed) are unloaded, one
/// reference each: the agent gives back only what it loaded, so the game's own loads stay.
/// </para>
/// <para>
/// When the last subscriber leaves (the editor stopped following, its connection was lost), the loads whose content is no
/// longer in their slot are unloaded, and those still in use stay until the game exits: unloading them would dispose content
/// the game still draws. When the game exits, everything left is unloaded.
/// </para>
/// </remarks>
internal sealed class PlayContentLoads(IPlayContent content)
{
    private readonly Dictionary<Slot, List<object>> loads = [];
    private readonly Dictionary<Slot, IReadOnlyList<PlayPathSegment>> paths = [];
    private readonly List<object> loading = [];
    // The loads kept until the game exits (prefabs spawned)
    private readonly List<object> untilExit = [];

    /// <summary>
    /// The content loaded and not given back yet.
    /// </summary>
    public int Count => loads.Values.Sum(x => x.Count) + untilExit.Count;

    /// <summary>
    /// The loads made since the agent started.
    /// </summary>
    public int Loaded { get; private set; }

    /// <summary>
    /// The loads given back since the agent started.
    /// </summary>
    public int Unloaded { get; private set; }

    /// <summary>
    /// The content of the game's build.
    /// </summary>
    public IPlayContent Content => content;

    /// <summary>
    /// Loads the content of a URL for the edit being applied, and checks that it is of the type the editor sent.
    /// </summary>
    /// <exception cref="PlayEditException">The URL is not in the build, or its content is of another type, or failed to load.</exception>
    public object Load(Type type, string url)
    {
        if (!content.Exists(url))
            throw new PlayEditException($"{url} is not in the game's build: no root asset or scene of the game references it, so it was not built.");
        object loaded;
        try
        {
            loaded = content.Load(type, url);
        }
        catch (Exception e)
        {
            // The content manager checks the type in the content's header
            var reason = e.GetBaseException().Message;
            const string actual = "the actual type of the resource was ";
            if (reason.IndexOf(actual, StringComparison.Ordinal) is var at and >= 0)
            {
                var actualType = reason[(at + actual.Length)..].Trim();
                throw new PlayEditException($"{url} is a {actualType[(actualType.LastIndexOf('.') + 1)..]} in the game's build, not a {type.Name}.");
            }
            throw new PlayEditException($"The game could not load {url}: {reason}");
        }
        if (!type.IsInstanceOfType(loaded))
        {
            content.Unload(loaded);
            throw new PlayEditException($"{url} is a {loaded.GetType().Name} in the game's build, not a {type.Name}.");
        }
        loading.Add(loaded);
        return loaded;
    }

    /// <summary>
    /// The edit being applied loaded content that the game uses in ways the agent cannot follow (the entities of a prefab
    /// spawned share the content it loaded): what it loaded stays until the game exits.
    /// </summary>
    public void KeepUntilExit()
    {
        Loaded += loading.Count;
        untilExit.AddRange(loading);
        loading.Clear();
    }

    /// <summary>
    /// The edit being applied failed: what it loaded is given back.
    /// </summary>
    public void Abort()
    {
        Loaded += loading.Count;
        foreach (var loaded in loading)
            UnloadOne(loaded);
        loading.Clear();
    }

    /// <summary>
    /// The edit being applied changed its slot: what it loaded is kept under the slot, and the slot's loads that are no longer
    /// in it are given back.
    /// </summary>
    /// <param name="slot">The member or collection the edit changed.</param>
    /// <param name="path">The path of the slot from its component.</param>
    /// <param name="value">What the slot holds now (a reference, or a collection of them).</param>
    /// <param name="release">Whether to give back the loads no longer in the slot; <c>false</c> keeps them all (the slot could
    /// not be read).</param>
    public void Commit(Slot slot, IReadOnlyList<PlayPathSegment> path, object? value, bool release = true)
    {
        if (loading.Count == 0 && !loads.ContainsKey(slot))
            return;
        if (!loads.TryGetValue(slot, out var kept))
            loads.Add(slot, kept = []);
        paths[slot] = path;
        Loaded += loading.Count;
        kept.AddRange(loading);
        loading.Clear();
        if (release)
            Release(slot, kept, value);
    }

    /// <summary>
    /// Gives back the loads whose content is no longer in its slot, and keeps the others (the last subscriber left: the game
    /// goes on, and still uses them).
    /// </summary>
    /// <param name="valueOf">Gives what a slot holds now, from the slot and its path: whether its entity and component are
    /// still in the game, and the value; throws when the slot cannot be read (its loads are kept).</param>
    /// <returns>The loads given back, and those kept.</returns>
    public (int Unloaded, int Kept) ReleaseUnused(Func<Slot, IReadOnlyList<PlayPathSegment>, (bool InGame, object? Value)> valueOf)
    {
        var before = Unloaded;
        foreach (var (slot, kept) in loads.ToList())
        {
            (bool InGame, object? Value) found;
            try
            {
                found = valueOf(slot, paths[slot]);
            }
            catch (Exception)
            {
                // Cannot tell: kept until the game exits
                continue;
            }
            // An entity that left the game no longer draws what it held
            Release(slot, kept, found.InGame ? found.Value : null);
        }
        return (Unloaded - before, Count);
    }

    /// <summary>
    /// Gives back every load (the game exits).
    /// </summary>
    /// <returns>The loads given back.</returns>
    public int ReleaseAll()
    {
        var before = Unloaded;
        Abort();
        foreach (var kept in loads.Values)
        {
            foreach (var loaded in kept)
                UnloadOne(loaded);
        }
        foreach (var loaded in untilExit)
            UnloadOne(loaded);
        untilExit.Clear();
        loads.Clear();
        paths.Clear();
        return Unloaded - before;
    }

    private void Release(Slot slot, List<object> kept, object? value)
    {
        // What the slot holds, as many times as it holds it
        var present = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        try
        {
            foreach (var item in Contents(value))
                present[item] = present.GetValueOrDefault(item) + 1;
        }
        catch (Exception)
        {
            // What the slot holds cannot be read: every load stays, none is given back while it may be in use
            return;
        }
        for (var i = kept.Count - 1; i >= 0; i--)
        {
            var loaded = kept[i];
            if (present.TryGetValue(loaded, out var count) && count > 0)
            {
                present[loaded] = count - 1;
                continue;
            }
            kept.RemoveAt(i);
            UnloadOne(loaded);
        }
        if (kept.Count == 0)
        {
            loads.Remove(slot);
            paths.Remove(slot);
        }
    }

    private void UnloadOne(object loaded)
    {
        try
        {
            content.Unload(loaded);
        }
        catch (InvalidOperationException)
        {
            // The game unloaded it already (its scene was unloaded with it): nothing left to give back
        }
        ++Unloaded;
    }

    /// <summary>
    /// The objects a slot holds: its value, or the items (the values of a dictionary) of a collection.
    /// </summary>
    private static IEnumerable<object> Contents(object? value)
    {
        if (value is null or string or Entity or EntityComponent)
            yield break;
        // Content first: a model is also a collection of its meshes
        if (PlayMirrorSerializer.IsContentType(value.GetType()))
        {
            yield return value;
            yield break;
        }
        if (PlayEditPath.TryAsDictionary(value) is { } dictionary)
        {
            foreach (var item in dictionary.Values)
            {
                if (item is not null)
                    yield return item;
            }
            yield break;
        }
        if (value is IEnumerable items)
        {
            foreach (var item in items)
            {
                if (item is not null)
                    yield return item;
            }
            yield break;
        }
        yield return value;
    }

    /// <summary>
    /// The member or collection of a live component that an edit changes.
    /// </summary>
    /// <param name="EntityHandle">The agent's handle of the entity.</param>
    /// <param name="ComponentId">The component's id; empty for the entity itself.</param>
    /// <param name="Path">The path of the member or collection, as text.</param>
    internal readonly record struct Slot(int EntityHandle, Guid ComponentId, string Path);
}
