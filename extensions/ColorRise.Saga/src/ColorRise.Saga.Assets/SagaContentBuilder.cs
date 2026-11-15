using System.Text.RegularExpressions;
using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;

namespace ColorRise.Saga.Assets;

/// <summary>
/// Builds compiled content from story and dialogue assets: parses and checks every condition and action, checks ids and
/// references, and reports each problem with its asset, card and position. The asset compilers and the story player in
/// Game Studio both use it, so they read the assets the same way.
/// </summary>
public static partial class SagaContentBuilder
{
    /// <summary>
    /// The id dialogues use for the player.
    /// </summary>
    public const string PlayerId = "player";

    /// <summary>
    /// Builds a story. Its dialogues stay as given: content references when compiling, built definitions for the player.
    /// </summary>
    public static StoryDefinition BuildStory(StoryAsset story, string assetUrl, ICollection<SagaBuildDiagnostic> diagnostics)
    {
        var builder = new Builder(assetUrl, story, diagnostics);
        builder.CheckDeclarations(story.Declarations);

        var actIds = new HashSet<string>();
        var scenarioIds = new HashSet<string>(story.Acts.SelectMany(x => x.Scenarios).Select(x => x.Id));
        var seenScenarios = new HashSet<string>();
        var seenQuests = new HashSet<string>();
        var plot = new PlotDefinition { FirstActId = story.FirstActId };
        foreach (var act in story.Acts)
        {
            var actCard = $"act {act.Id}";
            builder.CheckId(act.Id, actIds, actCard, "act");
            var definition = new ActDefinition
            {
                Id = act.Id,
                Title = act.Title,
                EndWhen = builder.Condition(act.EndWhen, $"{actCard} / EndWhen"),
                Attributes = builder.Attributes(act.Attributes, SagaCardKinds.Act, actCard),
            };
            foreach (var link in act.Next)
            {
                var linkCard = $"{actCard} / link to {link.TargetActId}";
                if (!story.Acts.Any(x => x.Id == link.TargetActId))
                    builder.Report(linkCard, $"The story has no act '{link.TargetActId}'.");
                definition.Next.Add(new ActLink { TargetActId = link.TargetActId, When = builder.Condition(link.When, $"{linkCard} / When") });
            }
            foreach (var scenario in act.Scenarios)
            {
                var card = $"{actCard} / scenario {scenario.Id}";
                builder.CheckId(scenario.Id, seenScenarios, card, "scenario");
                var outcomes = new HashSet<string>();
                definition.Scenarios.Add(new ScenarioDefinition
                {
                    Id = scenario.Id,
                    Title = scenario.Title,
                    StartWhen = builder.Condition(scenario.StartWhen, $"{card} / StartWhen"),
                    EndWhen = builder.Condition(scenario.EndWhen, $"{card} / EndWhen"),
                    ContinuesAcrossActs = scenario.ContinuesAcrossActs,
                    OnStart = builder.Actions(scenario.OnStart, $"{card} / OnStart"),
                    OnEnd = builder.Actions(scenario.OnEnd, $"{card} / OnEnd"),
                    Outcomes = [.. scenario.Outcomes.Select(x =>
                    {
                        builder.CheckId(x.Name, outcomes, $"{card} / outcome {x.Name}", "outcome");
                        return new ScenarioOutcome { Name = x.Name, When = builder.Condition(x.When, $"{card} / outcome {x.Name} / When") };
                    })],
                    Dialogues = [.. scenario.Dialogues],
                    Attributes = builder.Attributes(scenario.Attributes, SagaCardKinds.Scenario, card),
                });
            }
            foreach (var quest in act.Quests)
            {
                var card = $"{actCard} / quest {quest.Id}";
                builder.CheckId(quest.Id, seenQuests, card, "quest");
                if (quest.ScenarioId.Length > 0 && !scenarioIds.Contains(quest.ScenarioId))
                    builder.Report($"{card} / ScenarioId", $"The story has no scenario '{quest.ScenarioId}'.");
                var stages = new HashSet<string>();
                definition.Quests.Add(new QuestDefinition
                {
                    Id = quest.Id,
                    Title = quest.Title,
                    ScenarioId = quest.ScenarioId.Length > 0 ? quest.ScenarioId : null,
                    OfferWhen = builder.Condition(quest.OfferWhen, $"{card} / OfferWhen"),
                    StartsAutomatically = quest.StartsAutomatically,
                    Stages = [.. quest.Stages.Select(x =>
                    {
                        var stageCard = $"{card} / stage {x.Id}";
                        builder.CheckId(x.Id, stages, stageCard, "stage");
                        return new QuestStage
                        {
                            Id = x.Id,
                            Title = x.Title,
                            DoneWhen = builder.Condition(x.DoneWhen, $"{stageCard} / DoneWhen"),
                            OnDone = builder.Actions(x.OnDone, $"{stageCard} / OnDone"),
                        };
                    })],
                    SucceedWhen = builder.Condition(quest.SucceedWhen, $"{card} / SucceedWhen"),
                    FailWhen = builder.Condition(quest.FailWhen, $"{card} / FailWhen"),
                    OnActivate = builder.Actions(quest.OnActivate, $"{card} / OnActivate"),
                    OnSucceed = builder.Actions(quest.OnSucceed, $"{card} / OnSucceed"),
                    OnFail = builder.Actions(quest.OnFail, $"{card} / OnFail"),
                    Dialogues = [.. quest.Dialogues],
                    Attributes = builder.Attributes(quest.Attributes, SagaCardKinds.Quest, card),
                });
            }
            plot.Acts.Add(definition);
        }

        if (story.Acts.Count == 0)
            builder.Report("Acts", "The story has no act.");
        else if (!actIds.Contains(story.FirstActId))
            builder.Report("FirstActId", story.FirstActId.Length == 0 ? "The story has no first act." : $"The story has no act '{story.FirstActId}'.");

        return new StoryDefinition { Name = NameOf(assetUrl), Declarations = story.Declarations, Plot = plot };
    }

