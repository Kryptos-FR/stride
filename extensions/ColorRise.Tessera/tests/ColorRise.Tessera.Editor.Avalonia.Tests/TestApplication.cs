using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using ColorRise.Tessera.Editor.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// The application of the view tests: Skia draws for real, so a test reads the pixels of a rendered frame, with the
/// theme and the styles of the editor.
/// </summary>
public sealed class TestApplication : Application
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://ColorRise.Tessera.Editor.Avalonia.Tests/"))
        {
            Source = new Uri("avares://Stride.Core.Presentation.Avalonia/Themes/EditorStyles.axaml"),
        });
    }
}
