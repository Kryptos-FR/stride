// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// The Avalonia thread of these tests, with Avalonia set up the way a game sets it up: the whole-frame Skia renderer, found among the registered assemblies.
/// </summary>
internal static class TestAvalonia
{
    /// <summary>
    /// Runs an action on the Avalonia thread once Avalonia is set up, and waits for it.
    /// </summary>
    public static void Run(Action action) => AvaloniaThread.Run(() =>
    {
        AvaloniaUISystem.EnsureAvalonia();
        action();
    });
}