    /// <summary>
    /// Builds a dialogue, checking it against the names its story declares.
    /// </summary>
    public static DialogueDefinition BuildDialogue(DialogueAsset dialogue, string assetUrl, StoryAsset story, ICollection<SagaBuildDiagnostic> diagnostics)
    {
        var builder = new Builder(assetUrl, story, diagnostics);
        var characters = story.Declarations.Characters.Select(x => x.Id).Append(PlayerId).ToHashSet();
        if (!characters.Contains(dialogue.CharacterId) || dialogue.CharacterId == PlayerId)
            builder.Report("CharacterId", dialogue.CharacterId.Length == 0 ? "The dialogue is attached to no character." : $"The story declares no character '{dialogue.CharacterId}'.");
        foreach (var participant in dialogue.Participants.Where(x => !characters.Contains(x)))
            builder.Report("Participants", $"The story declares no character '{participant}'.");

        var nodeIds = dialogue.Nodes.Select(x => x.Id).ToHashSet();
        if (dialogue.Nodes.Count == 0)
            builder.Report("Nodes", "The dialogue has no node.");
        else if (!nodeIds.Contains(dialogue.EntryNodeId))
            builder.Report("EntryNodeId", dialogue.EntryNodeId.Length == 0 ? "The dialogue has no entry node." : $"The dialogue has no node '{dialogue.EntryNodeId}'.");

        var seenNodes = new HashSet<string>();
        var nodes = new List<DialogueNode>();
        foreach (var node in dialogue.Nodes)
        {
            var card = $"node {node.Id}";
            if (node.Id.Length == 0)
                builder.Report(card, "A node has no id.");
            else if (!seenNodes.Add(node.Id))
                builder.Report(card, $"Two nodes have the id '{node.Id}'.");
            if (node.Name.Length > 0 && !IdentifierPattern().IsMatch(node.Name))
                builder.Report($"{card} / Name", $"'{node.Name}' is not a name: use letters, digits and '_'.");

            DialogueNode built;
            switch (node)
            {
                case LineNodeCard line:
                    if (line.SpeakerId.Length > 0 && !characters.Contains(line.SpeakerId))
                        builder.Report($"{card} / SpeakerId", $"The story declares no character '{line.SpeakerId}'.");
                    CheckTarget(line.NextId, $"{card} / NextId", optional: true);
                    built = new LineNode { SpeakerId = line.SpeakerId, Text = line.Text, Actions = builder.Actions(line.Actions, $"{card} / Actions"), NextId = NullIfEmpty(line.NextId) };
                    break;

                case ChoicesNodeCard choices:
                    if (choices.Choices.Count == 0)
                        builder.Report(card, "A choices node needs at least one choice.");
                    built = new ChoicesNode
                    {
                        Choices = [.. choices.Choices.Select((x, i) =>
                        {
                            var choiceCard = $"{card} / choice {i + 1}";
                            CheckTarget(x.TargetId, $"{choiceCard} / TargetId", optional: true);
                            return new DialogueChoice
                            {
                                Text = x.Text,
                                When = builder.Condition(x.When, $"{choiceCard} / When"),
                                HideWhenFalse = x.HideWhenFalse,
                                Once = x.Once,
                                Actions = builder.Actions(x.Actions, $"{choiceCard} / Actions"),
                                TargetId = NullIfEmpty(x.TargetId),
                            };
                        })],
                    };
                    break;

                case BranchNodeCard branch:
                    if (branch.Links.Count == 0)
                        builder.Report(card, "A branch needs at least one link.");
                    built = new BranchNode
                    {
                        Links = [.. branch.Links.Select((x, i) =>
                        {
                            var linkCard = $"{card} / link {i + 1}";
                            CheckTarget(x.TargetId, $"{linkCard} / TargetId", optional: false);
                            return new DialogueLink { When = builder.Condition(x.When, $"{linkCard} / When"), TargetId = x.TargetId };
                        })],
                    };
                    break;

                default:
                    built = new EndNode();
                    break;
            }
            built.Id = node.Id;
            built.Name = NullIfEmpty(node.Name);
            nodes.Add(built);
        }

        return new DialogueDefinition
        {
            Name = NameOf(assetUrl),
            CharacterId = dialogue.CharacterId,
            When = builder.Condition(dialogue.When, "When"),
            Priority = dialogue.Priority,
            Once = dialogue.Once,
            OnlyWhileOwnerLive = dialogue.OnlyWhileOwnerLive,
            Participants = [.. dialogue.Participants],
            EntryNodeId = dialogue.EntryNodeId,
            Nodes = nodes,
            Attributes = builder.Attributes(dialogue.Attributes, SagaCardKinds.Dialogue, "dialogue"),
        };

        void CheckTarget(string target, string card, bool optional)
        {
            if (target.Length == 0 ? !optional : !nodeIds.Contains(target))
                builder.Report(card, target.Length == 0 ? "The link leads nowhere." : $"The dialogue has no node '{target}'.");
        }
    }

