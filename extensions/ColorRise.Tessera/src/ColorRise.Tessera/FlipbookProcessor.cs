using Stride.Engine;
using Stride.Games;

namespace ColorRise.Tessera;

/// <summary>
/// Starts each flipbook component, playing when it says <see cref="FlipbookComponent.PlayOnStart"/>, and advances the
/// enabled ones by the game's time.
/// </summary>
public sealed class FlipbookProcessor : EntityProcessor<FlipbookComponent>
{
    // The components of the frame, copied: a handler of an event may add or remove components
    private readonly List<FlipbookComponent> advancing = [];

    /// <summary>
    /// Advances each enabled component by the time of the frame.
    /// </summary>
    public override void Update(GameTime time)
    {
        var elapsed = time.Elapsed.TotalSeconds;
        advancing.AddRange(ComponentDatas.Keys);
        try
        {
            foreach (var component in advancing)
            {
                if (component.Enabled)
                {
                    component.Advance(elapsed);
                }
            }
        }
        finally
        {
            advancing.Clear();
        }
    }

    /// <summary>
    /// What the processor does to a component it is given: its animation from the time 0, playing when the component
    /// plays on start.
    /// </summary>
    internal static void Start(FlipbookComponent component) => component.Start();

    /// <summary>
    /// Starts a component added to the scene.
    /// </summary>
    protected override void OnEntityComponentAdding(Entity entity, FlipbookComponent component, FlipbookComponent data) => Start(component);
}
