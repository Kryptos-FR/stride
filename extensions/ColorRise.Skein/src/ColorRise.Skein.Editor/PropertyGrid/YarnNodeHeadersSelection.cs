using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Nodes;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Extensions;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.Services;
using Stride.Core.Transactions;

namespace ColorRise.Skein.Editor.PropertyGrid;

/// <summary>
/// Shows the headers of the cards selected in a script editor in the session's property grid, and writes what the grid
/// changes into the script's text. The text stays the truth: when it says something else than the grid, the grid shows
/// the headers again.
/// </summary>
internal sealed class YarnNodeHeadersSelection : IDisposable
{
    private readonly YarnScriptEditorViewModel editor;
    private readonly List<(YarnNodeCardViewModel Card, YarnNodeHeadersViewModel Headers)> shown = [];
    // The last request made to the grid: until it is served, the grid's selection is still the one before
    private Task request = Task.CompletedTask;
    private bool writing;
    private int lastAsked;

    public YarnNodeHeadersSelection(YarnScriptEditorViewModel editor)
    {
        this.editor = editor;
        editor.SelectedCards.CollectionChanged += OnSelectionChanged;
        editor.Refreshed += OnRefreshed;
    }

    /// <summary>
    /// The headers the grid was last given, one for each selected card, in the order of the text.
    /// </summary>
    public IReadOnlyList<YarnNodeHeadersViewModel> Shown => [.. shown.Select(s => s.Headers)];

    /// <summary>
    /// Whether the property grid shows <see cref="Shown"/>, or is about to: it shows something else once the user
    /// selects elsewhere.
    /// </summary>
    public bool IsShowing
    {
        get
        {
            if (shown.Count == 0 || editor.Session.ActiveProperties != editor.EditorProperties)
                return false;

            return !request.IsCompleted || (editor.EditorProperties.Selection?.Contains(shown[0].Headers) ?? false);
        }
    }

    /// <summary>
    /// Gives the grid the headers of the selected cards when it shows something else.
    /// </summary>
    public void Take()
    {
        if (!IsShowing)
        {
            Show(take: true);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        editor.SelectedCards.CollectionChanged -= OnSelectionChanged;
        editor.Refreshed -= OnRefreshed;
        if (IsShowing)
        {
            editor.EditorProperties.GenerateSelectionPropertiesAsync([]).Forget();
        }

        Clear();
    }

    private void OnSelectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Show(take: true);

    private void OnRefreshed() => Show(take: false);

    /// <param name="take">Whether to show the headers when the grid shows something else: the user selected a card. A
    /// change of the text only keeps current what the grid shows.</param>
    private void Show(bool take)
    {
        if (writing)
            return;

        var showing = IsShowing;
        if (!take && !showing)
        {
            Clear();
            return;
        }

        var cards = editor.Cards.Where(editor.SelectedCards.Contains).ToList();
        if (showing && cards.Count == shown.Count && shown.Select((s, i) => s.Card == cards[i] && YarnNodeHeadersText.Matches(s.Headers.Headers, s.Card.Span)).All(same => same))
        {
            ShowName();
            return;
        }

        Clear();
        if (cards.Count == 0)
        {
            // Nothing of the graph is selected any more: the grid goes back to the script
            if (showing)
            {
                editor.ShowScriptProperties();
            }

            return;
        }

        foreach (var card in cards)
        {
            var headers = new YarnNodeHeadersViewModel(editor.Session, YarnNodeHeadersText.Read(card.Span));
            headers.Changed += changed => OnChanged(card, changed);
            shown.Add((card, headers));
        }

        ShowName();
        request = AskGridAsync([.. shown.Select(s => (IPropertyProviderViewModel)s.Headers)]);
    }

    private async Task AskGridAsync(IReadOnlyList<IPropertyProviderViewModel> headers)
    {
        var asked = ++lastAsked;

        // The grid waits for the end of the transaction in progress on a task that a nested transaction replaces, and
        // then waits for ever (finding 23 of docs/plugin-findings.md): it is asked once the transaction is over
        var undoRedo = editor.ServiceProvider.Get<IUndoRedoService>();
        if (undoRedo.TransactionInProgress)
        {
            var over = new TaskCompletionSource();
            void OnDone(object? sender, TransactionEventArgs e) => over.TrySetResult();
            undoRedo.Done += OnDone;
            try
            {
                await over.Task;
            }
            finally
            {
                undoRedo.Done -= OnDone;
            }

            // The text changed again in the transaction: the headers to show are others
            if (asked != lastAsked)
                return;
        }

        await editor.EditorProperties.GenerateSelectionPropertiesAsync(headers);
    }

    private void ShowName()
        => editor.EditorProperties.UpdateTypeAndName(shown, s => s.Card.IsNote ? "Yarn note" : "Yarn node", s => s.Card.Title, "Yarn nodes");

    /// <summary>
    /// Writes into the text what the grid changed in the headers of a card. It runs inside the undo transaction of the
    /// grid, so the change of the text is that step of the history.
    /// </summary>
    private void OnChanged(YarnNodeCardViewModel card, YarnNodeHeaders headers)
    {
        if (writing || !editor.Cards.Contains(card))
            return;

        writing = true;
        try
        {
            var title = headers.Title?.Trim() ?? "";
            if (title != card.Title)
            {
                editor.Rename(card, title);
            }
            else
            {
                editor.WriteHeaders(card, headers);
            }
        }
        finally
        {
            writing = false;
        }

        // What the text refused, or wrote another way, shows as the text has it
        Show(take: false);
    }

    private void Clear()
    {
        foreach (var (_, headers) in shown)
        {
            headers.Dispose();
        }

        shown.Clear();
    }
}