    /// <summary>
    /// The name of the content an asset URL gives (its last segment): the story's name, the dialogue's name for visits.
    /// </summary>
    public static string NameOf(string assetUrl) => assetUrl[(assetUrl.LastIndexOf('/') + 1)..];

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex DottedIdentifierPattern();

    private sealed class Builder(string assetUrl, StoryAsset story, ICollection<SagaBuildDiagnostic> diagnostics)
    {
        private readonly SagaTypeChecker checker = new(new StoryAssetScope(story));

        public void Report(string card, string message, int position = 0) => diagnostics.Add(new SagaBuildDiagnostic(assetUrl, card, position, message));

        public SagaCondition Condition(string source, string card)
        {
            var parsed = SagaParser.ParseCondition(source);
            var found = parsed.Succeeded ? checker.CheckCondition(parsed.Value) : parsed.Diagnostics;
            foreach (var diagnostic in found)
                Report(card, diagnostic.Message, diagnostic.Position);
            return new SagaCondition { Source = source, Tree = found.Count == 0 ? parsed.Value : null };
        }

        public SagaActions Actions(string source, string card)
        {
            var parsed = SagaParser.ParseActions(source);
            var found = parsed.Succeeded ? checker.CheckActions(parsed.Value) : parsed.Diagnostics;
            foreach (var diagnostic in found)
                Report(card, diagnostic.Message, diagnostic.Position);
            return new SagaActions { Source = source, Statements = found.Count == 0 ? parsed.Value : [] };
        }

