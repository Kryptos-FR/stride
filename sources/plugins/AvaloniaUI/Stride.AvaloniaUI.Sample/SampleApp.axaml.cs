// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Markup.Xaml;

namespace Stride.AvaloniaUI.Sample;

/// <summary>
/// The game's Avalonia application: its theme, colors and shared styles, in <c>SampleApp.axaml</c>.
/// </summary>
public sealed class SampleApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
