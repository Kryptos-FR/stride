using ColorRise.Saga;
using ColorRise.Saga.Content;
using Stride.Engine;

namespace SagaSandbox;

/// <summary>
/// Loads the story and plays a short scene through it, logging what happens: the cave-in, the foreman's dialogue, the guards'
/// talk and the rescue. Set SAGA_SANDBOX_EXIT=1 to quit once it is done (for headless runs).
/// </summary>
public sealed class StoryRunner : StartupScript
{
    public StoryDefinition? Story { get; set; }

    public override void Start()
    {
        if (Story is null)
        {
            Say("No story to play.");
            return;
        }

        var runtime = Services.GetService<SagaSystem>()!.Load(Story);
        runtime.Changed += (_, change) => Say($"[story] {change.Kind} {change.Id}");
        runtime.On("give_item", signal => Say($"[game] give {signal.Arguments[1]} x {signal.Arguments[0]}"));
        runtime.On("play_sound", signal => Say($"[game] play sound {signal.Arguments[0]}"));
        runtime.RegisterFunction("has_item", arguments => ColorRise.Saga.Expressions.SagaValue.Bool(arguments[0].AsString() == "lamp"));

        Say($"Act: {runtime.State.CurrentActId}");
        runtime.Raise("cave_in");

        Play(Character("Foreman"), "Who is trapped?", "I'll dig them out.");
        runtime.Raise("rubble_cleared");
        Play(Character("Captain"));
        Play(Character("Foreman"), "The guild will pay the diggers.");
        runtime.Raise("rescue_done");

        Say($"Act: {runtime.State.CurrentActId}, cave-in outcome: {runtime.State.Scenarios["cave_in"].Outcome}, reputation: {runtime.Get("reputation")}");

        if (Environment.GetEnvironmentVariable("SAGA_SANDBOX_EXIT") == "1")
            ((Stride.Games.GameBase)Game).Exit();
    }

    private SagaCharacterComponent Character(string entityName) =>
        SceneSystem.SceneInstance.RootScene.Entities.Single(x => x.Name == entityName).Get<SagaCharacterComponent>()!;

    /// <summary>
    /// Plays the character's dialogue, taking the given choices in order, or the last choice once they run out.
    /// </summary>
    private void Play(SagaCharacterComponent character, params string[] choices)
    {
        var session = character.Talk();
        if (session is null)
        {
            Say($"{character.CharacterId} has nothing to say.");
            return;
        }

        var next = 0;
        Say($"-- {session.Dialogue.Name}");
        while (!session.IsEnded)
        {
            if (session.Choices.Count == 0)
            {
                Say($"{session.CurrentLine!.SpeakerId}: {session.CurrentLine.Text}");
                session.Continue();
                continue;
            }

            foreach (var choice in session.Choices)
                Say($"   {(choice.IsEnabled ? "*" : "x")} {choice.Text}");
            var picked = session.Choices.FirstOrDefault(x => x.IsEnabled && next < choices.Length && x.Text == choices[next]) ?? session.Choices.Last(x => x.IsEnabled);
            next++;
            Say($"> {picked.Text}");
            session.Choose(picked.Index);
        }
    }

    private void Say(string text)
    {
        Log.Info(text);
        Console.WriteLine(text);
    }
}