        public List<AttributeValue> Attributes(List<AttributeValue> attributes, SagaCardKinds kind, string card)
        {
            foreach (var attribute in attributes)
            {
                var attributeCard = $"{card} / attribute {attribute.Name}";
                var type = story.Declarations.AttributeTypes.FirstOrDefault(x => x.Name == attribute.Name);
                if (type is null)
                    Report(attributeCard, $"The story declares no attribute '{attribute.Name}'.");
                else if ((type.AppliesTo & kind) == 0)
                    Report(attributeCard, $"The attribute '{attribute.Name}' does not apply to this kind of card.");
                else if (SagaLiterals.TryParse(attribute.Value, type.Type, out _) is { } error)
                    Report(attributeCard, error);
            }
            return [.. attributes.Select(x => new AttributeValue { Name = x.Name, Value = x.Value })];
        }

        public void CheckId(string id, HashSet<string> seen, string card, string what)
        {
            if (id.Length == 0)
                Report(card, $"The {what} has no id.");
            else if (!IdentifierPattern().IsMatch(id))
                Report(card, $"'{id}' is not an id: use letters, digits and '_'.");
            else if (!seen.Add(id))
                Report(card, $"Two {what}s have the id '{id}'.");
        }

        public void CheckDeclarations(StoryDeclarations declarations)
        {
            var names = new HashSet<string>();
            foreach (var variable in declarations.Variables)
            {
                var card = $"declarations / variable {variable.Name}";
                if (!DottedIdentifierPattern().IsMatch(variable.Name))
                    Report(card, $"'{variable.Name}' is not a name: use letters, digits, '_' and '.'.");
                else if (!names.Add(variable.Name))
                    Report(card, $"Two variables are named '{variable.Name}'.");
                else if (variable.Name == "act" || variable.Name.StartsWith("quest.", StringComparison.Ordinal) || variable.Name.StartsWith("scenario.", StringComparison.Ordinal))
                    Report(card, $"'{variable.Name}' is taken by the story's own names (act, quest.*, scenario.*).");
                if (SagaLiterals.TryParse(variable.Default, variable.Type, out _) is { } error)
                    Report($"{card} / Default", error);
            }
            CheckNames(declarations.Events.Select(x => x.Name), "event");
            CheckNames(declarations.Functions.Select(x => x.Name), "function");
            CheckNames(declarations.Signals.Select(x => x.Name), "signal");
            CheckNames(declarations.Characters.Select(x => x.Id), "character");
            CheckNames(declarations.AttributeTypes.Select(x => x.Name), "attribute");
            if (declarations.Characters.Any(x => x.Id == PlayerId))
                Report($"declarations / character {PlayerId}", $"'{PlayerId}' is the player; dialogues use it without declaring it.");
            foreach (var function in declarations.Functions.Where(x => x.Name is SagaTypeChecker.RaisedFunction or SagaTypeChecker.VisitedFunction or SagaTypeChecker.VisitsFunction))
                Report($"declarations / function {function.Name}", $"'{function.Name}' is a built-in function.");
        }

        private void CheckNames(IEnumerable<string> names, string what)
        {
            var seen = new HashSet<string>();
            foreach (var name in names)
                CheckId(name, seen, $"declarations / {what} {name}", what);
        }
    }
}
