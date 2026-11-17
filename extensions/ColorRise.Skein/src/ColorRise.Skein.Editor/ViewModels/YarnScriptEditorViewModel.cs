using ColorRise.Skein.Assets;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Nodes;
using ColorRise.Skein.Editor.Player;
using ColorRise.Skein.Editor.PropertyGrid;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.Text;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;

namespace ColorRise.Skein.Editor.ViewModels;

/// <summary>
/// The editor of a Yarn script: its text, and the graph of its nodes. The text is the truth: the cards are read from it
/// at each change, and what the graph does is a change of the text, one step of the undo history each.
/// </summary>
/// <remarks>
/// The cards come from the text as it is, so they are there while the script does not compile. What only the compiler
/// knows (the links, the speakers, the options) comes from the last analysis of the script's project.
/// </remarks>
[AssetEditorViewModel<YarnScriptViewModel>]
public sealed class YarnScriptEditorViewModel : AssetEditorViewModel<YarnScriptViewModel>
{
    /// <summary>
    /// The <see cref="Notice"/> of a script no Yarn project includes.
    /// </summary>
    public const string NoProjectNotice = "This script is in no Yarn project.";

    // How far a new card goes from the one it is made from
    private const int CopyOffset = 40;
    // Where the stubs of a card with a place of its own go: on its right, one under the other
    private const int StubOffsetX = 280;
    private const int StubRowHeight = 60;
    // About the room a card and a stub take on the graph, to keep the stubs clear of the cards
    private const int CardWidth = 240;
    private const int StubWidth = 220;
    private const int StubHeight = 44;
    private const int StubMargin = 10;
    private const int StubRowsTried = 40;
    private const int FrameLabelHeight = 60;
    private const int PreviewLines = 3;
    private const int PreviewLineLength = 100;
    // About the room the parts of a card take, to estimate its height: the title bar and the connectors under the
    // rows, a header or a row of chips, a line of text, and the characters a line of a row holds
    private const int CardFrameHeight = 56;
    private const int CardLineHeight = 17;
    private const int RowCharacters = 34;
    private const int RowMaxLines = 3;

    // The headers that are not for grouping: each node has its own title and place, a condition is not a name, and
    // the colour and the style are looks
    private static readonly HashSet<string> UngroupableHeaders = new(StringComparer.Ordinal) { "title", "position", "when", "color", "style" };

    private readonly YarnAnalysisService? analysisService;
    private YarnDocument document;
    private YarnProjectAnalysis? analysis;
    private IReadOnlyList<YarnDiagnostic> diagnostics = [];
    private IReadOnlyList<YarnDiagnostic> scriptDiagnostics = [];
    private IReadOnlyList<string> groupableHeaders = [];
    private string? notice;
    private string? problem;
    private string? groupBy;
    private string search = "";
    private bool isAnalysisCurrent;
    private bool showWholeFile;
    private YarnTextPlace? requestedPlace;
    private YarnNodeHeadersSelection? nodeHeaders;
    private DialoguePlayerService? player;
    private int? playLine;

    /// <summary>
    /// Makes the editor of a Yarn script.
    /// </summary>
    public YarnScriptEditorViewModel(YarnScriptViewModel script)
        : base(script)
    {
        analysisService = ServiceProvider.TryGet<YarnAnalysisService>();
        document = YarnDocument.Parse(TextBuffer.Text);

        AddNodeCommand = new AnonymousCommand<YarnGraphPoint>(ServiceProvider, at => AddNode(at));
        AddNoteCommand = new AnonymousCommand<YarnGraphPoint>(ServiceProvider, at => AddNote(at));
        AddToGroupCommand = new AnonymousCommand<YarnNodeCardViewModel>(ServiceProvider, card => AddToGroup(card));
        RenameCommand = new AnonymousCommand<YarnRenameRequest>(ServiceProvider, request => Rename(request.Card, request.Title));
        DuplicateCommand = new AnonymousCommand(ServiceProvider, Duplicate);
        DeleteSelectionCommand = new AnonymousCommand(ServiceProvider, DeleteSelection);
        SetColorCommand = new AnonymousCommand<string>(ServiceProvider, SetColor);
        LinkCommand = new AnonymousCommand<YarnLinkRequest>(ServiceProvider, request => Link(request.Source, request.Target, request.IsDetour, request.Row));
        MoveCommand = new AnonymousCommand<IReadOnlyList<YarnCardMove>>(ServiceProvider, Move);
        LayoutCommand = new AnonymousCommand(ServiceProvider, Layout);
        OpenTargetCommand = new AnonymousTaskCommand<YarnStubCardViewModel>(ServiceProvider, OpenTarget);
        PlayFromHereCommand = new AnonymousTaskCommand<YarnNodeCardViewModel?>(ServiceProvider, PlayFromHere);
    }

    /// <summary>
    /// The script's text.
    /// </summary>
    public YarnTextBuffer TextBuffer => Asset.TextBuffer;

    /// <summary>
    /// The cards of the script's nodes, in the order of the text.
    /// </summary>
    public ObservableList<YarnNodeCardViewModel> Cards { get; } = [];

    /// <summary>
    /// The cards of the nodes the script jumps to that are not its own.
    /// </summary>
    public ObservableList<YarnStubCardViewModel> Stubs { get; } = [];

    /// <summary>
    /// Everything the graph shows as a node: <see cref="Cards"/>, then <see cref="Stubs"/>.
    /// </summary>
    public ObservableList<object> Nodes { get; } = [];

    /// <summary>
    /// The jumps and the detours between the cards, as the last compilation without an error saw them.
    /// </summary>
    public ObservableList<YarnLinkViewModel> Links { get; } = [];

    /// <summary>
    /// The cards and the stubs that are selected on the graph.
    /// </summary>
    public ObservableList<object> SelectedCards { get; } = [];

    /// <summary>
    /// What the compiler says of the script's project: the script's own errors and warnings first.
    /// </summary>
    public IReadOnlyList<YarnDiagnostic> Diagnostics
    {
        get => diagnostics;
        private set
        {
            if (!diagnostics.SequenceEqual(value))
            {
                SetValue(ref diagnostics, value);
            }
        }
    }

    /// <summary>
    /// The ones of <see cref="Diagnostics"/> that are about this script.
    /// </summary>
    public IReadOnlyList<YarnDiagnostic> ScriptDiagnostics
    {
        get => scriptDiagnostics;
        private set
        {
            if (!scriptDiagnostics.SequenceEqual(value))
            {
                SetValue(ref scriptDiagnostics, value);
            }
        }
    }

