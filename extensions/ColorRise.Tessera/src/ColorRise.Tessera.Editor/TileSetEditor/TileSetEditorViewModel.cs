using System.Globalization;
using ColorRise.Tessera.Assets;
using ColorRise.Tessera.Editor.Canvas;
using ColorRise.Tessera.Editor.Palette;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Assets.Presentation.ViewModels;
using Stride.Assets.Sprite;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.Services;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Quantum;
using Stride.Core.Extensions;
using Stride.Core.IO;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Services;
using Stride.Core.Serialization;
using Stride.Core.Transactions;
using Stride.Graphics;

namespace ColorRise.Tessera.Editor.TileSetEditor;

/// <summary>
/// The editor of a tile set: its sources (images cut on a grid, and sprite sheets) and the data of its tiles.
/// </summary>
[AssetEditorViewModel<TileSetViewModel>]
public sealed class TileSetEditorViewModel : AssetEditorViewModel<TileSetViewModel>, IAddChildViewModel
{
    // The tiles a solid drag passed, in order, and the value they get at its release; null when no drag is open
    private List<TileId>? solidDrag;
    private bool solidValue;
    // An undo or a redo dropped the drag in progress: the rest of it is ignored until the button goes up
    private bool solidDragDropped;
    // The grid changed a copy of tile data, and the write to the asset waits for the grid's transaction to end
    private bool tileDataDirty;
    private bool tileDataWriteQueued;
    private bool writingTileData;

    /// <summary>
    /// Makes the editor of a tile set; <see cref="Initialize"/> opens it.
    /// </summary>
    public TileSetEditorViewModel(TileSetViewModel asset)
        : base(asset)
    {
        Pictures = TilePictureService.Of(asset.Session).Get(asset);
        AddImageCommand = new AnonymousTaskCommand(ServiceProvider, AddImageAsync);
        AddSpriteSheetCommand = new AnonymousTaskCommand(ServiceProvider, AddSpriteSheetAsync);
        RemoveSourceCommand = new AnonymousTaskCommand(ServiceProvider, RemoveSourceAsync);
        RefreshCommand = new AnonymousCommand(ServiceProvider, Pictures.Refresh);
        RebindSpriteCommand = new AnonymousTaskCommand<string>(ServiceProvider, RebindSpriteAsync) { IsEnabled = false };
        Scene.Mode = TilePickMode.Many;
    }

    /// <summary>
    /// The pictures of the tile set's sources.
    /// </summary>
    public TileSetPictures Pictures { get; }

    /// <summary>
    /// The sources, in the order of the asset; the list follows the asset.
    /// </summary>
    public IReadOnlyObservableList<TileSourceItemViewModel> Sources => Items;

