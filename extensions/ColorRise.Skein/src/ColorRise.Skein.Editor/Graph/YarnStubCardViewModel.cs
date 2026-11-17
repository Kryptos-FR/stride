using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// The card of a node a jump names that is not in the script of the graph: a node of another script of the project, or
/// a node that is nowhere.
/// </summary>
public sealed class YarnStubCardViewModel : ViewModelBase, IYarnGraphNode
{
    private YarnGraphPoint position;

    internal YarnStubCardViewModel(YarnNodeCardViewModel source, string title, string? fileName, YarnScriptViewModel? script, bool isComputed = false)
    {
        Source = source;
        IsComputed = isComputed;
        Title = title;
        FileName = fileName;
        Script = script;
        InSlot = new YarnCardSlot(this, isInput: true);
    }

    /// <summary>
    /// The card the jump is in: the stub sits beside it.
    /// </summary>
    public YarnNodeCardViewModel Source { get; }

    /// <summary>
    /// The title the jump names; for a jump to an expression, the expression.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The file of the node, as the compiler names it; null when no node of the project has the title.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// The script of the node; null when no node of the project has the title.
    /// </summary>
    public YarnScriptViewModel? Script { get; }

    /// <summary>
    /// Whether the jump names an expression (<c>&lt;&lt;jump {$next}&gt;&gt;</c>): the node is known when it plays.
    /// </summary>
    public bool IsComputed { get; }

    /// <summary>
    /// Whether the jump leads nowhere.
    /// </summary>
    public bool IsBroken => FileName is null && !IsComputed;

    /// <summary>
    /// Where the stub is on the graph.
    /// </summary>
    public YarnGraphPoint Position { get => position; internal set => SetValue(ref position, value); }

    /// <summary>
    /// The slot the links to the node arrive on.
    /// </summary>
    public YarnCardSlot InSlot { get; }
}
