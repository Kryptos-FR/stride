using Stride.Core.Assets.Presentation.Components.Properties;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// <summary>
/// An animation in the tree of the flipbook editor, with its clips under it, and what the property grid shows for it.
/// </summary>
public sealed class FlipbookAnimationItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel, IIsEditableViewModel
{
    private readonly FlipbookEditorViewModel editor;
    private ObservableList<FlipbookClipItemViewModel> ClipItems { get; } = [];

    internal FlipbookAnimationItemViewModel(FlipbookEditorViewModel editor, FlipbookAnimation animation)
        : base(editor.ServiceProvider)
    {
        this.editor = editor;
        Animation = animation;
        Follow();
    }

    /// <summary>
    /// The animation of the asset.
    /// </summary>
    public FlipbookAnimation Animation { get; }

    /// <summary>
    /// The name of the animation. A new name is one step "Rename animation"; an empty name, or the name of another animation,
    /// is refused, the name goes back, and the editor's message says why.
    /// </summary>
    public string Name
    {
        get => Animation.Name;
        set
        {
            editor.Rename(this, value);
            OnPropertyChanged(nameof(Name));
        }
    }

    /// <summary>
    /// Whether the name can be typed in the tree: always.
    /// </summary>
    public bool IsEditable => true;

    /// <summary>
    /// Whether the name is being typed in the tree.
    /// </summary>
    public bool IsEditing
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// The clips of the animation, in the order of the asset.
    /// </summary>
    public IReadOnlyObservableList<FlipbookClipItemViewModel> Clips => ClipItems;

    bool IPropertyProviderViewModel.CanProvidePropertiesViewModel => !IsDestroyed;

    AssetViewModel IAssetPropertyProviderViewModel.RelatedAsset => editor.Asset;

    /// <summary>
    /// Lets go of the items of the clips.
    /// </summary>
    public override void Destroy()
    {
        foreach (var item in ClipItems)
        {
            item.Destroy();
        }
        base.Destroy();
    }

    /// <summary>
    /// Follows the animation: its name, and an item per clip, kept while the clip is in the animation.
    /// </summary>
    internal void Follow()
    {
        var wanted = new List<FlipbookClipItemViewModel>();
        foreach (var clip in Animation.Clips)
        {
            if (clip is not null)
            {
                wanted.Add(ClipItems.FirstOrDefault(item => item.Clip == clip) ?? new FlipbookClipItemViewModel(editor, this, clip));
            }
        }
        FlipbookEditorViewModel.Sync(ClipItems, wanted);
        foreach (var item in ClipItems)
        {
            item.Refresh();
        }
        OnPropertyChanged(nameof(Name));
    }

    IObjectNode? IPropertyProviderViewModel.GetRootNode() => editor.Session.AssetNodeContainer.GetNode(Animation);

    GraphNodePath IAssetPropertyProviderViewModel.GetAbsolutePathToRootNode() => editor.PathOf(Animation);

    bool IPropertyProviderViewModel.ShouldConstructMember(IMemberNode member) => FlipbookEditorViewModel.ShowsMember(member);

    bool IPropertyProviderViewModel.ShouldConstructItem(IObjectNode collection, NodeIndex index) => true;
}
