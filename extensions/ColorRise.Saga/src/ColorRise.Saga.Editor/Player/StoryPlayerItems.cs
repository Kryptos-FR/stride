using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.ViewModels;

namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// A scenario or a quest as the story player shows it.
/// </summary>
/// <param name="Id">The id of the card.</param>
/// <param name="State">Its state, as the expressions write it (<c>running</c>, <c>available</c>).</param>
/// <param name="Detail">The outcome of a scenario or the stage of a quest, or empty.</param>
public sealed record StoryPlayerEntry(string Id, string State, string Detail);

/// <summary>
/// A variable the story declares, which the story player shows and lets the user set.
/// </summary>
public sealed class PlayerVariableViewModel : ViewModelBase
{
    private readonly SagaTypeKind type;
    private readonly Func<SagaValue, string?> apply;
    private string value;
    private string error = string.Empty;

    /// <param name="serviceProvider">The services of the session.</param>
    /// <param name="declaration">The declaration of the variable.</param>
    /// <param name="value">Its value now.</param>
    /// <param name="apply">Sets the variable in the story; returns why it couldn't, or null.</param>
    public PlayerVariableViewModel(IViewModelServiceProvider serviceProvider, VariableDeclaration declaration, SagaValue value, Func<SagaValue, string?> apply)
        : base(serviceProvider)
    {
        Name = declaration.Name;
        type = declaration.Type;
        this.value = SagaLiterals.Format(value);
        this.apply = apply;
    }

    public string Name { get; }

    public string Type => type.ToString();

    /// <summary>
    /// The value, as the expressions write it; setting a literal of the variable's type sets the variable in the story.
    /// </summary>
    public string Value
    {
        get => value;
        set
        {
            if (!SetValue(ref this.value, value))
                return;
            var message = SagaLiterals.TryParse(value, type, out var parsed);
            Error = message ?? apply(parsed) ?? string.Empty;
        }
    }

    /// <summary>
    /// Why the value could not be set, or empty.
    /// </summary>
    public string Error { get => error; private set => SetValue(ref error, value); }

    /// <summary>
    /// Shows the value the story has now, after the story changed it.
    /// </summary>
    internal void Update(SagaValue current)
    {
        if (Error.Length == 0)
            SetValue(ref value, SagaLiterals.Format(current), nameof(Value));
    }
}

/// <summary>
/// An event the story declares, which the user raises by hand in the story player.
/// </summary>
public sealed class PlayerEventViewModel : ViewModelBase
{
    private int count;

    public PlayerEventViewModel(IViewModelServiceProvider serviceProvider, string name, Action raise)
        : base(serviceProvider)
    {
        Name = name;
        RaiseCommand = new AnonymousCommand(serviceProvider, raise);
    }

    public string Name { get; }

    /// <summary>
    /// How many times the event was raised.
    /// </summary>
    public int Count { get => count; internal set => SetValue(ref count, value); }

    public ICommandBase RaiseCommand { get; }
}

/// <summary>
/// A character the story declares, whom the user talks to in the story player.
/// </summary>
public sealed class PlayerCharacterViewModel : ViewModelBase
{
    public PlayerCharacterViewModel(IViewModelServiceProvider serviceProvider, CharacterDeclaration declaration, Action talk)
        : base(serviceProvider)
    {
        Id = declaration.Id;
        DisplayName = declaration.DisplayName.Length > 0 ? declaration.DisplayName : declaration.Id;
        TalkCommand = new AnonymousCommand(serviceProvider, talk);
    }

    public string Id { get; }

    public string DisplayName { get; }

    public ICommandBase TalkCommand { get; }
}

/// <summary>
/// A choice the dialogue being played shows.
/// </summary>
public sealed class PlayerChoiceViewModel : ViewModelBase
{
    public PlayerChoiceViewModel(IViewModelServiceProvider serviceProvider, string text, bool isEnabled, Action choose)
        : base(serviceProvider)
    {
        Text = text;
        IsEnabled = isEnabled;
        ChooseCommand = new AnonymousCommand(serviceProvider, choose) { IsEnabled = isEnabled };
    }

    public string Text { get; }

    /// <summary>
    /// Whether the choice can be taken; a disabled choice is shown greyed out.
    /// </summary>
    public bool IsEnabled { get; }

    public ICommandBase ChooseCommand { get; }
}
