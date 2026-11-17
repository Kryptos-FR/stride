using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using ColorRise.Skein.Editor.Conditions;

namespace ColorRise.Skein.Editor.Avalonia.Views;

/// <summary>
/// The box of a <c>when:</c> header in the property grid: the condition as text, and a button that opens the condition
/// builder. The text becomes the <see cref="Text"/> on Enter or when the focus leaves, or when the builder writes.
/// </summary>
public sealed class YarnConditionBox : UserControl
{
    /// <summary>
    /// The value of the header: what a property binds to.
    /// </summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<YarnConditionBox, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// What the builder offers.
    /// </summary>
    public static readonly StyledProperty<YarnConditionKnowledge?> KnowledgeProperty = AvaloniaProperty.Register<YarnConditionBox, YarnConditionKnowledge?>(nameof(Knowledge));

    /// <summary>
    /// Makes an empty box.
    /// </summary>
    public YarnConditionBox()
    {
        Box = new TextBox { VerticalAlignment = VerticalAlignment.Center };
        Box.LostFocus += (_, _) => Commit();
        Box.KeyDown += OnBoxKeyDown;
        Button = new Button { Content = "…", Padding = new Thickness(6, 0), Margin = new Thickness(3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Stretch };
        ToolTip.SetTip(Button, "Build the condition");
        Button.Click += OnButtonClick;
        DockPanel.SetDock(Button, Dock.Right);
        Content = new DockPanel { Children = { Button, Box } };
    }

    /// <summary>
    /// The value of the header.
    /// </summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// What the builder offers.
    /// </summary>
    public YarnConditionKnowledge? Knowledge
    {
        get => GetValue(KnowledgeProperty);
        set => SetValue(KnowledgeProperty, value);
    }

    /// <summary>
    /// The text box of the condition.
    /// </summary>
    public TextBox Box { get; }

    /// <summary>
    /// The button that opens the builder.
    /// </summary>
    public Button Button { get; }

    /// <summary>
    /// Opens the condition builder under the box.
    /// </summary>
    public YarnConditionBuilderViewModel OpenBuilder()
    {
        Commit();
        return YarnBuilderPopups.OpenHeader(this, Knowledge ?? YarnConditionKnowledge.Empty, Text, value => SetCurrentValue(TextProperty, value));
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Box.Text = Text ?? "";
        }
    }

    private void Commit()
    {
        if ((Box.Text ?? "") != (Text ?? ""))
        {
            SetCurrentValue(TextProperty, Box.Text ?? "");
        }
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Commit();
                e.Handled = true;
                break;
            case Key.Escape:
                Box.Text = Text ?? "";
                e.Handled = true;
                break;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e) => OpenBuilder();
}
