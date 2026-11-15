using ColorRise.Skein.Tests.Engine;
using ColorRise.Skein.UI;
using Stride.Core.Diagnostics;
using Stride.Engine;
using Stride.Input;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Events;
using Stride.UI.Panels;

namespace ColorRise.Skein.Tests.UI;

/// <summary>
/// The presenters of Skein's UI show a dialogue in the named elements of a page, and take the player's input.
/// </summary>
[Collection(GameCollection.Name)]
public sealed class PresenterTests
{
    private const string Greeting = """
        title: Start
        ---
        Odile: Hello there.
        Bye.
        ===
        """;

    private const string Choice = """
        title: Start
        ---
        <<declare $gold = 0>>
        -> Wait
            Odile: Then we wait.
        -> Pay <<if $gold > 5>>
            Odile: Thank you.
        -> Leave
            Odile: Go on, then.
        ===
        """;

    /// <summary>
    /// A page a game would make: a box with the speaker, the text and the mark, and a panel of buttons.
    /// </summary>
    private sealed class Page
    {
        public Page(int buttons = 0)
        {
            var box = new StackPanel { Name = DefaultDialogueLook.LineName };
            box.Children.Add(Speaker);
            box.Children.Add(Text);
            box.Children.Add(Mark);
            for (var i = 0; i < buttons; i++)
            {
                Options.Children.Add(new Button { Content = new TextBlock() });
            }

            var root = new Grid();
            root.Children.Add(box);
            root.Children.Add(Options);
            Component = new UIComponent { Page = new UIPage { RootElement = root } };
        }

        public UIComponent Component { get; }

        public TextBlock Speaker { get; } = new() { Name = DefaultDialogueLook.SpeakerName };

        public TextBlock Text { get; } = new() { Name = DefaultDialogueLook.TextName };

        public Border Mark { get; } = new() { Name = DefaultDialogueLook.ContinueName };

        public StackPanel Options { get; } = new() { Name = DefaultDialogueLook.OptionsName };

        public UIElement Box => Component.Page.RootElement.FindName(DefaultDialogueLook.LineName);

        public List<Button> Buttons => [.. Options.Children.OfType<Button>()];

        public string Label(int button) => ((TextBlock)Buttons[button].Content).Text;

        public static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    private static DialogueRunnerComponent Runner(GameHarness harness, string script, Page page, params DialoguePresenter[] presenters)
    {
        var runner = harness.AddRunner(script, "Runner", presenters);
        runner.Entity.Add(page.Component);
        harness.Step();
        return runner;
    }

    [Fact]
    public void A_line_is_typed_in_the_text_of_the_page_under_its_speaker()
    {
        using var harness = new GameHarness();
        var page = new Page();
        var runner = Runner(harness, Greeting, page, new LinePresenter { Speed = 10 });

        var dialogue = runner.StartDialogue("Start");
        harness.Step(1, 0);
        Assert.Equal("Odile", page.Speaker.Text);
        Assert.Equal("", page.Text.Text);
        Assert.Equal(Visibility.Visible, page.Box.Visibility);

        harness.Step(5, 0.1);
        Assert.Equal("Hello", page.Text.Text);
        Assert.Equal(Visibility.Hidden, page.Mark.Visibility);

        // All shown: the line waits for the player, and says so
        harness.Step(10, 0.1);
        Assert.Equal("Hello there.", page.Text.Text);
        Assert.Equal(Visibility.Visible, page.Mark.Visibility);
        harness.Step(100, 0.1);
        Assert.Equal("Hello there.", page.Text.Text);

        // A line without a speaker hides the speaker's name
        runner.RequestNextLine();
        harness.Step(3, 0.1);
        Assert.Equal(Visibility.Collapsed, page.Speaker.Visibility);
        Assert.StartsWith("B", page.Text.Text);

        runner.RequestNextLine();
        harness.Step(2);
        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.Equal(Visibility.Collapsed, page.Box.Visibility);
        Assert.Equal("", page.Text.Text);
        Assert.Empty(harness.Logged(LogMessageType.Error));
    }

