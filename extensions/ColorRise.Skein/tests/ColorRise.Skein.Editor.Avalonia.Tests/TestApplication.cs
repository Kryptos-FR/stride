using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using ColorRise.Skein.Editor.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]
// One application for the whole assembly, as in Stride's own view tests: a new one for each test resets the UI thread
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace ColorRise.Skein.Editor.Avalonia.Tests;

/// <summary>
/// The application the views are tested in, without a window: the styles Game Studio gives its views (the Fluent theme,
/// the theme of the text editor, Stride's editor styles, which have the look of the nodes of a graph).
/// </summary>
public sealed class TestApplication : Application
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    public override void Initialize()
    {
        var baseUri = new Uri("avares://ColorRise.Skein.Editor.Avalonia.Tests/");
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") });
        Styles.Add(new StyleInclude(baseUri) { Source = new Uri("avares://Stride.Core.Presentation.Avalonia/Themes/EditorStyles.axaml") });
    }
}
