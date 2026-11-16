using ColorRise.Skein.UI;
using Stride.Core;
using Stride.Engine;
using Stride.Input;

namespace ColorRise.Skein.Tests.Guide;

// The code of docs/guide-developers.md, compiled: GuideTests checks that each block of the guide is here, line for line.

public sealed class Innkeeper : SyncScript
{
    public DialogueRunnerComponent? Runner { get; set; }

    public override void Update()
    {
        // The task of StartDialogue completes when the dialogue is over
        if (Runner is { IsRunning: false } && Input.IsKeyPressed(Keys.T))
            _ = Runner.StartDialogue("Tam");
    }
}

public sealed class Opening : AsyncScript
{
    public DialogueRunnerComponent? Runner { get; set; }

    public override async Task Execute()
    {
        if (Runner is null)
            return;

        Runner.NodeStarted += node => Log.Info($"The story enters {node}");
        Runner.UnhandledCommand += command => Log.Warning($"The story asks for <<{command}>>");

        await Runner.StartDialogue("Opening");
        Log.Info("The opening is over");
    }
}

public sealed class CaptionPresenter : DialoguePresenter
{
    public float SecondsPerLetter { get; set; } = 0.05f;

    public override async Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        var text = line.TextWithoutCharacterName.Text;
        Show(line.CharacterName, text);

        // The line stays for a time that fits its length, unless the player asks for the next one
        var left = 1.5f + text.Length * SecondsPerLetter;
        while (left > 0 && !cancellation.NextLine.IsCancellationRequested)
        {
            left -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
            await Script.NextFrame();
        }
    }

    public override Task OnDialogueCompletedAsync()
    {
        Hide();
        return Task.CompletedTask;
    }

    private void Show(string? speaker, string text) => Log.Info(speaker is null ? text : $"{speaker}: {text}");

    private void Hide() => Log.Info("");
}

public sealed class FirstOptionPresenter : DialoguePresenter
{
    public override Task<int?> RunOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
        => Task.FromResult(options.FirstOrDefault(option => option.IsAvailable)?.Index);
}

public sealed class Shaker : StartupScript
{
    public LinePresenter? Lines { get; set; }

    public override void Start()
    {
        if (Lines is null)
            return;

        // "The ground moves. [shake/] Did you feel that?"
        Lines.MarkerReached += marker =>
        {
            if (marker.Name == "shake")
                Log.Info("The camera shakes");
        };
    }
}

public static class Purse
{
    public static int Coins { get; set; } = 10;

    [YarnFunction("coins", Description = "How many coins the player has: coins().")]
    public static int CoinCount() => Coins;

    [YarnCommand("pay", Description = "Takes coins from the player: <<pay 3>>.")]
    public static void Pay(int amount)
    {
        if (amount > Coins)
            throw new YarnCommandException($"pay {amount}", $"the player has {Coins} coins.");
        Coins -= amount;
    }
}

public sealed class Gate : StartupScript
{
    public bool IsOpen { get; private set; }

    // <<lower Portcullis>> runs on the entity named Portcullis, at once
    [YarnCommand("lower", Description = "Closes a gate: <<lower Portcullis>>.")]
    public void Lower() => IsOpen = false;

    // <<raise Portcullis 2>>: the dialogue waits until the gate is up
    [YarnCommand("raise", Description = "Opens a gate in so many seconds, and waits: <<raise Portcullis 2>>.")]
    public async Task Raise(float seconds = 1, CancellationToken cancellation = default)
    {
        while (seconds > 0 && !cancellation.IsCancellationRequested)
        {
            seconds -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
            await Script.NextFrame();
        }

        IsOpen = true;
    }
}

public sealed class Fanfare : StartupScript
{
    public override void Start()
    {
        var dialogues = Services.GetSafeServiceAs<DialogueSystem>();
        dialogues.Actions.AddCommandHandler("fanfare", (int trumpets) => Log.Info($"{trumpets} trumpets play"));
        dialogues.Actions.AddFunction("difficulty", () => 2);
    }

    public override void Cancel()
    {
        var dialogues = Services.GetSafeServiceAs<DialogueSystem>();
        dialogues.Actions.RemoveCommandHandler("fanfare");
        dialogues.Actions.RemoveFunction("difficulty");
    }
}

public sealed class LampLight : SyncScript
{
    private VariableStorage? variables;

    public YarnProject? Story { get; set; }

    public override void Start()
    {
        variables = Services.GetSafeServiceAs<DialogueSystem>().Variables;
        variables.VariableChanged += OnVariableChanged;

        // The storage learns a story when a runner of it starts: a script that may start first tells it
        if (Story is not null)
            variables.Declare(Story);

        // A variable that was never set reads the default its script declares
        if (variables.TryGetValue<bool>("$lamp_lit", out var lit))
            Entity.EnableAll(lit, applyOnChildren: true);
    }

    public override void Update()
    {
        if (variables is not null && Input.IsKeyPressed(Keys.F1))
            variables.SetValue("$lamp_lit", true);
    }

    public override void Cancel()
    {
        if (variables is not null)
            variables.VariableChanged -= OnVariableChanged;
    }

    private void OnVariableChanged(string name, object? value)
    {
        if (name == "$lamp_lit" && value is bool lit)
            Entity.EnableAll(lit, applyOnChildren: true);
    }
}

public sealed class SaveFile
{
    public Dictionary<string, float> Numbers { get; set; } = [];

    public Dictionary<string, string> Strings { get; set; } = [];

    public Dictionary<string, bool> Bools { get; set; } = [];

    public static SaveFile Capture(DialogueSystem dialogues)
    {
        var snapshot = dialogues.Variables.Save();
        return new SaveFile { Numbers = snapshot.Numbers, Strings = snapshot.Strings, Bools = snapshot.Bools };
    }

    public IReadOnlyList<string> Restore(DialogueSystem dialogues)
    {
        var snapshot = new VariableSnapshot();
        foreach (var (name, value) in Numbers)
            snapshot.Numbers[name] = value;
        foreach (var (name, value) in Strings)
            snapshot.Strings[name] = value;
        foreach (var (name, value) in Bools)
            snapshot.Bools[name] = value;

        // What the story no longer declares is dropped, and returned
        return dialogues.Variables.Load(snapshot);
    }
}

public sealed class LanguageKey : SyncScript
{
    public DialogueRunnerComponent? Runner { get; set; }

    public override void Update()
    {
        if (Runner?.Project is not { } project || !Input.IsKeyPressed(Keys.L))
            return;

        // The base language first, then the languages that have a strings file
        var dialogues = Services.GetSafeServiceAs<DialogueSystem>();
        var current = dialogues.Language.Length > 0 ? dialogues.Language : project.BaseLanguage;
        var next = project.Languages[(project.Languages.IndexOf(current) + 1) % project.Languages.Count];
        dialogues.Language = next;
    }
}

public sealed class Events : SyncScript
{
    public DialogueRunnerComponent? Runner { get; set; }

    public override void Update()
    {
        // The node group "Events" plays whenever one of its nodes has all its conditions true
        if (Runner is { IsRunning: false } && Runner.HasSalientContent("Events"))
            _ = Runner.StartDialogue("Events");
    }
}

public static class TrimmedGame
{
    public static void Register(DialogueSystem dialogues)
    {
        dialogues.Actions.RegisterType(typeof(Purse));
        dialogues.Actions.RegisterType(typeof(Gate));
    }
}
