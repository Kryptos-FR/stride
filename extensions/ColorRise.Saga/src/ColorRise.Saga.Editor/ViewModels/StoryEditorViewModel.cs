using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Extensions;
using Stride.Core.IO;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;
using Stride.Core.Quantum;
using Stride.Core.Serialization;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// The editor of a story: one graph opened at a depth (the plot, an act, a scenario or a quest) with a breadcrumb back up.
/// </summary>
/// <remarks>
/// The graph follows the story, whatever changed it (this editor, the property grid, an undo); when the cards of the open level
/// go away, it goes up to the deepest level that is still there.
/// </remarks>
[AssetEditorViewModel<StoryViewModel>]
public sealed class StoryEditorViewModel : SagaGraphEditorViewModel<StoryViewModel>
{
    public StoryEditorViewModel(StoryViewModel story)
        : base(story)
    {
        Breadcrumb.Add(new StoryGraphLevel(StoryGraphDepth.Plot, null, null, story.Name));

        OpenCommand = new AnonymousCommand<SagaGraphNodeViewModel>(ServiceProvider, Open);
        GoToLevelCommand = new AnonymousCommand<StoryGraphLevel>(ServiceProvider, GoToLevel);
        AddActCommand = new AnonymousCommand(ServiceProvider, AddAct);
        AddScenarioCommand = new AnonymousCommand(ServiceProvider, () => AddToAct(isQuest: false));
        AddQuestCommand = new AnonymousCommand(ServiceProvider, () => AddToAct(isQuest: true));
        AddDialogueCommand = new AnonymousCommand(ServiceProvider, AddDialogue);
        UpdateLevelCommands();
        // What can be added depends on the open level, which the views show
        Breadcrumb.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentLevel));
            UpdateLevelCommands();
        };
    }

    /// <summary>
    /// The levels from the plot down to the open one.
    /// </summary>
    public ObservableList<StoryGraphLevel> Breadcrumb { get; } = [];

    /// <summary>
    /// The open level.
    /// </summary>
    public StoryGraphLevel CurrentLevel => Breadcrumb[^1];

    /// <summary>
    /// Opens a node: an act shows its scenarios and quests, a scenario or quest its dialogues, a dialogue opens in its editor.
    /// </summary>
    public ICommandBase OpenCommand { get; }

    /// <summary>
    /// Goes back to a level of the breadcrumb.
    /// </summary>
    public ICommandBase GoToLevelCommand { get; }

    public ICommandBase AddActCommand { get; }

    public ICommandBase AddScenarioCommand { get; }

    public ICommandBase AddQuestCommand { get; }

    /// <summary>
    /// Creates a dialogue asset next to the story, for the open scenario or quest.
    /// </summary>
    public ICommandBase AddDialogueCommand { get; }

    private StoryAsset Story => Asset.Asset;

    /// <summary>
    /// Whether a link can go from a slot to another: from an act to another act it doesn't lead to yet, or from a scenario to a
    /// quest of another scenario or of the act.
    /// </summary>
    public override bool CanLink(SagaGraphSlot source, SagaGraphSlot target)
    {
        if (source.Direction != SagaGraphSlotDirection.Output || target.Direction != SagaGraphSlotDirection.Input || source.Node == target.Node)
            return false;
        return (source.Node.Card, target.Node.Card) switch
        {
            (ActCard from, ActCard to) => from.Next.All(x => x.TargetActId != to.Id),
            (ScenarioCard scenario, QuestCard quest) => quest.ScenarioId != scenario.Id,
            _ => false,
        };
    }

    /// <inheritdoc />
    protected override void Link(SagaGraphSlot source, SagaGraphSlot target)
    {
        switch (source.Node.Card, target.Node.Card)
        {
            case (ActCard from, ActCard to):
                Change("Link acts", () => ListOf(from, nameof(ActCard.Next)).Add(new ActLinkCard { TargetActId = to.Id }));
                break;
            case (ScenarioCard scenario, QuestCard quest):
                Change("Attach quest to scenario", () => NodeOf(quest)[nameof(QuestCard.ScenarioId)].Update(scenario.Id));
                break;
        }
    }

    /// <inheritdoc />
    protected override void MoveLink(SagaGraphLinkViewModel link, SagaGraphSlot source, SagaGraphSlot target)
    {
        // A link between acts keeps its condition: only its target changes
        if (link.Card is ActLinkCard next && source == link.Source && target.Node.Card is ActCard to)
            NodeOf(next)[nameof(ActLinkCard.TargetActId)].Update(to.Id);
        else
            base.MoveLink(link, source, target);
    }

    /// <inheritdoc />
    protected override string UnlinkName(SagaGraphLinkViewModel link) => link.Card is QuestCard ? "Detach quest from scenario" : "Unlink acts";

    /// <inheritdoc />
    protected override void Unlink(SagaGraphLinkViewModel link)
    {
        switch (link.Card)
        {
            case ActLinkCard next when link.Source.Node.Card is ActCard act:
                RemoveFrom(act, nameof(ActCard.Next), act.Next, next);
                break;
            case QuestCard quest:
                NodeOf(quest)[nameof(QuestCard.ScenarioId)].Update(string.Empty);
                break;
        }
    }

    /// <inheritdoc />
    protected override void Delete(SagaGraphNodeViewModel node)
    {
        switch (node.Card)
        {
            case ActCard act:
                foreach (var other in Story.Acts)
                {
                    foreach (var next in other.Next.Where(x => x.TargetActId == act.Id).ToList())
                        RemoveFrom(other, nameof(ActCard.Next), other.Next, next);
                }
                RemoveFrom(Story, nameof(StoryAsset.Acts), Story.Acts, act);
                break;
            case ScenarioCard scenario when OwnerAct(scenario) is { } act:
                foreach (var quest in Story.Acts.SelectMany(x => x.Quests).Where(x => x.ScenarioId == scenario.Id))
                    NodeOf(quest)[nameof(QuestCard.ScenarioId)].Update(string.Empty);
                RemoveFrom(act, nameof(ActCard.Scenarios), act.Scenarios, scenario);
                break;
            case QuestCard quest when OwnerAct(quest) is { } act:
                RemoveFrom(act, nameof(ActCard.Quests), act.Quests, quest);
                break;
            case DialogueDefinition reference when CurrentLevel.Owner is { } owner:
                // The dialogue asset stays; only the scenario or quest lets go of it
                RemoveFrom(owner, nameof(ScenarioCard.Dialogues), DialoguesOf(owner), reference);
                break;
        }
    }

    /// <inheritdoc />
    protected override void RenameReferences(object card, string oldId, string newId)
    {
        switch (card)
        {
            case ActCard:
                foreach (var next in Story.Acts.SelectMany(x => x.Next).Where(x => x.TargetActId == oldId).ToList())
                    NodeOf(next)[nameof(ActLinkCard.TargetActId)].Update(newId);
                if (Story.FirstActId == oldId)
                    RootNode[nameof(StoryAsset.FirstActId)].Update(newId);
                break;
            case ScenarioCard:
                foreach (var quest in Story.Acts.SelectMany(x => x.Quests).Where(x => x.ScenarioId == oldId).ToList())
                    NodeOf(quest)[nameof(QuestCard.ScenarioId)].Update(newId);
                break;
        }
    }

    /// <summary>
    /// Goes up to the deepest level whose cards are still in the story, and updates the titles.
    /// </summary>
    protected override void OnRefreshing()
    {
        var levels = new List<StoryGraphLevel> { new(StoryGraphDepth.Plot, null, null, Asset.Name) };
        foreach (var level in Breadcrumb.Skip(1))
        {
            if (level.Act is null || !Story.Acts.Contains(level.Act))
                break;
            var refreshed = level.Owner switch
            {
                null => level with { Title = TitleOf(level.Act) },
                ScenarioCard scenario when level.Act.Scenarios.Contains(scenario) => level with { Title = TitleOf(scenario) },
                QuestCard quest when level.Act.Quests.Contains(quest) => level with { Title = TitleOf(quest) },
                _ => null,
            };
            if (refreshed is null)
                break;
            levels.Add(refreshed);
        }
        while (Breadcrumb.Count > levels.Count)
            Breadcrumb.RemoveAt(Breadcrumb.Count - 1);
        for (var i = 0; i < levels.Count; ++i)
        {
            if (i >= Breadcrumb.Count)
                Breadcrumb.Add(levels[i]);
            else if (Breadcrumb[i] != levels[i])
                Breadcrumb[i] = levels[i];
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<SagaGraphNodeViewModel> BuildNodes(IReadOnlyDictionary<object, SagaGraphNodeViewModel> previous)
    {
        SagaGraphNodeViewModel Card(SagaGraphNodeKind kind, object card, Func<GraphNodePath> path)
        {
            if (previous.TryGetValue(card, out var node))
                return node;
            node = new SagaGraphNodeViewModel(ServiceProvider, kind, Asset, card, path);
            node.AddOutput();
            return node;
        }

        switch (CurrentLevel)
        {
            case { Depth: StoryGraphDepth.Plot }:
                foreach (var act in Story.Acts)
                    yield return Card(SagaGraphNodeKind.Act, act, () => PathOf(act));
                break;
            case { Depth: StoryGraphDepth.Act, Act: { } act }:
                foreach (var scenario in act.Scenarios)
                    yield return Card(SagaGraphNodeKind.Scenario, scenario, () => PathToItem(PathOf(act), nameof(ActCard.Scenarios), act.Scenarios.IndexOf(scenario)));
                foreach (var quest in act.Quests)
                    yield return Card(SagaGraphNodeKind.Quest, quest, () => PathToItem(PathOf(act), nameof(ActCard.Quests), act.Quests.IndexOf(quest)));
                break;
            case { Owner: { } owner }:
                foreach (var reference in DialoguesOf(owner))
                {
                    if (previous.TryGetValue(reference, out var node))
                    {
                        yield return node;
                        continue;
                    }
                    var target = AttachedReferenceManager.GetAttachedReference(reference);
                    var dialogue = target is null ? null : Session.GetAssetById(target.Id) as DialogueViewModel;
                    yield return new SagaGraphNodeViewModel(ServiceProvider, SagaGraphNodeKind.Dialogue, dialogue ?? (AssetViewModel)Asset, reference, null)
                    {
                        Dialogue = dialogue,
                        DialogueName = dialogue?.Name ?? (target is null ? "?" : SagaContentBuilder.NameOf(target.Url)),
                    };
                }
                break;
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<SagaGraphLinkViewModel> BuildLinks(IReadOnlyDictionary<object, SagaGraphNodeViewModel> nodes)
    {
        switch (CurrentLevel)
        {
            case { Depth: StoryGraphDepth.Plot }:
                foreach (var act in Story.Acts)
                {
                    foreach (var next in act.Next)
                    {
                        if (Story.Acts.FirstOrDefault(x => x.Id == next.TargetActId) is { } target)
                            yield return new SagaGraphLinkViewModel(ServiceProvider, Asset, nodes[act].Output!, nodes[target].Input, next, () => PathToItem(PathOf(act), nameof(ActCard.Next), act.Next.IndexOf(next)));
                    }
                }
                break;
            case { Depth: StoryGraphDepth.Act, Act: { } act }:
                foreach (var quest in act.Quests)
                {
                    if (act.Scenarios.FirstOrDefault(x => x.Id == quest.ScenarioId) is { } scenario)
                        yield return new SagaGraphLinkViewModel(ServiceProvider, Asset, nodes[scenario].Output!, nodes[quest].Input, quest, null);
                }
                break;
        }
    }

    private void Open(SagaGraphNodeViewModel node)
    {
        switch (node.Card)
        {
            case ActCard act:
                Breadcrumb.Add(new StoryGraphLevel(StoryGraphDepth.Act, act, null, TitleOf(act)));
                break;
            case ScenarioCard scenario:
                Breadcrumb.Add(new StoryGraphLevel(StoryGraphDepth.Scenario, CurrentLevel.Act, scenario, TitleOf(scenario)));
                break;
            case QuestCard quest:
                Breadcrumb.Add(new StoryGraphLevel(StoryGraphDepth.Quest, CurrentLevel.Act, quest, TitleOf(quest)));
                break;
            default:
                if (node.Dialogue is { } dialogue)
                    ServiceProvider.TryGet<IAssetEditorsManager>()?.OpenAssetEditorAsync(dialogue).Forget();
                return;
        }
        ClearSelection();
        Refresh();
    }

    private void UpdateLevelCommands()
    {
        AddActCommand.IsEnabled = CurrentLevel.Depth == StoryGraphDepth.Plot;
        AddScenarioCommand.IsEnabled = AddQuestCommand.IsEnabled = CurrentLevel.Depth == StoryGraphDepth.Act;
        AddDialogueCommand.IsEnabled = CurrentLevel.Owner is not null;
    }

    private void GoToLevel(StoryGraphLevel level)
    {
        var index = Breadcrumb.IndexOf(level);
        if (index < 0 || index == Breadcrumb.Count - 1)
            return;
        while (Breadcrumb.Count > index + 1)
            Breadcrumb.RemoveAt(Breadcrumb.Count - 1);
        ClearSelection();
        Refresh();
    }

    private void AddAct()
    {
        Change("Add act", () =>
        {
            var id = NewId("act_", Story.Acts.Select(x => x.Id));
            ListOf(Story, nameof(StoryAsset.Acts)).Add(new ActCard { Id = id, Position = NextPosition() });
            if (Story.FirstActId.Length == 0)
                RootNode[nameof(StoryAsset.FirstActId)].Update(id);
        });
    }

    private void AddToAct(bool isQuest)
    {
        if (CurrentLevel.Act is not { } act)
            return;
        if (isQuest)
        {
            Change("Add quest", () =>
            {
                var id = NewId("quest_", Story.Acts.SelectMany(x => x.Quests).Select(x => x.Id));
                ListOf(act, nameof(ActCard.Quests)).Add(new QuestCard { Id = id, Position = NextPosition() });
            });
        }
        else
        {
            Change("Add scenario", () =>
            {
                var id = NewId("scenario_", Story.Acts.SelectMany(x => x.Scenarios).Select(x => x.Id));
                ListOf(act, nameof(ActCard.Scenarios)).Add(new ScenarioCard { Id = id, Position = NextPosition() });
            });
        }
    }

    private void AddDialogue()
    {
        if (CurrentLevel is not { Owner: { } owner, OwnerId: { } ownerId } || Asset.Directory is not { } directory)
            return;
        Change("Add dialogue", () =>
        {
            var folder = Asset.AssetItem.Location.GetFullDirectory();
            var package = directory.Package;
            var name = NamingHelper.ComputeNewName($"{ownerId}_dialogue", x => package.Package.Assets.Find(UPath.Combine(folder, new UFile(x))) is not null);
            var location = UPath.Combine(folder, new UFile(name));
            var dialogue = new DialogueAsset
            {
                Story = new AssetReference(Asset.Id, Asset.AssetItem.Location),
                Participants = { SagaContentBuilder.PlayerId },
                EntryNodeId = "start",
                Nodes = { new LineNodeCard { Id = "start" } },
            };
            var created = package.CreateAsset(directory, new AssetItem(location, dialogue), true, null);
            ListOf(owner, nameof(ScenarioCard.Dialogues)).Add(AttachedReferenceManager.CreateProxyObject<DialogueDefinition>(created.Id, created.AssetItem.Location));
        });
    }

    private GraphNodePath PathOf(ActCard act) => PathToItem(null, nameof(StoryAsset.Acts), Story.Acts.IndexOf(act));

    private ActCard? OwnerAct(object card) => Story.Acts.FirstOrDefault(x => x.Scenarios.Contains(card) || x.Quests.Contains(card));

    private static List<DialogueDefinition> DialoguesOf(object owner) => owner is ScenarioCard scenario ? scenario.Dialogues : ((QuestCard)owner).Dialogues;

    private static string TitleOf(ActCard act) => act.Title.Length > 0 ? act.Title : act.Id;

    private static string TitleOf(ScenarioCard scenario) => scenario.Title.Length > 0 ? scenario.Title : scenario.Id;

    private static string TitleOf(QuestCard quest) => quest.Title.Length > 0 ? quest.Title : quest.Id;
}
