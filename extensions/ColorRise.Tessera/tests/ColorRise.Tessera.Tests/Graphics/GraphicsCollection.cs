namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// The tests that run a game, one after the other: a game has global parts (the graphics device, the log).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GraphicsCollection
{
    public const string Name = "Graphics";
}
