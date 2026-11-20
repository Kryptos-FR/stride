// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace OrbHunt.Gameplay;

/// <summary>
/// Draws the orb field of the scene (<see cref="OrbField"/>), in the game's graphics compositor, after the view of the
/// camera. A scene without a field, such as the planet scene, draws nothing here. The Avalonia views are drawn over it.
/// </summary>
[DataContract("OrbFieldRenderer")]
[Display("Orb field")]
public sealed class OrbFieldRenderer : SceneRendererBase
{
    protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
        => context.Services.GetService<OrbField>()?.Draw(drawContext);
}
