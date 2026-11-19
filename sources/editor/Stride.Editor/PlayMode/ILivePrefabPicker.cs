// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Stride.Editor.PlayMode;

/// <summary>
/// Lets the user pick a prefab of the game's build, to spawn it in the running game from the Live tab.
/// </summary>
[Experimental("STREXP012")]
public interface ILivePrefabPicker
{
    /// <summary>
    /// Opens the picker of the prefabs: those of the game's build (<see cref="PlaySession.BuildContent"/>) can be picked, the
    /// others are listed disabled with the reason.
    /// </summary>
    /// <param name="session">The play session of the running game.</param>
    /// <param name="cancellationToken">Closes the picker without a pick (the game stopped).</param>
    /// <returns>The URL of the prefab in the game's build, or <c>null</c> when nothing was picked.</returns>
    Task<string?> PickPrefabAsync(PlaySession session, CancellationToken cancellationToken);
}
