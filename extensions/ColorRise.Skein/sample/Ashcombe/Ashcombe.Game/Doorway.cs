using ColorRise.Skein;
using Stride.Core;
using Stride.Core.Serialization;
using Stride.Engine;

namespace Ashcombe;

/// <summary>
/// A way to another place: a door, stairs, a lift, a road. The cartographer uses it with E, and a script with
/// <c>&lt;&lt;go CellarStairs&gt;&gt;</c>, which names the doorway's entity. Into an interior, the dialogue goes on
/// inside; on a road to another root scene, the journey waits for the end of the dialogue.
/// </summary>
[Display("Doorway")]
[ComponentCategory("Ashcombe")]
public sealed class Doorway : StartupScript
{
    /// <summary>
    /// The scene of the place it leads to.
    /// </summary>
    /// <userdoc>The scene of the place it leads to: an interior, the scene it is in (to go back out), or another root scene for a road.</userdoc>
    [DataMember(10)]
    public UrlReference<Scene>? Target { get; set; }

    /// <summary>
    /// The name of the entity of the target where the cartographer arrives; empty for its <c>Arrival</c>.
    /// </summary>
    /// <userdoc>The entity of the place where the cartographer arrives. Empty: the entity named Arrival.</userdoc>
    [DataMember(20)]
    public string Spawn { get; set; } = "";

    /// <summary>
    /// Whether it leads to another root scene: the village, the castle or the mine.
    /// </summary>
    /// <userdoc>A road to another root scene (the village, the castle, the mine), which replaces this one. Off: an interior, loaded under the root scene.</userdoc>
    [DataMember(30)]
    public bool IsJourney { get; set; }

    /// <summary>
    /// The id of the line of the story that says where it leads ("go down to the cellar"), shown after the key "E".
    /// </summary>
    /// <userdoc>The id of the line that says where it leads, as in line:use_cellar. The screen shows it after the key "E".</userdoc>
    [DataMember(40)]
    public string Label { get; set; } = "";

    /// <summary>
    /// A node, or a node group, played instead of going when it has something to say: a guard at the bridge.
    /// </summary>
    /// <userdoc>A node (or node group) of the story played instead of going through, when it has something to say now. The story goes through with go when it wants.</userdoc>
    [DataMember(50)]
    public string Node { get; set; } = "";

    /// <summary>
    /// Whether the story shows it now (<see cref="ShownWhen"/>).
    /// </summary>
    [DataMemberIgnore]
    public bool IsShown => Entity.Get<ShownWhen>()?.IsShown ?? true;

    /// <summary>
    /// Takes the cartographer through.
    /// </summary>
    [YarnCommand("go", Description = "Takes the cartographer through a door, down stairs or on a road: <<go CellarStairs>>. A road to another scene waits for the end of the dialogue.")]
    public Task Go()
    {
        if (Target?.Url is not { Length: > 0 } url)
            throw new YarnCommandException($"go {Entity.Name}", "the doorway leads nowhere: it has no target.");

        return Services.GetSafeServiceAs<StorySystem>().GoTo(url, Spawn, Entity.Scene, IsJourney);
    }
}
