// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Stride.AvaloniaUI.Native.Tests;

/// <summary>
/// Checks that the native renderer is held to the golds of the whole-frame Skia renderer.
/// </summary>
public sealed class GoldTests
{
    [SkippableFact]
    public void NativeGoldsAreTheWholeFrameSkiaGolds()
    {
        var baselines = FindBaselines();
        Skip.If(baselines == null, "The repository's tests folder isn't found from the folder of the tests.");

        var skiaGolds = Path.Combine(baselines, "Stride.AvaloniaUI.Tests");
        var nativeGolds = Path.Combine(baselines, "Stride.AvaloniaUI.Native.Tests");
        var skiaPages = ListReferenceViewGolds(skiaGolds);
        var nativePages = ListReferenceViewGolds(nativeGolds);

        Assert.NotEmpty(skiaPages);
        Assert.Equal(skiaPages, nativePages);
        foreach (var page in nativePages)
        {
            var same = File.ReadAllBytes(Path.Combine(skiaGolds, page)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(nativeGolds, page)));
            Assert.True(same, $"{page} of the native renderer isn't the gold of the whole-frame Skia renderer.");
        }
    }

    // The gold images of the reference views under a folder, by their path in it
    private static string[] ListReferenceViewGolds(string folder) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, "ReferenceViewTests.*.png", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(folder, file))
            .Order(StringComparer.Ordinal)
            .ToArray()
        : [];

    // The baselines folder of the repository the tests run from, or null when the tests run elsewhere (on a device)
    private static string? FindBaselines()
        => TestsFolder.Find() is { } tests && Path.Combine(tests, "baselines") is var baselines && Directory.Exists(baselines) ? baselines : null;
}
