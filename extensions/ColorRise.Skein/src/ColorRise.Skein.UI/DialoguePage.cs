using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace ColorRise.Skein.UI;

/// <summary>
/// The UI page the presenters show in, and what they find or add in it.
/// </summary>
internal static class DialoguePage
{
    /// <summary>
    /// The UI of a presenter: the one it was given, else the one of its entity, made when the entity has none.
    /// </summary>
    public static UIComponent Of(Entity entity, UIComponent? given)
    {
        var ui = given ?? entity.Get<UIComponent>();
        if (ui is null)
        {
            ui = new UIComponent();
            entity.Add(ui);
        }

        return ui;
    }

    /// <summary>
    /// The element of a name in the page, when it is there and of the type.
    /// </summary>
    public static T? Find<T>(UIComponent ui, string name)
        where T : UIElement
        => ui.Page?.RootElement?.FindName(name) as T;

    /// <summary>
    /// A font of the page, for the elements a presenter adds to it.
    /// </summary>
    public static SpriteFont? FontOf(UIComponent ui) => ui.Page?.RootElement?.FindVisualChildrenOfType<TextBlock>().FirstOrDefault(t => t.Font is not null)?.Font;

    /// <summary>
    /// Adds an element over what the page shows.
    /// </summary>
    public static void Add(UIComponent ui, UIElement element)
    {
        ui.Page ??= new UIPage();
        switch (ui.Page.RootElement)
        {
            case null:
                var root = new Grid();
                root.Children.Add(element);
                ui.Page.RootElement = root;
                break;

            case Panel panel:
                panel.Children.Add(element);
                break;

            case var single:
                // One element that takes no children: it shares a grid with the new one
                ui.Page.RootElement = null;
                var shared = new Grid();
                shared.Children.Add(single);
                shared.Children.Add(element);
                ui.Page.RootElement = shared;
                break;
        }
    }
}
