using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using Stride.Core.Diagnostics;
using Stride.Core.Serialization;

namespace ColorRise.Saga.Editor.Player;

/// <summary>
/// A story played in Game Studio: built from its asset and the dialogues it references, as they are in memory, by the same
/// builder as the compiler (<see cref="SagaContentBuilder"/>), and run by a <see cref="SagaRuntime"/>.
/// </summary>
public sealed class SagaPlayback
{
    private SagaPlayback(SagaRuntime runtime)
    {
        Runtime = runtime;
    }

    /// <summary>
    /// The runtime that plays the story, not started.
    /// </summary>
    public SagaRuntime Runtime { get; }

    /// <summary>
    /// Builds a story to play.
    /// </summary>
    /// <param name="story">The story asset.</param>
    /// <param name="url">The URL of the story asset; its last segment names the story.</param>
    /// <param name="resolveDialogue">Gives the asset and the URL of a dialogue that a scenario or quest references, or null when
    /// the session doesn't have it.</param>
    /// <param name="diagnostics">Receives what is wrong with the story or its dialogues, errors and warnings, as the compiler would
    /// report it.</param>
    /// <param name="logger">Where the runtime reports the problems the story can live with.</param>
    /// <returns>The playback, or null when the story or one of its dialogues has an error.</returns>
    public static SagaPlayback? Create(StoryAsset story, string url, Func<DialogueDefinition, (DialogueAsset Asset, string Url)?> resolveDialogue,
        ICollection<SagaBuildDiagnostic> diagnostics, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(resolveDialogue);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var found = new List<SagaBuildDiagnostic>();
        var definition = SagaContentBuilder.BuildStory(story, url, found);
        // Each dialogue is built once, against the story, whatever references it
        var built = new Dictionary<string, DialogueDefinition>();
        void Resolve(List<DialogueDefinition> dialogues, string card)
        {
            for (var i = 0; i < dialogues.Count; ++i)
            {
                var reference = dialogues[i];
                if (resolveDialogue(reference) is not var (asset, dialogueUrl))
                {
                    var target = AttachedReferenceManager.GetAttachedReference(reference)?.Url ?? "?";
                    found.Add(new SagaBuildDiagnostic(url, $"{card} / Dialogues", 0, $"The dialogue '{target}' is not in the session."));
                    continue;
                }
                if (!built.TryGetValue(dialogueUrl, out var dialogue))
                    built[dialogueUrl] = dialogue = SagaContentBuilder.BuildDialogue(asset, dialogueUrl, story, found);
                dialogues[i] = dialogue;
            }
        }
        foreach (var act in definition.Plot.Acts)
        {
            foreach (var scenario in act.Scenarios)
                Resolve(scenario.Dialogues, $"act {act.Id} / scenario {scenario.Id}");
            foreach (var quest in act.Quests)
                Resolve(quest.Dialogues, $"act {act.Id} / quest {quest.Id}");
        }

        foreach (var diagnostic in found)
            diagnostics.Add(diagnostic);
        return found.Any(x => x.Severity == SagaDiagnosticSeverity.Error) ? null : new SagaPlayback(new SagaRuntime(definition, null, logger));
    }
}
