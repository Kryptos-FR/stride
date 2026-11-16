using ColorRise.Skein.Editor.Services;
using ColorRise.Skein.Editor.ViewModels;
using ColorRise.Skein.UI;
using Stride.Assets.Editor.EntityFactories;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Presentation.Services;
using Stride.Engine;

namespace ColorRise.Skein.Editor.EntityFactories;

/// <summary>
/// "Dialogue runner" of the scene editor's <i>Add entity</i>: an entity that plays a Yarn project, with the presenters of
/// <c>ColorRise.Skein.UI</c> when the game can use that package. In a game with one Yarn project, the runner plays it.
/// </summary>
[Display(10, "Dialogue runner", Category)]
public sealed class DialogueRunnerFactory : EntityFactory
{
    /// <summary>
    /// The category of the factory in the menu.
    /// </summary>
    public const string Category = "Dialogue";

    [ModuleInitializer]
    internal static void RegisterCategory()
    {
        // After the categories of Stride's own entities
        EntityFactoryCategory.RegisterCategory(5, Category);
    }

    /// <inheritdoc/>
    public override Task<Entity> CreateEntity(IEntityHierarchyItemViewModel parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        // The presenters are scripts of the UI package: a game that does not reference it could not load them
        var package = parent.Asset.AssetItem.Package;
        var withPresenters = package is null || PackageTypeScope.For(package).Contains(typeof(LinePresenter));
        var entity = Create(ComputeNewName(parent, "Dialogue runner"), withPresenters);

        if (parent.Asset.Session.AllAssets.OfType<YarnProjectViewModel>().Where(p => !p.IsDeleted).Take(2).ToList() is [var project])
        {
            entity.Get<DialogueRunnerComponent>()!.Project = ContentReferenceHelper.CreateReference<YarnProject>(project);
        }

        return Task.FromResult(entity);
    }

    /// <summary>
    /// Makes the entity: a runner that starts at the node <c>Start</c>, and when asked a line presenter, an options
    /// presenter and a line advancer that the runner shows its dialogue with.
    /// </summary>
    public static Entity Create(string name, bool withPresenters)
    {
        var runner = new DialogueRunnerComponent { StartNode = YarnProjectAnalysis.DefaultStartNode };
        var entity = new Entity { Name = name };
        entity.Components.Add(runner);
        if (withPresenters)
        {
            foreach (var presenter in (DialoguePresenter[])[new LinePresenter(), new OptionsPresenter(), new LineAdvancer()])
            {
                entity.Components.Add(presenter);
                runner.Presenters.Add(presenter);
            }
        }

        return entity;
    }
}
