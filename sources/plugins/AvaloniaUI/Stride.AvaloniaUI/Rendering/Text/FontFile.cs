// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Media;

namespace Stride.AvaloniaUI.Rendering.Text;

/// <summary>
/// The bytes of a font file, pinned for FreeType, which reads them for as long as a face opened from them lives.
/// </summary>
/// <remarks>
/// A typeface's file is read once and kept while the typeface lives, so the atlas of a device attached later doesn't read
/// it again; typefaces whose files have the same bytes, such as the faces of a collection, share one copy.
/// </remarks>
internal sealed class FontFile
{
    // The atlases of all devices share the files
    private static readonly Lock Gate = new();
    private static readonly ConditionalWeakTable<IPlatformTypeface, FontFile> FilesOfTypefaces = new();
    private static readonly Dictionary<int, List<WeakReference<FontFile>>> FilesByLength = [];

    private FontFile(byte[] data) => Data = data;

    /// <summary>
    /// Gets the bytes of the file, on the pinned object heap.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the file of a typeface, read the first time it is asked for.
    /// </summary>
    /// <returns><c>false</c> when the typeface gives no file.</returns>
    public static bool TryGet(IPlatformTypeface typeface, [NotNullWhen(true)] out FontFile? file)
    {
        lock (Gate)
        {
            if (FilesOfTypefaces.TryGetValue(typeface, out file))
                return true;
            if (!typeface.TryGetStream(out var stream))
                return false;
            byte[] data;
            using (stream)
                data = ReadPinned(stream);
            file = FindOrAdd(data);
            FilesOfTypefaces.Add(typeface, file);
            return true;
        }
    }

    // The file read before with the same bytes, else a new one; forgets the files no typeface or face keeps
    private static FontFile FindOrAdd(byte[] data)
    {
        if (!FilesByLength.TryGetValue(data.Length, out var files))
            FilesByLength.Add(data.Length, files = []);
        files.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in files)
        {
            if (reference.TryGetTarget(out var existing) && existing.Data.AsSpan().SequenceEqual(data))
                return existing;
        }

        var file = new FontFile(data);
        files.Add(new WeakReference<FontFile>(file));
        return file;
    }

    private static byte[] ReadPinned(Stream stream)
    {
        if (stream.CanSeek)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)(stream.Length - stream.Position)), pinned: true);
            stream.ReadExactly(bytes);
            return bytes;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var copy = GC.AllocateUninitializedArray<byte>((int)memory.Length, pinned: true);
        memory.GetBuffer().AsSpan(0, copy.Length).CopyTo(copy);
        return copy;
    }
}
