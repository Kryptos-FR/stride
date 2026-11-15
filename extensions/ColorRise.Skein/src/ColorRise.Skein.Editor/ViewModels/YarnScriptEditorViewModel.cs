using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Assets.Text;
using ColorRise.Skein.Editor.Graph;
using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.Text;
using Stride.Core.Assets.Editor.Annotations;
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
    private const int PreviewLines = 3;
    private const int PreviewLineLength = 100;

    // The headers that are not for grouping: each node has its own title and place, and a condition is not a name
    private static readonly HashSet<string> UngroupableHeaders = new(StringComparer.Ordinal) { "title", "position", "when" };

    private readonly YarnAnalysisService? analysisService;
    private YarnDocument document;
    private YarnProjectAnalysis? analysis;
    private IReadOnlyList<YarnDiagnostic> diagnostics = [];
    private IReadOnlyList<string> groupableHeaders = [];
    private string? notice;
    private string? problem;
    private string? groupBy;
    private string search = "";

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
        LinkCommand = new AnonymousCommand<YarnLinkRequest>(ServiceProvider, request => Link(request.Source, request.Target));
        MoveCommand = new AnonymousCommand<IReadOnlyList<YarnCardMove>>(ServiceProvider, Move);
        LayoutCommand = new AnonymousCommand(ServiceProvider, Layout);
        OpenTargetCommand = new AnonymousTaskCommand<YarnStubCardViewModel>(ServiceProvider, OpenTarget);
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
    /// What the user should know of the place of the script among the Yarn projects, or null: no project includes it,
    /// or several do.
    /// </summary>
    public string? Notice { get => notice; private set => SetValue(ref notice, value); }

    /// <summary>
    /// Why the last thing asked of the graph was not done, or null.
    /// </summary>
    public string? Problem { get => problem; private set => SetValue(ref problem, value); }

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

    /// <inheritdoc/>
    public override Task<bool> Initialize()
    {
        TextBuffer.Changed += OnTextChanged;
        if (analysisService is not null)
        {
            analysisService.Changed += OnAnalysisChanged;
            analysis = analysisService.AnalysisOf(Asset);
        }

        Refresh(withLinks: true);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override void Destroy()
    {
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
    /// Adds a jump to a node at the end of another: <paramref name="target"/> is a card of the script or a stub.
    /// </summary>
    public void Link(YarnNodeCardViewModel source, object target)
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
        Apply("Link nodes", YarnTextPatcher.AppendJump(current, source.Span, title));
    }

    /// <summary>
    /// Writes the places of cards in their <c>position:</c> headers, as one step whatever their number.
    /// </summary>
    public void Move(IReadOnlyList<YarnCardMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var current = Synchronize();
        var places = new Dictionary<YarnNodeCardViewModel, YarnGraphPoint>();
        foreach (var move in moves)
        {
            if (Knows(move.Card))
            {
                places[move.Card] = move.Position;
            }
        }

        var edits = places.Select(p => YarnTextPatcher.SetPosition(current, p.Key.Span, p.Value.X, p.Value.Y)).ToArray();
        Apply(edits.Length == 1 ? "Move node" : "Move nodes", edits);
    }

    /// <summary>
    /// Writes a place for every card: in columns along the jumps, from the nodes nothing jumps to.
    /// </summary>
    public void Layout()
    {
        var current = Synchronize();
        var places = YarnGraphLayout.Compute(Cards.Count, CardLinks());
        var edits = Cards.Select((card, i) => YarnTextPatcher.SetPosition(current, card.Span, places[i].X, places[i].Y)).ToArray();
        Apply("Lay out nodes", edits);
    }

    /// <summary>
    /// Opens the editor of the script a stub's node is in, with the node selected.
    /// </summary>
    public async Task OpenTarget(YarnStubCardViewModel stub)
    {
        if (stub?.Script is not { } script || ServiceProvider.TryGet<IAssetEditorsManager>() is not { } editors)
            return;

        await editors.OpenAssetEditorAsync(script);
        if (editors.TryGetAssetEditor<YarnScriptEditorViewModel>(script, out var editor))
        {
            editor.Select(stub.Title);
        }
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

    private void OnTextChanged(YarnTextChange change) => Refresh(withLinks: false);

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
        withLinks |= analysis?.TextOf(Asset) == document.Text;
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

        Place();
        RefreshNodes();

        Diagnostics = analysis is null
            ? []
            : [.. analysis.Diagnostics.OrderBy(d => d.File == fileName ? 0 : 1).ThenBy(d => d.File, StringComparer.Ordinal).ThenBy(d => d.Line).ThenBy(d => d.Column)];
        RefreshNotice();
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
            cards[i] ??= spare.TryDequeue(out var card) ? card : new YarnNodeCardViewModel(nodes[i]);
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
            if (!compiled.TryGetValue((source.Title, source.GroupIndex), out var data))
                continue;

            foreach (var jump in data.Jumps)
            {
                // A jump to a node group shows on the first node of the group
                object target = Cards.FirstOrDefault(c => c.Title == jump.Target) is { } card ? card : StubOf(stubs, source, jump.Target, fileName);
                if (links.Exists(l => l.Source == source && l.Target == target && l.IsDetour == jump.IsDetour))
                    continue;

                links.Add(Links.FirstOrDefault(l => l.Source == source && l.Target == target && l.IsDetour == jump.IsDetour && l.Line == jump.Line)
                    ?? new YarnLinkViewModel(source, target, jump.IsDetour, jump.Line));
            }
        }

        Synchronize(Stubs, stubs);
        Synchronize(Links, links);
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
        var places = YarnGraphLayout.Compute(free.Count, links);
        var top = Cards.Where(c => c.HasPosition).Select(c => c.Span.Position!.Value.Y + YarnGraphLayout.RowHeight).DefaultIfEmpty(0).Max();

        foreach (var card in Cards)
        {
            card.Position = card.Span.Position is var (x, y) ? new YarnGraphPoint(x, y) : Free(card);
        }

        // The stubs of a card with a place go on its right, one under the other
        var ranks = new Dictionary<YarnNodeCardViewModel, int>();
        foreach (var stub in Stubs)
        {
            var rank = ranks.GetValueOrDefault(stub.Source);
            ranks[stub.Source] = rank + 1;
            stub.Position = stub.Source.HasPosition
                ? new YarnGraphPoint(stub.Source.Position.X + StubOffsetX, stub.Source.Position.Y + rank * StubRowHeight)
                : Free(stub);
        }

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
public sealed record YarnLinkRequest(YarnNodeCardViewModel Source, object Target);

/// <summary>
/// A card and the place the user dragged it to.
/// </summary>
public sealed record YarnCardMove(YarnNodeCardViewModel Card, YarnGraphPoint Position);
