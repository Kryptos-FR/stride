using Stride.Core.Assets.Presentation.Components.Properties;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// <summary>
/// A clip in the tree of the flipbook editor, under its animation, and what the property grid shows for it.
/// </summary>
public sealed class FlipbookClipItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    private readonly FlipbookEditorViewModel editor;

    internal FlipbookClipItemViewModel(FlipbookEditorViewModel editor, FlipbookAnimationItemViewModel animation, FlipbookClip clip)
        : base(editor.ServiceProvider)
    {
        this.editor = editor;
        Animation = animation;
        Clip = clip;
    }

    /// <summary>
    /// The item of the animation the clip is in.
    /// </summary>
    public FlipbookAnimationItemViewModel Animation { get; }

    /// <summary>
    /// The clip of the asset.
    /// </summary>
    public FlipbookClip Clip { get; }

    /// <summary>
    /// The direction the clip is for.
    /// </summary>
    public FlipbookDirection Direction => Clip.Direction;

    /// <summary>
    /// The direction as the tree shows it: "Any direction" for <see cref="FlipbookDirection.None"/>, else "Down", "Up left"...
    /// </summary>
    public string Label => LabelOf(Clip.Direction);

    /// <summary>
    /// How many frames the clip has.
    /// </summary>
    public int FrameCount => Clip.Frames.Count(frame => frame is not null);

    bool IPropertyProviderViewModel.CanProvidePropertiesViewModel => !IsDestroyed;

    AssetViewModel IAssetPropertyProviderViewModel.RelatedAsset => editor.Asset;

    /// <summary>
    /// The name of a direction as the editor shows it: "Any direction" for <see cref="FlipbookDirection.None"/>, else its
    /// words, such as "Up left".
    /// </summary>
    public static string LabelOf(FlipbookDirection direction) => FlipbookDirections.NameOf(direction);

    /// <summary>
    /// Tells the views that the direction or the frames of the clip may have changed.
    /// </summary>
    internal void Refresh() => OnPropertyChanged(nameof(Direction), nameof(Label), nameof(FrameCount));

    /// <summary>
    /// The question asked before the clip is removed with its frames, such as "Remove the clip Left of 'walk' and its 4 frames?".
    /// </summary>
    internal string RemoveQuestion()
        => $"Remove the clip {LabelOf(Clip.Direction)} of '{Animation.Name}' and its {FlipbookEditorViewModel.FramesText(FrameCount)}?";

    IObjectNode? IPropertyProviderViewModel.GetRootNode() => editor.Session.AssetNodeContainer.GetNode(Clip);

    GraphNodePath IAssetPropertyProviderViewModel.GetAbsolutePathToRootNode() => editor.PathOf(Animation.Animation, Clip);

    bool IPropertyProviderViewModel.ShouldConstructMember(IMemberNode member) => FlipbookEditorViewModel.ShowsMember(member);

    bool IPropertyProviderViewModel.ShouldConstructItem(IObjectNode collection, NodeIndex index) => true;
}
