// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Assets.Presentation.ViewModels;

namespace Stride.Core.Assets.Editor.Services;

/// <summary>
/// What an asset picker lists and how (<see cref="IAssetPickerService.OpenAssetPickerAsync(ISessionViewModel, AssetPickerOptions)"/>):
/// beyond the accepted types and the filter, assets listed but not pickable, with the reason, a note on some assets, and a
/// cancellation that closes the picker.
/// </summary>
/// <param name="AcceptedTypes">The types of asset that can be picked; empty to accept any asset.</param>
[Experimental("STREXP012")]
public sealed record AssetPickerOptions(IEnumerable<Type> AcceptedTypes)
{
    /// <summary>
    /// An additional filter on the listed assets, or <c>null</c>.
    /// </summary>
    public Func<AssetViewModel, bool>? Filter { get; init; }

    /// <summary>
    /// Why a listed asset cannot be picked (it is shown disabled, with the reason), or <c>null</c> when it can; <c>null</c> for
    /// none disabled.
    /// </summary>
    public Func<AssetViewModel, string?>? DisabledReason { get; init; }

    /// <summary>
    /// A note shown on a listed asset that can be picked (it is marked, with the note), or <c>null</c>; <c>null</c> for none.
    /// </summary>
    public Func<AssetViewModel, string?>? Note { get; init; }

    /// <summary>
    /// The asset selected when the picker opens, or <c>null</c>.
    /// </summary>
    public AssetViewModel? InitialAsset { get; init; }

    /// <summary>
    /// The folder shown when the picker opens if there is no <see cref="InitialAsset"/>, or <c>null</c>.
    /// </summary>
    public DirectoryBaseViewModel? InitialLocation { get; init; }

    /// <summary>
    /// A message shown at the top of the picker, or <c>null</c>.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Closes the picker without a pick when cancelled (what the asset was picked for is gone).
    /// </summary>
    public CancellationToken Cancellation { get; init; }
}
