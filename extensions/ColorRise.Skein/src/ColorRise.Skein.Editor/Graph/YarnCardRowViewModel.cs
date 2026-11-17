using ColorRise.Skein.Assets.Text;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// A row of a card: a statement of its node's body, or the row that adds a line at the end of the node. A row stays the
/// same object while its rank in the card stays, so what the graph holds of it (its connector, its links) lasts
/// through the changes of the text.
/// </summary>
public sealed class YarnCardRowViewModel : ViewModelBase
{
    internal YarnCardRowViewModel(YarnNodeCardViewModel card, bool isAdd = false)
    {
        Card = card;
        IsAdd = isAdd;
        OutSlot = new YarnCardSlot(card, isInput: false, this);
    }

    /// <summary>
    /// The card of the row.
    /// </summary>
    public YarnNodeCardViewModel Card { get; }

    /// <summary>
    /// Whether the row adds a line at the end of the node instead of showing a statement.
    /// </summary>
    public bool IsAdd { get; }

    /// <summary>
    /// The statement of the row, in the document the card was last read from; null for the row that adds a line.
    /// </summary>
    public YarnStatementSpan? Statement
    {
        get;
        internal set
        {
            // The statement of the current document is kept even when it says the same: the edits check that it is of it
            var changed = !Equals(field, value);
            field = value;
            if (changed)
            {
                OnPropertyChanged(nameof(Statement), nameof(Kind), nameof(Depth), nameof(Speaker), nameof(Display), nameof(Condition), nameof(IsEditable), nameof(HasConnector));
            }
        }
    }

    /// <summary>
    /// What the statement of the row is.
    /// </summary>
    public YarnStatementKind Kind => Statement?.Kind ?? YarnStatementKind.Line;

    /// <summary>
    /// How deep the statement is in options and blocks.
    /// </summary>
    public int Depth => Statement?.Depth ?? 0;

    /// <summary>
    /// The speaker of a line, or null.
    /// </summary>
    public string? Speaker => Statement?.Speaker;

    /// <summary>
    /// What the row shows: the words of a line after its speaker, the text of an option, or the command as Yarn writes
    /// it without its brackets (<c>if $met</c>, <c>jump Pier</c>).
    /// </summary>
    public string Display
    {
        get
        {
            if (Statement is not { } statement)
                return "";

            return statement.Kind switch
            {
                YarnStatementKind.Line or YarnStatementKind.LineGroupItem when statement.Speaker is not null => statement.Text[(statement.Text.IndexOf(':', StringComparison.Ordinal) + 1)..].TrimStart(),
                YarnStatementKind.ElseIf => "else if " + statement.Condition,
                YarnStatementKind.Once => statement.Condition is null ? "once" : "once if " + statement.Condition,
                _ => statement.Text,
            };
        }
    }

    /// <summary>
    /// The condition written after a line or an option, as the row shows it (<c>if $met</c>, <c>once</c>); null without one.
    /// </summary>
    public string? Condition
    {
        get
        {
            if (Statement is not { IsText: true } statement || (statement.Condition is null && !statement.IsOnce))
                return null;

            return string.Join(' ', new[] { statement.IsOnce ? "once" : null, statement.Condition is null ? null : "if " + statement.Condition }.OfType<string>());
        }
    }

    /// <summary>
    /// Whether the text of the row is edited on the card: a line, an option or a line group item.
    /// </summary>
    public bool IsEditable => IsAdd || Statement?.IsText == true;

    /// <summary>
    /// Whether the row has a connector the links of what it holds leave from: an option, to drag a jump from, and a
    /// line group item or a branch of a block that a link leaves from. A folded card still shows these rows.
    /// </summary>
    public bool HasConnector => Statement?.Kind == YarnStatementKind.Option || (IsLinked && Statement?.IsBranch == true);

    /// <summary>
    /// Whether a link of the graph leaves from the row.
    /// </summary>
    public bool IsLinked
    {
        get;
        internal set
        {
            if (SetValue(ref field, value))
            {
                OnPropertyChanged(nameof(HasConnector));
            }
        }
    }

    /// <summary>
    /// The slot the links of what the row holds start from.
    /// </summary>
    public YarnCardSlot OutSlot { get; }

    /// <summary>
    /// Whether the text of the row is being edited on the card.
    /// </summary>
    public bool IsEditing { get; internal set => SetValue(ref field, value); }

    /// <summary>
    /// The text being edited, which <see cref="ViewModels.YarnScriptEditorViewModel.CommitEdit"/> writes.
    /// </summary>
    public string EditText { get; set => SetValue(ref field, value ?? ""); } = "";
}
