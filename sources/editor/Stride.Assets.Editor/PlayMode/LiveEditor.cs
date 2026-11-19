// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using Stride.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Core.Assets;
using Stride.Core.Diagnostics;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.PlayMode.Protocol;
using Stride.PlayMode.Snapshots;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// A node presenter of the live grid that can show a value without sending it to the game: the value the game read back, and
/// the value typed while its edit is on its way.
/// </summary>
internal interface ILiveNodePresenter : INodePresenter
{
    /// <summary>
    /// Writes a value into the mirror only.
    /// </summary>
    void WriteLocal(object? value);

    /// <summary>
    /// Whether the presenter was disposed (the grid was built again).
    /// </summary>
    bool IsDisposed { get; }
}

/// <summary>
/// A row of the live grid for an item of a collection that the game may not hold, such as a material slot of a model that the
/// scene does not override: its edits go to that item.
/// </summary>
internal interface ILiveItemSlot : ILiveNodePresenter
{
    /// <summary>
    /// The collection of the item.
    /// </summary>
    INodePresenter Collection { get; }
}

/// <summary>
/// Turns the writes of the live grid into edits of the running game, and shows their answers.
/// </summary>
/// <remarks>
/// <para>
/// A write of the view (a value typed, a slider dragged, a check box ticked) shows the value at once in the grid, marked as
/// pending (<see cref="LiveEditData.StateKey"/>), and goes to the game as an edit (<see cref="PlaySession.EditAsync"/>). The
/// game's answer replaces it with the value the game read back (a setter may clamp it), or marks the property as refused, with
/// the error in its tooltip (<see cref="LiveEditData.TipKey"/>) and in the log.
/// </para>
/// <para>
/// Coalescing: a property has at most one edit on its way. The values written meanwhile wait, the last one replacing the
/// others, and the last goes as soon as the game answers the previous one, which it does at its next frame. So a slider
/// dragged or an arrow key held sends at most one edit per frame of the game per property, and the last value always goes.
/// </para>
/// <para>
/// Collection operations (add, insert, remove, move, a dictionary key renamed) go at once, in order, and change nothing in the
/// mirror: the snapshot the game sends after them shows the collection.
/// </para>
/// <para>
/// References: content (a model, a material, a prefab...) goes by its URL, which the game loads from its build; an entity or a
/// component goes by the agent's handle of its entity and its id (<see cref="SetReference"/> for one picked from the Live tab).
/// The game's answer reads back what the reference holds: the stand-in of the entity or the component, or the content's URL.
/// </para>
/// <para>
/// Live edits never reach the session's undo stack or an asset: the mirror belongs to no asset.
/// </para>
/// </remarks>
internal sealed class LiveEditor(Func<PlaySession?> session, Func<Entity, LiveMirrorReferences?> findMirror, Func<string, AssetId?>? findAsset, IDispatcherService dispatcher, Logger log)
{
    private readonly Dictionary<INodePresenter, PendingSet> sets = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<INodePresenter, int> operations = new(ReferenceEqualityComparer.Instance);
    // The nodes whose value a snapshot of an earlier frame than this one must not overwrite (their read-back is newer)
    private readonly Dictionary<(IGraphNode Node, NodeIndex Index), long> settled = [];
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private int generation;

    /// <summary>
    /// Raised on the UI thread when an edit is answered: the edit, its answer, and the time from the write to the answer.
    /// </summary>
    public event EventHandler<LiveEditEventArgs>? Answered;

    /// <summary>
    /// The edits sent to the game.
    /// </summary>
    public int SentEdits { get; private set; }

    /// <summary>
    /// The edits the game refused (or could not answer).
    /// </summary>
    public int RefusedEdits { get; private set; }

    /// <summary>
    /// The writes kept for a property whose edit was on its way, replaced by a later one before they could go.
    /// </summary>
    public int CoalescedWrites { get; private set; }

    /// <summary>
    /// The edits on their way.
    /// </summary>
    public int PendingEdits => sets.Values.Count(x => x.InFlight != 0) + operations.Values.Sum();

