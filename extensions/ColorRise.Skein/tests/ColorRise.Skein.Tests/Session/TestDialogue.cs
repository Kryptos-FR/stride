using ColorRise.Skein.Assets.Compilation;
using Stride.Core.Diagnostics;

namespace ColorRise.Skein.Tests.Session;

/// <summary>
/// A session on one script, with all a test reads of it: the responder's record, the variables, the log.
/// </summary>
internal sealed class TestDialogue
{
    public TestDialogue(
        string script,
        YarnActionRegistry? actions = null,
        VariableStorage? variables = null,
        YarnSaliency saliency = YarnSaliency.RandomBestLeastRecentlyViewed,
        int? seed = null,
        YarnFunctionDeclaration[]? declarations = null)
    {
        Actions = actions ?? new YarnActionRegistry();
        Variables = variables ?? new VariableStorage(Log);

        var functions = Actions.Describe().Where(a => a.Kind == YarnActionKind.Function).Select(YarnFunctionDeclaration.From);
        Compilation = Scripts.Compile(script, [.. functions, .. declarations ?? []]);
        Session = new DialogueSession(Compilation.Program!, Variables, LinesOf(Compilation, Log), Actions, Responder, new DialogueSessionOptions
        {
            Saliency = saliency,
            RandomSeed = seed,
            Clock = Clock,
            Log = Log,
            Nodes = NodesOf(Compilation),
        });
    }

    public YarnCompilation Compilation { get; }

    public DialogueSession Session { get; }

    public RecordingResponder Responder { get; } = new();

    public YarnActionRegistry Actions { get; }

    public VariableStorage Variables { get; }

    public FakeClock Clock { get; } = new();

    public LoggerResult Log { get; } = new();

    public List<string> Transcript => Responder.Transcript;

    public IEnumerable<string> Errors => Log.Messages.Where(m => m.Type == LogMessageType.Error).Select(m => m.Text);

    public Task RunAsync(string node = "Start") => Session.RunAsync(node);

    /// <summary>
    /// The lines of a compilation as the base language of a project.
    /// </summary>
    public static TextLineProvider LinesOf(YarnCompilation compilation, ILogger? log = null)
    {
        var table = new YarnStringTable { Language = "en" };
        var metadata = new Dictionary<string, List<string>>();
        var shadows = new Dictionary<string, string>();
        foreach (var (id, line) in compilation.Lines)
        {
            if (line.ShadowOf is { } source)
                shadows[id] = source;
            else
                table.Strings[id] = line.Text;
            if (line.Metadata.Count > 0)
                metadata[id] = [.. line.Metadata];
        }

        return new TextLineProvider(language => language == "en" ? table : null, "en", log) { LineMetadata = metadata, ShadowLines = shadows };
    }

    /// <summary>
    /// What a compiled project keeps of its nodes.
    /// </summary>
    public static List<YarnNodeInfo> NodesOf(YarnCompilation compilation)
    {
        var nodes = new List<YarnNodeInfo>();
        foreach (var node in compilation.Nodes)
        {
            var info = new YarnNodeInfo { Name = node.UniqueTitle, Title = node.Title };
            info.Headers.AddRange(node.Headers.Select(h => new YarnNodeHeader(h.Key, h.Value)));
            info.Tags.AddRange(node.Tags);
            nodes.Add(info);
        }

        return nodes;
    }
}
