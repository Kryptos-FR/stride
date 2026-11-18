// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Stride.Editor.PlayMode;

/// <summary>
/// The live entities selected in the Live tab: what the property grid shows while a game plays.
/// </summary>
/// <remarks>
/// The tab subscribes the session to these entities (<see cref="PlaySession.Subscribe"/>), so their snapshots arrive through
/// <see cref="PlaySession.SnapshotReceived"/>; a view of their properties reads them there, by handle.
/// </remarks>
/// <param name="Session">The session that plays the game.</param>
/// <param name="Handles">The agent's handles of the selected entities (<see cref="Stride.PlayMode.Protocol.PlayEntityInfo.Handle"/>),
/// in the order they were selected; never empty.</param>
[Experimental("STREXP012")]
public sealed record LiveSelection(PlaySession Session, IReadOnlyList<int> Handles);