    /// <summary>
    /// Whether a value of the mirror must not be overwritten by a snapshot of this frame: its edit is on its way, or its
    /// read-back came from a later frame.
    /// </summary>
    public bool IsProtected(IGraphNode node, NodeIndex index, long frame)
    {
        foreach (var presenter in sets.Keys)
        {
            var accessor = presenter.GetNodeAccessor();
            if (accessor.Node == node && accessor.Index == index)
                return true;
        }
        if (settled.TryGetValue((node, index), out var settledFrame))
        {
            if (frame < settledFrame)
                return true;
            settled.Remove((node, index));
        }
        return false;
    }

    /// <summary>
    /// Forgets the edits on their way (the grid was built again or stopped following the game): their answers change nothing.
    /// </summary>
    public void Reset()
    {
        ++generation;
        sets.Clear();
        operations.Clear();
        settled.Clear();
    }

    /// <summary>
    /// Sends a value written into a property.
    /// </summary>
    /// <returns><c>false</c> when the value cannot be sent (the game stopped, the property is not part of a live component);
    /// nothing was written.</returns>
    public bool Set(ILiveNodePresenter presenter, object? value)
    {
        if (Locate(presenter) is not { } target || session() is not { State: PlaySessionState.Connected } playSession)
            return false;
        PlayEditValue encoded;
        try
        {
            encoded = Encode(value, target.References);
        }
        catch (Exception e)
        {
            log.Warning($"The live value of {target.Display} cannot be sent to the game: {e.Message}");
            return false;
        }

        // The value shows at once, marked as pending until the game answers
        var original = presenter.Value;
        presenter.WriteLocal(value);
        var started = clock.Elapsed;
        if (sets.TryGetValue(presenter, out var pending))
        {
            // An edit of this property is on its way: this value waits for its answer, and replaces the one waiting
            if (pending.Waiting is not null)
                ++CoalescedWrites;
            pending.Waiting = (encoded, started);
            return true;
        }
        pending = new PendingSet { Original = original };
        sets.Add(presenter, pending);
        SetState(presenter, LiveEditData.Pending, null);
        Send(playSession, presenter, pending, target, encoded, started);
        return true;
    }

    /// <summary>
    /// Sets a reference to a live entity, or to one of its components, picked in the editor (from the Live tab).
    /// </summary>
    /// <returns><c>false</c> when the reference cannot be sent (see <see cref="Set"/>), or the editor cannot make the stand-in
    /// of the component (its type is not in the game's build as the editor knows it).</returns>
    public bool SetReference(ILiveNodePresenter presenter, PlayMirrorReference reference)
    {
        if (Locate(presenter) is not { } target || session() is not { State: PlaySessionState.Connected } playSession)
            return false;
        object? standIn;
        if (reference.EntityHandle == target.References.Mirror.Handle)
        {
            // The entity of the grid itself, or one of its components
            var mirror = target.References.Mirror.Entity;
            standIn = reference.Kind == PlayMirrorReferenceKind.Entity ? mirror : mirror.Components.FirstOrDefault(x => x.Id == reference.ComponentId);
        }
        else if (playSession.CreateStandIn(reference) is { } made)
        {
            target.References.Remember(made.Map);
            standIn = made.StandIn;
        }
        else
        {
            standIn = null;
        }
        if (standIn is null)
        {
            log.Warning($"The reference of {target.Display} cannot be set to {reference.EntityName}: the editor does not know the type of its component ({reference.ComponentTypeName}).");
            return false;
        }
        return Set(presenter, standIn);
    }

    /// <summary>
    /// The live entity or component that a reference of the grid holds, or <c>null</c>.
    /// </summary>
    public PlayMirrorReference? FindReference(INodePresenter presenter)
    {
        if (presenter.Root.Value is not Entity entity || findMirror(entity) is not { } references)
            return null;
        var mirror = references.Mirror;
        return presenter.Value switch
        {
            Entity self when self == mirror.Entity => new PlayMirrorReference(PlayMirrorReferenceKind.Entity, mirror.Handle, self.Id, self.Name, Guid.Empty, null),
            EntityComponent own when own.Entity == mirror.Entity => new PlayMirrorReference(PlayMirrorReferenceKind.Component, mirror.Handle, mirror.Entity.Id, mirror.Entity.Name, own.Id, PlayMirrorSerializer.TypeNameOf(own.GetType())),
            { } value => references.Find(value),
            _ => null,
        };
    }

    /// <summary>
    /// Adds an item at the end of the list of <paramref name="collection"/>.
    /// </summary>
    public bool Add(ILiveNodePresenter collection, object? value)
        => SendOperation(collection, "an item added", PlayEditOperation.Add, null, value, null);

