using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.ViewModels;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Dirtiables;
using Stride.Core.Presentation.Services;
using Stride.Core.Transactions;

namespace ColorRise.Skein.Editor.Text;

/// <summary>
/// The text of a Yarn script as the editor changes it: every change is a step of the session's undo history that dirties the
/// script, whether it comes from the text pane, the graph or the property grid. A script has one buffer for as long as the
/// session knows it, so the steps still work after its editor closed.
/// </summary>
public sealed class YarnTextBuffer : IDisposable
{
    /// <summary>
    /// The name of typing steps. Typing joins the typing step before it for as long as <see cref="TypingMergeWindow"/> allows.
    /// </summary>
    public const string TypingActionName = "Typing";

    /// <summary>
    /// The name of the step <see cref="ReloadFromDisk"/> makes.
    /// </summary>
    public const string ReloadActionName = "Reload from disk";

    /// <summary>
    /// The longest pause between two typing changes of one step.
    /// </summary>
    public static readonly TimeSpan TypingMergeWindow = TimeSpan.FromSeconds(1);

    private static readonly Logger Log = GlobalLogger.GetLogger("ColorRise.Skein");

    private readonly YarnScriptViewModel script;
    private readonly IUndoRedoService undoRedo;
    // The typing step the next typing change may join, with the time of its last change
    private TextOperation? typing;
    private long typingTimestamp;
    private bool pushing;

    /// <summary>
    /// Makes the buffer of a script.
    /// </summary>
    public YarnTextBuffer(YarnScriptViewModel script)
    {
        this.script = script;
        undoRedo = script.ServiceProvider.Get<IUndoRedoService>();
        undoRedo.Done += OnDone;
        undoRedo.Undone += OnHistoryMoved;
        undoRedo.Redone += OnHistoryMoved;
        undoRedo.Cleared += OnHistoryCleared;
    }

    /// <summary>
    /// The script's text.
    /// </summary>
    public string Text => script.Asset.Text ?? string.Empty;

    /// <summary>
    /// The clock typing is timed with.
    /// </summary>
    public TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>
    /// Raised after each change of the text, also by an undo or a redo.
    /// </summary>
    public event Action<YarnTextChange>? Changed;

    /// <summary>
    /// Replaces <paramref name="length"/> characters at <paramref name="offset"/> with <paramref name="text"/>, as one step
    /// of the undo history named <paramref name="actionName"/>.
    /// </summary>
    public void Replace(int offset, int length, string text, string actionName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(actionName);
        var current = Text;
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, current.Length, nameof(length));

        var edit = new TextEdit(offset, current.Substring(offset, length), text);
        if (edit.Removed == edit.Inserted)
            return;

        if (actionName == TypingActionName && CanJoinTyping())
        {
            typing!.Add(edit);
            Apply([edit]);
            typingTimestamp = Time.GetTimestamp();
            return;
        }

