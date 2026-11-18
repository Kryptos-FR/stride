// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The scene assets of the session, as the Live tab sees them: which entities a running scene's asset holds, and how to show
/// one of them in its scene editor.
/// </summary>
[Experimental("STREXP012")]
public interface ILiveSceneAssets
{
    /// <summary>
    /// The ids of the entities of the asset of a running scene.
    /// </summary>
    /// <param name="scene">The running scene: its id is its asset's, and its URL the asset's location.</param>
    /// <returns>The ids; <c>null</c> when the session has no asset for this scene (a scene made at run time).</returns>
    IReadOnlyCollection<Guid>? GetEntityIds(PlaySceneInfo scene);

    /// <summary>
    /// Opens the scene editor of a running scene's asset and selects one of its entities there.
    /// </summary>
    /// <param name="scene">The running scene whose asset holds the entity.</param>
    /// <param name="entityId">The id of the entity in the asset.</param>
    /// <returns>Whether the entity was found and selected.</returns>
    Task<bool> ShowInSceneEditorAsync(PlaySceneInfo scene, Guid entityId);
}
