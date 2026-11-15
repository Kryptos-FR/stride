using Stride.Core;

namespace ColorRise.Saga.Content;

/// <summary>
/// The main plot: acts linked to each other. An object of its own so that sub-plots can come later.
/// </summary>
[DataContract("SagaPlotDefinition")]
public sealed class PlotDefinition
{
    public string FirstActId { get; set; } = string.Empty;

    public List<ActDefinition> Acts { get; set; } = [];
}
