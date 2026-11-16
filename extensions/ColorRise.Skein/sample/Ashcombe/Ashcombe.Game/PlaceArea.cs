using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Ashcombe;

/// <summary>
/// Where the cartographer can walk in a place: a rectangle around its entity. Each scene of a place has one.
/// </summary>
[Display("Place area")]
[ComponentCategory("Ashcombe")]
public sealed class PlaceArea : EntityComponent
{
    /// <summary>
    /// How far from the entity the cartographer walks, along X and along Z, in metres.
    /// </summary>
    /// <userdoc>How far from this entity the cartographer walks, along X and along Z, in metres.</userdoc>
    [DataMember(10)]
    public Vector2 HalfSize { get; set; } = new(10f, 10f);

    /// <summary>
    /// The id of the line that names the place on the screen.
    /// </summary>
    /// <userdoc>The id of the line that names the place, shown when the cartographer gets there, as in line:place_inn.</userdoc>
    [DataMember(20)]
    public string Title { get; set; } = "";
}
