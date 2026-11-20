// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Stride.AvaloniaUI;

/// <summary>
/// The Avalonia application of a game that doesn't set one up: the Fluent theme, dark.
/// </summary>
/// <remarks>
/// A game that wants its own application (resources, styles, theme) sets it up before the game runs, on the game thread:
/// <code>AppBuilder.Configure&lt;MyApp&gt;().UseStride().WithInterFont().SetupWithoutStarting();</code>
/// </remarks>
public class StrideAvaloniaApplication : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }
}
