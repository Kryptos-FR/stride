// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Translation;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The Live tab, a panel of the main window: the scenes and entities of the game that plays.
/// </summary>
[Experimental("STREXP012")]
public static class LivePanel
{
    /// <summary>
    /// The identifier of the panel.
    /// </summary>
    public const string Id = "Stride.Live";

    /// <summary>
    /// Creates the descriptor of the panel: on the left, next to the solution explorer, hidden until a game connects.
    /// </summary>
    /// <param name="createViewModel">Creates its view model (<see cref="LivePanelViewModel"/>).</param>
    public static PanelDescriptor Create(Func<PanelContext, IPanelViewModel> createViewModel)
    {
        ArgumentNullException.ThrowIfNull(createViewModel);
        return new PanelDescriptor
        {
            Id = Id,
            Title = () => Tr._p("Panel", "Live"),
            MenuGroup = BuiltInPanels.ExploreMenuGroup,
            Order = 130,
            DefaultPlacement = PanelPlacement.Left,
            VisibleByDefault = false,
            HiddenWhenSessionOpens = true,
            CreateViewModel = createViewModel,
        };
    }
}
