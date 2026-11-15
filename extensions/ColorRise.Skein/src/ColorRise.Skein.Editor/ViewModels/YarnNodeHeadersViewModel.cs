using ColorRise.Skein.Editor.Nodes;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Quantum;

namespace ColorRise.Skein.Editor.ViewModels;

/// <summary>
/// Gives the session's property grid the headers of a Yarn node. A script has no property graph, so the headers get a graph
/// of their own in the session's node container.
/// </summary>
public sealed class YarnNodeHeadersViewModel : IPropertyProviderViewModel, IDisposable
{
    private readonly IObjectNode rootNode;
    private readonly GraphNodeChangeListener listener;

    public YarnNodeHeadersViewModel(ISessionViewModel session, YarnNodeHeaders headers)
    {
        Headers = headers;
        rootNode = session.AssetNodeContainer.GetOrCreateNode(headers);
        listener = new GraphNodeChangeListener(rootNode);
        listener.Initialize();
        listener.ValueChanged += OnChanged;
        listener.ItemChanged += OnChanged;
    }

    /// <summary>
    /// The headers shown.
    /// </summary>
    public YarnNodeHeaders Headers { get; }

    /// <summary>
    /// Raised after the property grid changed a header, inside the undo transaction of that change.
    /// </summary>
    public event Action<YarnNodeHeaders>? Changed;

    /// <inheritdoc/>
    bool IPropertyProviderViewModel.CanProvidePropertiesViewModel => true;

    /// <inheritdoc/>
    IObjectNode IPropertyProviderViewModel.GetRootNode() => rootNode;

    /// <inheritdoc/>
    bool IPropertyProviderViewModel.ShouldConstructMember(IMemberNode member) => true;

    /// <inheritdoc/>
    bool IPropertyProviderViewModel.ShouldConstructItem(IObjectNode collection, NodeIndex index) => true;

    /// <inheritdoc/>
    public void Dispose()
    {
        listener.ValueChanged -= OnChanged;
        listener.ItemChanged -= OnChanged;
        listener.Dispose();
    }

    private void OnChanged(object? sender, INodeChangeEventArgs e) => Changed?.Invoke(Headers);
}
