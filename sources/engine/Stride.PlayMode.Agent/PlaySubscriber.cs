// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Agent;

/// <summary>
/// One subscriber of the agent (the editor's play session, or another viewer): its connection, the messages it sent, and
/// the entities it wants snapshots of.
/// </summary>
internal sealed class PlaySubscriber(PlayConnection connection)
{
    public PlayConnection Connection { get; } = connection;

    /// <summary>The messages read, waiting for the game thread.</summary>
    public ConcurrentQueue<PlayMessage> Incoming { get; } = new();

    /// <summary>The edits and structure commands read, in the order they came, waiting for the start of the next frame
    /// (<see cref="PlayAgentSystem.ApplyEdits"/>): <see cref="PlayEdit"/> and <see cref="PlayStructureEdit"/>.</summary>
    public ConcurrentQueue<PlayMessage> Edits { get; } = new();

    /// <summary>
    /// Keeps a message read from the connection for the game thread: an edit or a structure command for the start of the next
    /// frame, anything else for the agent's update.
    /// </summary>
    public void Receive(PlayMessage message)
    {
        if (message is PlayEdit or PlayStructureEdit)
            Edits.Enqueue(message);
        else
            Incoming.Enqueue(message);
    }

    /// <summary>Whether the subscriber still needs the whole tree (it just connected).</summary>
    public bool NeedsTree { get; set; } = true;

    /// <summary>The handles of the entities to take snapshots of.</summary>
    public IReadOnlyList<int> Selection { get; set; } = [];

    /// <summary>The snapshots a second it asked for.</summary>
    public double RateHz { get; set; }

    /// <summary>When the last snapshot was taken, from <see cref="Stopwatch.GetTimestamp"/>.</summary>
    public long LastSnapshot { get; set; }

    /// <summary>
    /// Whether a snapshot is due at this time.
    /// </summary>
    public bool IsSnapshotDue(long now)
        => Selection.Count > 0 && RateHz > 0 && (LastSnapshot == 0 || Stopwatch.GetElapsedTime(LastSnapshot, now).TotalSeconds >= 1 / RateHz);
}