        var operation = Push([edit], actionName);
        typing = actionName == TypingActionName ? operation : null;
        typingTimestamp = Time.GetTimestamp();
    }

    /// <summary>
    /// Applies the edits of one operation of <see cref="YarnTextPatcher"/> as one step of the undo history named
    /// <paramref name="actionName"/>. Inside a transaction of the caller, the step is a part of that transaction.
    /// </summary>
    /// <param name="edits">Places of the text as it is now, in order, that do not overlap.</param>
    public void Apply(IReadOnlyList<YarnTextEdit> edits, string actionName)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentException.ThrowIfNullOrEmpty(actionName);

        // From the last to the first, each edit is applied to a text whose start is still the one it was made on
        var current = Text;
        var steps = new List<TextEdit>(edits.Count);
        var limit = current.Length;
        for (var i = edits.Count - 1; i >= 0; i--)
        {
            var edit = edits[i];
            ArgumentOutOfRangeException.ThrowIfNegative(edit.Offset, nameof(edits));
            ArgumentOutOfRangeException.ThrowIfNegative(edit.Length, nameof(edits));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(edit.Offset + edit.Length, limit, nameof(edits));
            limit = edit.Offset;

            var step = new TextEdit(edit.Offset, current.Substring(edit.Offset, edit.Length), edit.Text);
            if (step.Removed != step.Inserted)
            {
                steps.Add(step);
            }
        }

        if (steps.Count == 0)
            return;

        Push(steps, actionName);
        typing = null;
    }

    /// <summary>
    /// Takes the text another program wrote to the script's file. It is a step of the undo history, so the steps before it
    /// still apply to the text they were made on, and it leaves the script without unsaved changes.
    /// </summary>
    public void ReloadFromDisk(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var current = Text;
        if (text == current)
            return;

        // Only what differs, so that views keep their place in the rest
        var start = 0;
        while (start < current.Length && start < text.Length && current[start] == text[start])
            ++start;
        var end = 0;
        while (end < current.Length - start && end < text.Length - start && current[^(end + 1)] == text[^(end + 1)])
            ++end;

        Push([new TextEdit(start, current[start..^end], text[start..^end])], ReloadActionName);
        typing = null;
        undoRedo.NotifySave(script);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        undoRedo.Done -= OnDone;
        undoRedo.Undone -= OnHistoryMoved;
        undoRedo.Redone -= OnHistoryMoved;
        undoRedo.Cleared -= OnHistoryCleared;
    }

    private TextOperation Push(IReadOnlyList<TextEdit> edits, string actionName)
    {
        var operation = new TextOperation(this, script.Dirtiables, edits);
        Apply(edits);
        pushing = true;
        try
        {
            using var transaction = undoRedo.CreateTransaction();
            undoRedo.PushOperation(operation);
            undoRedo.SetName(transaction, actionName);
        }
        finally
        {
            pushing = false;
        }
        return operation;
    }

    private bool CanJoinTyping()
    {
        // A saved step stays as it was saved, and a step inside someone's open transaction belongs to that transaction
        return typing is { IsDone: true, IsDiscarded: false }
            && script.IsDirty
            && !undoRedo.TransactionInProgress
            && Time.GetElapsedTime(typingTimestamp) <= TypingMergeWindow;
    }

    /// <summary>
    /// Applies edits one after the other, each to the text the one before left. Does nothing when the text is not the one
    /// the edits were made on.
    /// </summary>
    /// <returns>Whether the edits were applied.</returns>
    private bool Apply(IReadOnlyList<TextEdit> edits)
    {
        var text = Text;
        foreach (var edit in edits)
        {
            if (edit.Offset + edit.Removed.Length > text.Length || string.CompareOrdinal(text, edit.Offset, edit.Removed, 0, edit.Removed.Length) != 0)
                return false;

            text = string.Concat(text.AsSpan(0, edit.Offset), edit.Inserted, text.AsSpan(edit.Offset + edit.Removed.Length));
        }

        script.Asset.Text = text;
        foreach (var edit in edits)
        {
            Changed?.Invoke(new YarnTextChange(edit.Offset, edit.Removed.Length, edit.Inserted));
        }

        return true;
    }

    /// <summary>
    /// Undoes or redoes the edits of a step, or says that it cannot: the text was changed by something that is not a step.
    /// </summary>
    private void Move(IReadOnlyList<TextEdit> edits, string move)
    {
        if (!Apply(edits))
            Log.Warning($"{script.Url}: the {move} of a step of the text was skipped, because the text is no longer the one the step was made on. Steps further back may not apply either.");
    }

    private void OnDone(object? sender, TransactionEventArgs e)
    {
        // Any other step ends the typing step
        if (!pushing)
            typing = null;
    }

    private void OnHistoryMoved(object? sender, TransactionEventArgs e) => typing = null;

    private void OnHistoryCleared(object? sender, EventArgs e) => typing = null;

    private readonly record struct TextEdit(int Offset, string Removed, string Inserted)
    {
        public TextEdit Inverse => new(Offset, Inserted, Removed);
    }

    private sealed class TextOperation(YarnTextBuffer buffer, IEnumerable<IDirtiable> dirtiables, IReadOnlyList<TextEdit> first) : DirtyingOperation(dirtiables)
    {
        private readonly List<TextEdit> edits = [.. first];

        public bool IsDiscarded { get; private set; }

        public void Add(TextEdit edit) => edits.Add(edit);

        protected override void FreezeContent()
        {
            IsDiscarded = true;
            edits.Clear();
        }

        protected override void Undo() => buffer.Move([.. Enumerable.Reverse(edits).Select(x => x.Inverse)], "undo");

        protected override void Redo() => buffer.Move(edits, "redo");
    }
}