    [Fact]
    public void The_advancer_finishes_the_typing_at_a_press_and_goes_on_at_the_next()
    {
        using var harness = new GameHarness();
        var page = new Page();
        var runner = Runner(harness, Greeting, page, new LinePresenter { Speed = 10 }, new LineAdvancer());

        var dialogue = runner.StartDialogue("Start");
        harness.Step(3, 0.1);
        Assert.NotEqual("Hello there.", page.Text.Text);

        harness.Press(Keys.Space);
        harness.Step(2);
        Assert.Equal("Hello there.", page.Text.Text);
        Assert.Equal(Visibility.Visible, page.Mark.Visibility);

        // A key held down is one press
        harness.Keyboard.SimulateDown(Keys.Space);
        harness.Step(1);
        harness.Step(5);
        harness.Keyboard.SimulateUp(Keys.Space);
        harness.Step(2, 0);
        Assert.Equal(Visibility.Collapsed, page.Speaker.Visibility);
        Assert.NotEqual("Bye.", page.Text.Text);

        harness.Press(Keys.Space);
        harness.Press(Keys.Space);
        harness.Step(2);
        Assert.True(dialogue.IsCompletedSuccessfully);
    }

    [Fact]
    public void A_line_all_shown_goes_on_at_the_first_press()
    {
        using var harness = new GameHarness();
        var page = new Page();
        var runner = Runner(harness, Greeting, page, new LinePresenter { Typewriter = TypewriterMode.Instant }, new LineAdvancer { AdvanceKey = Keys.Enter });

        var dialogue = runner.StartDialogue("Start");
        harness.Step(3);
        Assert.Equal("Hello there.", page.Text.Text);

        // Not its key
        harness.Press(Keys.Space);
        harness.Step(2);
        Assert.Equal("Hello there.", page.Text.Text);

        harness.Press(Keys.Enter);
        harness.Step(2);
        Assert.Equal("Bye.", page.Text.Text);

        harness.Press(Keys.Enter);
        harness.Step(2);
        Assert.True(dialogue.IsCompletedSuccessfully);
    }

    [Fact]
    public void A_line_goes_on_by_itself_after_its_hold_time_when_asked()
    {
        using var harness = new GameHarness();
        var page = new Page();
        var runner = Runner(harness, Greeting, page, new LinePresenter { Typewriter = TypewriterMode.Instant, AutoAdvance = true, HoldTime = 1 });

        var dialogue = runner.StartDialogue("Start");
        harness.Step(5, 0.1);
        Assert.Equal("Hello there.", page.Text.Text);
        Assert.Equal(Visibility.Hidden, page.Mark.Visibility);

        harness.Step(10, 0.1);
        Assert.Equal("Bye.", page.Text.Text);

        harness.Step(15, 0.1);
        Assert.True(dialogue.IsCompletedSuccessfully);
    }

    [Fact]
    public void The_markers_of_a_line_are_told_as_the_typing_passes_them()
    {
        using var harness = new GameHarness();
        var page = new Page();
        var presenter = new LinePresenter { Speed = 10 };
        var markers = new List<string>();
        presenter.MarkerReached += marker => markers.Add($"{marker.Name} at {page.Text.Text.Length}");
        var runner = Runner(harness, "title: Start\n---\nOdile: Hey [shake/]you [pause=500/]there.\n===\n", page, presenter);

        runner.StartDialogue("Start");
        harness.Step(3, 0.1);
        Assert.Empty(markers);

        harness.Step(30, 0.1);
        Assert.Equal("Hey you there.", page.Text.Text);
        Assert.Equal("shake", Assert.Single(markers).Split(' ')[0]);

        runner.Stop();
        harness.Step(2);
    }

    [Fact]
    public void A_line_presenter_with_nothing_to_show_in_says_so_once_and_holds_no_line()
    {
        using var harness = new GameHarness();
        var runner = harness.AddRunner(Greeting, "Runner", new LinePresenter());
        harness.Step();

        var dialogue = runner.StartDialogue("Start");
        harness.Step(2);

        Assert.True(dialogue.IsCompletedSuccessfully);
        var error = Assert.Single(harness.Logged(LogMessageType.Error));
        Assert.Contains("SkeinText", error);
    }

