using ColorRise.Skein.Editor.Graph;

namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The graph of a script exports as Mermaid or DOT, for design documents.
/// </summary>
public sealed class GraphExportTests
{
    private const string Act1 =
        "title: Start\n---\nOdile: Hello.\n"
        + "-> Ask about \"the\" pier\n    <<jump Pier>>\n"
        + "-> Leave\n    <<detour Harbour>>\n"
        + "<<jump {$next}>>\n<<jump Nowhere>>\n===\n"
        + "title: Pier\nwhen: always\n---\nFisher: Hi.\n===\n"
        + "title: Pier\nwhen: once\n---\nFisher: First time.\n===\n"
        + "title: Todo\nstyle: note\n---\nLater.\n<<jump Pier>>\n===\n";

    private const string Act2 = "title: Harbour\n---\nThe boats are in.\n===\n";

    [Fact]
    public async Task The_graph_exports_as_Mermaid_and_as_DOT()
    {
        await using var session = await SkeinSession.OpenAsync(SkeinSession.Project("Dialogue/Story"), SkeinSession.Script("Dialogue/Act1", Act1), SkeinSession.Script("Dialogue/Act2", Act2));
        var editor = session.Edit("Dialogue/Act1");
        await session.Analysis.WhenIdle();

        // A node group is one node; a note stays out; the options label their links; other scripts, expressions and
        // missing nodes have a shape of their own
        var mermaid = YarnGraphExport.Write("Act1", editor.Cards, editor.Links, YarnGraphFormat.Mermaid);
        Assert.Equal(
            "flowchart TD\n"
            + "    n0[\"Start\"]\n"
            + "    n1[\"Pier (2 nodes)\"]\n"
            + "    n2([\"Harbour (Dialogue/Act2.yarn)\"]):::other\n"
            + "    n3([\"Nowhere (missing)\"]):::missing\n"
            + "    n4([\"{$next}\"]):::computed\n"
            + "    n0 -->|\"Ask about #quot;the#quot; pier\"| n1\n"
            + "    n0 -.->|\"detour: Leave\"| n2\n"
            + "    n0 --> n3\n"
            + "    n0 --> n4\n"
            + "    classDef other stroke-dasharray: 4 3\n"
            + "    classDef computed stroke-dasharray: 2 2\n"
            + "    classDef missing stroke:#c33,color:#c33\n",
            mermaid);

        var dot = YarnGraphExport.Write("Act1", editor.Cards, editor.Links, YarnGraphFormat.Dot);
        Assert.StartsWith("digraph \"Act1\" {\n    node [shape=box, style=rounded];\n    n0 [label=\"Start\"];\n", dot, StringComparison.Ordinal);
        Assert.Contains("    n0 -> n1 [label=\"Ask about \\\"the\\\" pier\"];\n", dot, StringComparison.Ordinal);
        Assert.Contains("    n0 -> n2 [style=dashed, label=\"detour: Leave\"];\n", dot, StringComparison.Ordinal);
        Assert.Contains("    n3 [label=\"Nowhere (missing)\", color=red, fontcolor=red];\n", dot, StringComparison.Ordinal);
        Assert.EndsWith("}\n", dot, StringComparison.Ordinal);

        // A file takes the format of its extension
        var folder = Directory.CreateTempSubdirectory("skein-graph-");
        try
        {
            var markdown = Path.Combine(folder.FullName, "act1.md");
            editor.ExportGraphTo(markdown);
            Assert.Equal($"# Act1\n\n```mermaid\n{mermaid}```\n", File.ReadAllText(markdown));
            var graphviz = Path.Combine(folder.FullName, "act1.gv");
            editor.ExportGraphTo(graphviz);
            Assert.Equal(dot, File.ReadAllText(graphviz));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
