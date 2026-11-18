// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using Stride.Assets.Editor.PlayMode;
using Stride.Assets.Editor.ViewModels;
using Stride.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Presentation.Services;
using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;

namespace Stride.GameStudio.Avalonia.PlayMode;

/// <summary>
/// The Debug menu's way to the live grid before the Live tab: plays a built game with a play session and shows the live
/// entities that match the scene editor's selection in the property grid.
/// </summary>
/// <remarks>
/// <para>
/// The game is the assembly named by the variable <c>STRIDE_PLAY_GAME_ASSEMBLY</c>, or the one picked in a file dialog. The
/// entities are those of the scene editor's selection (matched by id), else those named by <c>STRIDE_PLAY_ENTITIES</c>
/// (comma-separated), else the entity with the most components. Each apply is logged to the file named by
/// <c>STRIDE_PLAY_LIVE_LOG</c>, when it is set.
/// </para>
/// <para>
/// The Live tab replaces this: it calls <see cref="LivePropertiesViewModel.Show"/> with the handles of its selection.
/// </para>
/// </remarks>
internal sealed class LiveEntityDebugController : IAsyncDisposable
{
    private static LiveEntityDebugController? current;
    private readonly SessionViewModel session;
    private readonly PlaySession playSession;
    private readonly LivePropertiesViewModel properties;
    private readonly TaskCompletionSource<PlayTree> treeReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string? logPath = Environment.GetEnvironmentVariable("STRIDE_PLAY_LIVE_LOG");
    private readonly Logger log = GlobalLogger.GetLogger("PlayMode");

    private LiveEntityDebugController(SessionViewModel session)
    {
        this.session = session;
        var output = new LoggerResult();
        output.MessageLogged += (_, e) => Log($"[game] {e.Message}");
        playSession = new PlaySession(new PipePlayTransport(), PlayAgentFiles.NextToEditor, new PlaySessionOptions { Log = output });
        playSession.StateChanged += (_, e) => Log($"state {e.OldState} -> {e.NewState}{(e.Message is null ? string.Empty : ": " + e.Message)}");
        playSession.TreeReceived += (_, tree) => treeReceived.TrySetResult(tree);
        properties = new LivePropertiesViewModel(session);
        properties.Applied += OnApplied;
    }

    /// <summary>
    /// Plays the game if it is not playing, then shows the live entities of the selection.
    /// </summary>
    public static async Task ShowSelectionAsync(SessionViewModel? session, IDialogService dialogs)
    {
        if (session is null)
            return;
        if (current is not null && (current.session != session || current.playSession.State is not (PlaySessionState.Starting or PlaySessionState.Connected)))
            await StopAsync();

        if (current is null)
        {
            var assembly = Environment.GetEnvironmentVariable("STRIDE_PLAY_GAME_ASSEMBLY");
            if (string.IsNullOrEmpty(assembly))
            {
                var picked = await dialogs.OpenFilePickerAsync(filters: [new FilePickerFilter("Game assembly") { Patterns = ["*.dll"] }]);
                if (picked is null)
                    return;
                assembly = picked.ToOSPath();
            }
            current = new LiveEntityDebugController(session);
            if (!await current.StartAsync(assembly))
            {
                await StopAsync();
                return;
            }
        }
        await current.ShowAsync();
    }

    /// <summary>
    /// Stops the game and the live grid.
    /// </summary>
    public static async Task StopAsync()
    {
        if (Interlocked.Exchange(ref current, null) is { } controller)
            await controller.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        properties.Detach();
        properties.Applied -= OnApplied;
        await playSession.DisposeAsync();
        Log("stopped");
    }

    private async Task<bool> StartAsync(string assembly)
    {
        Log($"playing {assembly}");
        try
        {
            var output = new LoggerResult();
            output.MessageLogged += (_, e) => Log($"[game output] {e.Message}");
            await playSession.StartAsync(environment => PlayGameLauncher.Start(assembly, output, environment), PlayAgentFiles.GetGameStrideVersion(assembly));
            // The session connects to the agent after the start: wait for the tree, unless the game could not start
            if (playSession.State is PlaySessionState.Failed or PlaySessionState.VersionMismatch or PlaySessionState.GameExited)
            {
                Log($"not connected: {playSession.State}, exit code {playSession.ExitCode}, {playSession.StateMessage}");
                log.Error($"The game could not be played: {playSession.StateMessage}");
                return false;
            }
            var exited = playSession.Exited;
            if (await Task.WhenAny(treeReceived.Task, exited, Task.Delay(TimeSpan.FromSeconds(60))) != treeReceived.Task)
            {
                Log($"no tree: {playSession.State}, exit code {playSession.ExitCode}, {playSession.StateMessage}");
                log.Error($"The game did not send its entities: {playSession.StateMessage}");
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            log.Error("The game could not be played.", e);
            return false;
        }
    }

    private async Task ShowAsync()
    {
        var tree = playSession.Tree ?? await treeReceived.Task;
        var handles = FindEntities(tree);
        Log($"showing {string.Join(", ", handles.Select(h => $"#{h} {tree.Entities.First(x => x.Handle == h).Name}"))}");
        properties.Show(playSession, handles);
        // The grid takes the place of the scene editor's, until something else is selected
        session.ActiveProperties = properties;
    }

    private IReadOnlyList<int> FindEntities(PlayTree tree)
    {
        if (session.EditorCollection.ActiveEditor is EntityHierarchyEditorViewModel editor)
        {
            var ids = editor.SelectedContent.OfType<EntityViewModel>().Select(x => x.AssetSideEntity.Id).ToHashSet();
            var selected = tree.Entities.Where(x => ids.Contains(x.Id)).Select(x => x.Handle).ToList();
            if (selected.Count > 0)
                return selected;
        }
        if (Environment.GetEnvironmentVariable("STRIDE_PLAY_ENTITIES") is { Length: > 0 } names)
        {
            var named = names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => tree.Entities.FirstOrDefault(x => x.Name == n)).OfType<PlayEntityInfo>().Select(x => x.Handle).ToList();
            if (named.Count > 0)
                return named;
        }
        return tree.Entities.OrderByDescending(x => x.Components.Count).Take(1).Select(x => x.Handle).ToList();
    }

    private void OnApplied(object? sender, LiveApplyEventArgs e)
    {
        if (logPath is not { Length: > 0 })
            return;
        var entities = string.Join("; ", e.Entities.Select(x => string.Create(CultureInfo.InvariantCulture,
            $"#{x.Handle} {x.Elapsed.TotalMilliseconds:F2} ms compared {x.Result.ComparedValues} written {x.Result.WrittenValues} skipped {x.Result.SkippedValues}")));
        Log(string.Create(CultureInfo.InvariantCulture,
            $"frame {e.Snapshot?.Frame}: {(e.Rebuilt ? "rebuilt" : "in place")} {e.Elapsed.TotalMilliseconds:F2} ms{(e.StructuralChange is { } change ? $" ({change})" : string.Empty)}; {entities}; {properties.LiveStatus}"));
    }

    private void Log(string message)
    {
        if (logPath is not { Length: > 0 })
            return;
        lock (this)
            File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
    }
}