    /// <summary>
    /// Whether <see cref="Diagnostics"/> are about the text as it is: their lines are places of the text that was compiled.
    /// </summary>
    public bool IsAnalysisCurrent { get => isAnalysisCurrent; private set => SetValue(ref isAnalysisCurrent, value); }

    /// <summary>
    /// Whether the text pane shows the whole file, or only the selected nodes.
    /// </summary>
    public bool ShowWholeFile { get => showWholeFile; set => SetValue(ref showWholeFile, value); }

    /// <summary>
    /// Whether the cards show the preview of their lines instead of all their rows: setting it folds or unfolds every
    /// card, and the cards of new nodes come so.
    /// </summary>
    public bool FoldCards
    {
        get;
        set
        {
            if (SetValue(ref field, value))
            {
                foreach (var card in Cards)
                {
                    card.IsFolded = value;
                }
            }
        }
    }

    /// <summary>
    /// Raised when the text pane is to show a place: <see cref="TakeRequestedPlace"/> gives it.
    /// </summary>
    public event Action? PlaceRequested;

    /// <summary>
    /// Raised after the cards were read from the text again.
    /// </summary>
    public event Action? Refreshed;

    /// <summary>
    /// The headers of the selected cards that the property grid was last given, in the order of the text.
    /// </summary>
    public IReadOnlyList<YarnNodeHeadersViewModel> SelectedHeaders => nodeHeaders?.Shown ?? [];

    /// <summary>
    /// What the user should know of the place of the script among the Yarn projects, or null: no project includes it,
    /// or several do.
    /// </summary>
    public string? Notice { get => notice; private set => SetValue(ref notice, value); }

    /// <summary>
    /// Why the last thing asked of the graph was not done, or null.
    /// </summary>
    public string? Problem { get => problem; private set => SetValue(ref problem, value); }

    /// <summary>
    /// Takes away what <see cref="Problem"/> says.
    /// </summary>
    public void DismissProblem() => Problem = null;

    /// <summary>
    /// The header whose value groups the cards (<see cref="YarnNodeCardViewModel.GroupValue"/>), or null.
    /// </summary>
    public string? GroupBy
    {
        get => groupBy;
        set
        {
            if (SetValue(ref groupBy, string.IsNullOrEmpty(value) ? null : value))
            {
                foreach (var card in Cards)
                {
                    card.GroupValue = groupBy is null ? null : card.Span.Header(groupBy);
                }

                // The cards without a place of their own are placed by section, and the sections changed
                Place();
            }
        }
    }

    /// <summary>
    /// The headers the cards can be grouped by: the ones the nodes of the script have, in the order of the text.
    /// </summary>
    public IReadOnlyList<string> GroupableHeaders
    {
        get => groupableHeaders;
        private set
        {
            if (!groupableHeaders.SequenceEqual(value))
            {
                SetValue(ref groupableHeaders, value);
            }
        }
    }

    /// <summary>
    /// What the cards are searched for, in their titles, headers and text, whatever the case: the others are
    /// <see cref="YarnNodeCardViewModel.IsFilteredOut"/>. Empty for no search.
    /// </summary>
    public string Search
    {
        get => search;
        set
        {
            if (SetValue(ref search, value ?? ""))
            {
                foreach (var card in Cards)
                {
                    card.IsFilteredOut = !Matches(card.Span);
                }
            }
        }
    }

    /// <summary>
    /// Adds a node at a place of the graph.
    /// </summary>
    public ICommandBase AddNodeCommand { get; }

    /// <summary>
    /// Adds a note of the writer at a place of the graph.
    /// </summary>
    public ICommandBase AddNoteCommand { get; }

    /// <summary>
    /// Adds a node to the node group of a card, which becomes a node group when it is not one.
    /// </summary>
    public ICommandBase AddToGroupCommand { get; }

    /// <summary>
    /// Renames the node of a card, with the jumps to it.
    /// </summary>
    public ICommandBase RenameCommand { get; }

    /// <summary>
    /// Copies the selected nodes.
    /// </summary>
    public ICommandBase DuplicateCommand { get; }

    /// <summary>
    /// Removes the selected nodes.
    /// </summary>
    public ICommandBase DeleteSelectionCommand { get; }

    /// <summary>
    /// Gives the selected nodes a colour; no colour without a parameter.
    /// </summary>
    public ICommandBase SetColorCommand { get; }

    /// <summary>
    /// Adds a jump from a node to another.
    /// </summary>
    public ICommandBase LinkCommand { get; }

    /// <summary>
    /// Writes the places the user dragged cards to.
    /// </summary>
    public ICommandBase MoveCommand { get; }

    /// <summary>
    /// Places every card anew, in columns along the jumps.
    /// </summary>
    public ICommandBase LayoutCommand { get; }

    /// <summary>
    /// Opens the script of a stub, at its node.
    /// </summary>
    public ICommandBase OpenTargetCommand { get; }

    /// <summary>
    /// Plays the node of a card in the dialogue player; without a card, the selected node, else the first.
    /// </summary>
    public ICommandBase PlayFromHereCommand { get; }

    /// <summary>
    /// The line, from 1, the run of the dialogue player is at in this script; null when the run is elsewhere, or when
    /// the scripts changed since the run.
    /// </summary>
    public int? PlayLine { get => playLine; private set => SetValue(ref playLine, value); }

    /// <inheritdoc/>
    public override Task<bool> Initialize()
    {
        TextBuffer.Changed += OnTextChanged;
        if (analysisService is not null)
        {
            analysisService.Changed += OnAnalysisChanged;
            analysis = analysisService.AnalysisOf(Asset);
        }

        player = DialoguePlayerService.Of(Session);
        player.Changed += RefreshPlayMarks;
        Refresh(withLinks: true);
        nodeHeaders = new YarnNodeHeadersSelection(this);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
        nodeHeaders?.Dispose();
        if (player is not null)
        {
            player.Changed -= RefreshPlayMarks;
        }

        TextBuffer.Changed -= OnTextChanged;
        if (analysisService is not null)
        {
            analysisService.Changed -= OnAnalysisChanged;
        }

        base.Destroy();
    }