    /// <summary>
    /// Inserts an item in the list of <paramref name="collection"/>, or adds it to its dictionary under a key.
    /// </summary>
    public bool Insert(ILiveNodePresenter collection, object? value, NodeIndex index)
        => SendOperation(collection, "an item added", PlayEditOperation.Insert, index, value, null);

    /// <summary>
    /// Removes an item of the list or the dictionary of <paramref name="collection"/>.
    /// </summary>
    public bool Remove(ILiveNodePresenter collection, NodeIndex index)
        => SendOperation(collection, "an item removed", PlayEditOperation.Remove, index, null, null);

    /// <summary>
    /// Moves an item of the list of <paramref name="collection"/> to another position, or gives an item of its dictionary
    /// another key. The item itself moves in the game.
    /// </summary>
    public bool Move(ILiveNodePresenter collection, NodeIndex from, NodeIndex to)
        => SendOperation(collection, "an item moved", PlayEditOperation.Move, from, null, to);

    private bool SendOperation(ILiveNodePresenter collection, string what, PlayEditOperation operation, NodeIndex? index, object? value, NodeIndex? target)
    {
        if (Locate(collection) is not { } located || session() is not { State: PlaySessionState.Connected } playSession)
            return false;
        PlayEdit edit;
        try
        {
            var path = located.Path.ToList();
            if (index is { } item)
                path.Add(Segment(collection, item, located.References));
            var targetSegment = target is { } to ? Segment(collection, to, located.References) : null;
            var encoded = operation is PlayEditOperation.Add or PlayEditOperation.Insert ? Encode(value, located.References) : PlayEditValue.Null;
            edit = new PlayEdit(playSession.NextEditId(), located.Handle, located.ComponentId, path, operation, encoded, targetSegment);
        }
        catch (Exception e)
        {
            log.Warning($"{what} in {located.Display} cannot be sent to the game: {e.Message}");
            return false;
        }

        operations[collection] = operations.GetValueOrDefault(collection) + 1;
        SetState(collection, LiveEditData.Pending, null);
        ++SentEdits;
        var started = clock.Elapsed;
        var sentGeneration = generation;
        playSession.EditAsync(edit).ContinueWith(task => dispatcher.InvokeAsync(() =>
        {
            if (sentGeneration != generation || task.Result is not { } result)
                return;
            var left = operations.GetValueOrDefault(collection) - 1;
            if (left > 0)
                operations[collection] = left;
            else
                operations.Remove(collection);
            Answer(collection, edit, result, started, left > 0, applyReadBack: false);
        }), TaskScheduler.Default).Forget();
        return true;
    }

    private void Send(PlaySession playSession, ILiveNodePresenter presenter, PendingSet pending, Target target, PlayEditValue value, TimeSpan started)
    {
        var edit = new PlayEdit(playSession.NextEditId(), target.Handle, target.ComponentId, target.Path, PlayEditOperation.Set, value);
        pending.InFlight = edit.Id;
        ++SentEdits;
        var sentGeneration = generation;
        playSession.EditAsync(edit).ContinueWith(task => dispatcher.InvokeAsync(() =>
        {
            if (sentGeneration == generation && task.Result is { } result)
                OnSetAnswered(playSession, presenter, pending, target, edit, result, started);
        }), TaskScheduler.Default).Forget();
    }

    private void OnSetAnswered(PlaySession playSession, ILiveNodePresenter presenter, PendingSet pending, Target target, PlayEdit edit, PlayEditResult result, TimeSpan started)
    {
        if (!sets.TryGetValue(presenter, out var current) || current != pending || pending.InFlight != edit.Id)
            return;
        pending.InFlight = 0;
        if (pending.Waiting is { } waiting && !presenter.IsDisposed && playSession.State == PlaySessionState.Connected)
        {
            // A later value waits: it goes now, and this answer is already old (its read-back would undo the later value)
            pending.Waiting = null;
            Answer(presenter, edit, result, started, stillPending: true, applyReadBack: false);
            Send(playSession, presenter, pending, target, waiting.Value, waiting.Started);
            return;
        }
        sets.Remove(presenter);
        Answer(presenter, edit, result, started, stillPending: false, applyReadBack: true, target, pending.Original);
    }

