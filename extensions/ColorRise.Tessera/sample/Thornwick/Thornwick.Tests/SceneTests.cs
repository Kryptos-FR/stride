using Stride.Core.Mathematics;
using Stride.Core.Yaml.Serialization;

namespace Thornwick.Tests;

/// <summary>
/// The scene's places, read from its file: both are in one scene, so they must be far enough apart that no view of one
/// shows the other.
/// </summary>
public sealed class SceneTests
{
    [Theory]
    // An 8K screen, as wide as 7680 and as short as 100 pixels (zoom 1, the widest view), and as tall as 4320
    [InlineData(7680, 100)]
    [InlineData(100, 4320)]
    [InlineData(7680, 4320)]
    public void No_view_of_a_place_shows_the_other(int width, int height)
    {
        var places = new[] { Frame("Town", "Maps/Town"), Frame("House", "Maps/House") };
        foreach (var place in places)
        {
            foreach (var target in new[] { place.TopLeft, place.BottomRight, place.Center })
            {
                var view = View(CameraFraming.Fit(target, place, new Int2(width, height), 16), width, height);
                foreach (var other in places)
                {
                    Assert.True(other == place || !view.Intersects(other), $"A view of {width} by {height} on {target} shows {other}.");
                }
            }
        }
    }

    // The frame of a place in the world: its entity's position in the scene, and its map's size
    private static RectangleF Frame(string entity, string map)
    {
        var size = SampleAssets.Map(map).Size;
        var position = Position(entity);
        return new RectangleF(position.X, position.Y, size.X, size.Y);
    }

    private static RectangleF View(CameraFrame frame, int width, int height)
    {
        var (viewWidth, viewHeight) = (frame.OrthographicSize * width / height, frame.OrthographicSize);
        return new RectangleF(frame.Center.X - viewWidth / 2, frame.Center.Y - viewHeight / 2, viewWidth, viewHeight);
    }

    // The position of an entity of the main scene, read with a YAML parser
    private static Vector2 Position(string name)
    {
        var stream = new YamlStream();
        using (var reader = new StreamReader(SampleFiles.Asset("MainScene", ".sdscene")))
        {
            stream.Load(reader);
        }
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var hierarchy = (YamlMappingNode)root.Children[new YamlScalarNode("Hierarchy")];
        var parts = (YamlSequenceNode)hierarchy.Children[new YamlScalarNode("Parts")];
        foreach (var part in parts.Children.Cast<YamlMappingNode>())
        {
            var entity = (YamlMappingNode)part.Children[new YamlScalarNode("Entity")];
            if (((YamlScalarNode)entity.Children[new YamlScalarNode("Name")]).Value != name)
            {
                continue;
            }
            var components = (YamlMappingNode)entity.Children[new YamlScalarNode("Components")];
            var transform = components.Children.Values.Cast<YamlMappingNode>().Single(component => component.Tag == "!TransformComponent");
            var position = (YamlMappingNode)transform.Children[new YamlScalarNode("Position")];
            float Read(string axis) => float.Parse(((YamlScalarNode)position.Children[new YamlScalarNode(axis)]).Value, System.Globalization.CultureInfo.InvariantCulture);
            return new Vector2(Read("X"), Read("Y"));
        }
        throw new InvalidOperationException($"The main scene has no entity named {name}.");
    }
}