    /// <summary>
    /// The source shown on the canvas and in the property grid; the first one at first.
    /// </summary>
    public TileSourceItemViewModel? SelectedSource
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            // The scene shows the source before the view hears of it, so that the view fits the new source
            ShowLayout(value?.Layout);
            SetValue(ref field, value);
            SelectionFollows(show: true);
        }
    }

    /// <summary>
    /// What a press on a tile does: select it, or set it solid.
    /// </summary>
    public TileSetEditorMode Mode
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                // The scene forgets the press in progress
                EndSolidDrag();
                Scene.Mode = value == TileSetEditorMode.Solid ? TilePickMode.Paint : TilePickMode.Many;
            }
        }
    }

    /// <summary>
    /// The selected tiles of the selected source, in the order of their ids; the property grid shows their data.
    /// </summary>
    public IReadOnlyList<TileId> SelectedTiles
    {
        get;
        private set => SetValue(ref field, value);
    } = [];

    /// <summary>
    /// The name of the sprite that the selected tile has and its sheet has not, when one missing tile of a sprite sheet source
    /// is selected; null otherwise.
    /// </summary>
    public string? MissingSpriteName
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The names of the sprites that the selected tile can take, when it is one missing tile of a sprite sheet source: the
    /// sprites of the sheet that other tiles have.
    /// </summary>
    public IReadOnlyList<string> RebindCandidates
    {
        get;
        private set => SetValue(ref field, value);
    } = [];

    /// <summary>
    /// Gives the selected missing tile a sprite of <see cref="RebindCandidates"/>, the parameter, after a question: the tile
    /// that had the sprite goes away.
    /// </summary>
    public ICommandBase RebindSpriteCommand { get; }

    /// <summary>
    /// The selected source on the canvas.
    /// </summary>
    public TileSourceScene Scene { get; } = new();

    /// <summary>
    /// The zoom and the pan of the canvas.
    /// </summary>
    public CanvasView View { get; } = new();

    /// <summary>
    /// An image that waits for its Add or its Cancel; null when there is none.
    /// </summary>
    public PendingImageSourceViewModel? PendingImage
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// What is wrong in the tile set: the checks of the build, the images that cannot be read, and the sprites that are gone.
    /// </summary>
    public IReadOnlyList<string> Problems
    {
        get;
        private set => SetValue(ref field, value);
    } = [];

    /// <summary>
    /// Why the last thing asked was not done; null when it was.
    /// </summary>
    public string? Message
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// Picks an image file, and proposes it as a source.
    /// </summary>
    public ICommandBase AddImageCommand { get; }

    /// <summary>
    /// Picks a sprite sheet of the session, and adds it as a source.
    /// </summary>
    public ICommandBase AddSpriteSheetCommand { get; }

    /// <summary>
    /// Removes the selected source, after a question.
    /// </summary>
    public ICommandBase RemoveSourceCommand { get; }

    /// <summary>
    /// Reads the image files again.
    /// </summary>
    public ICommandBase RefreshCommand { get; }

    private ObservableList<TileSourceItemViewModel> Items { get; } = [];

    // The copies of the data of the selected tiles that the property grid shows
    private List<TileDataViewModel> TileData { get; } = [];

    private IEditorDialogService Dialogs => ServiceProvider.Get<IEditorDialogService>();

    /// <summary>
    /// Shows the sources, and binds the sprites of its sheets that have no tile yet, in one step.
    /// </summary>
    public override Task<bool> Initialize()
    {
        Asset.PropertyGraph.Changed += GraphChanged;
        Asset.PropertyGraph.ItemChanged += GraphItemChanged;
        Pictures.Changed += PicturesChanged;
        Session.AssetPropertiesChanged += AssetPropertiesChanged;
        UndoRedoService.Undone += HistoryMoved;
        UndoRedoService.Redone += HistoryMoved;
        Scene.SelectionChanged += SceneSelectionChanged;
        Scene.Painted += ScenePainted;
        Scene.PaintEnded += ScenePaintEnded;
        Scene.PaintCancelled += ScenePaintCancelled;
        Follow();
        SelectedSource = Items.FirstOrDefault();
        if (SelectedSource is null)
        {
            ShowProperties();
        }
        BindNewSprites();
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        Asset.PropertyGraph.Changed -= GraphChanged;
        Asset.PropertyGraph.ItemChanged -= GraphItemChanged;
        Pictures.Changed -= PicturesChanged;
        Session.AssetPropertiesChanged -= AssetPropertiesChanged;
        UndoRedoService.Undone -= HistoryMoved;
        UndoRedoService.Redone -= HistoryMoved;
        Scene.SelectionChanged -= SceneSelectionChanged;
        Scene.Painted -= ScenePainted;
        Scene.PaintEnded -= ScenePaintEnded;
        Scene.PaintCancelled -= ScenePaintCancelled;
        DropTileData();
        foreach (var item in Items)
        {
            item.Destroy();
        }
        base.Destroy();
    }

    /// <summary>
    /// Proposes an image file, an absolute path, as a source: <see cref="PendingImage"/> shows its grid, and nothing is in the
    /// asset until its Add.
    /// </summary>
    public void ProposeImage(UFile image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var asset = Asset.Asset;
        var picture = TilePictureService.Of(Session).Load(image, asset.ColorKeyEnabled ? asset.ColorKeyColor : null, out var problem);
        PendingImage = new PendingImageSourceViewModel(ServiceProvider, image, picture, problem, UniqueName(image.GetFileNameWithoutExtension() ?? "image"), asset.TileSize, AddPendingImage, () => PendingImage = null);
        Message = null;
    }

    /// <summary>
    /// Adds a sprite sheet as a source, with each of its sprites bound to a tile, in one step; false, with a
    /// <see cref="Message"/>, when the tile set has that sheet already.
    /// </summary>
    public bool AddSpriteSheet(SpriteSheetViewModel sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (Asset.Asset.Sources.OfType<SpriteSheetTileSetSource>().Any(source => SheetId(source) == sheet.Id))
        {
            Message = $"The tile set has the sprite sheet '{sheet.Name}' already.";
            return false;
        }
        if (!CanAddSource())
        {
            return false;
        }
        SpriteSheetTileSetSource added;
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            added = TileSetGraph.AddSheet(Asset, UniqueName(sheet.Name), ContentReferenceHelper.CreateReference<SpriteSheet>(sheet), sheet.Asset.Sprites.Select(sprite => sprite.Name));
            UndoRedoService.SetName(transaction, "Add sprite sheet");
        }
        Select(added);
        Message = null;
        return true;
    }

    /// <inheritdoc/>
    public bool CanAddChildren(IReadOnlyCollection<object> children, AddChildModifiers modifiers, out string message)
    {
        if (children.Count > 0 && children.All(child => child is SpriteSheetViewModel))
        {
            message = children.Count == 1 ? "Add the sprite sheet as a source" : "Add the sprite sheets as sources";
            return true;
        }
        message = "Only sprite sheets and image files can be dropped on a tile set.";
        return false;
    }

    /// <inheritdoc/>
    public void AddChildren(IReadOnlyCollection<object> children, AddChildModifiers modifiers)
    {
        foreach (var sheet in children.OfType<SpriteSheetViewModel>())
        {
            AddSpriteSheet(sheet);
        }
    }

    /// <summary>
    /// Picks an image file, and proposes it as a source.
    /// </summary>
    internal async Task AddImageAsync()
    {
        var images = new FilePickerFilter("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga", "*.dds", "*.gif"] };
        if (await Dialogs.OpenFilePickerAsync(Asset.AssetItem.FullPath.GetParent(), [images]) is { } file)
        {
            ProposeImage(file);
        }
    }

    /// <summary>
    /// Picks a sprite sheet in the session, and adds it as a source.
    /// </summary>
    internal async Task AddSpriteSheetAsync()
    {
        if (await Dialogs.OpenAssetPickerAsync(Session, [typeof(SpriteSheetAsset)]) is SpriteSheetViewModel sheet)
        {
            AddSpriteSheet(sheet);
        }
    }

    private async Task RemoveSourceAsync()
    {
        if (SelectedSource is not { } item)
        {
            return;
        }
        var question = $"Remove the source '{item.Name}'? Maps that use its tiles keep their cells, and show them as missing.";
        if (await Dialogs.MessageBoxAsync(question, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        using var transaction = UndoRedoService.CreateTransaction();
        TileSetGraph.RemoveSource(Asset, item.Source);
        UndoRedoService.SetName(transaction, "Remove source");
    }

    private void AddPendingImage(PendingImageSourceViewModel pending)
    {
        if (!CanAddSource())
        {
            return;
        }
        GridTileSetSource added;
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            added = TileSetGraph.AddImage(Asset, UniqueName(pending.Name), pending.Image, pending.TileSize, pending.Margin, pending.Spacing);
            UndoRedoService.SetName(transaction, "Add image");
        }
        PendingImage = null;
        Select(added);
        Message = null;
    }

    private bool CanAddSource()
    {
        if (Asset.Asset.LastSourceId < TileId.MaxSource)
        {
            return true;
        }
        Message = string.Create(CultureInfo.InvariantCulture, $"The tile set has given all {TileId.MaxSource} ids of sources: make another tile set for more images.");
        return false;
    }

    // A name no source has, without regard to case: "town", then "town 2"
    private string UniqueName(string name)
    {
        var taken = Asset.Asset.Sources.Select(source => source.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name))
        {
            return name;
        }
        for (var number = 2; ; number++)
        {
            var numbered = string.Create(CultureInfo.InvariantCulture, $"{name} {number}");
            if (!taken.Contains(numbered))
            {
                return numbered;
            }
        }
    }

    private void Select(TileSetSource source) => SelectedSource = Items.FirstOrDefault(item => item.Source == source) ?? SelectedSource;

    // The sprites of the sheets that no tile has, bound in one step; nothing when every sprite has its tile
    private void BindNewSprites()
    {
        var asset = Asset.Asset;
        var unbound = new List<(SpriteSheetTileSetSource Source, List<string> Names)>();
        foreach (var source in asset.Sources.OfType<SpriteSheetTileSetSource>())
        {
            if (SheetOf(source) is not { } sheet)
            {
                continue;
            }
            var bound = source.Bindings.Select(binding => binding.SpriteName).ToHashSet(StringComparer.Ordinal);
            var names = sheet.Sprites.Select(sprite => sprite.Name).Where(name => !bound.Contains(name)).Distinct(StringComparer.Ordinal).ToList();
            if (names.Count > 0)
            {
                unbound.Add((source, names));
            }
        }
        if (unbound.Count == 0)
        {
            return;
        }
        using var transaction = UndoRedoService.CreateTransaction();
        foreach (var (source, names) in unbound)
        {
            TileSetGraph.BindSprites(Asset, source, names);
        }
        UndoRedoService.SetName(transaction, "Bind sprites");
    }

    // The list follows the sources of the asset, the selection stays on its source, or goes to the one before when it is gone
    private void Follow()
    {
        var sources = Asset.Asset.Sources;
        var selected = SelectedSource;
        var selectedIndex = selected is null ? -1 : Items.IndexOf(selected);
        foreach (var gone in Items.Where(item => !sources.Contains(item.Source)).ToList())
        {
            Items.Remove(gone);
            gone.Destroy();
        }
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var item = Items.FirstOrDefault(existing => existing.Source == source);
            if (item is null)
            {
                item = new TileSourceItemViewModel(this, source);
                Items.Insert(index, item);
            }
            else if (Items.IndexOf(item) != index)
            {
                Items.Remove(item);
                Items.Insert(index, item);
            }
            item.Name = source.Name;
            item.Layout = Pictures.Sources.FirstOrDefault(layout => layout.Source == source);
        }
        if (selected is not null && !Items.Contains(selected))
        {
            SelectedSource = Items.Count == 0 ? null : Items[Math.Clamp(selectedIndex - 1, 0, Items.Count - 1)];
        }
        ShowLayout(SelectedSource?.Layout);
        // The selected tiles that the source no longer has leave the selection
        SelectionFollows(show: false);
        UpdateRebind();
        TileDataFollows();
        UpdateProblems();
        // The marks of the solid tiles are drawn again
        Scene.MarkOf = SolidMark;
    }

    // The scene forgets a press when its source changes: the solid drag in progress ends with it. The pictures of the same
    // source, made again, keep both
    private void ShowLayout(TileSourceLayout? layout)
    {
        if (Scene.Layout?.Source != layout?.Source)
        {
            EndSolidDrag();
        }
        Scene.Layout = layout;
    }

    // The selected tiles are the scene's; the property grid shows them when they change, or when asked
    private void SelectionFollows(bool show)
    {
        if (SelectedTiles.SequenceEqual(Scene.Selection))
        {
            if (show)
            {
                ShowProperties();
            }
            return;
        }
        SelectedTiles = Scene.Selection;
        UpdateRebind();
        ShowProperties();
    }

    // After a change of the asset that is not the editor's own, the copies that say something else than the asset are made
    // again, and the grid shows them
    private void TileDataFollows()
    {
        if (writingTileData || tileDataDirty || TileData.Count == 0)
        {
            return;
        }
        var asset = Asset.Asset;
        if (TileData.All(data => TileSetGraph.HaveSameData(data.ToDefinition(), asset.GetDefinition(data.Tile) ?? new TileDefinition { Tile = data.Tile })))
        {
            return;
        }
        ShowProperties();
    }

    private void UpdateRebind()
    {
        MissingSpriteName = SelectedTiles is [var tile] && Scene.Layout is { Source: SpriteSheetTileSetSource } layout && layout.SlotOf(tile) is { IsMissing: true } slot
            ? slot.SpriteName
            : null;
        RebindCandidates = RebindCandidatesOf(SelectedTiles);
        RebindSpriteCommand.IsEnabled = RebindCandidates.Count > 0;
    }

    // The sprites of the sheet that other tiles have, when the tiles are one missing tile of a sheet source
    private List<string> RebindCandidatesOf(IReadOnlyList<TileId> tiles)
    {
        if (tiles is not [var tile]
            || Scene.Layout is not { Source: SpriteSheetTileSetSource source } layout
            || layout.SlotOf(tile) is not { IsMissing: true }
            || SheetOf(source) is not { } sheet)
        {
            return [];
        }
        return [.. sheet.Sprites.Select(sprite => sprite.Name).Distinct(StringComparer.Ordinal)
            .Where(name => source.Bindings.Any(binding => binding.SpriteName == name && binding.Number != tile.Number))];
    }

    private async Task RebindSpriteAsync(string spriteName)
    {
        if (!RebindCandidates.Contains(spriteName) || SelectedTiles is not [var tile] || Scene.Layout?.Source is not SpriteSheetTileSetSource source)
        {
            return;
        }
        var from = source.Bindings.First(binding => binding.SpriteName == spriteName && binding.Number != tile.Number).Number;
        var question = string.Create(CultureInfo.InvariantCulture, $"Give the sprite '{spriteName}' to the tile #{tile.Number}? The tile #{from}, which has it now, goes away: cells of maps that use #{from} will show as missing.");
        if (await Dialogs.MessageBoxAsync(question, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        using var transaction = UndoRedoService.CreateTransaction();
        TileSetGraph.Rebind(Asset, source, tile.Number, from);
        UndoRedoService.SetName(transaction, "Rebind sprite");
    }

    private bool IsSolid(TileId tile) => Asset.Asset.GetDefinition(tile) is { } definition && definition.Collision.HasFlag(TileCollision.Solid);

    // A solid tile is hatched; during a solid drag, the tiles it passed show the value they get at its release
    private CanvasMark? SolidMark(TileId tile)
    {
        var solid = solidDrag is not null && solidDrag.Contains(tile) ? solidValue : IsSolid(tile);
        return solid ? CanvasMark.Hatch : null;
    }

    private void SceneSelectionChanged(object? sender, EventArgs e) => SelectionFollows(show: false);

    // The first tile of a drag gives the value: solid when it was not, not solid when it was
    private void ScenePainted(object? sender, TileId tile)
    {
        if (solidDragDropped)
        {
            return;
        }
        if (solidDrag is null)
        {
            solidDrag = [];
            solidValue = !IsSolid(tile);
        }
        if (!solidDrag.Contains(tile))
        {
            solidDrag.Add(tile);
            Scene.MarkOf = SolidMark;
        }
    }

    // The release writes the drag in one step, and makes none when no tile changes
    private void ScenePaintEnded(object? sender, EventArgs e)
    {
        var tiles = solidDrag;
        EndSolidDrag();
        if (tiles is null)
        {
            return;
        }
        using (var transaction = UndoRedoService.CreateTransaction())
        {
            foreach (var tile in tiles)
            {
                TileSetGraph.SetSolid(Asset, tile, solidValue);
            }
            UndoRedoService.SetName(transaction, "Solid");
        }
        Scene.MarkOf = SolidMark;
    }

    // The canvas lost the pointer in the middle of a drag: nothing is written, and the marks show the asset again
    private void ScenePaintCancelled(object? sender, EventArgs e)
    {
        EndSolidDrag();
        Scene.MarkOf = SolidMark;
    }

    private void EndSolidDrag()
    {
        solidDrag = null;
        solidDragDropped = false;
    }

    // An undo or a redo drops what the person had in progress: the solid drag, and a change of tile data not written yet.
    // The editor then follows the asset
    private void HistoryMoved(object? sender, TransactionEventArgs e)
    {
        if (solidDrag is not null)
        {
            solidDrag = null;
            solidDragDropped = true;
            Scene.MarkOf = SolidMark;
        }
        tileDataDirty = false;
        TileDataFollows();
    }

    // Raised inside the grid's transaction, which names the step after the grid's own action: the asset is written once that
    // transaction is over, in a step of its own, and the changes of several tiles in one step
    private void TileDataChanged(object? sender, EventArgs e)
    {
        if (writingTileData)
        {
            return;
        }
        tileDataDirty = true;
        if (!tileDataWriteQueued)
        {
            tileDataWriteQueued = true;
            _ = Dispatcher.InvokeAsync(WriteTileData);
        }
    }

    private void WriteTileData()
    {
        if (IsDestroyed)
        {
            return;
        }
        if (UndoRedoService.TransactionInProgress)
        {
            _ = UndoRedoService.TransactionCompletion.ContinueWith(_ => Dispatcher.InvokeAsync(WriteTileData), TaskScheduler.Default);
            return;
        }
        tileDataWriteQueued = false;
        if (!tileDataDirty)
        {
            return;
        }
        tileDataDirty = false;
        var asset = Asset.Asset;
        var changed = TileData
            .Select(data => data.ToDefinition())
            .Where(definition => !TileSetGraph.HaveSameData(definition, asset.GetDefinition(definition.Tile) ?? new TileDefinition { Tile = definition.Tile }))
            .ToList();
        if (changed.Count == 0)
        {
            return;
        }
        writingTileData = true;
        try
        {
            using var transaction = UndoRedoService.CreateTransaction();
            foreach (var definition in changed)
            {
                TileSetGraph.SetDefinition(Asset, definition);
            }
            UndoRedoService.SetName(transaction, "Tile data");
        }
        finally
        {
            writingTileData = false;
        }
    }

    private void DropTileData()
    {
        foreach (var data in TileData)
        {
            data.Changed -= TileDataChanged;
            data.Destroy();
        }
        TileData.Clear();
        tileDataDirty = false;
    }

    private void UpdateProblems()
    {
        var problems = new List<string>(TileAssetChecks.Of(Asset.Asset));
        foreach (var layout in Pictures.Sources)
        {
            if (layout.Problem is { } problem && !problems.Contains(problem))
            {
                problems.Add(problem);
            }
            if (layout.Source is SpriteSheetTileSetSource sheet)
            {
                foreach (var slot in layout.Slots.Where(slot => slot.IsMissing && slot.SpriteName is not null))
                {
                    problems.Add(string.Create(CultureInfo.InvariantCulture, $"The sprite '{slot.SpriteName}' of the source '{sheet.Name}' is not in its sheet: its tile #{slot.Tile.Number} shows as missing."));
                }
            }
        }
        if (!problems.SequenceEqual(Problems))
        {
            Problems = problems;
        }
    }

    private void ShowProperties()
    {
        if (IsDestroyed)
        {
            return;
        }
        DropTileData();
        if (SelectedTiles.Count > 0)
        {
            foreach (var tile in SelectedTiles)
            {
                var data = new TileDataViewModel(ServiceProvider, Session.AssetNodeContainer, tile, Asset.Asset.GetDefinition(tile));
                data.Changed += TileDataChanged;
                TileData.Add(data);
            }
            EditorProperties.TypeDescription = SelectedTiles.Count == 1 ? "Tile" : "Tiles";
            EditorProperties.Name = SelectedTiles.Count == 1 ? SelectedTiles[0].ToString() : string.Create(CultureInfo.InvariantCulture, $"{SelectedTiles.Count} tiles");
            EditorProperties.GenerateSelectionPropertiesAsync(TileData.ToList()).Forget();
            return;
        }
        if (SelectedSource is not { } item)
        {
            ShowAssetProperties();
            return;
        }
        EditorProperties.TypeDescription = item.IsGrid ? "Image source" : "Sprite sheet source";
        EditorProperties.Name = item.Name;
        EditorProperties.GenerateSelectionPropertiesAsync([item]).Forget();
    }

    private void GraphChanged(object? sender, AssetMemberNodeChangeEventArgs e) => Follow();

    private void GraphItemChanged(object? sender, AssetItemNodeChangeEventArgs e) => Follow();

    private void PicturesChanged(object? sender, EventArgs e) => Follow();

    // Raised on a thread of the pool, once the session's history recorded a change: a sheet that changed may have new sprites
    private void AssetPropertiesChanged(object? sender, AssetChangedEventArgs e)
    {
        if (e.Assets.Any(asset => asset is SpriteSheetViewModel))
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (!IsDestroyed)
                {
                    BindNewSprites();
                }
            });
        }
    }

    private SpriteSheetAsset? SheetOf(SpriteSheetTileSetSource source)
        => SheetId(source) is { } id && Session.GetAssetById(id) is { IsDeleted: false, Asset: SpriteSheetAsset sheet } ? sheet : null;

    private static AssetId? SheetId(SpriteSheetTileSetSource source)
        => source.Sheet is null ? null : AttachedReferenceManager.GetAttachedReference(source.Sheet)?.Id;
}
