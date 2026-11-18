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
/// A subscriber turns on or off keeping the game's window on top while the editor or the game is the active app (the play
/// toolbar's pin). The launch gives the first value (<see cref="PlayLaunchOptions.WindowOnTop"/>).
/// </summary>
/// <param name="OnTop">Whether the window stays on top.</param>
public sealed record PlayWindowOnTop(bool OnTop) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.WindowOnTop;

    internal static PlayWindowOnTop Read(PlayReader reader) => new(reader.ReadBoolean());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer) => writer.Write(OnTop);
}

/// <summary>
/// A subscriber says whether the editor is the active app (its main window has the focus): the game's window stays on top
/// while the editor or the game is active, and goes back among the other windows when the user switches to another app.
/// Sent when the editor connects, and each time its main window is activated or deactivated.
/// </summary>
/// <param name="Active">Whether the editor is active.</param>
public sealed record PlayEditorActive(bool Active) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.EditorActive;

    internal static PlayEditorActive Read(PlayReader reader) => new(reader.ReadBoolean());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer) => writer.Write(Active);
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
/// <param name="PauseReason">Why the game is paused; <see cref="PlayPauseReason.None"/> while it plays. Read only when the
/// payload has it, so that a state without it reads as before.</param>
public sealed record PlayGameState(long Frame, long PlayedFrames, bool Paused, double TimeScale, double GameTimeFactor, double FramesPerSecond, int EntityCount, int StepsPending, PlayPauseReason PauseReason = PlayPauseReason.None) : PlayMessage
{
    /// <summary>The slowest time scale the agent accepts.</summary>
    public const double MinTimeScale = 0.01;

    /// <summary>The fastest time scale the agent accepts.</summary>
    public const double MaxTimeScale = 16;

    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.State;

    internal static PlayGameState Read(PlayReader reader)
        => new(reader.ReadInt64(), reader.ReadInt64(), reader.ReadBoolean(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadInt32(), reader.ReadInt32(),
            reader.Remaining > 0 ? (PlayPauseReason)reader.ReadByte() : PlayPauseReason.None);

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
        writer.Write((byte)PauseReason);
    }
}

/// <summary>
/// Why a played game is paused (<see cref="PlayGameState.PauseReason"/>).
/// </summary>
public enum PlayPauseReason : byte
{
    /// <summary>The game plays.</summary>
    None = 0,

    /// <summary>A subscriber paused it (the editor's Pause, or a step).</summary>
    Editor = 1,

    /// <summary>The user pressed the play mode's key in the game's window (<see cref="PlayLaunchOptions.Hotkey"/>).</summary>
    Hotkey = 2,

    /// <summary>The game was started paused (<see cref="PlayLaunchOptions.StartPaused"/>): no script started yet.</summary>
    StartPaused = 3,

    /// <summary>A script threw an exception (<see cref="PlayLaunchOptions.PauseOnScriptException"/>, <see cref="PlayScriptException"/>).</summary>
    ScriptException = 4,
}

/// <summary>
/// Where a script's exception was thrown (<see cref="PlayScriptException.Kind"/>).
/// </summary>
public enum PlayScriptExceptionKind : byte
{
    /// <summary>Not known: a script method the agent could not tell (a <c>Cancel</c>, a task a script added).</summary>
    Other = 0,

    /// <summary>The <c>Update</c> of a sync script. The engine stops the script: it does not update again.</summary>
    Update = 1,

    /// <summary>The <c>Start</c> of a script. The engine stops the script: a sync script does not update.</summary>
    Start = 2,

    /// <summary>The <c>Execute</c> of an async script (its micro-thread), or a micro-thread it awaits. The micro-thread ends.</summary>
    Async = 3,
}

/// <summary>
/// A script of the game threw an exception, which the engine caught: sent when the game was started with
/// <see cref="PlayLaunchOptions.PauseOnScriptException"/>. The game pauses (<see cref="Paused"/>) instead of ending, and a
/// subscriber that joins later gets the last one.
/// </summary>
/// <param name="Frame">The agent's frame of the exception.</param>
/// <param name="Kind">Where it was thrown.</param>
/// <param name="ScriptType">The full name of the script's type; <c>null</c> when not known.</param>
/// <param name="EntityHandle">The agent's handle of the script's entity (<see cref="PlayEntityInfo.Handle"/>); 0 when not known
/// or not in a running scene.</param>
/// <param name="EntityId">The id of the script's entity; <see cref="Guid.Empty"/> when not known.</param>
/// <param name="EntityName">The name of the script's entity; <c>null</c> when not known.</param>
/// <param name="ExceptionType">The full name of the exception's type.</param>
/// <param name="Message">The exception's message.</param>
/// <param name="Details">The exception as text: its type, message, stack and inner exceptions.</param>
/// <param name="Paused">Whether the game paused for it.</param>
public sealed record PlayScriptException(long Frame, PlayScriptExceptionKind Kind, string? ScriptType, int EntityHandle, Guid EntityId, string? EntityName, string ExceptionType, string Message, string? Details, bool Paused) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.ScriptException;

    /// <summary>The short name of the script's type (without its namespace); <c>null</c> when not known.</summary>
    public string? ScriptName => ScriptType is { } type ? type[(type.LastIndexOf('.') + 1)..] : null;

    internal static PlayScriptException Read(PlayReader reader)
        => new(reader.ReadInt64(), (PlayScriptExceptionKind)reader.ReadByte(), reader.ReadString(), reader.ReadInt32(), reader.ReadGuid(), reader.ReadString(),
            reader.ReadString() ?? string.Empty, reader.ReadString() ?? string.Empty, reader.ReadString(), reader.ReadBoolean());

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
        writer.Write(Frame);
        writer.Write((byte)Kind);
        writer.Write(ScriptType);
        writer.Write(EntityHandle);
        writer.Write(EntityId);
        writer.Write(EntityName);
        writer.Write(ExceptionType);
        writer.Write(Message);
        writer.Write(Details);
        writer.Write(Paused);
    }
}
