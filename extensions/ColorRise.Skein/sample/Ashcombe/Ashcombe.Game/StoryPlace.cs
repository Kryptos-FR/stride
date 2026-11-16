using ColorRise.Skein;
using Stride.Core;
using Stride.Core.Serialization;
using Stride.Engine;

namespace Ashcombe;

/// <summary>
/// A place the story loads by itself under the root scene while one of its variables is true: the fair of the last act
/// is up on the village green. The cartographer does not move; the place is there, with its people.
/// </summary>
[DataContract]
[Display("Story place")]
[ComponentCategory("Ashcombe")]
public sealed class StoryPlace : EntityComponent
{
    /// <summary>
    /// The scene of the place.
    /// </summary>
    /// <userdoc>The scene loaded under this root scene while the variable is true.</userdoc>
    [DataMember(10)]
    public UrlReference<Scene>? Place { get; set; }

    /// <summary>
    /// The variable of the story, a boolean: <c>$fair_up</c>.
    /// </summary>
    /// <userdoc>The boolean variable of the story that loads the place, as in $fair_up.</userdoc>
    [DataMember(20)]
    public string Variable { get; set; } = "";

    /// <summary>
    /// Whether the place should be loaded now.
    /// </summary>
    public bool IsWanted(VariableStorage variables)
        => variables.TryGetValue<bool>(Variable, out var value) && value;
}
