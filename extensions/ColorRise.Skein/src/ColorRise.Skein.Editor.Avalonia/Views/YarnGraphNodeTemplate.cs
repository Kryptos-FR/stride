using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ColorRise.Skein.Editor.Graph;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The template of the nodes of the graph of a script: one for the cards of its nodes, one for the stubs of the nodes it
/// jumps to elsewhere.
/// </summary>
public sealed class YarnGraphNodeTemplate : IDataTemplate
{
    /// <summary>
    /// The template of a <see cref="YarnNodeCardViewModel"/>.
    /// </summary>
    public IDataTemplate? Card { get; set; }

    /// <summary>
    /// The template of a <see cref="YarnStubCardViewModel"/>.
    /// </summary>
    public IDataTemplate? Stub { get; set; }

    /// <inheritdoc/>
    public Control? Build(object? param) => (param is YarnStubCardViewModel ? Stub : Card)?.Build(param);

    /// <inheritdoc/>
    public bool Match(object? data) => data is YarnNodeCardViewModel or YarnStubCardViewModel;
}
