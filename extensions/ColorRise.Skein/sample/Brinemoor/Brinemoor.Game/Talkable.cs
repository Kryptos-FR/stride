using ColorRise.Skein;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Brinemoor;

/// <summary>
/// Someone the courier talks to (T), or something the courier uses (E): its node of the story plays. A node group says
/// different things as the story goes, and nothing when none of its nodes has its conditions true.
/// </summary>
[Display("Talkable")]
[ComponentCategory("Brinemoor")]
public sealed class Talkable : StartupScript
{
    // A walk ends this close to its place, so that two people do not stand in each other
    private const float Arrival = 1.2f;

    /// <summary>
    /// The node, or the node group, of the story that plays.
    /// </summary>
    /// <userdoc>The node (or node group) of the story that plays when the courier talks to this entity, or uses it.</userdoc>
    [DataMember(10)]
    public string Node { get; set; } = "";

    /// <summary>
    /// Whether it is a thing, used with E, and not someone, talked to with T.
    /// </summary>
    /// <userdoc>A thing is used with E and shows no name. Off, it is someone the courier talks to with T.</userdoc>
    [DataMember(20)]
    public bool IsThing { get; set; }

    /// <summary>
    /// For a thing, the id of the line of the story that says what using it does ("light the lamp"), shown after "E:".
    /// </summary>
    /// <userdoc>For a thing: the id of the line that says what using it does, as in line:use_lamp. The screen shows it after "E:".</userdoc>
    [DataMember(30)]
    public string Label { get; set; } = "";

    /// <summary>
    /// Metres per second, when the story makes it walk.
    /// </summary>
    /// <userdoc>How fast it walks when the story tells it to, in metres per second.</userdoc>
    [DataMember(40)]
    public float WalkSpeed { get; set; } = 3f;

    /// <summary>
    /// Whether the story shows it now (<see cref="ShownWhen"/>).
    /// </summary>
    [DataMemberIgnore]
    public bool IsShown => Entity.Get<ShownWhen>()?.IsShown ?? true;

    /// <summary>
    /// Walks to an entity of the scene; the dialogue waits until it is there.
    /// </summary>
    [YarnCommand("walk", Description = "Makes someone walk to a place of the scene, and waits: <<walk Brandt Pier>>.")]
    public async Task Walk(Entity place, CancellationToken cancellation)
    {
        var target = place.Transform.WorldMatrix.TranslationVector;
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
