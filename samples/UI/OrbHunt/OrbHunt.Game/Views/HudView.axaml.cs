// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;

namespace OrbHunt.Views;

/// <summary>
/// The HUD of a round: score, time left, lives and the pause button, and the orbs in the planet scene.
/// </summary>
public sealed partial class HudView : UserControl
{
    public HudView() => InitializeComponent();
}
