// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Editor.Commands;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Translation;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// The commands of the property grid's context menu that copy values of the running game into the scene assets: Apply to asset
/// on a property, and Apply component to asset... on a component's header. They act on the property the menu opened on (the
/// context's selection), in the live grid.
/// </summary>
[Experimental("STREXP012")]
public static class LiveApplyCommands
{
    /// <summary>
    /// The identifier of the command that sets the property of the scene asset to the live value.
    /// </summary>
    public const string ApplyToAssetId = "Stride.PlayMode.ApplyToAsset";

    /// <summary>
    /// The identifier of the command that lists the live values of a component that differ from the asset's, to apply them.
    /// </summary>
    public const string ApplyComponentToAssetId = "Stride.PlayMode.ApplyComponentToAsset";

    /// <summary>
    /// Creates the commands.
    /// </summary>
    public static IReadOnlyList<CommandDescriptor> Create() =>
    [
        new()
        {
            Id = ApplyToAssetId,
            Category = CommandCategories.Edit,
            Title = () => Tr._p("Menu", "Apply to asset"),
            ContextTitle = context => Find(context) is { } found && found.Grid.GetApplyState(found.Node.NodePresenters) is { Entities: > 1 } state
                ? string.Format(Tr._p("Menu", "Apply to asset ({0} of {1} entities)"), state.Applicable, state.Entities)
                : Tr._p("Menu", "Apply to asset"),
            Keywords = ["play", "live", "save", "keep", "scene"],
            ShowInPalette = false,
            CanExecute = context => Find(context) is { } found && found.Grid.GetApplyState(found.Node.NodePresenters).CanApply,
            DisabledReason = context => Find(context) is { } found ? found.Grid.GetApplyState(found.Node.NodePresenters).Reason : null,
            ExecuteAsync = context =>
            {
                if (Find(context) is { } found)
                    found.Grid.ApplyToAsset(found.Node.NodePresenters);
                return Task.CompletedTask;
            },
        },
        new()
        {
            Id = ApplyComponentToAssetId,
            Category = CommandCategories.Edit,
            Title = () => Tr._p("Menu", "Apply component to asset..."),
            Keywords = ["play", "live", "save", "keep", "scene"],
            ShowInPalette = false,
            CanExecute = context => Find(context) is { } found && found.Grid.GetApplyState(found.Node.NodePresenters, wholeComponent: true).CanApply,
            DisabledReason = context => Find(context) is { } found ? found.Grid.GetApplyState(found.Node.NodePresenters, wholeComponent: true).Reason : null,
            ExecuteAsync = async context =>
            {
                if (Find(context) is { } found)
                    await found.Grid.ApplyComponentToAssetAsync(found.Node.NodePresenters);
            },
        },
    ];

    // The property the menu opened on, in the live grid
    private static (LivePropertiesViewModel Grid, NodeViewModel Node)? Find(CommandContext context)
    {
        if (context.Session.ActiveProperties is not LivePropertiesViewModel grid || context.Selection is not [NodeViewModel node, ..])
            return null;
        return (grid, node);
    }
}
