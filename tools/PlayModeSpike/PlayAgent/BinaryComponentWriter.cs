// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine;

namespace Stride.PlayMode.Spike;

/// <summary>
/// Writes a component with Stride's binary serializers, references to other entities and components as their ids, and
/// references to content as their URLs.
/// </summary>
/// <remarks>
/// Each component is its own chunk, so that a component the editor cannot read fails alone. Its own entity is written as
/// an id too, like any other entity.
/// </remarks>
internal sealed class BinaryComponentWriter
{
    // The selector of the asset cloner: content is written by reference ("Content"), identifiables can be ids.
    private readonly SerializerSelector selector = new(reuseReferences: true, externalIdentifiableAsGuid: true, "Default", "Content");
    private readonly MemoryStream buffer = new(4096);

    public ReadOnlySpan<byte> Write(EntityComponent component, Dictionary<Guid, IIdentifiable> identifiables)
    {
        buffer.SetLength(0);
        // The root itself must be written in full, not as an id.
        var wasExternal = identifiables.Remove(component.Id);
        try
        {
            var writer = new BinarySerializationWriter(buffer);
            writer.Context.SerializerSelector = selector;
            writer.Context.Set(MemberSerializer.ExternalIdentifiables, identifiables);
            writer.Context.Set(ContentSerializerContext.SerializeAttachedReferenceProperty, ContentSerializerContext.AttachedReferenceSerialization.AsSerializableVersion);
            writer.SerializeExtended(component, ArchiveMode.Serialize);
            writer.Flush();
        }
        finally
        {
            if (wasExternal)
                identifiables[component.Id] = component;
        }
        return buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
    }
}
