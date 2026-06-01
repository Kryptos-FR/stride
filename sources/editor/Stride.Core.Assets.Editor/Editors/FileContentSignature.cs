// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;

namespace Stride.Core.Assets.Editor.Editors;

/// <summary>
/// An immutable signature of a file's on-disk state (existence, byte length and last-write time),
/// used to detect external changes without a file-system watcher.
/// </summary>
/// <param name="Exists">Whether the file existed when the signature was captured.</param>
/// <param name="Length">The file's byte length, or <c>0</c> when it did not exist.</param>
/// <param name="LastWriteTimeUtc">The file's last-write time in UTC, or <see langword="default"/> when it did not exist.</param>
public readonly record struct FileContentSignature(bool Exists, long Length, DateTime LastWriteTimeUtc)
{
    /// <summary>
    /// Captures the current signature of the file at the given path.
    /// </summary>
    /// <param name="path">The OS path of the file, or <c>null</c>.</param>
    /// <returns>
    /// A signature describing the file, or a signature whose <see cref="Exists"/> is <c>false</c>
    /// when <paramref name="path"/> is null/empty or the file does not exist.
    /// </returns>
    public static FileContentSignature Capture(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return new FileContentSignature(false, 0L, default);

        var info = new FileInfo(path);
        return new FileContentSignature(true, info.Length, info.LastWriteTimeUtc);
    }
}
