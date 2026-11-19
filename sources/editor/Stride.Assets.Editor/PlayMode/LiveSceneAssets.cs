// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Assets.Editor.ViewModels;
using Stride.Assets.Entities;
using Stride.Assets.Presentation.ViewModels;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Extensions;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// The scene assets of a session, for the Live tab: the running scenes are found by their asset's id, or by their URL.
/// </summary>
/// <param name="session">The session.</param>
[Experimental("STREXP012")]
public sealed class LiveSceneAssets(ISessionViewModel session) : ILiveSceneAssets
{
    /// <inheritdoc/>
    public IReadOnlyCollection<Guid>? GetEntityIds(PlaySceneInfo scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return FindScene(scene)?.Asset.Hierarchy.Parts.Keys.ToHashSet();
    }

    /// <inheritdoc/>
    public async Task<bool> ShowInSceneEditorAsync(PlaySceneInfo scene, Guid entityId)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (FindScene(scene) is not { } asset || session.ServiceProvider.TryGet<IAssetEditorsManager>() is not { } editors)
            return false;

        await editors.OpenAssetEditorAsync(asset);
        if (!editors.TryGetAssetEditor<EntityHierarchyEditorViewModel>(asset, out var editor)
            || editor.FindPartViewModel(new AbsoluteId(asset.Id, entityId)) is not EntityViewModel entity)
            return false;

        editor.ClearSelection();
        editor.SelectedContent.Add(entity);
        // The camera moves onto it once the editor's game runs; a scene editor that just opened selects it only
        editor.FocusOnEntityAsync(entity).Forget();
        return true;
    }

    private SceneViewModel? FindScene(PlaySceneInfo scene) => FindScene(session, scene);

    /// <summary>
    /// The scene asset of a running scene: by its asset's id, else by its URL.
    /// </summary>
    internal static SceneViewModel? FindScene(ISessionViewModel session, PlaySceneInfo scene)
    {
        if (session.GetAssetById(new AssetId(scene.Id)) is SceneViewModel byId)
            return byId;
        return string.IsNullOrEmpty(scene.Url) ? null : session.AllAssets.OfType<SceneViewModel>().FirstOrDefault(x => string.Equals(x.Url, scene.Url, StringComparison.OrdinalIgnoreCase));
    }
}
