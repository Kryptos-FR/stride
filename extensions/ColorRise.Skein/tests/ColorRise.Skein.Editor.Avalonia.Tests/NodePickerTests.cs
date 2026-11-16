using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Skein.Editor.Avalonia.Views;

namespace ColorRise.Skein.Editor.Avalonia.Tests;

/// <summary>
/// The box that picks a Yarn node sends a title to its property when the user has chosen, not at each key.
/// </summary>
public sealed class NodePickerTests
{
    [AvaloniaFact]
    public void The_picker_sends_the_node_on_enter_and_forgets_what_was_typed_on_escape()
    {
        var source = new Runner { StartNode = "Arrival" };
        var (window, picker) = Show(source);
        try
        {
            Assert.Equal("Arrival", picker.Text);

            // The list opens with the focus and shows every node, and typing filters it without changing the property
            picker.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.True(picker.IsDropDownOpen);
            Assert.Equal(["Arrival", "Lighthouse", "Pier"], Shown(picker));
            picker.Text = "";
            window.KeyTextInput("pi");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Arrival", source.StartNode);
            Assert.Equal(["Pier"], Shown(picker));

            // Enter takes the first node shown
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Pier", source.StartNode);
            Assert.Equal("Pier", picker.Text);

            // Escape shows the property again
            window.KeyTextInput("x");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Pierx", picker.Text);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Pier", picker.Text);
            Assert.Equal("Pier", source.StartNode);

            // A title that is not in the list is kept when the focus leaves: the node may come later
            window.KeyTextInput("s");
            Dispatcher.UIThread.RunJobs();
            ((StackPanel)window.Content!).Children[1].Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Piers", source.StartNode);

            // Coming back shows every node again
            picker.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.True(picker.IsDropDownOpen);
            Assert.Equal(["Arrival", "Lighthouse", "Pier"], Shown(picker));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_click_on_a_node_of_the_list_sends_it()
    {
        var source = new Runner { StartNode = "Arrival" };
        var (window, picker) = Show(source);
        try
        {
            picker.Focus();
            Dispatcher.UIThread.RunJobs();

            var item = List(picker).ContainerFromIndex(2)!;
            var place = item.TranslatePoint(new Point(8, 8), window)!.Value;
            window.MouseDown(place, MouseButton.Left);
            window.MouseUp(place, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Pier", picker.Text);
            Assert.Equal("Pier", source.StartNode);
            Assert.False(picker.IsDropDownOpen);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, YarnNodePicker Picker) Show(Runner source)
    {
        var picker = new YarnNodePicker { ItemsSource = new[] { "Arrival", "Lighthouse", "Pier" } };
        picker.Bind(YarnNodePicker.NodeProperty, new Binding(nameof(Runner.StartNode)) { Source = source });
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { picker, new Button { Content = "Elsewhere" } } } };
        window.Show();
        return (window, picker);
    }

    // The titles in the list of the picker
    private static IEnumerable<string> Shown(YarnNodePicker picker) => List(picker).Items.Cast<string>();

    private static ListBox List(YarnNodePicker picker)
    {
        var popup = picker.GetVisualDescendants().OfType<Popup>().Single();
        return popup.Child!.GetVisualDescendants().OfType<ListBox>().Single();
    }

    private sealed class Runner
    {
        public string StartNode { get; set; } = "";
    }
}
