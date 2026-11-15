using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Xunit;

namespace ColorRise.Saga.Tests.Runtime;

/// <summary>
/// Builds compiled stories for tests: conditions and actions are written as text (<see cref="C"/>, <see cref="A"/>), then
/// parsed and checked against the story when it is built.
/// </summary>
internal sealed class StoryBuilder
{
    private readonly StoryDefinition story = new() { Name = "Test" };

    public static SagaCondition C(string source) => new() { Source = source };

    public static SagaActions A(string source) => new() { Source = source };

    public StoryBuilder Variable(string name, SagaTypeKind type, string defaultValue = "")
    {
        story.Declarations.Variables.Add(new VariableDeclaration { Name = name, Type = type, Default = defaultValue });
        return this;
    }

    public StoryBuilder Events(params string[] names)
    {
        story.Declarations.Events.AddRange(names.Select(x => new EventDeclaration { Name = x }));
        return this;
    }

    public StoryBuilder Function(string name, SagaTypeKind returnType, params SagaTypeKind[] parameters)
    {
        story.Declarations.Functions.Add(new FunctionDeclaration { Name = name, ReturnType = returnType, Parameters = [.. parameters] });
        return this;
    }

    public StoryBuilder Signal(string name, params SagaTypeKind[] parameters)
    {
        story.Declarations.Signals.Add(new SignalDeclaration { Name = name, Parameters = [.. parameters] });
        return this;
    }

    public StoryBuilder Character(string id)
    {
        story.Declarations.Characters.Add(new CharacterDeclaration { Id = id, DisplayName = id });
        return this;
    }

    public StoryBuilder Act(ActDefinition act)
    {
        story.Plot.Acts.Add(act);
        if (story.Plot.FirstActId.Length == 0)
            story.Plot.FirstActId = act.Id;
        return this;
    }

    public StoryDefinition Build()
    {
        var checker = new SagaTypeChecker(SagaScope.FromStory(story));
        foreach (var act in story.Plot.Acts)
        {
            Compile(checker, act.EndWhen);
            act.Next.ForEach(x => Compile(checker, x.When));
            foreach (var scenario in act.Scenarios)
            {
                Compile(checker, scenario.StartWhen, scenario.EndWhen);
                Compile(checker, scenario.OnStart, scenario.OnEnd);
                scenario.Outcomes.ForEach(x => Compile(checker, x.When));
                scenario.Dialogues.ForEach(x => CompileDialogue(checker, x));
            }
            foreach (var quest in act.Quests)
            {
                Compile(checker, quest.OfferWhen, quest.SucceedWhen, quest.FailWhen);
                Compile(checker, quest.OnActivate, quest.OnSucceed, quest.OnFail);
                quest.Stages.ForEach(x => { Compile(checker, x.DoneWhen); Compile(checker, x.OnDone); });
                quest.Dialogues.ForEach(x => CompileDialogue(checker, x));
            }
        }
        return story;
    }

    private static void CompileDialogue(SagaTypeChecker checker, DialogueDefinition dialogue)
    {
        Compile(checker, dialogue.When);
        foreach (var node in dialogue.Nodes)
        {
            switch (node)
            {
                case LineNode line:
                    Compile(checker, line.Actions);
                    break;
                case ChoicesNode choices:
                    choices.Choices.ForEach(x => { Compile(checker, x.When); Compile(checker, x.Actions); });
                    break;
                case BranchNode branch:
                    branch.Links.ForEach(x => Compile(checker, x.When));
                    break;
            }
        }
    }

    private static void Compile(SagaTypeChecker checker, params SagaCondition[] conditions)
    {
        foreach (var condition in conditions)
        {
            var parsed = SagaParser.ParseCondition(condition.Source);
            Assert.True(parsed.Succeeded, $"{condition.Source}: {string.Join("; ", parsed.Diagnostics)}");
            var diagnostics = checker.CheckCondition(parsed.Value);
            Assert.True(diagnostics.Count == 0, $"{condition.Source}: {string.Join("; ", diagnostics)}");
            condition.Tree = parsed.Value;
        }
    }

    private static void Compile(SagaTypeChecker checker, params SagaActions[] actions)
    {
        foreach (var action in actions)
        {
            var parsed = SagaParser.ParseActions(action.Source);
            Assert.True(parsed.Succeeded, $"{action.Source}: {string.Join("; ", parsed.Diagnostics)}");
            var diagnostics = checker.CheckActions(parsed.Value);
            Assert.True(diagnostics.Count == 0, $"{action.Source}: {string.Join("; ", diagnostics)}");
            action.Statements = parsed.Value;
        }
    }
}
