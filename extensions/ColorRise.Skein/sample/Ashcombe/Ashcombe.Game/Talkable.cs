using ColorRise.Skein;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Ashcombe;

/// <summary>
/// Someone the cartographer talks to (T), or something the cartographer uses (E): its node of the story plays. A node
/// group says different things as the story goes, and nothing when none of its nodes has its conditions true.
/// </summary>
[Display("Talkable")]
[ComponentCategory("Ashcombe")]
public sealed class Talkable : StartupScript
{
    // A walk ends this close to its place, so that two people do not stand in each other
    private const float Arrival = 1.2f;

    /// <summary>
    /// The node, or the node group, of the story that plays.
    /// </summary>
    /// <userdoc>The node (or node group) of the story that plays when the cartographer talks to this entity, or uses it.</userdoc>
    [DataMember(10)]
    public string Node { get; set; } = "";

    /// <summary>
    /// Whether it is a thing, used with E, and not someone, talked to with T.
    /// </summary>
    /// <userdoc>A thing is used with E and shows no name. Off, it is someone the cartographer talks to with T.</userdoc>
    [DataMember(20)]
    public bool IsThing { get; set; }

    /// <summary>
    /// For a thing, the id of the line of the story that says what using it does ("read the carvings"), shown after the key "E".
    /// </summary>
    /// <userdoc>For a thing: the id of the line that says what using it does, as in line:use_carvings. The screen shows it after the key "E".</userdoc>
    [DataMember(30)]
    public string Label { get; set; } = "";

    /// <summary>
    /// Metres per second, when the story makes it walk.
    /// </summary>
    /// <userdoc>How fast it walks when the story tells it to, in metres per second.</userdoc>
    [DataMember(40)]
    public float WalkSpeed { get; set; } = 3f;

    /// <summary>
    /// A runner of its own, for someone who speaks another Yarn project than the story's: the echo of the mine. Null:
    /// the runner of the stage.
    /// </summary>
    /// <userdoc>A dialogue runner of its own, with another Yarn project than the story's. Empty: the runner of the scene's stage.</userdoc>
    [DataMember(50)]
    public DialogueRunnerComponent? Runner { get; set; }

    /// <summary>
    /// Whether the story shows it now (<see cref="ShownWhen"/>).
    /// </summary>
    [DataMemberIgnore]
    public bool IsShown => Entity.Get<ShownWhen>()?.IsShown ?? true;

    /// <summary>
    /// Walks to an entity of its place; the dialogue waits until it is there.
    /// </summary>
    [YarnCommand("walk", Description = "Makes someone walk to an entity of its place, and waits: <<walk Hedda Bar>>.")]
    public async Task Walk(Entity place, CancellationToken cancellation)
    {
        // Both are in the same scene: the walk is in the scene's own space
        var target = place.Transform.Position;
        while (!cancellation.IsCancellationRequested)
        {
            var position = Entity.Transform.Position;
            var path = new Vector3(target.X - position.X, 0, target.Z - position.Z);
            var step = WalkSpeed * (float)Game.UpdateTime.Elapsed.TotalSeconds;
            if (path.Length() <= Arrival + step)
                return;

            path.Normalize();
            Entity.Transform.Position = position + path * step;
            await Script.NextFrame();
        }
    }
}
