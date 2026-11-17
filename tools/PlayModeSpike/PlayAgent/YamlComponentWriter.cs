// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.IO;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Yaml;
using Stride.Core.Reflection;
using Stride.Core.Serialization;
using Stride.Core.Yaml;
using Stride.Core.Yaml.Serialization;
using Stride.Engine;

namespace Stride.PlayMode.Spike;

/// <summary>
/// Writes a component with the asset YAML serializer, references to other entities and components as <c>ref!! id</c>,
/// and references to content as <c>id:url</c>, as a scene asset holds them.
/// </summary>
/// <remarks>
/// Needs the design-time assemblies (Stride.Core.Design, Stride.Core.Assets, Stride.Core.Yaml), which the game does not
/// reference: the startup hook resolves them from the agent's folder.
/// </remarks>
internal sealed class YamlComponentWriter
{
    private readonly MemoryStream buffer = new(4096);
    private readonly ReferenceCollector collector = new();

    public YamlComponentWriter()
    {
        // Collection item ids live in shadow objects; without them a path to a collection item cannot be written.
        ShadowObject.Enable = true;
    }

    public ReadOnlySpan<byte> Write(EntityComponent component)
    {
        AssetCollectionItemIdHelper.GenerateMissingItemIds(component);
        var settings = new SerializerContextSettings();
        settings.Properties.Add(AssetObjectSerializerBackend.ObjectReferencesKey, collector.Collect(component));
        buffer.SetLength(0);
        AssetYamlSerializer.Default.Serialize(buffer, component, typeof(EntityComponent), settings);
        return buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
    }

    /// <summary>
    /// Finds the members of a component that hold another entity or component, which the YAML writes as references.
    /// </summary>
    private sealed class ReferenceCollector : DataVisitorBase
    {
        private object? root;
        private YamlAssetMetadata<Guid> references = new();

        public YamlAssetMetadata<Guid> Collect(object component)
        {
            Reset();
            root = component;
            references = new YamlAssetMetadata<Guid>();
            Visit(component);
            return references;
        }

        public override void VisitObject(object obj, ObjectDescriptor descriptor, bool visitMembers)
        {
            if (!ReferenceEquals(obj, root))
            {
                if (obj is IIdentifiable identifiable and (Entity or EntityComponent))
                {
                    references.Set(YamlAssetPath.FromMemberPath(CurrentPath, root!), identifiable.Id);
                    return;
                }
                // Content is written as a URL: nothing to find inside.
                if (AttachedReferenceManager.GetAttachedReference(obj) is not null)
                    return;
            }
            base.VisitObject(obj, descriptor, visitMembers);
        }
    }
}
