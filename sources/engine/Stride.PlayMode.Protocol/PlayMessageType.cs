// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// The kinds of message of the play mode's protocol.
/// </summary>
public enum PlayMessageType : byte
{
    /// <summary>The game's first message: versions and loaded assemblies (<see cref="PlayHello"/>).</summary>
    Hello = 1,

    /// <summary>Assemblies the game loaded after its hello (<see cref="PlayAssemblies"/>).</summary>
    Assemblies = 2,

    /// <summary>The whole tree of the running scenes (<see cref="PlayTree"/>).</summary>
    Tree = 3,

    /// <summary>The changes of the tree during one frame (<see cref="PlayTreeBatch"/>).</summary>
    TreeBatch = 4,

    /// <summary>The state of the subscribed entities at the end of a frame (<see cref="PlaySnapshot"/>).</summary>
    Snapshot = 5,

    /// <summary>A message of the game's log (<see cref="PlayLog"/>).</summary>
    Log = 6,

    /// <summary>A sign of life of the game, with its frame rate and the agent's costs (<see cref="PlayHeartbeat"/>).</summary>
    Heartbeat = 7,

    /// <summary>The game is ending (<see cref="PlayGoodbye"/>).</summary>
    Goodbye = 8,

    /// <summary>A subscriber's choice of entities to receive snapshots of (<see cref="PlaySubscribe"/>).</summary>
    Subscribe = 32,

    /// <summary>A subscriber asks the game to exit (<see cref="PlayQuit"/>).</summary>
    Quit = 33,
}
