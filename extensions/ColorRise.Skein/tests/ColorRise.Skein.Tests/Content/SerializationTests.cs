using Stride.Core.Serialization;

namespace ColorRise.Skein.Tests.Content;

/// <summary>
/// What the asset compiler writes and a game loads goes through Stride's binary serializer unchanged.
/// </summary>
public sealed class SerializationTests
{
    private static T RoundTrip<T>(T value)
    {
        using var stream = new MemoryStream();
        new BinarySerializationWriter(stream).Write(value);
        stream.Position = 0;
        return new BinarySerializationReader(stream).Read<T>();
    }

    [Fact]
    public void A_project_round_trips()
    {
        var compilation = Scripts.Compile("title: Start\ntags: act1\nquest: lamp\n---\nOdile: Hello. #line:hello #shout\n===\n");
        var project = new YarnProject
        {
            Program = Google.Protobuf.MessageExtensions.ToByteArray(compilation.Program!),
            BaseLanguage = "en",
            Saliency = YarnSaliency.Best,
        };
        project.Languages.AddRange(["en", "fr"]);
        var node = new YarnNodeInfo { Name = "Start", Title = "Start" };
        node.Headers.Add(new YarnNodeHeader("title", "Start"));
        node.Headers.Add(new YarnNodeHeader("quest", "lamp"));
        node.Tags.Add("act1");
        project.Nodes.Add(node);
        project.LineMetadata["line:hello"] = ["shout"];
        project.ShadowLines["line:echo"] = "line:hello";

        var read = RoundTrip(project);

        Assert.Equal(project.Program, read.Program);
        Assert.Equal("en", read.BaseLanguage);
        Assert.Equal(["en", "fr"], read.Languages);
        Assert.Equal(YarnSaliency.Best, read.Saliency);
        var readNode = Assert.Single(read.Nodes);
        Assert.Equal(("Start", "Start"), (readNode.Name, readNode.Title));
        Assert.Equal([new YarnNodeHeader("title", "Start"), new YarnNodeHeader("quest", "lamp")], readNode.Headers);
        Assert.Equal("lamp", readNode.Header("quest"));
        Assert.Null(readNode.Header("colour"));
        Assert.Equal(["act1"], readNode.Tags);
        Assert.Equal(["shout"], read.LineMetadata["line:hello"]);
        Assert.Equal("line:hello", read.ShadowLines["line:echo"]);
        Assert.True(read.GetProgram().Nodes.ContainsKey("Start"));
        Assert.Same(read.GetProgram(), read.GetProgram());
    }

    [Fact]
    public void A_string_table_round_trips()
    {
        var table = new YarnStringTable { Language = "fr" };
        table.Strings["line:hello"] = "Odile : Bonjour.";

        var read = RoundTrip(table);

        Assert.Equal("fr", read.Language);
        Assert.Equal("Odile : Bonjour.", read.Strings["line:hello"]);
    }

    [Fact]
    public void A_snapshot_round_trips()
    {
        var snapshot = new VariableSnapshot();
        snapshot.Numbers["$gold"] = 12f;
        snapshot.Strings["$name"] = "Kye";
        snapshot.Bools["$met_kye"] = true;

        var read = RoundTrip(snapshot);

        Assert.Equal(12f, read.Numbers["$gold"]);
        Assert.Equal("Kye", read.Strings["$name"]);
        Assert.True(read.Bools["$met_kye"]);
    }

    [Fact]
    public void A_string_table_is_found_beside_its_project()
    {
        Assert.Equal("Dialogue/Story/strings/fr", YarnProject.StringTableUrl("Dialogue/Story", "fr"));
    }
}
