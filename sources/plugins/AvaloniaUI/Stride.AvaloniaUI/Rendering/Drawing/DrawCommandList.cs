// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Avalonia.Platform;

namespace Stride.AvaloniaUI.Rendering.Drawing;

/// <summary>
/// Calls to a drawing context, in the order they were made.
/// </summary>
internal sealed class DrawCommandList
{
    private readonly List<DrawCommand> commands = [];

    public IReadOnlyList<DrawCommand> Commands => commands;

    public void Add(DrawCommand command) => commands.Add(command);

    public void Clear() => commands.Clear();

    /// <summary>
    /// Makes the calls again, in order, on another drawing context.
    /// </summary>
    public void Replay(IDrawingContextImpl target)
    {
        foreach (var command in commands)
            command.Replay(target);
    }
}
