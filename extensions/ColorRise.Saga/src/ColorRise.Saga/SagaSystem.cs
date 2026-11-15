using ColorRise.Saga.Content;
using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Games;

namespace ColorRise.Saga;

/// <summary>
/// The game's access to its story. The game loads a story (from the content) and talks to it through <see cref="Runtime"/>.
/// </summary>
/// <remarks>
/// The game creates it by itself (<see cref="GameSystemAttribute"/>); get it with <c>Services.GetService&lt;SagaSystem&gt;()</c>.
/// </remarks>
[GameSystem]
public sealed class SagaSystem : GameSystemBase
{
    private static readonly Logger Log = GlobalLogger.GetLogger("Saga");

    public SagaSystem(IServiceRegistry registry)
        : base(registry)
    {
    }

    /// <summary>
    /// The story being played, or null before <see cref="Load"/>.
    /// </summary>
    public SagaRuntime? Runtime { get; private set; }

    /// <summary>
    /// Plays a story, from its start or from a saved state, replacing the one played so far.
    /// </summary>
    /// <param name="story">The compiled story.</param>
    /// <param name="state">The state to resume from (see <see cref="SagaRuntime.CaptureState"/>), or null to start.</param>
    /// <returns>The runtime of the story, started.</returns>
    public SagaRuntime Load(StoryDefinition story, SagaState? state = null)
    {
        var runtime = new SagaRuntime(story, state, Log);
        Runtime = runtime;
        runtime.Start();
        return runtime;
    }
}
