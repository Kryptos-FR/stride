using ColorRise.Skein.Assets.Text;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// The card of a node of a script on the graph. It is the same object for as long as the text keeps the node, so a
/// selection lasts through the changes of the text.
/// </summary>
public sealed class YarnNodeCardViewModel : ViewModelBase
{
    private string title = "";
    private int groupIndex;
    private int groupCount = 1;
    private IReadOnlyList<string> when = [];
    private int complexity = -1;
    private IReadOnlyList<string> tags = [];
    private IReadOnlyList<string> speakers = [];
    private string preview = "";
    private IReadOnlyList<string> options = [];
    private YarnGraphPoint position;
    private bool hasPosition;
    private string? color;
    private string? cluster;
    private bool isNote;
    private string? groupValue;
    private bool isFilteredOut;
    private bool hasError;
    private YarnPlayMark playMark;

    internal YarnNodeCardViewModel(YarnNodeSpan span)
    {
        Span = span;
        InSlot = new YarnCardSlot(this, isInput: true);
        OutSlot = new YarnCardSlot(this, isInput: false);
    }

    /// <summary>
    /// The place of the node in the text as it is now.
    /// </summary>
    public YarnNodeSpan Span { get; internal set; }

    /// <summary>
    /// The node's title. The nodes of a node group share it.
    /// </summary>
    public string Title { get => title; internal set => SetValue(ref title, value); }

    /// <summary>
    /// Which of the nodes of its title the node is in the script, from 0.
    /// </summary>
    public int GroupIndex { get => groupIndex; internal set => SetValue(ref groupIndex, value); }

    /// <summary>
    /// How many nodes of the script have the title: more than one for a node group.
    /// </summary>
    public int GroupCount { get => groupCount; internal set => SetValue(ref groupCount, value); }

    /// <summary>
    /// The conditions of the node's <c>when:</c> headers, as written.
    /// </summary>
    public IReadOnlyList<string> When { get => when; internal set => SetList(ref when, value, nameof(When)); }

    /// <summary>
    /// How specific the <c>when:</c> conditions are, as the compiler counts it; -1 outside of a node group, or before
    /// the node was compiled.
    /// </summary>
    public int Complexity { get => complexity; internal set => SetValue(ref complexity, value); }

    /// <summary>
    /// The words of the <c>tags:</c> header.
    /// </summary>
    public IReadOnlyList<string> Tags { get => tags; internal set => SetList(ref tags, value, nameof(Tags)); }

    /// <summary>
    /// The characters that speak in the node, in the order they first do.
    /// </summary>
    public IReadOnlyList<string> Speakers { get => speakers; internal set => SetList(ref speakers, value, nameof(Speakers)); }

    /// <summary>
    /// The first lines of the node.
    /// </summary>
    public string Preview { get => preview; internal set => SetValue(ref preview, value); }

    /// <summary>
    /// The texts of the node's options, in the order of the text.
    /// </summary>
    public IReadOnlyList<string> Options { get => options; internal set => SetList(ref options, value, nameof(Options)); }

    /// <summary>
    /// Where the card is: the <c>position:</c> header, or a place the editor worked out for a node without one.
    /// </summary>
    public YarnGraphPoint Position { get => position; internal set => SetValue(ref position, value); }

    /// <summary>
    /// Whether the node has a <c>position:</c> header.
    /// </summary>
    public bool HasPosition { get => hasPosition; internal set => SetValue(ref hasPosition, value); }

    /// <summary>
    /// The <c>color:</c> header, or null.
    /// </summary>
    public string? Color { get => color; internal set => SetValue(ref color, value); }

    /// <summary>
    /// The <c>cluster:</c> header, or null.
    /// </summary>
    public string? Cluster { get => cluster; internal set => SetValue(ref cluster, value); }

    /// <summary>
    /// Whether the node is a note of the writer (<c>style: note</c>), which the story never runs.
    /// </summary>
    public bool IsNote { get => isNote; internal set => SetValue(ref isNote, value); }

    /// <summary>
    /// The value of the header the graph is grouped by, or null.
    /// </summary>
    public string? GroupValue { get => groupValue; internal set => SetValue(ref groupValue, value); }

    /// <summary>
    /// Whether the search of the editor leaves the node out.
    /// </summary>
    public bool IsFilteredOut { get => isFilteredOut; internal set => SetValue(ref isFilteredOut, value); }

    /// <summary>
    /// Whether the last compilation has an error in the node.
    /// </summary>
    public bool HasError { get => hasError; internal set => SetValue(ref hasError, value); }

    /// <summary>
    /// What the player did with the node in the run that is open.
    /// </summary>
    public YarnPlayMark PlayMark { get => playMark; set => SetValue(ref playMark, value); }

    /// <summary>
    /// The slot the links to the node arrive on.
    /// </summary>
    public YarnCardSlot InSlot { get; }

    /// <summary>
    /// The slot the node's links start from.
    /// </summary>
    public YarnCardSlot OutSlot { get; }

    private void SetList(ref IReadOnlyList<string> field, IReadOnlyList<string> value, string propertyName)
    {
        if (!field.SequenceEqual(value))
        {
            SetValue(ref field, value, propertyName);
        }
    }
}
