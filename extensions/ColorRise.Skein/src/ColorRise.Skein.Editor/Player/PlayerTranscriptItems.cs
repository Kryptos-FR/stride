using System.Globalization;
using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Editor.Services;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Skein.Editor.Player;

/// <summary>
/// A step of the run as the transcript of the dialogue player shows it.
/// </summary>
/// <param name="Step">The index of the step in the run.</param>
/// <param name="Kind">What the step is.</param>
/// <param name="Speaker">Who says the line, or null.</param>
/// <param name="Text">The line without its speaker, the title of the node, the command between its brackets, the choice, the answer, the variable, what was picked, the failure.</param>
/// <param name="Changed">Whether the last replay made the step or made it differ.</param>
/// <param name="Candidates">The candidates of a pick, with why each was or was not chosen.</param>
/// <param name="SpeakerColor">The colour of the speaker, or null without one.</param>
public sealed record PlayerTranscriptItem(int Step, PlayerStepKind Kind, string? Speaker, string Text, bool Changed, IReadOnlyList<PlayerCandidate> Candidates, Color3? SpeakerColor = null)
{
    /// <summary>
    /// The transcript's item for a step, or null for a step the transcript does not show: the start, which the first
    /// node tells, and the options, which show as buttons while the run waits at them.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="index">The index of the step in the run.</param>
    /// <param name="characters">The colours of the characters; none when null.</param>
    public static PlayerTranscriptItem? Of(PlayerStep step, int index, YarnCharacterColors? characters = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        var text = step.Kind switch
        {
            PlayerStepKind.Start or PlayerStepKind.Options => null,
            PlayerStepKind.Line => step.Body,
            PlayerStepKind.Command => $"<<{step.Text}>>",
            PlayerStepKind.Choose => $"> {step.Text}",
            PlayerStepKind.End => "The dialogue is over.",
            _ => step.Text,
        };
        return text is null ? null : new PlayerTranscriptItem(index, step.Kind, step.Speaker, text, step.Changed, step.Candidates, step.Speaker is { } speaker ? characters?.Of(speaker) : null);
    }
}

/// <summary>
/// The values the dialogue player takes as text: a boolean, a number or a text, as a script writes them.
/// </summary>
internal static class PlayerValues
{
    /// <summary>
    /// A value as the panel shows it in a box: a text without its quotes.
    /// </summary>
    public static string Show(object? value) => value is string text ? text : PlayerRun.Format(value);

    /// <summary>
    /// Reads a text as a value of a Yarn type; null when it is not one.
    /// </summary>
    public static object? Parse(string text, string type)
    {
        var trimmed = text.Trim();
        switch (type)
        {
            case YarnFunctionDeclaration.Bool:
                return trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ? true : trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ? false : null;
            case YarnFunctionDeclaration.Number:
                return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
            default:
                // Quotes are welcome, and not needed
                return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"' ? trimmed[1..^1] : text;
        }
    }

    /// <summary>
    /// What a value of a Yarn type is, for a message: "a number", "true or false", "a text".
    /// </summary>
    public static string Describe(string type) => type switch
    {
        YarnFunctionDeclaration.Bool => "true or false",
        YarnFunctionDeclaration.Number => "a number",
        _ => "a text",
    };

    /// <summary>
    /// Runs an input of the player that plays with no wait; gives why the player refused it, or null.
    /// </summary>
    public static string? Try(Func<Task> input)
    {
        try
        {
            var playing = input();
            if (playing.IsCompleted)
                playing.GetAwaiter().GetResult();
            return null;
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }
}

/// <summary>
/// An option the run waits at, in the dialogue player.
/// </summary>
public sealed class PlayerOptionViewModel : ViewModelBase
{
    /// <param name="serviceProvider">The services of the session.</param>
    /// <param name="option">The option.</param>
    /// <param name="choose">Chooses the option.</param>
    public PlayerOptionViewModel(IViewModelServiceProvider serviceProvider, PlayerOption option, Func<Task> choose)
        : base(serviceProvider)
    {
        Text = option.Text;
        IsAvailable = option.IsAvailable;
        ChooseCommand = new AnonymousTaskCommand(serviceProvider, choose) { IsEnabled = option.IsAvailable };
    }

    /// <summary>
    /// The text of the option.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// False for an option whose condition fails: it shows greyed.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Chooses the option.
    /// </summary>
    public ICommandBase ChooseCommand { get; }
}

/// <summary>
/// A variable of the scripts in the dialogue player: its value at the cursor, which the user can set.
/// </summary>
public sealed class PlayerVariableViewModel : ViewModelBase
{
    private readonly Func<object, string?> apply;
    private readonly YarnVariableData variable;
    private string value;
    private string error = "";

    /// <param name="serviceProvider">The services of the session.</param>
    /// <param name="variable">The declaration of the variable.</param>
    /// <param name="expression">The expression of a smart variable.</param>
    /// <param name="apply">Sets the variable in the run; gives why it could not, or null.</param>
    public PlayerVariableViewModel(IViewModelServiceProvider serviceProvider, YarnVariableData variable, string? expression, Func<object, string?> apply)
        : base(serviceProvider)
    {
        this.apply = apply;
        this.variable = variable;
        Name = variable.Name;
        Type = variable.Type;
        Description = variable.Description ?? "";
        Cases = string.Join(", ", variable.Cases.Select(c => c.Name));
        IsSmart = variable.IsSmart;
        value = variable.IsSmart ? expression ?? "" : Shown(variable.Default);
    }

    /// <summary>
    /// The name of the variable, with its <c>$</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The type of the variable: <c>bool</c>, <c>number</c>, <c>string</c> or the name of an enum of the scripts.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// The names of the cases of the variable's enum, with commas; empty for the other types.
    /// </summary>
    public string Cases { get; }

    /// <summary>
    /// What the scripts say of the variable above its declaration, or empty.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Whether the value comes from an expression: it shows the expression and cannot be set.
    /// </summary>
    public bool IsSmart { get; }

    /// <summary>
    /// The value at the cursor, as a script writes it (a text without its quotes, a case of an enum by its name); the
    /// expression of a smart variable. Setting a value of the variable's type sets the variable in the run, as a step.
    /// </summary>
    public string Value
    {
        get => value;
        set
        {
            if (IsSmart || !SetValue(ref this.value, value))
                return;

            Error = Parsed(value) is { } parsed ? apply(parsed) ?? "" : $"{Name} is {PlayerValues.Describe(Type)}.";
        }
    }

    /// <summary>
    /// Why the text typed is not the value of the variable, or empty.
    /// </summary>
    public string Error { get => error; private set => SetValue(ref error, value); }

    /// <summary>
    /// Shows the value the run has at its cursor.
    /// </summary>
    internal void Show(object? current)
    {
        if (IsSmart)
            return;

        error = "";
        OnPropertyChanged(nameof(Error));
        SetValue(ref value, Shown(current), nameof(Value));
    }

    // A case of an enum shows by its name
    private string Shown(object? current) => variable.CaseOf(current)?.Name ?? PlayerValues.Show(current);

    // A case of an enum is typed by its name or by its value: the run says which cases there are when it is neither
    private object? Parsed(string text)
    {
        if (variable.Cases.Count == 0)
            return PlayerValues.Parse(text, Type);

        return variable.CaseNamed(text)?.Value ?? PlayerValues.Parse(text, YarnFunctionDeclaration.Number) ?? text;
    }
}
