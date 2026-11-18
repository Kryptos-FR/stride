// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Assets.Entities;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Translation;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The scene that Play this scene would start the game on: the scene of the session's active document, and whether the game's
/// build has it. When there is none, <see cref="DisabledReason"/> says why, for the command's tooltip.
/// </summary>
/// <remarks>
/// A scene is in the build when a root asset of the current project's build reaches it: a root asset of the project or of one
/// of its dependencies, the game settings' default scene, or an asset that references it. A child scene is not loaded with its
/// parent at run time, so it is in the build only when something else reaches it.
/// </remarks>
[Experimental("STREXP012")]
public sealed record PlaySceneTarget
{
    /// <summary>
    /// The scene of the active document; <c>null</c> when the active document is not a scene, or when there is none.
    /// </summary>
    public AssetViewModel? Scene { get; init; }

    /// <summary>
    /// The default scene of the current project's game settings; <c>null</c> when it has none.
    /// </summary>
    public AssetViewModel? DefaultScene { get; init; }

    /// <summary>
    /// Why the scene cannot be played; <c>null</c> when it can.
    /// </summary>
    public string? DisabledReason { get; init; }

    /// <summary>
    /// Whether the scene can be played: Play this scene can run.
    /// </summary>
    public bool CanPlay => DisabledReason is null && Scene is not null;

    /// <summary>
    /// Whether the scene is the game's default scene: Play this scene then does what Start project does.
    /// </summary>
    public bool IsDefaultScene => Scene is not null && Scene == DefaultScene;

    /// <summary>
    /// Whether the current project's build has the scene: a root asset reaches it. A scene that it does not have must be made a
    /// root asset first, or the game cannot load it.
    /// </summary>
    public bool IsInBuild { get; init; }

    /// <summary>
    /// What the game is asked to start on: this scene instead of the default one; <c>null</c> when it cannot be played or is the
    /// default scene, so that the game starts as with Start project.
    /// </summary>
    public PlaySceneRequest? ToRequest()
        => !CanPlay || IsDefaultScene ? null : new PlaySceneRequest(Scene!.Url, Scene.Name, DefaultScene?.Url, DefaultScene?.Name);

    /// <summary>
    /// Finds the scene of the active document of a session.
    /// </summary>
    /// <param name="session">The session; <c>null</c> when none is open.</param>
    public static PlaySceneTarget Find(SessionViewModel? session)
    {
        if (session is null)
            return new PlaySceneTarget { DisabledReason = Tr._p("Play", "Open a project to play one of its scenes.") };
        if (session.CurrentProject is not { } project)
            return new PlaySceneTarget { DisabledReason = Tr._p("Play", "There is no current project to start: set the game's project as the current project.") };
        var active = session.EditorCollection.ActiveEditor;
        if (active?.Asset is not { } asset)
            return new PlaySceneTarget { DisabledReason = Tr._p("Play", "No scene is open: open a scene to play it.") };
        if (!typeof(SceneAsset).IsAssignableFrom(asset.AssetType))
            return new PlaySceneTarget { DisabledReason = string.Format(Tr._p("Play", "The active document, {0}, is not a scene: select the tab of a scene to play it."), asset.Name) };
        if (asset.IsDeleted)
            return new PlaySceneTarget { DisabledReason = string.Format(Tr._p("Play", "The scene {0} was deleted."), asset.Name) };
        if (!project.IsInScope(asset))
            return new PlaySceneTarget { Scene = asset, DisabledReason = string.Format(Tr._p("Play", "{0} is not in a package that {1} builds: set the project of its game as the current project."), asset.Name, project.Name) };

        // The game settings, a root asset of every build, reach the default scene
        var defaultScene = SessionEditorsRestorer.FindDefaultScene(session);
        return new PlaySceneTarget
        {
            Scene = asset,
            DefaultScene = defaultScene,
            IsInBuild = asset == defaultScene || asset.Dependencies.IsRoot || asset.Dependencies.IsIndirectlyIncluded,
        };
    }
}
