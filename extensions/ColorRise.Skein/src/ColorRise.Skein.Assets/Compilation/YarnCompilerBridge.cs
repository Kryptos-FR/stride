using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Yarn;
using Yarn.Compiler;

namespace ColorRise.Skein.Assets.Compilation;

/// <summary>
/// The one door to Yarn Spinner's compiler: the asset compiler, the editor's analysis and the string tables all compile
/// through it, and read its result in Skein's own types.
/// </summary>
public static class YarnCompilerBridge
{
    /// <summary>
    /// The functions every dialogue has beside the standard library: the compiler knows the library, not these.
    /// </summary>
    private static readonly YarnFunctionDeclaration[] DialogueFunctions =
    [
        new() { Name = "visited", ParameterTypes = [YarnFunctionDeclaration.String], ReturnType = YarnFunctionDeclaration.Bool },
        new() { Name = "visited_count", ParameterTypes = [YarnFunctionDeclaration.String], ReturnType = YarnFunctionDeclaration.Number },
        new() { Name = "has_any_content", ParameterTypes = [YarnFunctionDeclaration.String], ReturnType = YarnFunctionDeclaration.Bool },
    ];

    /// <summary>
    /// Compiles the scripts of one Yarn project.
    /// </summary>
    /// <param name="request">The scripts, and what the project and the game tell the compiler.</param>
    /// <returns>The nodes, the lines and the diagnostics, with the variables and the program a full compilation gives.</returns>
    /// <exception cref="ArgumentException">A function of the request has a type that is not Yarn's.</exception>
    /// <exception cref="OperationCanceledException">The request's cancellation stopped the compilation.</exception>
    public static YarnCompilation Compile(YarnCompilationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var declarations = DialogueFunctions
            .Where(d => request.Functions.All(f => f.Name != d.Name))
            .Concat(request.Functions)
            .Select(ToDeclaration)
            .ToList();
        request.Cancellation.ThrowIfCancellationRequested();
        if (request.Files.Count == 0)
            return new YarnCompilation();

        var inputs = request.Files.Select(f => (ISourceInput)new CompilationJob.File { FileName = f.FileName, Source = f.Text }).ToList();
        var job = CompilationJob.CreateFromInputs(inputs);
        job.CompilationType = request.StringsOnly ? CompilationJob.Type.StringsOnly : CompilationJob.Type.FullCompilation;
        job.Declarations = declarations;
        job.CancellationToken = request.Cancellation;

        var result = Compiler.Compile(job);

        var nodes = ReadNodes(result);
        var diagnostics = ReadDiagnostics(result, request.Severities, nodes);
        return new YarnCompilation
        {
            // The compiler stops at its own errors; a severity the project raises to an error stops Skein the same way
            Program = diagnostics.Any(d => d.Severity == YarnSeverity.Error) ? null : result.Program,
            Nodes = nodes,
            Lines = ReadLines(result),
            Variables = ReadVariables(result),
            Diagnostics = diagnostics,
        };
    }

    private static Declaration ToDeclaration(YarnFunctionDeclaration function)
    {
        var type = new FunctionTypeBuilder().WithReturnType(ToType(function, function.ReturnType));
        foreach (var parameter in function.ParameterTypes)
        {
            type = type.WithParameter(ToType(function, parameter));
        }

        if (function.VariadicType is { } variadic)
            type = type.WithVariadicParameterType(ToType(function, variadic));

        return new DeclarationBuilder().WithName(function.Name).WithType(type.FunctionType).Declaration;
    }

    private static IType ToType(YarnFunctionDeclaration function, string name) => name switch
    {
        YarnFunctionDeclaration.Number => Types.Number,
        YarnFunctionDeclaration.String => Types.String,
        YarnFunctionDeclaration.Bool => Types.Boolean,
        _ => throw new ArgumentException(
            $"The function '{function.Name}' has the type '{name}': a function of Yarn scripts takes and returns " +
            $"'{YarnFunctionDeclaration.Number}', '{YarnFunctionDeclaration.String}' or '{YarnFunctionDeclaration.Bool}'.",
            nameof(function)),
    };

