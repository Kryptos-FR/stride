// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.PlayMode.Protocol;

/// <summary>
/// A subscriber asks for the URLs of the game's built content: the game answers with a <see cref="PlayContentIndex"/> at its next
/// update.
/// </summary>
public sealed record PlayContentRequest : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.ContentRequest;

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer)
    {
    }
}

/// <summary>
/// The URLs of the game's built content, as its content database lists them: a live reference can be set to the content of one
/// of them, which the game loads. An asset that no root asset or scene of the game references is not built, so it is not listed.
/// </summary>
/// <param name="Urls">The URLs, sorted. The database also lists the parts the build made of an asset (shaders, the data of a
/// texture or a model); a reader keeps the URLs of its own assets.</param>
public sealed record PlayContentIndex(IReadOnlyList<string> Urls) : PlayMessage
{
    /// <inheritdoc/>
    public override PlayMessageType Type => PlayMessageType.ContentIndex;

    internal static PlayContentIndex Read(PlayReader reader) => new(reader.ReadList(x => x.ReadString() ?? string.Empty, 2));

    /// <inheritdoc/>
    protected override void WritePayload(PlayWriter writer) => writer.WriteList(Urls, static (w, x) => w.Write(x));

    /// <inheritdoc/>
    public bool Equals(PlayContentIndex? other) => other is not null && Urls.SequenceEqual(other.Urls);

    /// <inheritdoc/>
    public override int GetHashCode() => Urls.Count;
}
