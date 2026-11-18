// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Core.Translation;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The scene a game starts on instead of its default scene (Play this scene), with the default scene it replaces: the Live
/// tab, the status bar and the Output pane say "Playing Level2 instead of MainScene".
/// </summary>
/// <param name="Url">The URL of the scene in the game's data.</param>
/// <param name="Name">The name of the scene, as the editor shows it.</param>
/// <param name="DefaultSceneUrl">The URL of the game's default scene, or <c>null</c> when the game has none.</param>
/// <param name="DefaultSceneName">The name of the game's default scene, or <c>null</c> when the game has none.</param>
[Experimental("STREXP012")]
public sealed record PlaySceneRequest(string Url, string Name, string? DefaultSceneUrl = null, string? DefaultSceneName = null)
{
    /// <summary>
    /// The scene that plays, and the one it replaces: "Playing Level2 instead of MainScene".
    /// </summary>
    public string Description => DefaultSceneName is null
        ? string.Format(Tr._p("Play", "Playing {0}"), Name)
        : string.Format(Tr._p("Play", "Playing {0} instead of {1}"), Name, DefaultSceneName);

    /// <summary>
    /// The game started on its default scene instead of this one, because its built data does not have it.
    /// </summary>
    public string NotInBuildDescription => DefaultSceneName is null
        ? string.Format(Tr._p("Play", "{0} is not in the game's built data: the game started without it."), Name)
        : string.Format(Tr._p("Play", "{0} is not in the game's built data: the game started on {1}."), Name, DefaultSceneName);

    /// <summary>
    /// Whether a scene that the game loaded, by the URL it loaded it with, is this one. The game may load a scene by its URL
    /// with the package's namespace (<c>/MyGame/Level2</c>) or without it (<c>Level2</c>).
    /// </summary>
    /// <param name="loadedUrl">The URL the game loaded the scene with; <c>null</c> when unknown.</param>
    public bool IsScene(string? loadedUrl) => IsSameUrl(Url, loadedUrl);

    /// <summary>
    /// Whether a scene that the game loaded, by the URL it loaded it with, is the game's default scene.
    /// </summary>
    /// <param name="loadedUrl">The URL the game loaded the scene with; <c>null</c> when unknown.</param>
    public bool IsDefaultScene(string? loadedUrl) => DefaultSceneUrl is not null && IsSameUrl(DefaultSceneUrl, loadedUrl);

    private static bool IsSameUrl(string url, string? other)
    {
        if (other is null)
            return false;
        var a = url.TrimStart('/');
        var b = other.TrimStart('/');
        return string.Equals(a, b, StringComparison.Ordinal)
            || a.EndsWith("/" + b, StringComparison.Ordinal)
            || b.EndsWith("/" + a, StringComparison.Ordinal);
    }
}
