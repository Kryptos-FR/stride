// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using Stride.Core.Diagnostics;
using Stride.Engine;
using Stride.Games;
using Stride.PlayMode.Agent;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests <see cref="PlayGamePauseHost"/> on a game that is not run (no graphics): what it changes in the game.
/// </summary>
public sealed class PlayGamePauseHostTests : IDisposable
{
    private readonly Game game = new();
    private readonly LoggerResult log = new();
    private readonly PlayGamePauseHost host;

    public PlayGamePauseHostTests()
    {
        host = new PlayGamePauseHost(game, log);
    }

    public void Dispose() => game.Dispose();

    [Fact]
    public void TheTimeFactorIsTheUpdateTimeFactor()
    {
        host.TimeFactor = 0.25;
        Assert.Equal(0.25, game.UpdateTime.Factor);

        host.TimeFactor = -1;
        Assert.Equal(0, host.TimeFactor);
    }

    [Fact]
    public void TheScriptsAndTheSpriteAnimationArePausedAndResumed()
    {
        game.Script.Enabled = true;
        game.SpriteAnimation.Visible = true;
        var pausables = host.FindPausables().ToList();

        foreach (var pausable in pausables)
            pausable.Pause();
        Assert.False(game.Script.Enabled);
        Assert.False(game.SpriteAnimation.Visible);
        Assert.All(pausables, x => Assert.True(x.RunsOnStep));

        foreach (var pausable in pausables)
            pausable.Resume(isActive: true);
        Assert.True(game.Script.Enabled);
        Assert.True(game.SpriteAnimation.Visible);
    }

    [Fact]
    public void APartTheGameStoppedItselfIsNotRestarted()
    {
        game.Script.Enabled = false;
        var scripts = host.FindPausables().First();

        scripts.Pause();
        scripts.Resume(isActive: true);

        Assert.False(game.Script.Enabled);
    }

    [Fact]
    public void ASingleStepForcesOneUpdateOfTheFixedStepAndGivesBackTheGameSettings()
    {
        var force = typeof(GameBase).GetProperty("ForceOneUpdatePerDraw", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var accumulated = typeof(GameBase).GetField("accumulatedElapsedGameTime", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var resetElapsed = typeof(GameBase).GetField("forceElapsedTimeToZero", BindingFlags.Instance | BindingFlags.NonPublic)!;
        accumulated.SetValue(game, TimeSpan.FromMilliseconds(5));

        Assert.True(host.BeginSingleStep());
        Assert.True(game.IsFixedTimeStep);
        Assert.True((bool)force.GetValue(game)!);
        Assert.True((bool)resetElapsed.GetValue(game)!);

        // The step's tick takes the fixed step from the accumulated time
        accumulated.SetValue(game, TimeSpan.FromMilliseconds(-11));
        host.EndSingleStep();
        Assert.False(game.IsFixedTimeStep);
        Assert.False((bool)force.GetValue(game)!);
        Assert.Equal(TimeSpan.FromMilliseconds(5), (TimeSpan)accumulated.GetValue(game)!);
    }

    [Fact]
    public void ResetElapsedTimeIsTheGameOwn()
    {
        var resetElapsed = typeof(GameBase).GetField("forceElapsedTimeToZero", BindingFlags.Instance | BindingFlags.NonPublic)!;

        host.ResetElapsedTime();

        Assert.True((bool)resetElapsed.GetValue(game)!);
    }

    [Fact]
    public void TheSystemsAndProcessorsStoppedByNameAreThoseOfTheAudit()
    {
        Assert.Equal(["Stride.UI.UISystem", "Stride.Audio.AudioSystem"], PlayGamePauseHost.PausedSystems.Select(x => x.TypeName));
        Assert.Equal(["Stride.Video.VideoProcessor", "Stride.BepuPhysics.Navigation.Processors.RecastNavigationProcessor"], PlayGamePauseHost.PausedProcessors);
    }

    [Fact]
    public void AWarningGoesToTheLog()
    {
        host.Warn("Careful.");

        Assert.Contains(log.Messages, x => x.Type == LogMessageType.Warning && x.Text == "Careful.");
    }

    [Fact]
    public void ReleasingTheInputOfAGameWithoutInputDoesNothing() => host.ReleaseInput();
}
