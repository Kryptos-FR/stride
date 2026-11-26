using ColorRise.Tessera.Editor.FlipbookEditor;
using ColorRise.Tessera.Editor.TileMapEditor;
using ColorRise.Tessera.Editor.TileSetEditor;
using Stride.Core.Assets.Editor.Commands;

namespace ColorRise.Tessera.Editor;

/// <summary>
/// The commands of Tessera's editors, for the command palette and the user's shortcuts. Each acts on the editor that has the
/// focus, and has no default shortcut: the map canvas reads the single keys of the tools itself.
/// </summary>
internal static class TesseraCommands
{
    private const string Category = "Tessera";

    /// <summary>
    /// Makes the commands, in the order the plugin registers them; their ids never change.
    /// </summary>
    public static IReadOnlyList<CommandDescriptor> Create() =>
    [
        Tool(TileToolKind.Stamp, "Stamp tool", "B"),
        Tool(TileToolKind.Rectangle, "Rectangle tool", "P"),
        Tool(TileToolKind.Fill, "Fill tool", "F"),
        Tool(TileToolKind.Erase, "Erase tool", "E"),
        Tool(TileToolKind.Pick, "Pick tool", "I"),
        Tool(TileToolKind.Select, "Select tool", "R"),
        Map("Tessera.Map.FlipStampX", "Flip stamp horizontally", ["X", "mirror"], editor => editor.FlipStampX()),
        Map("Tessera.Map.FlipStampY", "Flip stamp vertically", ["Y", "mirror"], editor => editor.FlipStampY()),
        Map("Tessera.Map.RotateStampClockwise", "Rotate stamp clockwise", ["Z", "turn"], editor => editor.RotateStamp(clockwise: true)),
        Map("Tessera.Map.RotateStampCounterClockwise", "Rotate stamp counterclockwise", ["Shift+Z", "turn"], editor => editor.RotateStamp(clockwise: false)),
        Map("Tessera.Map.ToggleGrid", "Show grid", ["grid", "cells"], editor => editor.ShowGrid = !editor.ShowGrid, editor => editor.ShowGrid),
        Map("Tessera.Map.ToggleDimOtherLayers", "Dim other layers", ["layers", "fade"], editor => editor.DimOtherLayers = !editor.DimOtherLayers, editor => editor.DimOtherLayers),
        Map("Tessera.Map.ToggleAnimations", "Play the animated tiles", ["animation", "play"], editor => editor.PlayAnimations = !editor.PlayAnimations, editor => editor.PlayAnimations),
        Map("Tessera.Map.AddLayer", "Add layer", ["layer", "new"], editor => editor.AddLayerCommand.Execute()),
        TileSet("Tessera.TileSet.Mode.Select", "Select tiles", ["mode"], editor => editor.Mode = TileSetEditorMode.Select, editor => editor.Mode == TileSetEditorMode.Select),
        TileSet("Tessera.TileSet.Mode.Solid", "Set tiles solid", ["mode", "collision"], editor => editor.Mode = TileSetEditorMode.Solid, editor => editor.Mode == TileSetEditorMode.Solid),
        TileSet("Tessera.TileSet.Mode.Animation", "Animation mode", ["mode", "animate", "frames"], editor => editor.Mode = TileSetEditorMode.Animation, editor => editor.Mode == TileSetEditorMode.Animation),
        TileSet("Tessera.TileSet.AddImage", "Add image source", ["image", "source"], editor => editor.AddImageAsync()),
        TileSet("Tessera.TileSet.AddSpriteSheet", "Add sprite sheet source", ["sprite sheet", "source"], editor => editor.AddSpriteSheetAsync()),
        Flipbook("Tessera.Flipbook.PlayPause", "Play or pause the preview", ["play", "pause", "preview"], editor => Run(() => editor.IsPlaying = !editor.IsPlaying), isChecked: editor => editor.IsPlaying),
        Flipbook("Tessera.Flipbook.AddAnimation", "Add an animation", ["animation", "new"], editor => Run(() => editor.AddAnimationCommand.Execute())),
        Flipbook("Tessera.Flipbook.Sheet", "Make clips from a sheet", ["sheet", "rows", "clips"], editor => Run(() => editor.SheetCommand.Execute()), editor => editor.SheetCommand.IsEnabled),
        Flipbook("Tessera.Flipbook.Mirror", "Mirror the clip", ["mirror", "flip", "left", "right"], editor => editor.MirrorAsync(), editor => editor.MirrorCommand.IsEnabled),
        Flipbook("Tessera.Flipbook.ReplaceTile", "Replace the tile of the selected frames", ["replace", "tile", "frame"],
            editor => Run(() => editor.ReplaceTileCommand.Execute()), editor => editor.ReplaceTileCommand.IsEnabled, editor => editor.ReplacesTile),
    ];

    private static CommandDescriptor Tool(TileToolKind tool, string title, string key)
        => Map($"Tessera.Map.Tool.{tool}", title, [key, "tool"], editor => editor.Tool = tool, editor => editor.Tool == tool);

    private static CommandDescriptor Map(string id, string title, string[] keywords, Action<TileMapEditorViewModel> execute, Func<TileMapEditorViewModel, bool>? isChecked = null)
        => Command(id, title, keywords, execute, isChecked);

    private static CommandDescriptor TileSet(string id, string title, string[] keywords, Action<TileSetEditorViewModel> execute, Func<TileSetEditorViewModel, bool>? isChecked = null)
        => Command(id, title, keywords, execute, isChecked);

    private static CommandDescriptor TileSet(string id, string title, string[] keywords, Func<TileSetEditorViewModel, Task> execute)
        => new()
        {
            Id = id,
            Title = () => title,
            Category = Category,
            Keywords = keywords,
            GestureScope = typeof(TileSetEditorViewModel),
            CanExecute = context => context.Editor is TileSetEditorViewModel,
            ExecuteAsync = context => context.Editor is TileSetEditorViewModel editor ? execute(editor) : Task.CompletedTask,
        };

    // A command on the flipbook editor that has the focus, when it can run there; a toggle when it has isChecked
    private static CommandDescriptor Flipbook(
        string id, string title, string[] keywords, Func<FlipbookEditorViewModel, Task> execute,
        Func<FlipbookEditorViewModel, bool>? canExecute = null, Func<FlipbookEditorViewModel, bool>? isChecked = null)
        => new()
        {
            Id = id,
            Title = () => title,
            Category = Category,
            Keywords = keywords,
            GestureScope = typeof(FlipbookEditorViewModel),
            CanExecute = context => context.Editor is FlipbookEditorViewModel editor && (canExecute?.Invoke(editor) ?? true),
            IsChecked = isChecked is null ? null : context => context.Editor is FlipbookEditorViewModel editor && isChecked(editor),
            ExecuteAsync = context => context.Editor is FlipbookEditorViewModel editor && (canExecute?.Invoke(editor) ?? true) ? execute(editor) : Task.CompletedTask,
        };

    private static Task Run(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    // A command on the editor of type T that has the focus; a toggle when it has isChecked
    private static CommandDescriptor Command<T>(string id, string title, string[] keywords, Action<T> execute, Func<T, bool>? isChecked)
        where T : class
        => new()
        {
            Id = id,
            Title = () => title,
            Category = Category,
            Keywords = keywords,
            GestureScope = typeof(T),
            CanExecute = context => context.Editor is T,
            IsChecked = isChecked is null ? null : context => context.Editor is T editor && isChecked(editor),
            ExecuteAsync = context =>
            {
                if (context.Editor is T editor)
                {
                    execute(editor);
                }
                return Task.CompletedTask;
            },
        };
}
