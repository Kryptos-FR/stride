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
    /// The variables that a played game removes from its own environment once its agent is loaded, so that the processes it
    /// starts do not load an agent too.
    /// </summary>
    public static IReadOnlyList<string> GameVariables { get; } = [StartupHooksVariable, PipeVariable, EngineVersionVariable];
}
