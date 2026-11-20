// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Platform;
using Stride.AvaloniaUI.Rendering.Fallback;

namespace Stride.AvaloniaUI.Rendering.Drawing;

/// <summary>
/// A call to a drawing context, recorded with its arguments.
/// </summary>
/// <remarks>
/// Each call has a sealed record of its own (<c>DrawCommands.cs</c>); the argument objects are kept as they were given.
/// </remarks>
internal abstract record DrawCommand
{
    /// <summary>
    /// Gets the kind of call, as counted by <see cref="FallbackChannel"/>.
    /// </summary>
    public abstract DrawCallKind Kind { get; }

    /// <summary>
    /// Makes the same call, with the same arguments, on another drawing context.
    /// </summary>
    public abstract void Replay(IDrawingContextImpl target);
}
