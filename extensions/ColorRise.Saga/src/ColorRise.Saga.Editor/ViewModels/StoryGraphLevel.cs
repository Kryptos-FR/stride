using ColorRise.Saga.Assets;

namespace ColorRise.Saga.Editor.ViewModels;

/// <summary>
/// How deep the story graph is opened.
/// </summary>
public enum StoryGraphDepth
{
    /// <summary>
    /// The plot: the acts and the links between them.
    /// </summary>
    Plot,

    /// <summary>
    /// An act: its scenarios and quests, each quest of a scenario linked from it.
    /// </summary>
    Act,

    /// <summary>
    /// A scenario: its dialogues.
    /// </summary>
    Scenario,

    /// <summary>
    /// A quest: its dialogues.
    /// </summary>
    Quest,
}

/// <summary>
/// A level of the story graph, as the breadcrumb shows it.
/// </summary>
/// <param name="Depth">How deep the level is.</param>
/// <param name="Act">The act of an act, scenario or quest level.</param>
/// <param name="Owner">The scenario or quest of a scenario or quest level, whose dialogues the level shows.</param>
/// <param name="Title">The title of the level in the breadcrumb.</param>
public sealed record StoryGraphLevel(StoryGraphDepth Depth, ActCard? Act, object? Owner, string Title)
{
    /// <summary>
    /// The id of the act, if the level is in one.
    /// </summary>
    public string? ActId => Act?.Id;

    /// <summary>
    /// The id of the scenario or quest of the level, if it is one.
    /// </summary>
    public string? OwnerId => Owner switch
    {
        ScenarioCard scenario => scenario.Id,
        QuestCard quest => quest.Id,
        _ => null,
    };
}
