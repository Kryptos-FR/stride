using ColorRise.Saga.Editor.Player;
using ColorRise.Saga.Editor.ViewModels;
using Stride.Assets.Editor.Commands;
using Stride.Assets.Editor.ViewModels;
using Stride.Assets.Presentation.ViewModels;
using Stride.Core.Assets.Editor.Commands;
using Stride.Core.Assets.Editor.Panels;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.Windows;

namespace ColorRise.Saga.Editor;

/// <summary>
/// Saga's commands: play a story from the asset view, open a character's dialogue from the entity tree. Both are in the command
/// palette.
/// </summary>
public static class SagaCommands
{
    /// <summary>
    /// Shows the story player with the selected story, started.
    /// </summary>
    public const string PlayStory = "ColorRise.Saga.PlayStory";

    /// <summary>
    /// Opens the editor of the dialogue of the selected entity's character: its highest-priority one, or the one the user picks
    /// when the character has several.
    /// </summary>
    public const string OpenDialogue = "ColorRise.Saga.OpenDialogue";

    /// <summary>
    /// The category of Saga's commands in the palette and the shortcut settings.
    /// </summary>
    public const string Category = "Saga";

    /// <summary>
    /// Creates the descriptors of the commands.
    /// </summary>
    public static IEnumerable<CommandDescriptor> Create()
    {
        yield return new CommandDescriptor
        {
            Id = PlayStory,
            Title = () => "Play story",
            Category = Category,
            Keywords = ["saga", "story", "player"],
            Placements = [new CommandPlacement(CommandLocations.AssetViewContext, Category)],
            CanExecute = context => StoryOf(context) is not null,
            ExecuteAsync = PlayAsync,
        };
        yield return new CommandDescriptor
        {
            Id = OpenDialogue,
            Title = () => "Open dialogue",
            Category = Category,
            Keywords = ["saga", "dialogue", "character"],
            Placements = [new CommandPlacement(SceneLocations.EntityTreeContext, Category)],
            CanExecute = context => CharacterOf(context) is not null,
            ExecuteAsync = OpenDialogueAsync,
        };
    }

    /// <summary>
    /// The dialogues of the session attached to a character, the highest priority first, then by URL.
    /// </summary>
    public static IReadOnlyList<DialogueViewModel> DialoguesOf(ISessionViewModel session, string characterId)
        => [.. session.AllAssets.OfType<DialogueViewModel>().Where(x => x.Asset.CharacterId == characterId)
            .OrderByDescending(x => x.Asset.Priority).ThenBy(x => x.Url.ToString(), StringComparer.Ordinal)];

    private static StoryViewModel? StoryOf(CommandContext context) => context.Selection is [StoryViewModel story] ? story : null;

    private static string? CharacterOf(CommandContext context)
        => EntityOf(context)?.Components.OfType<SagaCharacterComponent>().FirstOrDefault() is { CharacterId.Length: > 0 } character
            ? character.CharacterId
            : null;

    // The entity tree gives its selection. The palette gives the selection of the asset view, and the editor it was opened from,
    // whose selection is then the entity
    private static EntityViewModel? EntityOf(CommandContext context)
        => context.Selection is [EntityViewModel entity] ? entity
            : context.Editor is EntityHierarchyEditorViewModel { SelectedContent.Count: 1 } editor ? editor.SelectedContent.First() as EntityViewModel
            : null;

    private static Task PlayAsync(CommandContext context)
    {
        // An editor that shows no panels has no player
        if (StoryOf(context) is not { } story || context.Session.ServiceProvider.TryGet<IPanelService>() is not { } panels)
            return Task.CompletedTask;
        panels.Show(StoryPlayerViewModel.PanelId);
        if (panels.GetPanel(StoryPlayerViewModel.PanelId) is StoryPlayerViewModel player)
            player.Play(story);
        return Task.CompletedTask;
    }

    private static async Task OpenDialogueAsync(CommandContext context)
    {
        if (CharacterOf(context) is not { } characterId)
            return;
        var services = context.Session.ServiceProvider;
        var dialogues = DialoguesOf(context.Session, characterId);
        var dialogue = dialogues.Count == 1 ? dialogues[0] : null;
        if (dialogues.Count == 0)
        {
            await services.Get<IDialogService>().MessageBoxAsync($"No dialogue of the session is attached to the character '{characterId}'.", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (dialogues.Count > 1)
        {
            // A button per dialogue, the highest priority first and by default
            var buttons = dialogues.Select((x, i) => new DialogButtonInfo { Content = x.Name, Result = i + 1, IsDefault = i == 0 })
                .Append(new DialogButtonInfo { Content = "Cancel", Result = 0, IsCancel = true }).ToList();
            var picked = await services.Get<IDialogService>().MessageBoxAsync($"The character '{characterId}' has several dialogues. Which one to open?", buttons, MessageBoxImage.Question);
            dialogue = picked > 0 && picked <= dialogues.Count ? dialogues[picked - 1] : null;
        }
        if (dialogue is not null && services.TryGet<IAssetEditorsManager>() is { } editors)
            await editors.OpenAssetEditorAsync(dialogue);
    }
}
