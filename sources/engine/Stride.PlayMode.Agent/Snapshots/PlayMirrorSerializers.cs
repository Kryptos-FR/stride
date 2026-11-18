// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Assets;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;
using Stride.Engine;

namespace Stride.PlayMode.Snapshots;

/// <summary>
/// The serializer of an <see cref="Entity"/> in the "PlayMirror" profile: an entity is never part of a component's snapshot,
/// so it is written as a reference (<see cref="PlayMirrorReference"/>).
/// </summary>
public sealed class PlayMirrorEntitySerializer : DataSerializer<Entity>
{
    /// <inheritdoc/>
    public override void PreSerialize(ref Entity obj, ArchiveMode mode, SerializationStream stream)
    {
        var context = PlayMirrorContext.Of(stream);
        if (mode == ArchiveMode.Serialize)
            stream.Write(context.AddReference(obj));
        else
            obj = (context.GetReference(stream.ReadInt32()) as Entity)!;
    }

    /// <inheritdoc/>
    public override void Serialize(ref Entity obj, ArchiveMode mode, SerializationStream stream)
    {
        // Everything was in PreSerialize
    }
}

/// <summary>
/// The serializer of a component type in the "PlayMirror" profile: the snapshot's own component is written in full with its
/// default serializer; any other component (of its entity or of another) is written as a reference.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
public sealed class PlayMirrorComponentSerializer<T> : DataSerializer<T> where T : EntityComponent
{
    /// <inheritdoc/>
    public override void PreSerialize(ref T obj, ArchiveMode mode, SerializationStream stream)
    {
        var context = PlayMirrorContext.Of(stream);
        if (mode == ArchiveMode.Serialize)
        {
            var isRoot = !context.RootStarted && ReferenceEquals(obj, context.Root);
            stream.Write(isRoot);
            if (!isRoot)
            {
                stream.Write(context.AddReference(obj));
                context.SkipNextSerialize = true;
                return;
            }
        }
        else
        {
            if (!stream.ReadBoolean())
            {
                obj = (context.GetReference(stream.ReadInt32()) as T)!;
                context.SkipNextSerialize = true;
                return;
            }
            // Read into the instance the reader prepared (the mirror's component), if it has the type that was written
            if (!context.RootStarted && context.Root is T prepared)
                obj = prepared;
        }
        context.RootStarted = true;
        context.Inner.GetSerializer<T>()!.PreSerialize(ref obj, mode, stream);
        context.Root = obj;
    }

    /// <inheritdoc/>
    public override void Serialize(ref T obj, ArchiveMode mode, SerializationStream stream)
    {
        var context = PlayMirrorContext.Of(stream);
        if (context.SkipNextSerialize)
        {
            context.SkipNextSerialize = false;
            return;
        }
        // The root, or a base class's part of it when its serializer serializes its parent type
        context.Inner.GetSerializer<T>()!.Serialize(ref obj, mode, stream);
    }
}

/// <summary>
/// The serializer of a content type (a model, a material, a texture...) in the "PlayMirror" profile: content is written by its
/// URL, as the game's content manager knows it, and content the game made at run time, which has no URL, is written too, so
/// that it does not fail its component's snapshot.
/// </summary>
/// <typeparam name="T">The content type.</typeparam>
/// <remarks>
/// The reader gets a proxy with the URL and the id the game knows; content made at run time is a proxy with neither (or
/// <c>null</c> when its type cannot be made empty). A reader that loads content (the agent reading the value of an edit) gets the
/// content of the URL instead.
/// </remarks>
public sealed class PlayMirrorContentSerializer<T> : DataSerializer<T> where T : class
{
    /// <inheritdoc/>
    public override void Serialize(ref T obj, ArchiveMode mode, SerializationStream stream)
    {
        if (mode == ArchiveMode.Serialize)
        {
            var reference = AttachedReferenceManager.GetAttachedReference(obj);
            stream.Write(PlayMirrorSerializer.TypeNameOf(obj.GetType()));
            stream.Write(reference?.Id ?? AssetId.Empty);
            stream.Write(reference?.Url ?? string.Empty);
        }
        else
        {
            var typeName = stream.ReadString();
            var id = stream.Read<AssetId>();
            var url = stream.ReadString();
            var type = AssemblyRegistry.GetType(typeName, throwOnError: false) ?? typeof(T);
            if (stream.Context.Get(PlayMirrorContext.Key)?.LoadContent is { } load)
            {
                // The value of an edit: the content itself, as the game loads it
                if (url.Length == 0)
                    throw new InvalidOperationException($"The {type.Name} has no URL: content the game made at run time cannot be sent to it.");
                obj = (T)load(type, url)!;
                return;
            }
            try
            {
                obj = (T)AttachedReferenceManager.CreateProxyObject(type, id, url.Length > 0 ? url : null!);
            }
            catch (InvalidOperationException)
            {
                // A type with no empty constructor: the reader shows no content
                obj = null!;
            }
        }
    }
}
