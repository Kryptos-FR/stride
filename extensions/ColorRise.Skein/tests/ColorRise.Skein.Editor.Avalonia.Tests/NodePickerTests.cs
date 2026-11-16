using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
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
        var picker = new YarnNodePicker { ItemsSource = new[] { "Arrival", "Lighthouse", "Pier" } };
        picker.Bind(AutoCompleteBox.TextProperty, new Binding(nameof(Runner.StartNode)) { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.Explicit });
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { picker, new Button { Content = "Elsewhere" } } } };
        window.Show();
        try
        {
            Assert.Equal("Arrival", picker.Text);

            // The list opens with the focus, and typing filters it without changing the property
            picker.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.True(picker.IsDropDownOpen);
            picker.Text = "";
            window.KeyTextInput("pi");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Arrival", source.StartNode);

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
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Runner
    {
        public string StartNode { get; set; } = "";
    }
}
