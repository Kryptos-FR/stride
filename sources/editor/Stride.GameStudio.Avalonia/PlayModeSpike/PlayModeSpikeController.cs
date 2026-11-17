// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stride.Assets.Editor.ViewModels;
using Stride.Assets.Entities;
using Stride.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.Avalonia.Controls;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Quantum;
using Stride.Engine;

namespace Stride.GameStudio.Avalonia.PlayModeSpike;

/// <summary>
/// Play mode spike: the Debug menu command that shows a live mirror of the selected entity in the property grid. The "game" is
/// the recorder: a runtime copy of the entity, 50 recorded frames played in a loop at 5 snapshots a second.
/// </summary>
/// <remarks>
/// The value under the pointer and the one with the keyboard focus are not written while they are. Each apply is logged to
/// the file named by the variable PLAY_MODE_SPIKE_LOG, when it is set. Selecting something else ends the spike.
/// </remarks>
public sealed class PlayModeSpikeController
{
    public const int FrameCount = 50;
    private static PlayModeSpikeController? current;
    private readonly DispatcherTimer timer;
    private readonly MirrorPropertiesViewModel properties;
    private readonly MirrorRecording recording;
    private readonly string entityName;
    private readonly string? logPath = Environment.GetEnvironmentVariable("PLAY_MODE_SPIKE_LOG");
    private int frame;
    private bool applying;

    private PlayModeSpikeController(SessionViewModel session, Entity entity)
    {
        entityName = entity.Name ?? "Entity";
        recording = MirrorRecorder.Record(MirrorRecorder.MakeRuntimeEntity(entity), FrameCount);
        properties = new MirrorPropertiesViewModel(session) { InPlaceCollections = true };
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, (_, _) => OnTick(session));
        Log($"recorded {FrameCount} snapshots of {entityName}: capture {recording.CaptureAverage.TotalMilliseconds:F3} ms, {recording.AverageSize:F0} bytes on average");
    }

    /// <summary>
    /// Starts the live mirror of the selected entity (or of the first entity with a script of the first scene), or stops it.
    /// </summary>
    public static async Task ToggleAsync(SessionViewModel? session)
    {
        if (current is not null)
        {
            current.Stop();
            return;
        }
        if (session is null || FindEntity(session) is not { } entity)
            return;

        current = new PlayModeSpikeController(session, entity);
        var timing = await current.properties.ApplyAsync(current.recording.Snapshots[0]);
        current.Log($"frame 0: built in {timing.Total.TotalMilliseconds:F1} ms");
        current.timer.Start();
    }

    private static Entity? FindEntity(SessionViewModel session)
    {
        if (session.EditorCollection.ActiveEditor is EntityHierarchyEditorViewModel editor
            && editor.SelectedContent.OfType<EntityViewModel>().FirstOrDefault() is { } selected)
        {
            return selected.AssetSideEntity;
        }
        var scene = session.AllAssets.Select(x => x.Asset).OfType<SceneAsset>().FirstOrDefault();
        var entities = scene?.Hierarchy.Parts.Values.Select(x => x.Entity).ToList() ?? [];
        return entities.FirstOrDefault(x => x.Components.OfType<ScriptComponent>().Any()) ?? entities.FirstOrDefault();
    }

    private async void OnTick(SessionViewModel session)
    {
        if (applying)
            return;
        if (session.ActiveProperties != properties)
        {
            // Something else is selected
            Stop();
            return;
        }
        applying = true;
        try
        {
            frame = (frame + 1) % FrameCount;
            var protectedNodes = ProtectedNodes();
            properties.IsProtected = protectedNodes.Count == 0 ? null : (node, index) => protectedNodes.Contains((node, index));
            var timing = await properties.ApplyAsync(recording.Snapshots[frame]);
            properties.Name = $"{entityName}  (live, frame {frame})";
            var result = timing.Result;
            Log(string.Create(CultureInfo.InvariantCulture,
                $"frame {frame}: {(timing.Rebuilt ? "rebuilt" : "in place")} {timing.Total.TotalMilliseconds:F2} ms (restore {timing.Restore?.TotalMilliseconds:F3}), compared {result?.ComparedValues}, written {result?.WrittenValues}, skipped {result?.SkippedValues}, added {result?.AddedItems}, removed {result?.RemovedItems}{(result?.StructuralChange is { } change ? ", " + change : "")}"));
        }
        catch (Exception e)
        {
            Log($"frame {frame}: {e}");
            Stop();
        }
        finally
        {
            applying = false;
        }
    }

    private void Stop()
    {
        timer.Stop();
        if (current == this)
            current = null;
        Log("stopped");
    }

    /// <summary>
    /// The value nodes of the property with the keyboard focus and of the property under the pointer.
    /// </summary>
    private static HashSet<(IGraphNode, NodeIndex)> ProtectedNodes()
    {
        var nodes = new HashSet<(IGraphNode, NodeIndex)>();
        if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } window)
            return nodes;
        Add((window.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<PropertyViewItem>(includeSelf: true));
        // The innermost property under the pointer
        Add(window.GetVisualDescendants().OfType<PropertyViewItem>().LastOrDefault(x => x.IsPointerOver));
        return nodes;

        void Add(PropertyViewItem? item)
        {
            if (item?.DataContext is not NodeViewModel viewModel)
                return;
            foreach (var accessor in viewModel.NodePresenters.Select(x => x.GetNodeAccessor()))
                nodes.Add((accessor.Node, accessor.Index));
        }
    }

    private void Log(string message)
    {
        if (logPath is not { Length: > 0 })
            return;
        File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
    }
}