    [Fact]
    public void Options_take_the_buttons_of_the_page_and_a_click_chooses()
    {
        using var harness = new GameHarness();
        var page = new Page(buttons: 3);
        var runner = Runner(harness, Choice, page, new LinePresenter { Typewriter = TypewriterMode.Instant }, new OptionsPresenter());

        var dialogue = runner.StartDialogue("Start");
        harness.Step(2);

        // The option that cannot be chosen is not shown
        Assert.Equal(Visibility.Visible, page.Options.Visibility);
        Assert.Equal(["Wait", "Leave"], [page.Label(0), page.Label(1)]);
        Assert.Equal([Visibility.Visible, Visibility.Visible, Visibility.Collapsed], page.Buttons.Select(b => b.Visibility));

        // A button that shows no option chooses nothing
        Page.Click(page.Buttons[2]);
        harness.Step(2);
        Assert.Equal(Visibility.Visible, page.Options.Visibility);

        Page.Click(page.Buttons[1]);
        harness.Step(3);
        Assert.Equal(Visibility.Collapsed, page.Options.Visibility);
        Assert.Equal("Go on, then.", page.Text.Text);

        runner.RequestNextLine();
        harness.Step(2);
        Assert.True(dialogue.IsCompletedSuccessfully);
        Assert.Empty(harness.Logged(LogMessageType.Error));
    }

    [Fact]
    public void Options_are_chosen_with_the_keys_past_the_ones_that_cannot_be()
    {
        using var harness = new GameHarness();
        var page = new Page(buttons: 3);
        var runner = Runner(harness, Choice, page, new LinePresenter { Typewriter = TypewriterMode.Instant }, new OptionsPresenter { Unavailable = UnavailableOptions.Disabled });

        runner.StartDialogue("Start");
        harness.Step(2);
        Assert.Equal(["Wait", "Pay", "Leave"], [page.Label(0), page.Label(1), page.Label(2)]);
        Assert.Equal([true, false, true], page.Buttons.Select(b => b.IsEnabled));

        // A click on the option that cannot be chosen chooses nothing
        Page.Click(page.Buttons[1]);
        harness.Step(2);
        Assert.Equal(Visibility.Visible, page.Options.Visibility);

        // Down from the first goes past the second, and up from the first goes round to the last
        harness.Press(Keys.Down);
        harness.Press(Keys.Down);
        harness.Press(Keys.Up);
        harness.Press(Keys.Enter);
        harness.Step(3);

        Assert.Equal("Go on, then.", page.Text.Text);
        runner.Stop();
        harness.Step(2);
    }

    [Fact]
    public void Options_beyond_the_buttons_of_the_page_are_an_error_once()
    {
        using var harness = new GameHarness();
        var page = new Page(buttons: 1);
        var runner = Runner(harness, Choice, page, new LinePresenter { Typewriter = TypewriterMode.Instant }, new OptionsPresenter());

        runner.StartDialogue("Start");
        harness.Step(2);

        Assert.Equal("Wait", page.Label(0));
        var error = Assert.Single(harness.Logged(LogMessageType.Error));
        Assert.Contains("1 buttons for 2 options", error);

        harness.Press(Keys.Enter);
        harness.Step(3);
        Assert.Equal("Then we wait.", page.Text.Text);
        runner.Stop();
        harness.Step(2);
    }

    [Fact]
    public void A_stopped_dialogue_hides_its_options()
    {
        using var harness = new GameHarness();
        var page = new Page(buttons: 3);
        var runner = Runner(harness, Choice, page, new OptionsPresenter());

        var dialogue = runner.StartDialogue("Start");
        harness.Step(2);
        Assert.Equal(Visibility.Visible, page.Options.Visibility);

        runner.Stop();
        harness.Step(2);
        Assert.True(dialogue.IsCompleted);
        Assert.Equal(Visibility.Collapsed, page.Options.Visibility);
    }
}
