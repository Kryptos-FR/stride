using ColorRise.Skein.Assets;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.Text;
using Stride.Core.Assets.Presentation.Annotations;
using Stride.Core.Assets.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.ViewModels;

/// <summary>
/// A Yarn script in the session.
/// </summary>
[AssetViewModel<YarnScriptAsset>]
public sealed class YarnScriptViewModel : AssetViewModel<YarnScriptAsset>
{
    public YarnScriptViewModel(AssetViewModelConstructionParameters parameters)
        : base(parameters)
    {
        TextBuffer = new YarnTextBuffer(this);

        // A change of the text, and a new, renamed or moved script, are for the analysis to see
        TextBuffer.Changed += _ => ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
        PropertyChanged += (_, _) => ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
        ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
    }

    /// <summary>
    /// The script's text, changed as steps of the session's undo history.
    /// </summary>
    public YarnTextBuffer TextBuffer { get; }

    /// <inheritdoc/>
    public override void Destroy()
    {
        TextBuffer.Dispose();
        ServiceProvider.TryGet<YarnAnalysisService>()?.Invalidate();
        base.Destroy();
    }
}
