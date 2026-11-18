// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections;
using System.Reflection;
using Stride.Core.Reflection;
using Stride.Engine;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Applies the edits the subscribers sent, on the game thread, and gives the result of each.
/// </summary>
/// <remarks>
/// <para>
/// The edits of a frame apply in the order they arrived. A set that a later set of the same member of the same entity replaces
/// in the same frame (a slider dragged faster than the game draws) is not applied: the later one is, and both get its result,
/// the earlier as <see cref="PlayEditStatus.Coalesced"/>. A collection operation on the component in between keeps both, since
/// the path may then name another item.
/// </para>
/// <para>
/// After each set, the value is read back at its path and sent: a setter that clamps shows its clamped value. A setter that
/// throws is answered with its exception, and the game goes on.
/// </para>
/// </remarks>
internal sealed class PlayEditApplier
{
    /// <summary>
    /// Applies edits.
    /// </summary>
    /// <param name="edits">The edits of the frame, in the order they arrived, each with its sender.</param>
    /// <param name="frame">The agent's frame.</param>
    /// <param name="findEntity">Gives the live entity of a handle, or <c>null</c> when it is no longer in the game.</param>
    /// <param name="handleOf">Gives the agent's handle of an entity.</param>
    /// <returns>The result of each edit, in the same order.</returns>
    public IReadOnlyList<(T Sender, PlayEditResult Result)> Apply<T>(IReadOnlyList<(T Sender, PlayEdit Edit)> edits, long frame, Func<int, Entity?> findEntity, Func<Entity, int> handleOf)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(findEntity);
        ArgumentNullException.ThrowIfNull(handleOf);
        var results = new PlayEditResult?[edits.Count];
        // The index of the set that replaces each coalesced one
        var replacedBy = FindReplaced(edits);
        for (var i = 0; i < edits.Count; i++)
        {
            if (replacedBy[i] >= 0)
                continue;
            results[i] = Apply(edits[i].Edit, frame, findEntity, handleOf);
        }
        var answered = new List<(T, PlayEditResult)>(edits.Count);
        for (var i = 0; i < edits.Count; i++)
        {
            var result = results[i];
            if (result is null)
            {
                // The later set's result, under this edit's id
                var last = replacedBy[i];
                while (replacedBy[last] >= 0)
                    last = replacedBy[last];
                var final = results[last]!;
                result = final with { Id = edits[i].Edit.Id, Status = final.Status == PlayEditStatus.Applied ? PlayEditStatus.Coalesced : final.Status };
            }
            answered.Add((edits[i].Sender, result));
        }
        return answered;
    }

    private static int[] FindReplaced<T>(IReadOnlyList<(T Sender, PlayEdit Edit)> edits)
    {
        var replacedBy = new int[edits.Count];
        Array.Fill(replacedBy, -1);
        // The last set of each member seen so far, while no collection operation of its component came after it
        var lastSet = new Dictionary<(int Handle, Guid Component, string Path), int>();
        for (var i = 0; i < edits.Count; i++)
        {
            var edit = edits[i].Edit;
            if (edit.Operation != PlayEditOperation.Set)
            {
                foreach (var key in lastSet.Keys.Where(x => x.Handle == edit.EntityHandle && x.Component == edit.ComponentId).ToList())
                    lastSet.Remove(key);
                continue;
            }
            var memberKey = (edit.EntityHandle, edit.ComponentId, PathKey(edit.Path));
            if (lastSet.TryGetValue(memberKey, out var previous))
                replacedBy[previous] = i;
            lastSet[memberKey] = i;
        }
        return replacedBy;
    }

    private static string PathKey(IReadOnlyList<PlayPathSegment> path)
        => string.Join("/", path.Select(x => x.Kind == PlayPathSegmentKind.Key ? $"#{System.Convert.ToBase64String(x.Key.Span)}" : x.ToString()));

    private static PlayEditResult Apply(PlayEdit edit, long frame, Func<int, Entity?> findEntity, Func<Entity, int> handleOf)
    {
        var entity = findEntity(edit.EntityHandle);
        if (entity is null)
            return Gone(edit, frame, "The entity is no longer in the game.");
        // No component: a member of the entity itself (its name)
        object? component = edit.ComponentId == Guid.Empty ? entity : entity.Components.FirstOrDefault(x => x.Id == edit.ComponentId);
        if (component is null)
            return Gone(edit, frame, $"{entity.Name} no longer has this component.");

        object? Resolve(PlayMirrorReference reference) => ResolveReference(reference, findEntity);
        object? ReadKey(ReadOnlyMemory<byte> key) => PlayMirrorSerializer.ReadValue(key, Resolve);
        try
        {
            switch (edit.Operation)
            {
                case PlayEditOperation.Set:
                {
                    if (edit.Path.Count == 0)
                        throw new PlayEditException("An edit cannot replace the component or the entity itself.");
                    var value = CreateValue(edit.Value, Resolve);
                    var last = edit.Path[^1];
                    PlayEditPath.Change(component, edit.Path.Take(edit.Path.Count - 1).ToList(), x => PlayEditPath.SetStep(x, last, value, ReadKey), ReadKey);
                    return new PlayEditResult(edit.Id, frame, PlayEditStatus.Applied, ReadBack(component, edit.Path, ReadKey, handleOf), null);
                }
                case PlayEditOperation.Add:
                {
                    PlayEditPath.Change(component, edit.Path, collection =>
                    {
                        var list = PlayEditPath.AsList(collection, edit.Path.Count > 0 ? edit.Path[^1] : PlayPathSegment.Member(component.GetType().Name));
                        list.Add(CreateItem(edit.Value, collection, Resolve));
                        return collection;
                    }, ReadKey);
                    return Applied(edit, frame);
                }
                case PlayEditOperation.Insert:
                case PlayEditOperation.Remove:
                case PlayEditOperation.Move:
                {
                    if (edit.Path.Count == 0)
                        throw new PlayEditException("An item operation needs the item's index or key.");
                    var last = edit.Path[^1];
                    PlayEditPath.Change(component, edit.Path.Take(edit.Path.Count - 1).ToList(), collection => ChangeItems(edit, last, collection, Resolve, ReadKey), ReadKey);
                    return Applied(edit, frame);
                }
                default:
                    throw new PlayEditException($"The game does not know edits of kind {(byte)edit.Operation}.");
            }
        }
        catch (Exception e)
        {
            var error = Describe(e);
            // The value as it stayed, so that the editor shows it again
            PlayEditValue readBack;
            try
            {
                readBack = edit.Operation == PlayEditOperation.Set ? ReadBack(component, edit.Path, ReadKey, handleOf) : PlayEditValue.Null;
            }
            catch (Exception)
            {
                readBack = PlayEditValue.Null;
            }
            return new PlayEditResult(edit.Id, frame, PlayEditStatus.Failed, readBack, error);
        }
    }

    private static object ChangeItems(PlayEdit edit, PlayPathSegment last, object collection, Func<PlayMirrorReference, object?> resolve, Func<ReadOnlyMemory<byte>, object?> readKey)
    {
        if (last.Kind == PlayPathSegmentKind.Index)
        {
            var list = PlayEditPath.AsList(collection, last);
            switch (edit.Operation)
            {
                case PlayEditOperation.Insert:
                    if (last.Index < 0 || last.Index > list.Count)
                        throw new PlayEditException($"The list has {list.Count} items: an item cannot be inserted at {last}.");
                    list.Insert(last.Index, CreateItem(edit.Value, collection, resolve));
                    break;
                case PlayEditOperation.Remove:
                    PlayEditPath.CheckIndex(list.Count, last.Index, last);
                    list.RemoveAt(last.Index);
                    break;
                case PlayEditOperation.Move:
                    PlayEditPath.CheckIndex(list.Count, last.Index, last);
                    if (edit.Target is not { Kind: PlayPathSegmentKind.Index } target)
                        throw new PlayEditException("A move in a list needs the index the item goes to.");
                    var item = list[last.Index];
                    list.RemoveAt(last.Index);
                    if (target.Index < 0 || target.Index > list.Count)
                        throw new PlayEditException($"The list has {list.Count + 1} items: an item cannot go to {target}.");
                    list.Insert(target.Index, item);
                    break;
            }
            return collection;
        }
        if (last.Kind == PlayPathSegmentKind.Key)
        {
            var dictionary = PlayEditPath.AsDictionary(collection, last);
            var key = PlayEditPath.ReadKey(last, readKey)!;
            switch (edit.Operation)
            {
                case PlayEditOperation.Insert:
                    if (dictionary.Contains(key))
                        throw new PlayEditException($"The dictionary already has the key {last}.");
                    dictionary.Add(key, CreateItem(edit.Value, collection, resolve));
                    break;
                case PlayEditOperation.Remove:
                    if (!dictionary.Contains(key))
                        throw new PlayEditException($"The dictionary has no key {last}.");
                    dictionary.Remove(key);
                    break;
                case PlayEditOperation.Move:
                    if (edit.Target is not { Kind: PlayPathSegmentKind.Key } target)
                        throw new PlayEditException("A new key needs the key itself.");
                    if (!dictionary.Contains(key))
                        throw new PlayEditException($"The dictionary has no key {last}.");
                    var newKey = PlayEditPath.ReadKey(target, readKey)!;
                    if (dictionary.Contains(newKey))
                        throw new PlayEditException($"The dictionary already has the key {target}.");
                    var item = dictionary[key];
                    dictionary.Remove(key);
                    dictionary.Add(newKey, item);
                    break;
            }
            return collection;
        }
        throw new PlayEditException($"{last} is not an item of a list or a dictionary.");
    }

    private static PlayEditValue ReadBack(object component, IReadOnlyList<PlayPathSegment> path, Func<ReadOnlyMemory<byte>, object?> readKey, Func<Entity, int> handleOf)
    {
        var value = PlayEditPath.Get(component, path, readKey);
        return value is null ? PlayEditValue.Null : PlayEditValue.Of(PlayMirrorSerializer.WriteValue(value, handleOf));
    }

    private static object? CreateValue(PlayEditValue value, Func<PlayMirrorReference, object?> resolve)
    {
        switch (value.Kind)
        {
            case PlayEditValueKind.Null:
                return null;
            case PlayEditValueKind.Value:
                try
                {
                    return PlayMirrorSerializer.ReadValue(value.Payload, resolve);
                }
                catch (Exception e)
                {
                    throw new PlayEditException($"The game cannot read the value the editor sent ({e.GetType().Name}: {e.Message}); the game's code may differ from the editor's.");
                }
            case PlayEditValueKind.NewInstance:
                return CreateInstance(value.TypeName ?? string.Empty);
            default:
                throw new PlayEditException($"A value of kind {(byte)value.Kind} is not known.");
        }
    }

    private static object? CreateItem(PlayEditValue value, object collection, Func<PlayMirrorReference, object?> resolve)
    {
        var item = CreateValue(value, resolve);
        var itemType = PlayEditPath.ItemType(collection);
        if (item is null && itemType.IsValueType && Nullable.GetUnderlyingType(itemType) is null)
            return Activator.CreateInstance(itemType);
        if (item is not null && !itemType.IsInstanceOfType(item))
            throw new PlayEditException($"The items are {itemType.Name} in the game, and the editor sent a {item.GetType().Name}.");
        return item;
    }

    /// <summary>
    /// Makes a new instance by type name, in the game, with the type's own defaults (case 30: the editor's list of types comes
    /// from its own assemblies, which may differ from the game's).
    /// </summary>
    private static object CreateInstance(string typeName)
    {
        var type = AssemblyRegistry.GetType(typeName, throwOnError: false) ?? Type.GetType(typeName, throwOnError: false);
        if (type is null)
            throw new PlayEditException($"The game has no type {typeName}; the game's code may differ from the editor's.");
        if (type.IsAbstract || type.IsInterface)
            throw new PlayEditException($"{type.Name} is abstract in the game: no instance of it can be made.");
        try
        {
            return ObjectFactoryRegistry.NewInstance(type) ?? throw new PlayEditException($"The game cannot make a {type.Name}.");
        }
        catch (PlayEditException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new PlayEditException($"The game cannot make a {type.Name}: {e.GetBaseException().Message}");
        }
    }

    private static object? ResolveReference(PlayMirrorReference reference, Func<int, Entity?> findEntity)
    {
        var entity = reference.EntityHandle != 0 ? findEntity(reference.EntityHandle) : null;
        if (entity is null)
            return null;
        return reference.Kind == PlayMirrorReferenceKind.Entity ? entity : entity.Components.FirstOrDefault(x => x.Id == reference.ComponentId);
    }

    private static string Describe(Exception exception)
    {
        switch (exception)
        {
            case PlayEditException edit:
                return edit.Message;
            case TargetInvocationException { InnerException: { } inner }:
                return $"The setter threw {inner.GetType().Name}: {inner.Message}";
            default:
                var root = exception is ArgumentException or InvalidOperationException or NotSupportedException or IndexOutOfRangeException ? exception : exception.GetBaseException();
                return $"{root.GetType().Name}: {root.Message}";
        }
    }

    private static PlayEditResult Applied(PlayEdit edit, long frame) => new(edit.Id, frame, PlayEditStatus.Applied, PlayEditValue.Null, null);

    private static PlayEditResult Gone(PlayEdit edit, long frame, string error) => new(edit.Id, frame, PlayEditStatus.Gone, PlayEditValue.Null, error);
}
