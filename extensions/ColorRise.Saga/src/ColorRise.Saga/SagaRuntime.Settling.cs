using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;

namespace ColorRise.Saga;

public sealed partial class SagaRuntime
{
    /// <summary>
    /// How many passes settling may take before the story is judged never to settle.
    /// </summary>
    public const int MaxSettlingPasses = 100;

    private readonly Dictionary<string, ActDefinition> acts = [];
    private readonly Dictionary<string, (ActDefinition Act, ScenarioDefinition Scenario)> scenarios = [];
    private readonly Dictionary<string, (ActDefinition Act, QuestDefinition Quest)> quests = [];
    private readonly List<string> changedCards = [];
    private bool settling;

    private void IndexPlot()
    {
        foreach (var act in Story.Plot.Acts)
        {
            acts.TryAdd(act.Id, act);
            foreach (var scenario in act.Scenarios)
                scenarios.TryAdd(scenario.Id, (act, scenario));
            foreach (var quest in act.Quests)
                quests.TryAdd(quest.Id, (act, quest));
        }
    }

    /// <summary>
    /// Re-evaluates the story until a pass changes nothing.
    /// </summary>
    /// <remarks>
    /// A pass starts the scenarios of the current act whose condition holds, moves the quests forward, ends the scenarios whose
    /// condition holds, then ends the act when its condition holds. Quests come before the end of scenarios so that a fact that
    /// both completes a quest and ends its scenario completes the quest.
    /// </remarks>
    private void Settle()
    {
        if (settling)
            return;
        settling = true;
        try
        {
            for (var pass = 1; ; pass++)
            {
                changedCards.Clear();
                RunPass();
                if (changedCards.Count == 0)
                    return;
                if (pass == MaxSettlingPasses)
                {
                    throw new SagaException(
                        $"The story doesn't settle: after {MaxSettlingPasses} passes, these cards still change: {string.Join(", ", changedCards.Distinct())}. "
                        + "Look for an action that undoes what a condition just did, or acts that lead to each other at once.");
                }
            }
        }
        finally
        {
            settling = false;
        }
    }

    private void RunPass()
    {
        if (State.CurrentActId is { } actId && acts.TryGetValue(actId, out var currentAct))
        {
            foreach (var scenario in currentAct.Scenarios)
            {
                if (State.Scenarios[scenario.Id].State == ScenarioState.Pending && Holds(scenario.StartWhen, Card(scenario), "start"))
                    StartScenario(scenario);
            }
        }

        foreach (var (act, quest) in quests.Values)
            AdvanceQuest(act, quest);

        foreach (var (_, scenario) in scenarios.Values)
        {
            if (State.Scenarios[scenario.Id].State == ScenarioState.Running && !scenario.EndWhen.IsEmpty && Holds(scenario.EndWhen, Card(scenario), "end"))
                EndScenario(scenario);
        }

        if (State.CurrentActId is { } endingId && acts.TryGetValue(endingId, out var ending) && !ending.EndWhen.IsEmpty && Holds(ending.EndWhen, Card(ending), "end"))
            EndAct(ending);
    }

    private void AdvanceQuest(ActDefinition act, QuestDefinition quest)
    {
        var progress = State.Quests[quest.Id];
        if (progress.State == QuestState.NotStarted && IsOwnerLive(act, quest) && Holds(quest.OfferWhen, Card(quest), "offer"))
            SetQuestState(quest.Id, QuestState.Available);
        if (progress.State == QuestState.Available && quest.StartsAutomatically)
            SetQuestState(quest.Id, QuestState.Active);
        if (progress.State != QuestState.Active)
            return;

        while (progress.State == QuestState.Active && progress.Stage < quest.Stages.Count)
        {
            var stage = quest.Stages[progress.Stage];
            if (stage.DoneWhen.IsEmpty || !Holds(stage.DoneWhen, Card(quest), $"stage {stage.Id}"))
                break;
            progress.Stage++;
            MarkChanged(Card(quest));
            Notify(SagaChangeKind.QuestStageDone, quest.Id);
            Run(stage.OnDone, Card(quest), $"stage {stage.Id} done");
        }
        if (progress.State != QuestState.Active)
            return;

        if (!quest.FailWhen.IsEmpty && Holds(quest.FailWhen, Card(quest), "fail"))
            SetQuestState(quest.Id, QuestState.Failed);
        else if (quest.SucceedWhen.IsEmpty ? quest.Stages.Count > 0 && progress.Stage >= quest.Stages.Count : Holds(quest.SucceedWhen, Card(quest), "succeed"))
            SetQuestState(quest.Id, QuestState.Succeeded);
    }

    /// <summary>
    /// Whether the owner of a quest is live: its scenario runs, or, for a quest of the act itself, its act is the current one.
    /// </summary>
    private bool IsOwnerLive(ActDefinition act, QuestDefinition quest) => quest.ScenarioId is { } scenarioId
        ? State.Scenarios.TryGetValue(scenarioId, out var scenario) && scenario.State == ScenarioState.Running
        : State.CurrentActId == act.Id;

