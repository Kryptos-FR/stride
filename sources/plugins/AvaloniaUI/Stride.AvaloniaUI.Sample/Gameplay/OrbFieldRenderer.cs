// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Stride.AvaloniaUI.Sample.Gameplay;

/// <summary>
/// Draws the orb field of the scene, in the game's graphics compositor. The Avalonia views are drawn over it.
/// </summary>
[DataContract("OrbFieldRenderer")]
[Display("Orb field")]
public sealed class OrbFieldRenderer : SceneRendererBase
{
    protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
        => context.Services.GetService<OrbField>()?.Draw(drawContext);
}