    /// <summary>
    /// Adds a node with an empty body, under a title no node has, and selects it.
    /// </summary>
    /// <returns>The card of the node.</returns>
    public YarnNodeCardViewModel? AddNode(YarnGraphPoint at) => Add("Node", at, null, "Add node");

    /// <summary>
    /// Adds a note of the writer, a node the story never runs, and selects it.
    /// </summary>
    /// <returns>The card of the note.</returns>
    public YarnNodeCardViewModel? AddNote(YarnGraphPoint at) => Add("Note", at, [("style", "note")], "Add note");

    /// <summary>
    /// Adds a node with the title of a card, to run when its condition holds: both get <c>when: always</c> unless the
    /// card has a condition already.
    /// </summary>
    /// <returns>The card of the new node.</returns>
    public YarnNodeCardViewModel? AddToGroup(YarnNodeCardViewModel card)
    {
        if (!Knows(card))
            return null;

        var current = Synchronize();
        var first = card.When.Count == 0 ? YarnTextPatcher.SetHeaders(current, card.Span, "when", ["always"]) : [];
        var added = YarnTextPatcher.AddNode(current, card.Title, card.Position.X + CopyOffset, card.Position.Y + CopyOffset, [("when", "always")]);
        if (!Apply("Add to node group", first, added))
            return null;

        var member = Cards.LastOrDefault(c => c.Title == card.Title);
        Select(member);
        return member;
    }

    /// <summary>
    /// Gives the node of a card, and the other nodes of its node group, another title, in this script and in the others
    /// of its projects, with the jumps to it.
    /// </summary>
    /// <returns>Whether the node has the title; when not, <see cref="Problem"/> says why and no text has changed.</returns>
    public bool Rename(YarnNodeCardViewModel card, string title)
    {
        if (!Knows(card))
            return false;

        Synchronize();
        Problem = YarnRenameService.Rename(Asset, card.Title, title?.Trim() ?? "");
        return Problem is null;
    }

    /// <summary>
    /// Shows the headers of the selected cards in the property grid when it shows something else: the user came back
    /// to the graph.
    /// </summary>
    public void ShowNodeProperties() => nodeHeaders?.Take();

    /// <summary>
    /// Writes the headers of a card as <paramref name="headers"/> has them, but for the title; when a header cannot be
    /// written, <see cref="Problem"/> says why and no text has changed.
    /// </summary>
    internal void WriteHeaders(YarnNodeCardViewModel card, YarnNodeHeaders headers)
    {
        if (!Knows(card))
            return;

        var current = Synchronize();
        try
        {
            Apply("Edit node headers", YarnNodeHeadersText.Write(current, card.Span, headers));
        }
        catch (ArgumentException exception)
        {
            Problem = exception.Message;
        }
    }

    /// <summary>
    /// Shows the properties of the script in the property grid.
    /// </summary>
    internal void ShowScriptProperties() => ShowAssetProperties();

    /// <summary>
    /// Adds a copy of each selected node under a title no node has, a little aside, and selects the copies.
    /// </summary>
    public void Duplicate()
    {
        var current = Synchronize();
        var titles = KnownTitles().ToList();
        var copies = new List<string>();
        var edits = new List<IReadOnlyList<YarnTextEdit>>();
        foreach (var card in SelectedNodeCards())
        {
            var title = YarnTitles.Unique(card.Title + "_copy", titles);
            titles.Add(title);
            copies.Add(title);
            edits.Add(YarnTextPatcher.DuplicateNode(current, card.Span, title, (card.Position.X + CopyOffset, card.Position.Y + CopyOffset)));
        }

        if (Apply(copies.Count == 1 ? "Duplicate node" : "Duplicate nodes", [.. edits]))
        {
            SelectedCards.Clear();
            SelectedCards.AddRange(Cards.Where(c => copies.Contains(c.Title)));
        }
    }

    /// <summary>
    /// Removes the selected nodes from the script. The jumps to them stay, and show as broken.
    /// </summary>
    public void DeleteSelection()
    {
        var current = Synchronize();
        var edits = SelectedNodeCards().Select(c => YarnTextPatcher.DeleteNode(current, c.Span)).ToArray();
        SelectedCards.Clear();
        Apply(edits.Length == 1 ? "Delete node" : "Delete nodes", edits);
    }

    /// <summary>
    /// Gives the selected nodes a <c>color:</c> header, or takes it away.
    /// </summary>
    /// <param name="color">The colour as the header writes it; null or empty for none.</param>
    public void SetColor(string? color)
    {
        var current = Synchronize();
        try
        {
            var edits = SelectedNodeCards()
                .Select(c => string.IsNullOrWhiteSpace(color) ? YarnTextPatcher.RemoveHeader(current, c.Span, "color") : YarnTextPatcher.SetHeader(current, c.Span, "color", color.Trim()))
                .ToArray();
            Apply("Set color", edits);
        }
        catch (ArgumentException exception)
        {
            Problem = exception.Message;
        }
    }

    /// <summary>
    /// Adds a jump to a node at the end of another, or at the end of what a row of it holds: <paramref name="target"/>
    /// is a card of the script or a stub.
    /// </summary>
    /// <param name="detour">Whether to add a detour, from which the dialogue comes back, instead of a jump.</param>
    /// <param name="row">The row of an option, a line group item or a branch of a block to add the jump under; null for the end of the node.</param>
    public void Link(YarnNodeCardViewModel source, object target, bool detour = false, YarnCardRowViewModel? row = null)
    {
        var title = target switch
        {
            YarnNodeCardViewModel card when Knows(card) => card.Title,
            YarnStubCardViewModel stub => stub.Title,
            _ => null,
        };
        if (title is null || !Knows(source))
            return;

        var current = Synchronize();
        if (row is not null && source.Rows.Contains(row) && row.Statement is { IsBranch: true } branch)
        {
            Apply("Link nodes", YarnTextPatcher.AppendJump(current, source.Span, branch, title, detour));
        }
        else
        {
            Apply("Link nodes", YarnTextPatcher.AppendJump(current, source.Span, title, detour));
        }
    }

    /// <summary>
    /// Opens the box that edits the text of a row on its card: a line, an option, a line group item, or the row that
    /// adds a line, which starts with the speaker of the last line. Any other box that is open closes without writing.
    /// </summary>
    /// <returns>Whether the row is being edited.</returns>
    public bool BeginEdit(YarnCardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!Knows(row.Card) || !row.IsEditable)
            return false;