    private static string TypeName(IType type)
    {
        if (ReferenceEquals(type, Types.Number))
            return YarnFunctionDeclaration.Number;
        if (ReferenceEquals(type, Types.String))
            return YarnFunctionDeclaration.String;
        if (ReferenceEquals(type, Types.Boolean))
            return YarnFunctionDeclaration.Bool;
        return type.Name;
    }

    private static List<YarnNodeData> ReadNodes(CompilationResult result)
    {
        // What the compiler's metadata lacks, read from the syntax trees: the headers in order and where the node ends
        var syntax = new Dictionary<(string File, int HeaderStartLine), YarnSpinnerParser.NodeContext>();
        foreach (var file in result.ParseResults)
        {
            if (file.Tree.Payload is not YarnSpinnerParser.DialogueContext dialogue)
                continue;

            foreach (var node in dialogue.node())
            {
                syntax.TryAdd((file.Name, node.Start.Line), node);
            }
        }

        var nodes = new List<YarnNodeData>();
        foreach (var metadata in result.NodeMetadata)
        {
            var headerStartLine = metadata.HeaderStartLine + 1;
            syntax.TryGetValue((metadata.Uri, headerStartLine), out var node);
            var bodyStartLine = node?.BODY_START()?.Symbol is { } bodyStart ? bodyStart.Line + 1 : Math.Max(metadata.BodyStartLine + 1, headerStartLine);
            var bodyEndLine = node?.BODY_END()?.Symbol is { Line: > 0 } bodyEnd ? bodyEnd.Line : Math.Max(node?.Stop?.Line ?? 0, bodyStartLine);
            nodes.Add(new YarnNodeData
            {
                Title = metadata.Title,
                UniqueTitle = metadata.UniqueTitle ?? metadata.Title,
                File = metadata.Uri,
                Headers = node is not null ? ReadHeaders(node) : [new YarnHeaderData("title", metadata.Title)],
                Tags = [.. metadata.Tags],
                NodeGroup = metadata.NodeGroup,
                Complexity = metadata.NodeGroup is not null ? metadata.NodeGroupComplexity : -1,
                Speakers = [.. metadata.CharacterNames],
                Preview = string.Join('\n', metadata.PreviewText.Split('\n').Select(l => l.TrimEnd())),
                Options = [.. metadata.Options.Select(o => new YarnOptionData(o.Text.Trim(), o.LineNumber + 1, o.GroupId))],
                Jumps = [.. metadata.Jumps.Select(j => new YarnJumpData(j.DestinationTitle, j.Type == JumpType.Detour, j.Range.Start.Line + 1, j.Range.Start.Character + 1))],
                HeaderStartLine = headerStartLine,
                BodyStartLine = bodyStartLine,
                BodyEndLine = Math.Max(bodyEndLine, bodyStartLine),
            });
        }

        return nodes;
    }

    private static List<YarnHeaderData> ReadHeaders(YarnSpinnerParser.NodeContext node)
    {
        var headers = new List<YarnHeaderData>();
        foreach (var child in node.children ?? [])
        {
            switch (child)
            {
                case YarnSpinnerParser.Title_headerContext title:
                    headers.Add(new YarnHeaderData("title", title.title?.Text ?? ""));
                    break;
                case YarnSpinnerParser.When_headerContext condition:
                    headers.Add(new YarnHeaderData("when", SourceText(condition.header_expression)));
                    break;
                case YarnSpinnerParser.HeaderContext header when header.header_key is not null:
                    headers.Add(new YarnHeaderData(header.header_key.Text, header.header_value?.Text.Trim() ?? ""));
                    break;
            }
        }

        return headers;
    }

    /// <summary>
    /// The text of a piece of syntax as written, spaces included.
    /// </summary>
    private static string SourceText(ParserRuleContext? context)
    {
        if (context?.Start is not { StartIndex: >= 0 } start || context.Stop is not { } stop || stop.StopIndex < start.StartIndex)
            return "";

        return start.InputStream.GetText(Interval.Of(start.StartIndex, stop.StopIndex)).Trim();
    }

