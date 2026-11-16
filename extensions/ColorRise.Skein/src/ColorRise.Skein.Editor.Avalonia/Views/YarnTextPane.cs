using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Avalonia.Text;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Text;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Presentation.Avalonia.Commands;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The text of a Yarn script, typed into a <see cref="YarnTextBuffer"/>: what the user types becomes steps of the session's
/// undo history, and the pane shows every change of the buffer, wherever it comes from. The pane keeps no history of its own:
/// Undo and Redo are the session's.
/// </summary>
/// <remarks>
/// With an <see cref="Editor"/>, the pane shows the text of the cards selected on the graph, or the whole file; it
/// underlines what the compiler says of the script, and Ctrl+click on the name of a node leads to the node.
/// </remarks>
public sealed class YarnTextPane : TextEditor, IEditCommandHandler
{
    /// <summary>
    /// The buffer shown and typed into.
    /// </summary>
    public static readonly StyledProperty<YarnTextBuffer?> BufferProperty = AvaloniaProperty.Register<YarnTextPane, YarnTextBuffer?>(nameof(Buffer));

    /// <summary>
    /// The editor of the script, which says what part of the text to show and what the compiler says of it.
    /// </summary>
    public static readonly StyledProperty<YarnScriptEditorViewModel?> EditorProperty = AvaloniaProperty.Register<YarnTextPane, YarnScriptEditorViewModel?>(nameof(Editor));

    private readonly YarnHighlighter highlighter;
    private readonly YarnDiagnosticRenderer diagnostics;
    private readonly YarnHiddenLines hidden;
    private YarnScriptEditorViewModel? observedEditor;

    // True while the pane and the buffer are brought in line, so that the change is not sent back where it came from
    private bool synchronizing;
    private bool shownLinesPending;
    // True when the selection changed on the graph: the pane goes to the selected node
    private bool revealPending;
    private bool caretPending;
    // True while the pane selects the card of its caret, which is no reason to move the caret
    private bool selectingFromCaret;

    public YarnTextPane()
    {
        ShowLineNumbers = true;
        WordWrap = true;
        Document = new TextDocument();
        Document.UndoStack.SizeLimit = 0;
        Document.Changed += OnDocumentChanged;

        highlighter = new YarnHighlighter(Document);
        diagnostics = new YarnDiagnosticRenderer(Document);
        TextArea.TextView.LineTransformers.Add(highlighter);
        TextArea.TextView.BackgroundRenderers.Add(diagnostics);
        hidden = new YarnHiddenLines(TextArea.TextView);
        TextArea.TextView.ElementGenerators.Add(hidden);
        TextArea.ReadOnlySectionProvider = hidden;
        TextArea.TextView.PointerHover += OnPointerHover;
        TextArea.TextView.PointerHoverStopped += OnPointerHoverStopped;
        TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        ActualThemeVariantChanged += (_, _) => ShowTheme();
        ShowTheme();
        // Before the text area, which would move the caret
        AddHandler(PointerPressedEvent, OnPointerPressedFirst, RoutingStrategies.Tunnel);

        // The text area takes the keys of its own Undo and Redo even with nothing to undo: without these bindings the keys
        // reach the window, whose Undo and Redo are the session's
        var input = TextArea.DefaultInputHandler;
        foreach (var binding in input.CommandBindings.Where(x => x.Command == ApplicationCommands.Undo || x.Command == ApplicationCommands.Redo).ToList())
        {
            input.CommandBindings.Remove(binding);
        }
    }

    public YarnTextBuffer? Buffer
    {
        get => GetValue(BufferProperty);
        set => SetValue(BufferProperty, value);
    }

    public YarnScriptEditorViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>
    /// Whether a line of the text is shown: a line of a node that is not selected is not, unless the pane shows the
    /// whole file.
    /// </summary>
    /// <param name="line">The number of the line, from 1.</param>
    public bool IsLineShown(int line) => line >= 1 && line <= Document.LineCount && !hidden.Hides(line);

    /// <summary>
    /// The kind of the token at a place of the text, as the pane colours it, or null where the text is plain.
    /// </summary>
    public YarnTokenKind? TokenKindAt(int offset) => highlighter.TokenAt(offset)?.Kind;

    /// <summary>
    /// The message of the compiler underlined at a place of the text, or null.
    /// </summary>
    public string? MessageAt(int offset) => diagnostics.At(offset)?.Message;