    private void Answer(ILiveNodePresenter presenter, PlayEdit edit, PlayEditResult result, TimeSpan started, bool stillPending, bool applyReadBack, Target? target = null, object? original = null)
    {
        var elapsed = clock.Elapsed - started;
        // The game stopped before it answered (the session answers for it): nothing was refused, the grid is closing
        var cutShort = result.Status == PlayEditStatus.Failed && session() is not { State: PlaySessionState.Connected };
        var refused = !cutShort && result.Status is PlayEditStatus.Failed or PlayEditStatus.Gone;
        if (refused)
        {
            ++RefusedEdits;
            // The Output pane says it too (PlayModeService), for the edits of any view
            log.Verbose($"The game refused the live edit of {Describe(presenter, edit)}: {result.Error}");
        }
        if (!presenter.IsDisposed)
        {
            if (applyReadBack && !cutShort && edit.Operation == PlayEditOperation.Set)
            {
                if (target is not null && IsReference(presenter))
                    ApplyReferenceReadBack(presenter, target.References, result, original);
                else if (result.ReadBack.Kind != PlayEditValueKind.NewInstance)
                    ApplyReadBack(presenter, result);
            }
            if (stillPending)
                SetState(presenter, LiveEditData.Pending, null);
            else if (refused)
                SetState(presenter, LiveEditData.Refused, result.Error);
            else
                SetState(presenter, null, null);
        }
        Answered?.Invoke(this, new LiveEditEventArgs(edit, result, elapsed));
    }

    private void ApplyReadBack(ILiveNodePresenter presenter, PlayEditResult result)
    {
        // Only values: an object's members come with the snapshot that follows the edit
        var type = presenter.Type;
        if (!(type.IsValueType || type == typeof(string) || presenter.Factory.IsPrimitiveType(type)))
            return;
        if (result.Status is PlayEditStatus.Gone || result.Status is PlayEditStatus.Failed && result.ReadBack.Kind == PlayEditValueKind.Null && type.IsValueType)
            return;
        object? value;
        try
        {
            value = result.ReadBack.Kind == PlayEditValueKind.Null ? null : PlayMirrorSerializer.ReadValue(result.ReadBack.Payload, static _ => null);
        }
        catch (Exception e)
        {
            log.Warning($"The value the game read back for {presenter.Name} could not be read: {e.Message}");
            return;
        }
        var accessor = presenter.GetNodeAccessor();
        settled[(accessor.Node, accessor.Index)] = result.Frame;
        if (!Equals(value, presenter.Value))
            presenter.WriteLocal(value);
    }

    private void ApplyReferenceReadBack(ILiveNodePresenter presenter, LiveMirrorReferences references, PlayEditResult result, object? original)
    {
        object? value;
        if (result.Status == PlayEditStatus.Gone)
        {
            // Nothing changed in the game (the entity edited, or the one referenced, left it)
            value = original;
        }
        else
        {
            try
            {
                value = ReadReference(result.ReadBack, references);
            }
            catch (Exception e)
            {
                log.Warning($"The reference the game read back for {presenter.Name} could not be read: {e.Message}");
                return;
            }
        }
        var accessor = presenter.GetNodeAccessor();
        settled[(accessor.Node, accessor.Index)] = result.Frame;
        // The same live object or content: the value shown stays (no write, no refresh of the property)
        if (!SameReference(value, presenter.Value, references))
            presenter.WriteLocal(value);
    }

    /// <summary>
    /// Reads a reference the game read back: the stand-in of an entity or a component (the grid's own entity, or a stand-in it
    /// remembers), or content by URL mapped to its asset.
    /// </summary>
    private object? ReadReference(PlayEditValue readBack, LiveMirrorReferences references)
    {
        if (readBack.Kind != PlayEditValueKind.Value)
            return null;
        var mirror = references.Mirror;
        var value = PlayMirrorSerializer.ReadValue(readBack.Payload, reference =>
        {
            if (reference.EntityHandle == mirror.Handle)
                return reference.Kind == PlayMirrorReferenceKind.Entity ? mirror.Entity : mirror.Entity.Components.FirstOrDefault(x => x.Id == reference.ComponentId);
            if (session()?.CreateStandIn(reference) is not { } made)
                return null;
            references.Remember(made.Map);
            return made.StandIn;
        });
        if (findAsset is not null && AttachedReferenceManager.GetAttachedReference(value) is { } content && content.Id == AssetId.Empty && !string.IsNullOrEmpty(content.Url) && findAsset(content.Url) is { } id)
            content.Id = id;
        return value;
    }

