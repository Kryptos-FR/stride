// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;

namespace Stride.AvaloniaUI.Native.Tests;

/// <summary>
/// Finds the tests folder of the repository the tests run from: its golds in <c>baselines</c>, and the renders CI uploads in
/// <c>local</c>.
/// </summary>
internal static class TestsFolder
{
    /// <summary>
    /// Gets the repository's tests folder, or <c>null</c> when the tests run elsewhere (on a device).
    /// </summary>
    public static string? Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Stride.slnx")))
            {
                var tests = Path.Combine(folder.FullName, "tests");
                return Directory.Exists(tests) ? tests : null;
            }
        }
        return null;
    }
}