    /// <summary>
    /// Moves a quest to a state, running the actions of that state.
    /// </summary>
    private void SetQuestState(string id, QuestState state)
    {
        var quest = quests[id].Quest;
        var progress = State.Quests[id];
        if (progress.State == state)
            return;
        progress.State = state;
        MarkChanged(Card(quest));
        switch (state)
        {
            case QuestState.NotStarted:
                progress.Stage = 0;
                break;
            case QuestState.Available:
                Notify(SagaChangeKind.QuestAvailable, id);
                break;
            case QuestState.Active:
                Notify(SagaChangeKind.QuestActivated, id);
                Run(quest.OnActivate, Card(quest), "activate");
                break;
            case QuestState.Succeeded:
                Notify(SagaChangeKind.QuestSucceeded, id);
                Run(quest.OnSucceed, Card(quest), "succeed");
                break;
            case QuestState.Failed:
                Notify(SagaChangeKind.QuestFailed, id);
                Run(quest.OnFail, Card(quest), "fail");
                break;
        }
    }

    private void SetQuestStage(string id, int stage)
    {
        var progress = State.Quests[id];
        stage = Math.Clamp(stage, 0, quests[id].Quest.Stages.Count);
        if (progress.Stage == stage)
            return;
        progress.Stage = stage;
        MarkChanged(Card(quests[id].Quest));
    }

    private void StartScenario(ScenarioDefinition scenario)
    {
        State.Scenarios[scenario.Id].State = ScenarioState.Running;
        MarkChanged(Card(scenario));
        Notify(SagaChangeKind.ScenarioStarted, scenario.Id);
        Run(scenario.OnStart, Card(scenario), "start");
    }

    /// <summary>
    /// Ends a scenario, keeping the first outcome that holds; its active quests fail and its available ones are withdrawn.
    /// </summary>
    private void EndScenario(ScenarioDefinition scenario)
    {
        var progress = State.Scenarios[scenario.Id];
        progress.State = ScenarioState.Done;
        progress.Outcome = scenario.Outcomes.FirstOrDefault(x => Holds(x.When, Card(scenario), $"outcome {x.Name}"))?.Name;
        MarkChanged(Card(scenario));
        Notify(SagaChangeKind.ScenarioEnded, scenario.Id);
        Run(scenario.OnEnd, Card(scenario), "end");
        foreach (var (_, quest) in quests.Values.Where(x => x.Quest.ScenarioId == scenario.Id))
            CloseQuestOfEndedOwner(quest);
    }

    /// <summary>
    /// Ends an act: its scenarios that don't continue end, its own quests close, and the first link that holds is followed.
    /// </summary>
    private void EndAct(ActDefinition act)
    {
        foreach (var scenario in act.Scenarios.Where(x => !x.ContinuesAcrossActs && State.Scenarios[x.Id].State == ScenarioState.Running))
            EndScenario(scenario);
        foreach (var quest in act.Quests.Where(x => x.ScenarioId is null))
            CloseQuestOfEndedOwner(quest);

        State.CompletedActs.Add(act.Id);
        State.CurrentActId = null;
        MarkChanged(Card(act));
        Notify(SagaChangeKind.ActEnded, act.Id);

        var next = act.Next.FirstOrDefault(x => acts.ContainsKey(x.TargetActId) && Holds(x.When, Card(act), $"link to {x.TargetActId}"));
        if (next is not null)
            EnterAct(next.TargetActId);
    }

    private void CloseQuestOfEndedOwner(QuestDefinition quest)
    {
        switch (State.Quests[quest.Id].State)
        {
            case QuestState.Active:
                SetQuestState(quest.Id, QuestState.Failed);
                break;
            case QuestState.Available:
                SetQuestState(quest.Id, QuestState.NotStarted);
                break;
        }
    }

    private void EnterAct(string id)
    {
        State.CurrentActId = id;
        MarkChanged(Card(acts[id]));
        Notify(SagaChangeKind.ActEntered, id);
    }

    /// <summary>
    /// The dialogues of a character in declaration order (act by act, scenarios then quests), each with whether its owner is live.
    /// </summary>
    private IEnumerable<(DialogueDefinition Dialogue, Func<bool> IsOwnerLive)> DialoguesOf(string characterId)
    {
        foreach (var act in Story.Plot.Acts)
        {
            foreach (var scenario in act.Scenarios)
            {
                foreach (var dialogue in scenario.Dialogues.Where(x => x.CharacterId == characterId))
                    yield return (dialogue, () => State.Scenarios[scenario.Id].State == ScenarioState.Running);
            }
            foreach (var quest in act.Quests)
            {
                foreach (var dialogue in quest.Dialogues.Where(x => x.CharacterId == characterId))
                    yield return (dialogue, () => State.Quests[quest.Id].State is QuestState.Available or QuestState.Active);
            }
        }
    }

    private void MarkChanged(string card) => changedCards.Add(card);

    private bool Holds(SagaCondition condition, string card, string what)
    {
        try
        {
            return condition.Evaluate(context);
        }
        catch (Exception exception) when (exception is not SagaException { NamesCard: true })
        {
            throw new SagaException($"The condition '{condition.Source}' of {card} ({what}) failed: {exception.Message}", exception) { NamesCard = true };
        }
    }

    private void Run(SagaActions actions, string card, string what)
    {
        try
        {
            actions.Execute(context);
        }
        catch (Exception exception) when (exception is not SagaException { NamesCard: true })
        {
            throw new SagaException($"The actions '{actions.Source}' of {card} ({what}) failed: {exception.Message}", exception) { NamesCard = true };
        }
    }

    private static string Card(ActDefinition act) => $"act {act.Id}";

    private static string Card(ScenarioDefinition scenario) => $"scenario {scenario.Id}";

    private static string Card(QuestDefinition quest) => $"quest {quest.Id}";
}
