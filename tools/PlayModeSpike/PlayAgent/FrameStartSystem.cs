// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using Stride.Core;
using Stride.Games;

namespace Stride.PlayMode.Spike;

/// <summary>
/// Runs first in each update and each draw, so that the agent, which runs last, can time the two phases of a frame.
/// </summary>
internal sealed class FrameStartSystem : GameSystemBase
{
    public FrameStartSystem(IServiceRegistry registry) : base(registry)
    {
        Enabled = true;
        Visible = true;
        UpdateOrder = int.MinValue;
        DrawOrder = int.MinValue;
    }

    /// <summary>The time the current update started, from <see cref="Stopwatch.GetTimestamp"/>.</summary>
    public long UpdateStart { get; private set; }

    /// <summary>The time the current draw started, from <see cref="Stopwatch.GetTimestamp"/>.</summary>
    public long DrawStart { get; private set; }

    public override void Update(GameTime gameTime) => UpdateStart = Stopwatch.GetTimestamp();

    public override void Draw(GameTime gameTime) => DrawStart = Stopwatch.GetTimestamp();
}