        foreach (var card in Cards)
        {
            foreach (var other in card.Rows.Append(card.AddRow))
            {
                other.IsEditing = false;
            }
        }

        var speaker = row.Card.Rows.LastOrDefault(r => r.Speaker is not null)?.Speaker;
        row.EditText = row.IsAdd ? (speaker is null ? "" : speaker + ": ") : row.Statement!.Text;
        row.IsEditing = true;
        return true;
    }

    /// <summary>
    /// Closes the box of a row without writing it.
    /// </summary>
    public void CancelEdit(YarnCardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.IsEditing = false;
    }

    /// <summary>
    /// Writes what the box of a row has into the text, as one step, and closes it: a line takes the text and keeps its
    /// id, its hashtags and its condition; a line left empty goes; the row that adds a line adds it, unless nothing
    /// was written after the speaker. When the text cannot be written, <see cref="Problem"/> says why.
    /// </summary>
    /// <returns>Whether the text changed.</returns>
    public bool CommitEdit(YarnCardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!row.IsEditing)
            return false;

        row.IsEditing = false;
        if (!Knows(row.Card))
            return false;

        var card = row.Card;
        var text = row.EditText.Trim();
        var current = Synchronize();
        try
        {
            if (row.IsAdd)
            {
                // A speaker alone is no line
                var colon = text.IndexOf(':', StringComparison.Ordinal);
                var words = colon >= 0 ? text[(colon + 1)..].Trim() : text;
                return words.Length > 0 && Apply("Add line", YarnTextPatcher.AddLine(current, card.Span, text));
            }

            if (row.Statement is not { IsText: true } statement)
                return false;

            if (text.Length == 0)
            {
                if (statement.Kind != YarnStatementKind.Line)
                {
                    Problem = "An option or a line of a line group needs a text: to remove it, remove it in the text.";
                    return false;
                }

                return Apply("Delete line", YarnTextPatcher.DeleteStatement(current, card.Span, statement));
            }

            return Apply(statement.Kind == YarnStatementKind.Option ? "Edit option" : "Edit line", YarnTextPatcher.SetStatementText(current, card.Span, statement, text));
        }
        catch (ArgumentException exception)
        {
            Problem = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// Selects the card of a row and shows the row's line in the text pane: for the rows the card does not edit, such
    /// as commands and blocks.
    /// </summary>
    public void ShowInText(YarnCardRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!Knows(row.Card) || row.Statement is not { } statement)
            return;

        Select(row.Card);
        RequestPlace(new YarnTextPlace(statement.Line, statement.TextStart - statement.LineStart + 1));
    }

    /// <summary>
    /// Writes the places of cards in their <c>position:</c> headers, as one step whatever their number. The cards
    /// that had no place of their own get the one they are shown at with them: they would move otherwise, as their
    /// place is worked out from the cards that have one.
    /// </summary>
    public void Move(IReadOnlyList<YarnCardMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var current = Synchronize();
        var places = new Dictionary<YarnNodeCardViewModel, YarnGraphPoint>();
        foreach (var card in Cards.Where(c => !c.HasPosition))
        {
            places[card] = card.Position;
        }

        var moved = new HashSet<YarnNodeCardViewModel>();
        foreach (var move in moves)
        {
            if (Knows(move.Card))
            {
                places[move.Card] = move.Position;
                moved.Add(move.Card);
            }
        }

        if (moved.Count == 0)
            return;

        var edits = places.Select(p => YarnTextPatcher.SetPosition(current, p.Key.Span, p.Value.X, p.Value.Y)).ToArray();
        Apply(moved.Count == 1 ? "Move node" : "Move nodes", edits);
    }

    /// <summary>
    /// Writes a place for every card: along the jumps, in rows of the nodes nothing jumps to, each frame of
    /// <see cref="GroupBy"/> (else each cluster) in a block of its own.
    /// </summary>
    public void Layout()
    {
        var current = Synchronize();
        var places = YarnGraphLayout.Compute(Cards.Count, CardLinks(), NodeGroups(Cards), Sections(Cards), [.. Cards.Select(c => c.Height)]);
        var edits = Cards.Select((card, i) => YarnTextPatcher.SetPosition(current, card.Span, places[i].X, places[i].Y)).ToArray();
        Apply("Lay out nodes", edits);
    }

    /// <summary>
    /// Opens the editor of the script a stub's node is in, with the node selected.
    /// </summary>
    public async Task OpenTarget(YarnStubCardViewModel stub)
    {
        if (stub?.Script is { } script && await OpenEditor(script) is { } editor)
        {
            editor.Select(stub.Title);
        }
    }

    /// <summary>
    /// Shows the dialogue player and starts a run of the script's project at a node.
    /// </summary>
    /// <param name="card">The card of the node; null for the selected node, else the first node of the script.</param>
    public async Task PlayFromHere(YarnNodeCardViewModel? card)
    {
        card ??= SelectedCards.OfType<YarnNodeCardViewModel>().FirstOrDefault(c => !c.IsNote) ?? Cards.FirstOrDefault(c => !c.IsNote);
        if (card is null)
            return;

        if (card.IsNote)
        {
            Problem = "A note is not part of the story: there is nothing to play.";
            return;
        }

        if (analysisService?.ProjectsOf(Asset).FirstOrDefault() is not { } project)
        {
            Problem = "The dialogue player plays the nodes of a Yarn project, and this script is in none.";
            return;
        }

        ServiceProvider.TryGet<IPanelService>()?.Show(DialoguePlayerViewModel.PanelId);
        var player = DialoguePlayerService.Of(Session);
        await player.Start(project, card.Title);
        Problem = player.Problem;
    }

    /// <summary>
    /// Opens the editor of a script, or shows the one that is open.
    /// </summary>
    /// <returns>The editor, or null when the session cannot open one.</returns>
    public static async Task<YarnScriptEditorViewModel?> OpenEditor(YarnScriptViewModel script)
    {
        ArgumentNullException.ThrowIfNull(script);

        if (script.ServiceProvider.TryGet<IAssetEditorsManager>() is not { } editors)
            return null;

        await editors.OpenAssetEditorAsync(script);
        return editors.TryGetAssetEditor<YarnScriptEditorViewModel>(script, out var editor) ? editor : null;
    }

    /// <summary>
    /// The card of the node a place of the text is in, or null between two nodes.
    /// </summary>
    public YarnNodeCardViewModel? CardAt(int offset)
    {
        Synchronize();
        return Cards.FirstOrDefault(c => offset >= c.Span.HeaderStart && offset < Math.Max(c.Span.End, c.Span.HeaderStart + 1));
    }

    /// <summary>
    /// Asks the text pane to show a place of the script. A pane that opens later shows it then.
    /// </summary>
    public void RequestPlace(YarnTextPlace place)
    {
        requestedPlace = place;
        PlaceRequested?.Invoke();
    }

    /// <summary>
    /// Gives the place last asked for, once.
    /// </summary>
    public YarnTextPlace? TakeRequestedPlace()
    {
        var place = requestedPlace;
        requestedPlace = null;
        return place;
    }

    /// <summary>
    /// Shows the place of a message of the compiler: in this script, or in the editor of the script it is about.
    /// </summary>
    public async Task GoTo(YarnDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var place = new YarnTextPlace(Math.Max(1, diagnostic.Line), Math.Max(1, diagnostic.Column));
        var script = analysis?.ScriptOf(diagnostic.File);
        if (script is null || script == Asset)
        {
            RequestPlace(place);
        }
        else if (await OpenEditor(script) is { } editor)
        {
            editor.RequestPlace(place);
        }
    }

    /// <summary>
    /// Selects the node of a title: a card of this script, or a card of the script of the project that has it, in its editor.
    /// </summary>
    /// <returns>Whether a node of the title was found.</returns>
    public async Task<bool> GoToNode(string title)
    {
        if (Select(title))
            return true;

        var fileName = analysis?.FileNameOf(Asset);
        var node = analysis?.Nodes.FirstOrDefault(n => n.Title == title && n.File != fileName);
        return node is not null
            && analysis!.ScriptOf(node.File) is { } script
            && await OpenEditor(script) is { } editor
            && editor.Select(title);
    }

    /// <summary>
    /// Selects the cards of a title, and nothing else.
    /// </summary>
    /// <returns>Whether the script has a node of the title.</returns>
    public bool Select(string title)
    {
        Synchronize();
        var cards = Cards.Where(c => c.Title == title).ToList();
        if (cards.Count == 0)
            return false;

        SelectedCards.Clear();
        SelectedCards.AddRange(cards);
        return true;
    }

    private YarnNodeCardViewModel? Add(string wanted, YarnGraphPoint at, IReadOnlyList<(string Key, string Value)>? headers, string actionName)
    {
        var current = Synchronize();
        var title = YarnTitles.Unique(wanted, KnownTitles());
        if (!Apply(actionName, YarnTextPatcher.AddNode(current, title, at.X, at.Y, headers)))
            return null;

        var card = Cards.LastOrDefault(c => c.Title == title);
        Select(card);
        return card;
    }

    private void Select(YarnNodeCardViewModel? card)
    {
        SelectedCards.Clear();
        if (card is not null)
        {
            SelectedCards.Add(card);
        }
    }

    private bool Knows(YarnNodeCardViewModel? card) => card is not null && Cards.Contains(card);

    private List<YarnNodeCardViewModel> SelectedNodeCards() => [.. Cards.Where(SelectedCards.Contains)];

    // The titles a new node cannot take: the ones of the script, and the ones the compiler saw in the rest of the project
    private IEnumerable<string> KnownTitles()
    {
        var fileName = analysis?.FileNameOf(Asset);
        return document.Nodes.Select(n => n.Title).Concat(analysis?.Nodes.Where(n => n.File != fileName).Select(n => n.Title) ?? []);
    }

    /// <summary>
    /// Gives the document of the text as it is, with the cards on its nodes: the operations of the patcher take both.
    /// </summary>
    private YarnDocument Synchronize()
    {
        if (document.Text != TextBuffer.Text)
        {
            Refresh(withLinks: false);
        }

        return document;
    }

    /// <summary>
    /// Makes the edits of several operations on the document one step of the undo history.
    /// </summary>
    /// <returns>Whether the text changed.</returns>
    private bool Apply(string actionName, params IReadOnlyList<YarnTextEdit>[] operations)
    {
        Problem = null;
        var edits = operations.SelectMany(e => e).OrderBy(e => e.Offset).ToList();
        if (edits.Count == 0)
            return false;

        var before = TextBuffer.Text;
        TextBuffer.Apply(edits, actionName);
        Synchronize();
        return !ReferenceEquals(before, TextBuffer.Text);
    }

    private void OnTextChanged(YarnTextChange change)
    {
        // A step of several edits tells each of them, all once the text is the new one: the cards are read once
        if (document.Text != TextBuffer.Text)
            Refresh(withLinks: false);
    }

    private void OnAnalysisChanged(YarnProjectAnalysis changed)
    {
        // A script can come into a project or leave it, which changes the analysis that is the script's
        // (the one of a project it just came into is not there yet: the one before stays until then)
        var mine = analysisService!.AnalysisOf(Asset);
        if (mine is null || (ReferenceEquals(mine, analysis) && !ReferenceEquals(mine, changed)))
        {
            RefreshNotice();
            return;
        }

        analysis = mine;
        Refresh(withLinks: true);
    }

    /// <summary>
    /// Reads the cards from the text again. What the compiler says (the links among it) is read again only when it is
    /// about the text that is there: in between it stays on the cards, as its lines and its titles are of another text.
    /// </summary>
    private void Refresh(bool withLinks)
    {
        document = YarnDocument.Parse(TextBuffer.Text);
        IsAnalysisCurrent = analysis?.TextOf(Asset) == document.Text;
        withLinks |= IsAnalysisCurrent;
        var fileName = analysis?.FileNameOf(Asset);
        var compiled = CompiledNodes(fileName);

        RefreshCards(fileName, withLinks ? compiled : null);
        if (withLinks)
        {
            RefreshLinks(fileName, compiled);
        }
        else
        {
            PruneLinks();
        }

        RefreshEnds();
        Place();
        RefreshNodes();

        Diagnostics = analysis is null
            ? []
            : [.. analysis.Diagnostics.OrderBy(d => d.File == fileName ? 0 : 1).ThenBy(d => d.File, StringComparer.Ordinal).ThenBy(d => d.Line).ThenBy(d => d.Column)];
        ScriptDiagnostics = [.. Diagnostics.Where(d => d.File == fileName)];
        RefreshNotice();
        RefreshPlayMarks();
        Refreshed?.Invoke();
    }

    /// <summary>
    /// Marks the cards of the nodes the run of the dialogue player went through and is in, and tells its line.
    /// </summary>
    private void RefreshPlayMarks()
    {
        var names = new Dictionary<(string Title, int Index), string>();
        int? line = null;
        string? current = null;
        IReadOnlyList<string> path = [];
        if (player is { IsPlaying: true, Project: { } project } && analysisService?.ProjectsOf(Asset).Contains(project) == true)
        {
            // The nodes of the script as the run's build has them, by title and by rank among the nodes of the title
            var fileName = YarnProjectFiles.FileNameOf(Asset.AssetItem);
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (title, name) in player.NodesOf(fileName))
            {
                var rank = ranks.GetValueOrDefault(title);
                ranks[title] = rank + 1;
                names[(title, rank)] = name;
            }

            current = player.CurrentNode;
            path = player.NodePath;

            // The line is of the text the run was built from
            if (!player.IsStale && player.CurrentFile == fileName)
                line = player.CurrentLine;
        }

        foreach (var card in Cards)
        {
            card.PlayMark = !names.TryGetValue((card.Title, card.GroupIndex), out var name) ? YarnPlayMark.None
                : name == current ? YarnPlayMark.Current
                : path.Contains(name) ? YarnPlayMark.Visited
                : YarnPlayMark.None;
        }

        PlayLine = line;
    }

    // The nodes of the script in the analysis, by title and by rank among the nodes of the title
    private Dictionary<(string Title, int Index), YarnNodeData> CompiledNodes(string? fileName)
    {
        var compiled = new Dictionary<(string, int), YarnNodeData>();
        if (analysis is null || fileName is null)
            return compiled;

        var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in analysis.Nodes.Where(n => n.File == fileName).OrderBy(n => n.HeaderStartLine))
        {
            var rank = ranks.GetValueOrDefault(node.Title);
            ranks[node.Title] = rank + 1;
            compiled[(node.Title, rank)] = node;
        }

        return compiled;
    }

    /// <param name="compiled">The nodes of a new analysis; null to leave on the cards what the last one said.</param>
    private void RefreshCards(string? fileName, Dictionary<(string Title, int Index), YarnNodeData>? compiled)
    {
        var nodes = document.Nodes;

        // A node keeps the card of its title; a node whose title is new takes the card of a title that is gone, which
        // is how a renamed node keeps its card
        var byTitle = Cards.ToDictionary(c => (c.Title, c.GroupIndex));
        var cards = new YarnNodeCardViewModel?[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            if (byTitle.Remove((nodes[i].Title, nodes[i].Index), out var card))
            {
                cards[i] = card;
            }
        }

        var spare = new Queue<YarnNodeCardViewModel>(Cards.Where(byTitle.ContainsValue));
        for (var i = 0; i < nodes.Count; i++)
        {
            cards[i] ??= spare.TryDequeue(out var card) ? card : NewCard(nodes[i]);
        }

        var counts = nodes.GroupBy(n => n.Title, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var lines = new LineIndex(document.Text);
        var errors = analysis?.Diagnostics.Where(d => d.Severity == YarnSeverity.Error && d.File == fileName).ToList() ?? [];
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var card = cards[i]!;
            if (compiled is not null)
            {
                var data = compiled.GetValueOrDefault((node.Title, node.Index));
                var firstLine = lines.LineOf(node.HeaderStart);
                var lastLine = lines.LineOf(Math.Max(node.HeaderStart, node.End - 1));
                card.Complexity = data?.Complexity ?? -1;
                card.Speakers = data?.Speakers ?? [];
                card.Options = data is null ? [] : [.. data.Options.Select(o => o.Text)];
                card.HasError = errors.Exists(e => e.Line >= firstLine && e.Line <= lastLine && (e.Node is null || e.Node == node.Title));
            }

            card.Span = node;
            card.Title = node.Title;
            card.GroupIndex = node.Index;
            card.GroupCount = counts[node.Title];
            card.When = [.. node.Headers.Where(h => h.Key == "when").Select(h => h.Value)];
            card.Tags = (node.Header("tags") ?? "").Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            card.Color = NullIfEmpty(node.Header("color"));
            card.Cluster = NullIfEmpty(node.Header("cluster"));
            card.IsNote = node.Header("style") == "note";
            card.GroupValue = groupBy is null ? null : node.Header(groupBy);
            card.Preview = PreviewOf(node);
            card.HasPosition = node.Position is not null;
            card.IsFilteredOut = !Matches(node);
            RefreshRows(card, document.StatementsOf(node));
            card.Height = HeightOf(card);
        }

        Synchronize(Cards, [.. cards.OfType<YarnNodeCardViewModel>()]);
        GroupableHeaders = [.. nodes.SelectMany(n => n.Headers).Select(h => h.Key).Where(k => !UngroupableHeaders.Contains(k)).Distinct(StringComparer.Ordinal)];
    }

    private void RefreshLinks(string? fileName, Dictionary<(string Title, int Index), YarnNodeData> compiled)
    {
        var links = new List<YarnLinkViewModel>();
        var stubs = new List<YarnStubCardViewModel>();
        foreach (var source in Cards)
        {
            // The lines of the analysis are the ones of the text only when it is the text that was analysed
            var statements = IsAnalysisCurrent ? document.StatementsOf(source.Span) : null;
            if (compiled.TryGetValue((source.Title, source.GroupIndex), out var data))
            {
                foreach (var jump in data.Jumps)
                {
                    // A jump to a node group shows on the first node of the group
                    object target = Cards.FirstOrDefault(c => c.Title == jump.Target) is { } card ? card : StubOf(stubs, source, jump.Target, fileName);
                    Add(source, target, jump.IsDetour, jump.Line, RowOf(source, statements, jump.Line));
                }
            }

            // A jump to an expression leads to a node known when it plays
            foreach (var statement in statements ?? [])
            {
                if (statement is { Kind: YarnStatementKind.Jump or YarnStatementKind.Detour, Target: ['{', ..] expression })
                {
                    var stub = stubs.Find(s => s.Source == source && s.IsComputed && s.Title == expression)
                        ?? Stubs.FirstOrDefault(s => s.Source == source && s.IsComputed && s.Title == expression)
                        ?? new YarnStubCardViewModel(source, expression, null, null, isComputed: true);
                    if (!stubs.Contains(stub))
                        stubs.Add(stub);

                    Add(source, stub, statement.Kind == YarnStatementKind.Detour, statement.Line, RowOf(source, statements, statement.Line));
                }
            }
        }

        Synchronize(Stubs, stubs);
        Synchronize(Links, links);

        void Add(YarnNodeCardViewModel source, object target, bool isDetour, int line, YarnCardRowViewModel? row)
        {
            if (links.Exists(l => l.Source == source && l.Target == target && l.IsDetour == isDetour && l.Row == row))
                return;

            links.Add(Links.FirstOrDefault(l => l.Source == source && l.Target == target && l.IsDetour == isDetour && l.Line == line && l.Row == row)
                ?? new YarnLinkViewModel(source, target, isDetour, line, row));
        }
    }

    // The row a jump of a line is under: its option, its line group item or its branch of a block; null at the top of the body
    private static YarnCardRowViewModel? RowOf(YarnNodeCardViewModel source, IReadOnlyList<YarnStatementSpan>? statements, int line)
    {
        var statement = statements?.FirstOrDefault(s => s.Line == line);
        return statement is { Parent: >= 0 } && statement.Parent < source.Rows.Count ? source.Rows[statement.Parent] : null;
    }

    private YarnNodeCardViewModel NewCard(YarnNodeSpan node)
    {
        var card = new YarnNodeCardViewModel(node) { IsFolded = FoldCards };
        card.PropertyChanged += (_, e) =>
        {
            // A card that folds or unfolds takes another room
            if (e.PropertyName == nameof(YarnNodeCardViewModel.IsFolded) && Cards.Contains(card))
            {
                card.Height = HeightOf(card);
                Place();
            }
        };
        return card;
    }

    // A row stays with its rank: the slots of the rows are what the links of the graph hold
    private static void RefreshRows(YarnNodeCardViewModel card, IReadOnlyList<YarnStatementSpan> statements)
    {
        while (card.Rows.Count > statements.Count)
        {
            card.Rows.RemoveAt(card.Rows.Count - 1);
        }

        for (var i = 0; i < statements.Count; i++)
        {
            if (i == card.Rows.Count)
            {
                card.Rows.Add(new YarnCardRowViewModel(card));
            }

            card.Rows[i].Statement = statements[i];
        }
    }

    // The dialogue ends in a node that stops, or that jumps nowhere and is no detour's target
    private void RefreshEnds()
    {
        foreach (var card in Cards)
        {
            var statements = document.StatementsOf(card.Span);
            card.IsEnd = !card.IsNote
                && (statements.Any(s => s is { Kind: YarnStatementKind.Command, Text: "stop" })
                    || (!statements.Any(s => s.Kind == YarnStatementKind.Jump) && !Links.Any(l => l.IsDetour && l.Target == card)));
        }
    }

    /// <summary>
    /// About the height of a card as it shows: its title bar, its conditions, its rows (or its preview when folded),
    /// its chips and its connectors.
    /// </summary>
    private static int HeightOf(YarnNodeCardViewModel card)
    {
        var lines = card.When.Count + (card.Tags.Count > 0 || card.GroupValue is not null ? 1 : 0);
        IEnumerable<YarnCardRowViewModel> rows = card.Rows;
        if (card.IsFolded)
        {
            lines += (card.Speakers.Count > 0 ? 1 : 0) + (card.Preview.Length == 0 ? 0 : Math.Min(PreviewLines, card.Preview.Split('\n').Length));
            rows = rows.Where(r => r.HasConnector);
        }
        else if (!card.IsNote || card.Rows.Count > 0)
        {
            // The row that adds a line
            lines++;
        }

        foreach (var row in rows)
        {
            var length = (row.Speaker?.Length + 2 ?? 0) + row.Display.Length + (row.Condition?.Length + 3 ?? 0);
            var room = Math.Max(10, RowCharacters - 2 * row.Depth);
            lines += Math.Clamp((length + room - 1) / room, 1, RowMaxLines);
        }

        return CardFrameHeight + lines * CardLineHeight;
    }

    private YarnStubCardViewModel StubOf(List<YarnStubCardViewModel> stubs, YarnNodeCardViewModel source, string title, string? fileName)
    {
        var node = analysis?.Nodes.FirstOrDefault(n => n.Title == title && n.File != fileName);
        var stub = stubs.Find(s => s.Source == source && s.Title == title)
            ?? Stubs.FirstOrDefault(s => s.Source == source && s.Title == title && s.FileName == node?.File)
            ?? new YarnStubCardViewModel(source, title, node?.File, node is null ? null : analysis!.ScriptOf(node.File));
        if (!stubs.Contains(stub))
        {
            stubs.Add(stub);
        }

        return stub;
    }

    // Without a new analysis, the links stay but for the ones whose cards are gone
    private void PruneLinks()
    {
        Synchronize(Stubs, [.. Stubs.Where(s => Cards.Contains(s.Source))]);
        Synchronize(Links, [.. Links.Where(l => Cards.Contains(l.Source) && (l.Target is YarnNodeCardViewModel card ? Cards.Contains(card) : Stubs.Contains((YarnStubCardViewModel)l.Target)))]);
    }

    private List<(int Source, int Target)> CardLinks()
    {
        var links = new List<(int, int)>();
        foreach (var link in Links)
        {
            if (link.Target is YarnNodeCardViewModel target)
            {
                links.Add((Cards.IndexOf(link.Source), Cards.IndexOf(target)));
            }
        }

        return links;
    }

    // The nodes of a node group, which have one title, stay together in a layout
    /// <summary>
    /// The frame each card is laid out in: its value of <see cref="GroupBy"/> when the cards are grouped, else its cluster.
    /// A stub goes with the card it is a stub of.
    /// </summary>
    private List<string?> Sections(IEnumerable<object> cards)
    {
        string? Of(YarnNodeCardViewModel card) => groupBy is null ? card.Cluster : card.GroupValue;
        return [.. cards.Select(c => c switch
        {
            YarnNodeCardViewModel card => Of(card),
            YarnStubCardViewModel stub => Of(stub.Source),
            _ => null,
        })];
    }

    private static List<int> NodeGroups(IEnumerable<object> cards)
    {
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        var groups = new List<int>();
        foreach (var card in cards)
        {
            if (card is YarnNodeCardViewModel { IsNote: false } node)
            {
                if (!numbers.TryGetValue(node.Title, out var number))
                    numbers[node.Title] = number = numbers.Count;
                groups.Add(number);
            }
            else
            {
                groups.Add(-1);
            }
        }

        return groups;
    }

    /// <summary>
    /// Gives each card its place: the one its node has, or one worked out for it, among the others without a place,
    /// under the cards that have theirs.
    /// </summary>
    private void Place()
    {
        var free = new List<object>();
        free.AddRange(Cards.Where(c => !c.HasPosition));
        free.AddRange(Stubs.Where(s => !s.Source.HasPosition));
        var links = Links.Select(l => (free.IndexOf(l.Source), free.IndexOf(l.Target))).ToList();
        var heights = free.Select(n => n is YarnNodeCardViewModel card ? card.Height : YarnGraphLayout.DefaultHeight).ToList();
        var places = YarnGraphLayout.Compute(free.Count, links, NodeGroups(free), Sections(free), heights);
        var top = Cards.Where(c => c.HasPosition).Select(c => c.Span.Position!.Value.Y + c.Height + YarnGraphLayout.RowGap).DefaultIfEmpty(0).Max();

        foreach (var card in Cards)
        {
            card.Position = card.Span.Position is var (x, y) ? new YarnGraphPoint(x, y) : Free(card);
        }

        // The stubs of a card with a place go on its right, one under the other, each in the free row nearest to its own,
        // with room over a card for the label of a frame
        var taken = Cards.Select(c => (c.Position.X, Y: c.Position.Y - FrameLabelHeight, Width: CardWidth, Height: c.Height + FrameLabelHeight)).ToList();
        var rows = new Dictionary<YarnNodeCardViewModel, int>();
        foreach (var stub in Stubs)
        {
            if (!stub.Source.HasPosition)
            {
                stub.Position = Free(stub);
                continue;
            }

            var row = rows.GetValueOrDefault(stub.Source);
            rows[stub.Source] = row + 1;
            var x = stub.Source.Position.X + StubOffsetX;
            var y = stub.Source.Position.Y + row * StubRowHeight;
            for (var step = 1; step <= StubRowsTried && IsTaken(x, y); step++)
            {
                // Under, over, further under, further over
                y += step % 2 == 1 ? step * StubRowHeight : -step * StubRowHeight;
            }

            stub.Position = new YarnGraphPoint(x, y);
            taken.Add((x, y, StubWidth, StubHeight));
        }

        bool IsTaken(int x, int y)
            => taken.Exists(t => x < t.X + t.Width + StubMargin && t.X < x + StubWidth + StubMargin && y < t.Y + t.Height + StubMargin && t.Y < y + StubHeight + StubMargin);

        YarnGraphPoint Free(object node)
        {
            var place = places[free.IndexOf(node)];
            return new YarnGraphPoint(place.X, place.Y + top);
        }
    }

    private void RefreshNodes()
    {
        var nodes = new List<object>(Cards.Count + Stubs.Count);
        nodes.AddRange(Cards);
        nodes.AddRange(Stubs);
        Synchronize(Nodes, nodes);

        for (var i = SelectedCards.Count - 1; i >= 0; i--)
        {
            if (!Nodes.Contains(SelectedCards[i]))
            {
                SelectedCards.RemoveAt(i);
            }
        }
    }

    private void RefreshNotice()
    {
        var projects = analysisService?.ProjectsOf(Asset) ?? [];
        Notice = projects.Count switch
        {
            0 => NoProjectNotice,
            1 => null,
            _ => $"This script is in {projects.Count} Yarn projects: it is checked with '{projects[0].AssetItem.UnqualifiedUrl.FullPath}'.",
        };
    }

    private bool Matches(YarnNodeSpan node)
    {
        return search.Length == 0
            || document.Text.AsSpan(node.HeaderStart, node.End - node.HeaderStart).Contains(search.AsSpan().Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // The first lines of a node that someone says: neither an option, a command nor a comment
    private string PreviewOf(YarnNodeSpan node)
    {
        var lines = new List<string>();
        foreach (var raw in document.Text[node.BodyStart..node.BodyEnd].Split('\n'))
        {
            var line = raw.Trim();
            var tag = line.IndexOf(" #", StringComparison.Ordinal);
            if (tag >= 0)
            {
                line = line[..tag].TrimEnd();
            }

            if (line.Length == 0 || line.StartsWith("<<", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("->", StringComparison.Ordinal))
                continue;

            lines.Add(line.Length > PreviewLineLength ? line[..PreviewLineLength] + "..." : line);
            if (lines.Count == PreviewLines)
                break;
        }

        return string.Join('\n', lines);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// Makes a list hold the wanted items in their order, with the fewest changes: an item that stays is not removed.
    /// </summary>
    private static void Synchronize<T>(ObservableList<T> list, IReadOnlyList<T> wanted)
        where T : class
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(list[i]))
            {
                list.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < list.Count && ReferenceEquals(list[i], wanted[i]))
                continue;

            var index = list.IndexOf(wanted[i]);
            if (index >= 0)
            {
                list.Move(index, i);
            }
            else
            {
                list.Insert(i, wanted[i]);
            }
        }
    }

    /// <summary>
    /// The lines of a text, to give the line of a place, from 1.
    /// </summary>
    private readonly struct LineIndex
    {
        private readonly List<int> starts = [0];

        public LineIndex(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }
        }

        public int LineOf(int offset)
        {
            var index = starts.BinarySearch(offset);
            return index >= 0 ? index + 1 : ~index;
        }
    }
}

/// <summary>
/// What <see cref="YarnScriptEditorViewModel.RenameCommand"/> takes: a card and the title its node is to have.
/// </summary>
public sealed record YarnRenameRequest(YarnNodeCardViewModel Card, string Title);

/// <summary>
/// What <see cref="YarnScriptEditorViewModel.LinkCommand"/> takes: the card a jump is added to, and the card or the stub
/// it leads to.
/// </summary>
/// <param name="IsDetour">Whether the dialogue comes back from the target.</param>
/// <param name="Row">The row of the source the jump goes under: an option, a line group item or a branch of a block; null for the end of the node.</param>
public sealed record YarnLinkRequest(YarnNodeCardViewModel Source, object Target, bool IsDetour = false, YarnCardRowViewModel? Row = null);

/// <summary>
/// A place in the text of a script, from 1.
/// </summary>
public readonly record struct YarnTextPlace(int Line, int Column);

/// <summary>
/// A card and the place the user dragged it to.
/// </summary>
public sealed record YarnCardMove(YarnNodeCardViewModel Card, YarnGraphPoint Position);