    /// <summary>
    /// Leads to the node whose name is at a place of the text, as Ctrl+click does.
    /// </summary>
    /// <returns>Whether a name of a node is there.</returns>
    public bool FollowNodeNameAt(int offset)
    {
        if (Editor is not { } editor || highlighter.TokenAt(offset) is not { Kind: YarnTokenKind.NodeName } token)
            return false;

        _ = editor.GoToNode(Document.GetText(token.Offset, token.Length));
        return true;
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(TextEditor);

    /// <inheritdoc/>
    bool IEditCommandHandler.Handles(EditCommandKind kind) => kind is EditCommandKind.Cut or EditCommandKind.Copy or EditCommandKind.Paste or EditCommandKind.Delete;

    /// <inheritdoc/>
    bool IEditCommandHandler.CanExecute(EditCommandKind kind)
    {
        return kind switch
        {
            EditCommandKind.Cut or EditCommandKind.Delete => !IsReadOnly && SelectionLength > 0,
            EditCommandKind.Copy => SelectionLength > 0,
            EditCommandKind.Paste => !IsReadOnly,
            _ => false,
        };
    }

    /// <inheritdoc/>
    void IEditCommandHandler.Execute(EditCommandKind kind)
    {
        switch (kind)
        {
            case EditCommandKind.Cut:
                Cut();
                break;
            case EditCommandKind.Copy:
                Copy();
                break;
            case EditCommandKind.Paste:
                Paste();
                break;
            case EditCommandKind.Delete:
                Delete();
                break;
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EditorProperty)
        {
            Observe(this.IsAttachedToVisualTree() ? Editor : null);
            return;
        }

        if (change.Property != BufferProperty)
            return;

        if (change.OldValue is YarnTextBuffer old)
            old.Changed -= OnBufferChanged;

        synchronizing = true;
        try
        {
            Document.Text = Buffer?.Text ?? string.Empty;
        }
        finally
        {
            synchronizing = false;
        }

        if (change.NewValue is YarnTextBuffer buffer)
            buffer.Changed += OnBufferChanged;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The buffer lives as long as the script: a closed pane must not stay among its listeners
        if (Buffer is { } buffer)
            buffer.Changed -= OnBufferChanged;
        Observe(null);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Buffer is { } buffer)
        {
            buffer.Changed -= OnBufferChanged;
            buffer.Changed += OnBufferChanged;
            // What changed while the pane was not shown
            if (Document.Text != buffer.Text)
            {
                synchronizing = true;
                try
                {
                    Document.Text = buffer.Text;
                }
                finally
                {
                    synchronizing = false;
                }
            }
        }

        Observe(Editor);
    }

    private void ShowTheme()
    {
        highlighter.IsDark = ActualThemeVariant == ThemeVariant.Dark;
        TextArea.TextView.Redraw();
    }