    private static Dictionary<string, YarnLineData> ReadLines(CompilationResult result)
    {
        var lines = new Dictionary<string, YarnLineData>();
        foreach (var (id, info) in result.StringTable ?? new Dictionary<string, StringInfo>())
        {
            lines[id] = new YarnLineData
            {
                // A shadow line has no text of its own
                Text = info.text ?? "",
                Node = info.nodeName,
                File = info.fileName,
                Line = info.lineNumber,
                IsImplicitId = info.isImplicitTag,
                // The id and the shadowed line have their own place
                Metadata = [.. (info.metadata ?? []).Where(m => !m.StartsWith("line:", StringComparison.Ordinal) && !m.StartsWith("shadow:", StringComparison.Ordinal))],
                ShadowOf = info.shadowLineID,
            };
        }

        return lines;
    }

    private static List<YarnVariableData> ReadVariables(CompilationResult result)
    {
        var variables = new List<YarnVariableData>();
        foreach (var declaration in result.Declarations)
        {
            if (!declaration.IsVariable)
                continue;

            var hasPlace = declaration.SourceFileName != Declaration.ExternalDeclaration && declaration.Range.Start.Line >= 0;
            variables.Add(new YarnVariableData
            {
                Name = declaration.Name,
                Type = TypeName(declaration.Type),
                Default = declaration.IsInlineExpansion ? null : DefaultValue(declaration),
                Description = string.IsNullOrWhiteSpace(declaration.Description) ? null : declaration.Description.Trim(),
                File = hasPlace ? declaration.SourceFileName : null,
                Line = hasPlace ? declaration.Range.Start.Line + 1 : 0,
                IsSmart = declaration.IsInlineExpansion,
                IsImplicit = declaration.IsImplicit,
            });
        }

        return variables;
    }

    private static object? DefaultValue(Declaration declaration) => declaration.DefaultValue switch
    {
        null => null,
        string text => text,
        bool flag => flag,
        var number => number.ToSingle(System.Globalization.CultureInfo.InvariantCulture),
    };

    private static List<YarnDiagnostic> ReadDiagnostics(CompilationResult result, IReadOnlyDictionary<string, YarnSeverity> severities, List<YarnNodeData> nodes)
    {
        var raw = result.Diagnostics.ToList();
        var overrides = severities.ToDictionary(s => s.Key, s => ToYarn(s.Value));
        var set = raw.WithSeverityOverrides(overrides).ToList();

        var diagnostics = new List<YarnDiagnostic>();
        for (var i = 0; i < raw.Count; i++)
        {
            // What stopped the compiler stays an error, whatever the project says
            var severity = raw[i].Severity == Diagnostic.DiagnosticSeverity.Error ? Diagnostic.DiagnosticSeverity.Error : set[i].Severity;
            if (severity == Diagnostic.DiagnosticSeverity.None)
                continue;

            var diagnostic = raw[i];
            var range = diagnostic.Range;
            var line = Math.Max(range.Start.Line, 0) + 1;
            var column = Math.Max(range.Start.Character, 0) + 1;
            var endLine = Math.Max(range.End.Line + 1, line);
            var endColumn = endLine == line ? Math.Max(range.End.Character + 1, column) : Math.Max(range.End.Character, 0) + 1;
            diagnostics.Add(new YarnDiagnostic
            {
                File = diagnostic.FileName,
                Node = nodes.FirstOrDefault(n => n.File == diagnostic.FileName && n.HeaderStartLine <= line && line <= n.BodyEndLine)?.Title,
                Line = line,
                Column = column,
                EndLine = endLine,
                EndColumn = endColumn,
                Code = diagnostic.Code ?? "",
                Severity = severity switch
                {
                    Diagnostic.DiagnosticSeverity.Error => YarnSeverity.Error,
                    Diagnostic.DiagnosticSeverity.Warning => YarnSeverity.Warning,
                    _ => YarnSeverity.Info,
                },
                Message = diagnostic.Message,
            });
        }

        return diagnostics;
    }

    private static Diagnostic.DiagnosticSeverity ToYarn(YarnSeverity severity) => severity switch
    {
        YarnSeverity.Error => Diagnostic.DiagnosticSeverity.Error,
        YarnSeverity.Warning => Diagnostic.DiagnosticSeverity.Warning,
        YarnSeverity.Info => Diagnostic.DiagnosticSeverity.Info,
        _ => Diagnostic.DiagnosticSeverity.None,
    };
}