    private static bool SameReference(object? a, object? b, LiveMirrorReferences references)
    {
        if (a is null || b is null)
            return a is null && b is null;
        if (ReferenceEquals(a, b))
            return true;
        var contentA = AttachedReferenceManager.GetAttachedReference(a);
        var contentB = AttachedReferenceManager.GetAttachedReference(b);
        if (contentA is not null || contentB is not null)
            return contentA?.Url == contentB?.Url && a.GetType() == b.GetType();
        if (a is UrlReferenceBase urlA && b is UrlReferenceBase urlB)
            return urlA.Url == urlB.Url && a.GetType() == b.GetType();
        return KeyOf(a, references) is { } key && Equals(key, KeyOf(b, references));
    }

    private static (PlayMirrorReferenceKind Kind, int Handle, Guid ComponentId)? KeyOf(object value, LiveMirrorReferences references)
    {
        var mirror = references.Mirror;
        return value switch
        {
            Entity entity when entity == mirror.Entity => (PlayMirrorReferenceKind.Entity, mirror.Handle, Guid.Empty),
            EntityComponent component when component.Entity == mirror.Entity => (PlayMirrorReferenceKind.Component, mirror.Handle, component.Id),
            _ => references.Find(value) is { } found ? (found.Kind, found.EntityHandle, found.ComponentId) : null,
        };
    }

    /// <summary>
    /// Whether a property holds a reference: to an entity, a component or content.
    /// </summary>
    internal static bool IsReference(INodePresenter presenter)
        => IsReferenceType(presenter.Type) || presenter.Value is Entity or EntityComponent || AttachedReferenceManager.GetAttachedReference(presenter.Value) is not null;

    /// <summary>
    /// Whether a type is a reference to an entity, a component (an interface that components implement too) or content.
    /// </summary>
    internal static bool IsReferenceType(Type type)
        => typeof(Entity).IsAssignableFrom(type) || typeof(EntityComponent).IsAssignableFrom(type)
            || type.IsInterface && type.IsImplementedOnAny<EntityComponent>()
            || AssetRegistry.CanBeAssignedToContentTypes(type, checkIsUrlType: true);

    private static void SetState(INodePresenter presenter, string? state, string? error)
    {
        var properties = presenter.AttachedProperties;
        var oldState = properties.TryGetValue(LiveEditData.StateKey, out var previousState) ? previousState : null;
        var oldTip = properties.TryGetValue(LiveEditData.TipKey, out var previousTip) ? previousTip : null;
        var tip = state switch
        {
            LiveEditData.Refused => string.Format("The game refused this value: {0}", error),
            LiveEditData.Pending => "Sent to the game: waiting for the value it reads back.",
            _ => properties.TryGetValue(LiveEditData.ReadOnlyReasonKey, out var reason) ? reason : null,
        };
        // Each change refreshes the property's view: only real changes are written
        if (oldState == state && oldTip == tip)
            return;
        if (state is null)
            properties.Remove(LiveEditData.StateKey);
        else
            properties.Set(LiveEditData.StateKey, state);
        if (tip is null)
            properties.Remove(LiveEditData.TipKey);
        else
            properties.Set(LiveEditData.TipKey, tip);
    }


    private Target? Locate(INodePresenter presenter)
    {
        if (presenter.IsDisposedPresenter() || presenter.Root.Value is not Entity entity || findMirror(entity) is not { } references)
            return null;
        var path = new List<PlayPathSegment>();
        var componentId = Guid.Empty;
        string? componentName = null;
        for (var current = presenter; current != presenter.Root; current = current.Parent!)
        {
            if (current.Parent is null)
                return null;
            // The component the path starts from: an item of the entity's components
            if (current is ItemNodePresenter { Value: EntityComponent component } && current.Parent is MemberNodePresenter { Type: var type } && type == typeof(EntityComponentCollection) && current.Parent.Parent == presenter.Root)
            {
                componentId = component.Id;
                componentName = component.GetType().Name;
                break;
            }
            path.Insert(0, current switch
            {
                ILiveItemSlot slot => Segment(slot.Collection, slot.Index, references),
                MemberNodePresenter member => PlayPathSegment.Member(member.MemberDescriptor.MemberInfo?.Name ?? member.Name),
                ItemNodePresenter item => Segment(item.OwnerCollection, item.Index, references),
                _ => throw new InvalidOperationException($"{current.Name} is computed by the editor: it has no value in the game."),
            });
        }
        var display = $"{references.Mirror.Name} > {(componentName is null ? string.Empty : componentName + ".")}{PlayPathSegment.Format(path)}";
        return new Target(references.Mirror.Handle, componentId, path, references, display);
    }

