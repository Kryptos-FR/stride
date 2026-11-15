using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// A link of a Saga graph, from an output slot to an input slot.
/// </summary>
public sealed class SagaGraphLinkViewModel : SagaGraphItemViewModel
{
    internal SagaGraphLinkViewModel(IViewModelServiceProvider serviceProvider, AssetViewModel relatedAsset, SagaGraphSlot source, SagaGraphSlot target, object? card, Func<GraphNodePath>? path)
        : base(serviceProvider, relatedAsset, card, path)
    {
        Source = source;
        Target = target;
    }

    public SagaGraphSlot Source { get; }

    public SagaGraphSlot Target { get; }

    public override string ToString() => $"{Source} -> {Target}";
}
