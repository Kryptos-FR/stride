using Stride.Core;
using Stride.Core.Serialization;
using Stride.Core.Serialization.Contents;

namespace ColorRise.Saga.Content;

/// <summary>
/// A compiled story: its declarations and its plot. The game loads it from the content (<c>Content.Load</c>).
/// </summary>
[DataContract("SagaStoryDefinition")]
[ContentSerializer(typeof(DataContentSerializer<StoryDefinition>))]
[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<StoryDefinition>), Profile = "Content")]
public sealed class StoryDefinition
{
    public string Name { get; set; } = string.Empty;

    public StoryDeclarations Declarations { get; set; } = new();

    public PlotDefinition Plot { get; set; } = new();
}