    private static PlayPathSegment Segment(INodePresenter collection, NodeIndex index, LiveMirrorReferences references)
    {
        if (collection.Descriptor is DictionaryDescriptor)
            return PlayPathSegment.ForKey(PlayMirrorSerializer.WriteValue(index.Value, x => HandleOf(x, references)), index.Value?.ToString());
        if (index.Value is int position)
            return PlayPathSegment.Item(position);
        throw new InvalidOperationException($"{collection.Name} is not a list or a dictionary.");
    }

    private static PlayEditValue Encode(object? value, LiveMirrorReferences references)
    {
        if (value is null)
            return PlayEditValue.Null;
        var type = value.GetType();
        switch (value)
        {
            // A live entity or component: by the handle of its entity (and its id)
            case Entity or EntityComponent:
                return PlayEditValue.Of(PlayMirrorSerializer.WriteValue(value, x => HandleOf(x, references)));
            // Content: by its URL, which the game loads from its build
            case not null when AttachedReferenceManager.GetAttachedReference(value) is { } content:
                if (string.IsNullOrEmpty(content.Url))
                    throw new InvalidOperationException($"the {type.Name} was made by the game at run time: it has no URL to send.");
                return PlayEditValue.Of(PlayMirrorSerializer.WriteValue(value, x => HandleOf(x, references)));
            case UrlReferenceBase:
                return PlayEditValue.Of(PlayMirrorSerializer.WriteValue(value, x => HandleOf(x, references)));
            // Content made at run time, which the grid shows without a URL
            case not null when !type.IsValueType && AssetRegistry.IsExactContentType(type):
                throw new InvalidOperationException($"the {type.Name} was made by the game at run time: it has no URL to send.");
            // An object of a class: the game makes its own of that type (the grid makes new instances by type)
            case not string when !type.IsValueType:
                return PlayEditValue.NewInstanceOf(PlayMirrorSerializer.TypeNameOf(type));
            default:
                return PlayEditValue.Of(PlayMirrorSerializer.WriteValue(value, x => HandleOf(x, references)));
        }
    }

    private static int HandleOf(Entity entity, LiveMirrorReferences references)
        => entity == references.Mirror.Entity ? references.Mirror.Handle : references.Find(entity)?.EntityHandle ?? 0;

    private string Describe(INodePresenter presenter, PlayEdit edit)
        => presenter.Root.Value is Entity entity && findMirror(entity) is { } references ? $"{references.Mirror.Name} > {edit.PathText}" : edit.PathText;

    private sealed record Target(int Handle, Guid ComponentId, IReadOnlyList<PlayPathSegment> Path, LiveMirrorReferences References, string Display);

    private sealed class PendingSet
    {
        public int InFlight { get; set; }

        // The value before the first write: the game keeps it when it refuses the edit and reads nothing back
        public object? Original { get; init; }

        public (PlayEditValue Value, TimeSpan Started)? Waiting { get; set; }
    }
}

/// <summary>
/// An edit of the live grid answered (<see cref="LivePropertiesViewModel.EditAnswered"/>).
/// </summary>
/// <param name="Edit">The edit sent.</param>
/// <param name="Result">The answer.</param>
/// <param name="Elapsed">The time from the write in the grid to the answer on the UI thread (the read-back shown).</param>
[System.Diagnostics.CodeAnalysis.Experimental("STREXP012")]
public sealed record LiveEditEventArgs(PlayEdit Edit, PlayEditResult Result, TimeSpan Elapsed);

internal static class LiveNodePresenterExtensions
{
    /// <summary>
    /// Whether the presenter belongs to a grid that was built again.
    /// </summary>
    public static bool IsDisposedPresenter(this INodePresenter presenter) => presenter is ILiveNodePresenter { IsDisposed: true };
}
