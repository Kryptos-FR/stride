// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// The constants of the play mode's protocol: its version, the frame limits and the environment variables of a played game.
/// </summary>
public static class PlayProtocol
{
    /// <summary>
    /// The version of the protocol. Each frame carries it; a reader refuses a frame of another version.
    /// </summary>
    public const byte Version = 1;

    /// <summary>
    /// The size of a frame's header: the length (4 bytes, little-endian), the version and the message type.
    /// </summary>
    public const int HeaderLength = 6;

    /// <summary>
    /// The largest frame a reader accepts. A larger length means a broken stream.
    /// </summary>
    public const int MaxFrameLength = 64 * 1024 * 1024;

    /// <summary>
    /// The variable of the .NET host that names the startup hooks of a process.
    /// </summary>
    public const string StartupHooksVariable = "DOTNET_STARTUP_HOOKS";

    /// <summary>
    /// The variable that gives the played game the name of the pipe its agent connects to.
    /// </summary>
    public const string PipeVariable = "STRIDE_PLAY_PIPE";

    /// <summary>
    /// The variable that gives the startup hook the Stride version its agent was built against. The hook does not load the
    /// agent into a game that runs another version.
    /// </summary>
    public const string EngineVersionVariable = "STRIDE_PLAY_ENGINE_VERSION";

    /// <summary>
    /// The variable that gives the agent the key that pauses and resumes the game from its window (<see cref="PlayHotkey"/>);
    /// absent or empty for none.
    /// </summary>
    public const string HotkeyVariable = "STRIDE_PLAY_HOTKEY";

    /// <summary>
    /// The variable that starts the game paused, before its first update ("1").
    /// </summary>
    public const string StartPausedVariable = "STRIDE_PLAY_START_PAUSED";

    /// <summary>
    /// The variable that pauses the game when a script throws an exception, instead of ending it ("1").
    /// </summary>
    public const string PauseOnScriptExceptionVariable = "STRIDE_PLAY_PAUSE_ON_SCRIPT_EXCEPTION";

    /// <summary>
    /// The variable that gives the URL of the scene the game starts on instead of its default scene (Play this scene); absent or
    /// empty for the default scene.
    /// </summary>
    public const string SceneVariable = "STRIDE_PLAY_SCENE";

    /// <summary>
    /// The prefix of the play mode's variables: the startup hook removes every variable that starts with it from the game's
    /// environment, and hands them to the agent.
    /// </summary>
    public const string VariablePrefix = "STRIDE_PLAY_";

    /// <summary>
    /// The variables that a played game removes from its own environment once its agent is loaded, so that the processes it
    /// starts do not load an agent too. The hook also removes the other variables that start with <see cref="VariablePrefix"/>.
    /// </summary>
    public static IReadOnlyList<string> GameVariables { get; } = [StartupHooksVariable, PipeVariable, EngineVersionVariable, HotkeyVariable, StartPausedVariable, PauseOnScriptExceptionVariable, SceneVariable];
}
