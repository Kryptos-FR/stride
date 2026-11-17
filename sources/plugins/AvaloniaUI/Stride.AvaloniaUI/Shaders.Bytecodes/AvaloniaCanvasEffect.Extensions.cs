// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Shaders;

namespace Stride.AvaloniaUI.Rendering.Native;

/// <summary>
/// The effect of the native canvas, precompiled for the graphics API the plugin is built for.
/// </summary>
internal partial class AvaloniaCanvasEffect
{
    private static EffectBytecode? bytecode;

    /// <summary>
    /// Gets the bytecode of the effect, or <c>null</c> when the graphics API has none (yet).
    /// </summary>
    public static EffectBytecode? Bytecode => binaryBytecode.Length == 0 ? null : bytecode ??= EffectBytecode.FromBytesSafe(binaryBytecode);
}
