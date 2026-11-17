using System.Text;

namespace ColorRise.Skein.Editor.Graph;

/// <summary>
/// The formats a graph exports to.
/// </summary>
public enum YarnGraphFormat
{
    /// <summary>
    /// A Mermaid flowchart, as Markdown viewers and documentation sites draw it.
    /// </summary>
    Mermaid,

    /// <summary>
    /// A Mermaid flowchart in a Markdown file, under the name of the script.
    /// </summary>
    MermaidMarkdown,

    /// <summary>
    /// Graphviz's DOT language.
    /// </summary>
    Dot,
}

/// <summary>
/// Writes the graph of a script as text for design documents: its nodes, its jumps and detours labelled with the option
/// they leave from, and the nodes of other scripts they lead to. The notes of the writers stay out.
/// </summary>
public static class YarnGraphExport
{
    /// <summary>
    /// The format of a file, from its extension: <c>.mmd</c> or <c>.mermaid</c>, <c>.md</c>, <c>.dot</c> or <c>.gv</c>.
    /// </summary>
    /// <returns>The format, or null for another extension.</returns>
    public static YarnGraphFormat? FormatOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mmd" or ".mermaid" => YarnGraphFormat.Mermaid,
        ".md" or ".markdown" => YarnGraphFormat.MermaidMarkdown,
        ".dot" or ".gv" => YarnGraphFormat.Dot,
        _ => null,
    };

    /// <summary>
    /// Writes a graph.
    /// </summary>
    /// <param name="name">The name of the script, for the title of a document.</param>
    /// <param name="cards">The cards of the script's nodes; the nodes of a node group are one node.</param>
    /// <param name="links">The links between them.</param>
    /// <param name="format">The format.</param>
    public static string Write(string name, IEnumerable<YarnNodeCardViewModel> cards, IEnumerable<YarnLinkViewModel> links, YarnGraphFormat format)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(links);

        var graph = Read(cards, links);
        return format switch
        {
            YarnGraphFormat.Mermaid => Mermaid(graph),
            YarnGraphFormat.MermaidMarkdown => $"# {name}\n\n```mermaid\n{Mermaid(graph)}```\n",
            _ => Dot(name, graph),
        };
    }

    private static Graph Read(IEnumerable<YarnNodeCardViewModel> cards, IEnumerable<YarnLinkViewModel> links)
    {
        var nodes = new List<Node>();
        var ids = new Dictionary<string, Node>(StringComparer.Ordinal);
        Node NodeOf(string title, string label, NodeKind kind)
        {
            var key = kind == NodeKind.Node ? title : $"{kind}:{title}";
            if (!ids.TryGetValue(key, out var node))
            {
                ids.Add(key, node = new Node($"n{nodes.Count}", label, kind));
                nodes.Add(node);
            }

            return node;
        }

        // The nodes in the order of the text, a node group once with its count
        foreach (var group in cards.Where(c => !c.IsNote).GroupBy(c => c.Title, StringComparer.Ordinal))
        {
            var count = group.Count();
            NodeOf(group.Key, count > 1 ? $"{group.Key} ({count} nodes)" : group.Key, NodeKind.Node);
        }

        var edges = new List<Edge>();
        foreach (var link in links)
        {
            if (link.Source.IsNote || !ids.TryGetValue(link.Source.Title, out var source))
                continue;

            var target = link.Target switch
            {
                YarnNodeCardViewModel card => NodeOf(card.Title, card.Title, NodeKind.Node),
                YarnStubCardViewModel { IsComputed: true } stub => NodeOf(stub.Title, stub.Title, NodeKind.Computed),
                YarnStubCardViewModel { IsBroken: true } stub => NodeOf(stub.Title, $"{stub.Title} (missing)", NodeKind.Missing),
                YarnStubCardViewModel stub => NodeOf(stub.Title, $"{stub.Title} ({stub.FileName})", NodeKind.Other),
                _ => null,
            };
            if (target is null)
                continue;

            var edge = new Edge(source, target, link.IsDetour, link.Label);
            if (!edges.Contains(edge))
            {
                edges.Add(edge);
            }
        }

        return new Graph(nodes, edges);
    }

    private static string Mermaid(Graph graph)
    {
        var text = new StringBuilder("flowchart TD\n");
        foreach (var node in graph.Nodes)
        {
            var label = MermaidText(node.Label);
            text.Append("    ").Append(node.Id).Append(node.Kind switch
            {
                NodeKind.Node => $"[\"{label}\"]",
                _ => $"([\"{label}\"]):::{node.Kind.ToString().ToLowerInvariant()}",
            }).Append('\n');
        }

        foreach (var edge in graph.Edges)
        {
            var label = edge.Label is { } text1 ? MermaidText(edge.IsDetour ? $"detour: {text1}" : text1) : edge.IsDetour ? "detour" : null;
            text.Append("    ").Append(edge.Source.Id).Append(edge.IsDetour ? " -.->" : " -->");
            if (label is not null)
            {
                text.Append("|\"").Append(label).Append("\"|");
            }

            text.Append(' ').Append(edge.Target.Id).Append('\n');
        }

        if (graph.Nodes.Any(n => n.Kind != NodeKind.Node))
        {
            text.Append("    classDef other stroke-dasharray: 4 3\n");
            text.Append("    classDef computed stroke-dasharray: 2 2\n");
            text.Append("    classDef missing stroke:#c33,color:#c33\n");
        }

        return text.ToString();
    }

    // Mermaid reads its own entities in a quoted label
    private static string MermaidText(string text) => text.Replace("\"", "#quot;", StringComparison.Ordinal);

    private static string Dot(string name, Graph graph)
    {
        var text = new StringBuilder();
        text.Append("digraph ").Append(DotText(name)).Append(" {\n");
        text.Append("    node [shape=box, style=rounded];\n");
        foreach (var node in graph.Nodes)
        {
            text.Append("    ").Append(node.Id).Append(" [label=").Append(DotText(node.Label));
            text.Append(node.Kind switch
            {
                NodeKind.Other => ", style=\"rounded,dashed\"",
                NodeKind.Computed => ", style=\"rounded,dotted\"",
                NodeKind.Missing => ", color=red, fontcolor=red",
                _ => "",
            }).Append("];\n");
        }

        foreach (var edge in graph.Edges)
        {
            text.Append("    ").Append(edge.Source.Id).Append(" -> ").Append(edge.Target.Id);
            var attributes = new List<string>();
            if (edge.IsDetour)
            {
                attributes.Add("style=dashed");
            }

            if ((edge.Label ?? (edge.IsDetour ? "detour" : null)) is { } label)
            {
                attributes.Add("label=" + DotText(edge.Label is not null && edge.IsDetour ? $"detour: {label}" : label));
            }

            if (attributes.Count > 0)
            {
                text.Append(" [").Append(string.Join(", ", attributes)).Append(']');
            }

            text.Append(";\n");
        }

        return text.Append("}\n").ToString();
    }

    private static string DotText(string text) => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private enum NodeKind
    {
        Node,
        Other,
        Computed,
        Missing,
    }

    private sealed record Node(string Id, string Label, NodeKind Kind);

    private sealed record Edge(Node Source, Node Target, bool IsDetour, string? Label);

    private sealed record Graph(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);
}
