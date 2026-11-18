// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// A subscriber asks the game to pause: the game time stops (animation, physics, particles), the scripts and the systems that
/// follow the real time stop, the audio pauses, and the game goes on drawing.
/// </summary>
public sealed record PlayPause : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Pause;

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
    }
}

/// <summary>
/// A subscriber asks the paused game to go on, as it was before the pause.
/// </summary>
public sealed record PlayResume : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Resume;

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
    }
}

/// <summary>
/// A subscriber asks the game to run some updates, each with the game's fixed step, then to stay paused. A game that plays is
/// paused first. A step is one fixed step of game time at the game's own factor: the time scale does not slow it.
/// </summary>
/// <param name="Frames">How many updates (at least one).</param>
public sealed record PlayStep(int Frames = 1) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.Step;

    internal static PlayStep Read(PlayReader reader) => new(reader.ReadInt32());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer) => writer.Write(Frames);
}

/// <summary>
/// A subscriber sets the speed of the game time while it plays: the game's own time factor is multiplied by it.
/// </summary>
/// <param name="Scale">The speed; 1 is the game's own speed. The agent keeps it within
/// [<see cref="PlayGameState.MinTimeScale"/>, <see cref="PlayGameState.MaxTimeScale"/>].</param>
public sealed record PlayTimeScale(double Scale) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.TimeScale;

    internal static PlayTimeScale Read(PlayReader reader) => new(reader.ReadDouble());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer) => writer.Write(Scale);
}

/// <summary>
/// The state of the played game: sent when it is paused, resumed or stepped and when its time scale changes, and after each
/// heartbeat (about once a second).
/// </summary>
/// <param name="Frame">The agent's frame (the game's updates, paused or not).</param>
/// <param name="PlayedFrames">The updates that ran while the game was not paused, steps included: one step adds one.</param>
/// <param name="Paused">Whether the game is paused.</param>
/// <param name="TimeScale">The speed the subscribers chose.</param>
/// <param name="GameTimeFactor">The game's own time factor (1 unless the game sets it); the game time runs at its product with
/// <paramref name="TimeScale"/>.</param>
/// <param name="FramesPerSecond">The frame rate over the last heartbeat period: the game draws while paused.</param>
/// <param name="EntityCount">The entities of the running scenes.</param>
/// <param name="StepsPending">The steps asked for that have not run yet.</param>
public sealed record PlayGameState(long Frame, long PlayedFrames, bool Paused, double TimeScale, double GameTimeFactor, double FramesPerSecond, int EntityCount, int StepsPending) : PlayMessage
{
    /// <summary>The slowest time scale the agent accepts.</summary>
    public const double MinTimeScale = 0.01;

    /// <summary>The fastest time scale the agent accepts.</summary>
    public const double MaxTimeScale = 16;

    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.State;

    internal static PlayGameState Read(PlayReader reader)
        => new(reader.ReadInt64(), reader.ReadInt64(), reader.ReadBoolean(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadInt32(), reader.ReadInt32());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Frame);
        writer.Write(PlayedFrames);
        writer.Write(Paused);
        writer.Write(TimeScale);
        writer.Write(GameTimeFactor);
        writer.Write(FramesPerSecond);
        writer.Write(EntityCount);
        writer.Write(StepsPending);
    }
}
