using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using ColorRise.Skein.Editor.Text;
using Stride.Core.Presentation.Avalonia.Commands;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The text of a Yarn script, typed into a <see cref="YarnTextBuffer"/>: what the user types becomes steps of the session's
/// undo history, and the pane shows every change of the buffer, wherever it comes from. The pane keeps no history of its own:
/// Undo and Redo are the session's.
/// </summary>
public sealed class YarnTextPane : TextEditor, IEditCommandHandler
{
    /// <summary>
    /// The buffer shown and typed into.
    /// </summary>
    public static readonly StyledProperty<YarnTextBuffer?> BufferProperty = AvaloniaProperty.Register<YarnTextPane, YarnTextBuffer?>(nameof(Buffer));

    // True while the pane and the buffer are brought in line, so that the change is not sent back where it came from
    private bool synchronizing;

    public YarnTextPane()
    {
        ShowLineNumbers = true;
        WordWrap = true;
        Document = new TextDocument();
        Document.UndoStack.SizeLimit = 0;
        Document.Changed += OnDocumentChanged;

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
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Buffer is not { } buffer)
            return;

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
        // The caret goes where the change was, as after an undo in any text editor
        CaretOffset = change.Offset + change.InsertedText.Length;
        TextArea.Caret.BringCaretToView();
    }
}