    /// <summary>
    /// Follows an editor, or none: its selection, its messages and the places it asks to show.
    /// </summary>
    private void Observe(YarnScriptEditorViewModel? editor)
    {
        if (!ReferenceEquals(observedEditor, editor))
        {
            if (observedEditor is not null)
            {
                observedEditor.PropertyChanged -= OnEditorPropertyChanged;
                observedEditor.SelectedCards.CollectionChanged -= OnSelectedCardsChanged;
                observedEditor.PlaceRequested -= OnPlaceRequested;
            }

            observedEditor = editor;
            if (editor is not null)
            {
                editor.PropertyChanged += OnEditorPropertyChanged;
                editor.SelectedCards.CollectionChanged += OnSelectedCardsChanged;
                editor.PlaceRequested += OnPlaceRequested;
            }
        }

        if (editor is not null)
        {
            ShowMessages();
            RequestShownLines();
            OnPlaceRequested();
        }
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(YarnScriptEditorViewModel.ScriptDiagnostics):
            case nameof(YarnScriptEditorViewModel.IsAnalysisCurrent):
                ShowMessages();
                break;
            case nameof(YarnScriptEditorViewModel.ShowWholeFile):
                RequestShownLines();
                break;
        }
    }

    // The messages are about the text that was compiled: they are marked when the pane has that text, and their marks
    // follow what is typed until the compiler has read it
    private void ShowMessages()
    {
        if (observedEditor is { IsAnalysisCurrent: true } editor && Document.Text == editor.TextBuffer.Text)
        {
            diagnostics.Show(editor.ScriptDiagnostics);
            TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        }
    }

    private void OnSelectedCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        revealPending |= !selectingFromCaret;
        RequestShownLines();
    }

    /// <summary>
    /// Shows the lines of the selected cards: now, or once the pane and the buffer have the same text.
    /// </summary>
    private void RequestShownLines()
    {
        if (!synchronizing && observedEditor is { } editor && Document.Text == editor.TextBuffer.Text)
        {
            ShowLines();
        }
        else if (!shownLinesPending)
        {
            shownLinesPending = true;
            Dispatcher.UIThread.Post(ShowLines);
        }
    }

    private void ShowLines()
    {
        shownLinesPending = false;
        // A text that is not the buffer's is between two changes: the last one asks again
        if (observedEditor is { } unsynchronized && Document.Text != unsynchronized.TextBuffer.Text)
            return;

        var reveal = revealPending;
        revealPending = false;
        var hadHidden = !hidden.IsEmpty;
        hidden.Clear();
        var cards = observedEditor?.SelectedCards.OfType<YarnNodeCardViewModel>().OrderBy(c => c.Span.HeaderStart).ToList() ?? [];
        if (cards.Count == 0 || observedEditor!.ShowWholeFile)
        {
            if (hadHidden)
            {
                TextArea.TextView.Redraw();
            }

            if (reveal && cards.Count > 0)
            {
                ScrollTo(LineAt(cards[0].Span.HeaderStart), 1);
            }

            return;
        }

        var next = 1;
        foreach (var card in cards)
        {
            var first = LineAt(card.Span.HeaderStart);
            var last = LineAt(Math.Max(card.Span.HeaderStart, card.Span.End - 1));
            Hide(next, first - 1);
            next = Math.Max(next, last + 1);
        }

        Hide(next, Document.LineCount);
        TextArea.TextView.Redraw();

        if (!IsLineShown(TextArea.Caret.Line))
        {
            CaretOffset = Math.Min(cards[0].Span.BodyStart, Document.TextLength);
        }

        if (reveal)
        {
            TextArea.Caret.BringCaretToView();
        }
    }

    private int LineAt(int offset) => Document.GetLineByOffset(Math.Clamp(offset, 0, Document.TextLength)).LineNumber;

    private void Hide(int firstLine, int lastLine)
    {
        if (firstLine <= lastLine)
        {
            hidden.Hide(Document.GetLineByNumber(firstLine), Document.GetLineByNumber(lastLine));
        }
    }

    private void OnPlaceRequested()
    {
        if (observedEditor?.TakeRequestedPlace() is not { } place || place.Line > Document.LineCount)
            return;

        var line = Document.GetLineByNumber(Math.Max(1, place.Line));
        var offset = line.Offset + Math.Clamp(place.Column - 1, 0, line.Length);
        if (!IsLineShown(line.LineNumber))
        {
            // The place is in a node that is not shown: its card is selected, or the whole file is shown
            if (observedEditor.CardAt(offset) is { } card)
            {
                observedEditor.SelectedCards.Clear();
                observedEditor.SelectedCards.Add(card);
            }
            else
            {
                observedEditor.ShowWholeFile = true;
            }
        }

        CaretOffset = offset;
        ScrollTo(line.LineNumber, place.Column);
        Focus();
    }

    // In the whole file, the card of the node the caret is in is the selected one
    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (caretPending || observedEditor is not { ShowWholeFile: true } || !IsKeyboardFocusWithin)
            return;

        caretPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            caretPending = false;
            if (observedEditor is not { ShowWholeFile: true } editor || Document.Text != editor.TextBuffer.Text || editor.CardAt(CaretOffset) is not { } card)
                return;

            if (editor.SelectedCards.Count == 1 && ReferenceEquals(editor.SelectedCards[0], card))
                return;

            selectingFromCaret = true;
            try
            {
                editor.SelectedCards.Clear();
                editor.SelectedCards.Add(card);
            }
            finally
            {
                selectingFromCaret = false;
            }
        });
    }

    private int? OffsetAt(PointerEventArgs e)
    {
        var view = TextArea.TextView;
        return view.GetPosition(e.GetPosition(view) + view.ScrollOffset) is { } position ? Document.GetOffset(position.Location) : null;
    }

    private void OnPointerPressedFirst(object? sender, PointerPressedEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && OffsetAt(e) is { } offset && FollowNodeNameAt(offset))
        {
            e.Handled = true;
        }
    }

    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (OffsetAt(e) is { } offset && MessageAt(offset) is { } message)
        {
            ToolTip.SetTip(this, message);
            ToolTip.SetIsOpen(this, true);
        }
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e)
    {
        ToolTip.SetIsOpen(this, false);
        ToolTip.SetTip(this, null);
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (synchronizing || Buffer is not { } buffer)
            return;

        synchronizing = true;
        try
        {
            buffer.Replace(e.Offset, e.RemovalLength, e.InsertedText.Text, YarnTextBuffer.TypingActionName);
        }
        finally
        {
            synchronizing = false;
        }
    }

    private void OnBufferChanged(YarnTextChange change)
    {
        if (synchronizing)
            return;

        synchronizing = true;
        try
        {
            Document.Replace(change.Offset, change.RemovedLength, change.InsertedText);
        }
        finally
        {
            synchronizing = false;
        }

        // After an undo of what was typed, the caret goes where the change was, as in any text editor. A change made
        // elsewhere (on the graph, in the property grid) leaves the pane where the user reads
        if (IsKeyboardFocusWithin && IsLineShown(Document.GetLineByOffset(change.Offset).LineNumber))
        {
            CaretOffset = change.Offset + change.InsertedText.Length;
            TextArea.Caret.BringCaretToView();
        }

        // The nodes are at other lines, and the last of several changes brings the text in line with the buffer
        if (observedEditor is not null && !shownLinesPending)
        {
            shownLinesPending = true;
            Dispatcher.UIThread.Post(ShowLines);
        }
    }
}
