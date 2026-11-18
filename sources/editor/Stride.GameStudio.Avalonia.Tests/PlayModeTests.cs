// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Pipes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Stride.Core;
using Stride.Core.Assets.Editor.Avalonia.Commands;
using Stride.Editor.PlayMode;
using Stride.GameStudio.Avalonia.Commands;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.GameStudio.Avalonia.Views;
using Stride.GameStudio.Avalonia.Views.Panels;
using Stride.PlayMode.Protocol;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// Play mode from the shell: F5 starts the game of the editor's platform in a play session, Shift+F5 stops it, and the Live
/// tab shows its scenes. The games are fakes that connect to the session's real pipe, as the agent does.
/// </summary>
public sealed class PlayModeTests : IAsyncLifetime
{
    private const string Version = "1.0.0-test";

    private readonly string agentDirectory = Path.Combine(Path.GetTempPath(), $"stride-play-shell-tests-{Guid.NewGuid():N}");
    private readonly List<TestGame> games = [];
    private PlayModeService playMode = null!;

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(agentDirectory);
        foreach (var file in PlayAgentFiles.FileNames)
            File.WriteAllBytes(Path.Combine(agentDirectory, file), []);
        playMode = new PlayModeService(() => new PipePlayTransport(), new PlayAgentFiles(agentDirectory, Version),
            new PlaySessionOptions { StopGracePeriod = TimeSpan.FromSeconds(2) }, (_, _, environment) =>
            {
                var game = new TestGame(environment);
                lock (games)
                    games.Add(game);
                return game;
            });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await playMode.DisposeAsync();
        foreach (var game in games)
            await game.DisposeAsync();
        Directory.Delete(agentDirectory, recursive: true);
    }

    [AvaloniaFact]
    public async Task TheProjectMenuAndTheToolbarHaveStopGame()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var stop = MenuItem(window, "StopGameMenuItem");
            Assert.Equal(new KeyGesture(Key.F5, KeyModifiers.Shift), stop.InputGesture);
            Assert.Equal(GameStudioCommands.StopGameId, Assert.IsType<CommandReference>(stop.Command).Id);
            Assert.Equal(GameStudioCommands.StopGameId, Assert.IsType<CommandReference>(Button(window, "StopGameButton").Command).Id);
            Assert.Contains(window.View.CommandSlots!.Shortcuts, x => x.Gesture == new KeyGesture(Key.F5, KeyModifiers.Shift) && x.Descriptor.Id == GameStudioCommands.StopGameId);
            // No game plays
            Assert.False(await IsEnabledInOpenMenuAsync(stop));

            // The Live tab is a panel of its own, hidden until a game connects
            var live = fixture.Main.Panels.FindDockable(LivePanel.Id)!;
            Assert.Equal("Live", live.Title);
            Assert.False(live.ViewModel.IsShown);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task F5PlaysTheGameAndTheLiveTabShowsItsScenesUntilShiftF5StopsIt()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        fixture.ReplaceProjectBuilder();
        fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            var live = fixture.Main.Panels.FindDockable(LivePanel.Id)!;

            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1, "the start of the game");
            var game = games[0];
            await game.ConnectAsync();
            game.Send(new PlayHello(game.Id, Version, Version, null, []));
            game.Send(Tree());

            // The Live tab comes forward with the running scenes
            await WindowTestHelper.WaitUntilAsync(() => live.ViewModel.IsShown, "the Live tab in front");
            var view = window.GetVisualDescendants().OfType<LiveView>().Single();
            var list = view.GetVisualDescendants().OfType<ListBox>().Single(x => x.Name == "LiveTree");
            await WindowTestHelper.WaitUntilAsync(() => list.ItemCount == 3, "the rows of the tree");
            var viewModel = Assert.IsType<LivePanelViewModel>(view.DataContext);
            Assert.Equal(["MainScene", "Player", "Ground"], viewModel.Tree.Rows.Select(x => x.Name));
            Assert.Equal("Playing", view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "StateText").Text);
            Assert.False(list.Classes.Contains("stopped"));
            await WindowTestHelper.WaitUntilAsync(() => fixture.Main.StopGameCommand.IsEnabled, "Stop game enabled");

            // Shift+F5 asks the game to quit; it does
            Press(window, Key.F5, RawInputModifiers.Shift, PhysicalKey.F5);
            await game.Quit.WaitAsync(TimeSpan.FromSeconds(10));
            game.Exit(0);
            await WindowTestHelper.WaitUntilAsync(() => viewModel.State == LiveTabState.Stopped, "the stop of the game");

            // The last tree stays, greyed
            Assert.Equal("Stopped (exit code 0)", viewModel.StateText);
            Assert.Equal(3, list.ItemCount);
            await WindowTestHelper.WaitUntilAsync(() => list.Classes.Contains("stopped"), "the greyed tree");
            await WindowTestHelper.WaitUntilAsync(() => !fixture.Main.StopGameCommand.IsEnabled, "Stop game disabled");
            Assert.False(game.Killed);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task ASecondF5AsksToStopTheGameThatPlays()
    {
        using var fixture = await ShellSessionFixture.OpenExecutableAsync(Platform.Type, playMode);
        var builder = fixture.ReplaceProjectBuilder();
        var dialogs = fixture.ReplaceDialogService();
        var window = new MainWindow { DataContext = fixture.Main };
        await window.ShowAsync();
        try
        {
            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 1 && !fixture.Main.Build.BuildInProgress, "the start of the first game");
            var first = games[0];
            await first.ConnectAsync();
            first.Send(new PlayHello(first.Id, Version, Version, null, []));
            await WindowTestHelper.WaitUntilAsync(() => playMode.Session?.State == PlaySessionState.Connected, "the first game connected");
            // Ends as the agent does when it is asked to quit
            first.ExitOnQuit = true;

            Press(window, Key.F5, RawInputModifiers.None, PhysicalKey.F5);
            await WindowTestHelper.WaitUntilAsync(() => Count(games) == 2, "the start of the second game", 10000);

            // The user was asked (the recording dialogs answer OK), the first game quit, then the project was built again
            Assert.Contains(dialogs.Messages, x => x.Contains("only one game plays at a time"));
            Assert.Equal(0, await first.Exited.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(first.Killed);
            Assert.Equal(2, builder.Requests.Count);
            Assert.True(playMode.IsPlaying);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    private static PlayTree Tree()
    {
        const string transform = "Stride.Engine.TransformComponent, Stride.Engine";
        return new PlayTree(1,
            [new PlaySceneInfo(1, Guid.NewGuid(), "MainScene", "MainScene", 0)],
            [
                new PlayEntityInfo(2, Guid.NewGuid(), "Player", true, 0, 1, [new PlayComponentInfo(Guid.NewGuid(), transform)]),
                new PlayEntityInfo(3, Guid.NewGuid(), "Camera", true, 2, 1, [new PlayComponentInfo(Guid.NewGuid(), "Stride.Engine.CameraComponent, Stride.Engine")]),
                new PlayEntityInfo(4, Guid.NewGuid(), "Ground", true, 0, 1, [new PlayComponentInfo(Guid.NewGuid(), transform), new PlayComponentInfo(Guid.NewGuid(), "Stride.Engine.ModelComponent, Stride.Engine")]),
            ]);
    }

    private static int Count(List<TestGame> games)
    {
        lock (games)
            return games.Count;
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers, PhysicalKey physicalKey)
    {
        window.KeyPress(key, modifiers, physicalKey, null);
        window.KeyRelease(key, modifiers, physicalKey, null);
    }

    private static MenuItem MenuItem(Window window, string name) => window.GetLogicalDescendants().OfType<MenuItem>().Single(x => x.Name == name);

    private static Button Button(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(x => x.Name == name);

    /// <summary>
    /// Whether the item is enabled when the user opens its menu: Avalonia evaluates the command of an item of a closed menu
    /// only when the menu opens.
    /// </summary>
    private static async Task<bool> IsEnabledInOpenMenuAsync(MenuItem item)
    {
        var menu = Assert.IsType<MenuItem>(item.Parent);
        menu.Open();
        await WindowTestHelper.PumpAsync();
        try
        {
            return item.IsEffectivelyEnabled;
        }
        finally
        {
            menu.Close();
            await WindowTestHelper.PumpAsync();
        }
    }

    /// <summary>
    /// A game's process that the test drives: it connects to the session's pipe as the agent does, and ends by hand.
    /// </summary>
    private sealed class TestGame(IReadOnlyDictionary<string, string> environment) : IPlayGameProcess, IAsyncDisposable
    {
        private readonly TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource quit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private PlayConnection? connection;

        public int Id => 4321;

        public Task<int> Exited => exited.Task;

        /// <summary>
        /// Completes when the editor asks the game to quit.
        /// </summary>
        public Task Quit => quit.Task;

        public bool ExitOnQuit { get; set; }

        public bool Killed { get; private set; }

        public async Task ConnectAsync()
        {
            var pipe = new NamedPipeClientStream(".", environment[PlayProtocol.PipeVariable], PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            connection = new PlayConnection(pipe);
            connection.Start(message =>
            {
                if (message is not PlayQuit)
                    return;
                quit.TrySetResult();
                if (ExitOnQuit)
                    Exit(0);
            });
        }

        public void Send(PlayMessage message) => Assert.True(connection?.Send(message));

        public void Exit(int exitCode) => exited.TrySetResult(exitCode);

        public void Kill()
        {
            Killed = true;
            Exit(-1);
        }

        public async ValueTask DisposeAsync()
        {
            Exit(-1);
            if (connection is not null)
                await connection.DisposeAsync();
        }
    }
}
