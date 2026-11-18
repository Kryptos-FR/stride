// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Games;

namespace Stride.PlayMode.Agent;

/// <summary>
/// The first game system of each frame: it applies the edits of the play agent's subscribers (<see cref="PlayAgentSystem.ApplyEdits"/>)
/// before the scripts, the physics and the animations run, so that they see the edited values in the same frame.
/// </summary>
internal sealed class PlayEditSystem : GameSystemBase
{
    private readonly PlayAgentSystem agent;

    public PlayEditSystem(PlayAgentSystem agent)
        : base(agent.Services)
    {
        this.agent = agent;
        Enabled = true;
        UpdateOrder = int.MinValue;
    }

    public override void Update(GameTime gameTime) => agent.ApplyEdits();
}
