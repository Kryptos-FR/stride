// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core;
using Stride.Core.Presentation.Quantum.Presenters;

namespace Stride.Assets.Editor.Quantum.NodePresenters.Keys;

/// <summary>
/// What the live grid of a running game says on a property: an edit on its way or refused, and why a property cannot change.
/// The view reads them by name (<c>[LiveEditState]</c>, <c>[LiveEditTip]</c>).
/// </summary>
[Experimental("STREXP012")]
public static class LiveEditData
{
    /// <summary>The name of <see cref="StateKey"/>.</summary>
    public const string State = "LiveEditState";

    /// <summary>The name of <see cref="TipKey"/>.</summary>
    public const string Tip = "LiveEditTip";

    /// <summary>The name of <see cref="ReadOnlyReasonKey"/>.</summary>
    public const string ReadOnlyReason = "LiveReadOnlyReason";

    /// <summary>The name of <see cref="ApplyToAssetKey"/>.</summary>
    public const string ApplyToAsset = "LiveApplyToAsset";

    /// <summary>The name of <see cref="ApplyComponentKey"/>.</summary>
    public const string ApplyComponent = "LiveApplyComponent";

    /// <summary>The value of <see cref="StateKey"/> while an edit of the property is on its way to the game.</summary>
    public const string Pending = "Pending";

    /// <summary>The value of <see cref="StateKey"/> once the game refused the property's last edit.</summary>
    public const string Refused = "Refused";

    /// <summary>
    /// The state of the property's last edit: <see cref="Pending"/>, <see cref="Refused"/>, or none once the game took it. Several
    /// entities selected: pending if one is, else refused if one is.
    /// </summary>
    public static readonly PropertyKey<string> StateKey = new(State, typeof(LiveEditData), new PropertyCombinerMetadata(CombineState));

    /// <summary>
    /// The tooltip of the property's editor: why the game refused the last edit, that an edit is on its way, or why the property
    /// cannot change live (<see cref="ReadOnlyReasonKey"/>).
    /// </summary>
    public static readonly PropertyKey<string> TipKey = new(Tip, typeof(LiveEditData), new PropertyCombinerMetadata(CombineTips));

    /// <summary>
    /// Why the property cannot change while the game runs (a reference, a member the engine reads once), when it cannot.
    /// </summary>
    public static readonly PropertyKey<string> ReadOnlyReasonKey = new(ReadOnlyReason, typeof(LiveEditData), new PropertyCombinerMetadata(CombineTips));

    /// <summary>
    /// Set on a property whose value can be applied to the asset of the entity (Apply to asset): a member of a component, or of
    /// the entity itself. The view shows the command where it is (<c>[HasAssociatedData_LiveApplyToAsset]</c>).
    /// </summary>
    public static readonly PropertyKey<bool> ApplyToAssetKey = new(ApplyToAsset, typeof(LiveEditData), new PropertyCombinerMetadata(CombineAll));

    /// <summary>
    /// Set on a component's header: its values can be applied to the asset (Apply component to asset...).
    /// </summary>
    public static readonly PropertyKey<bool> ApplyComponentKey = new(ApplyComponent, typeof(LiveEditData), new PropertyCombinerMetadata(CombineAll));

    private static object? CombineAll(IEnumerable<object> values)
        => values.All(x => x is true) ? true : null;

    private static object? CombineState(IEnumerable<object> states)
    {
        var all = states.OfType<string>().ToList();
        return all.Contains(Pending) ? Pending : all.Contains(Refused) ? Refused : null;
    }

    private static object? CombineTips(IEnumerable<object> tips)
    {
        var all = tips.OfType<string>().Distinct().ToList();
        return all.Count == 0 ? null : string.Join(Environment.NewLine, all);
    }
}
