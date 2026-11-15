using System.Globalization;
using System.Text.RegularExpressions;

namespace ColorRise.Skein.Tests.Conformance;

/// <summary>
/// A step of a test plan: something the dialogue must say, or something the test does.
/// </summary>
internal abstract record PlanStep;

/// <summary>
/// The dialogue says a line; a null text is any line.
/// </summary>
internal sealed record ExpectLine(string? Text) : PlanStep;

/// <summary>
/// The dialogue offers an option, among the ones before the next <see cref="SelectOption"/>.
/// </summary>
internal sealed record ExpectOption(string Text, bool IsAvailable) : PlanStep;

internal sealed record ExpectCommand(string Text) : PlanStep;

/// <summary>
/// The dialogue ends.
/// </summary>
internal sealed record ExpectStop : PlanStep;

/// <summary>
/// The test picks an option: the first is 1, and 0 picks none.
/// </summary>
internal sealed record SelectOption(int Number) : PlanStep;

internal sealed record SetVariable(string Name, object Value) : PlanStep;

internal sealed record SetSaliency(string Mode) : PlanStep;

/// <summary>
/// One run of a test plan: a node and the steps of its dialogue.
/// </summary>
internal sealed class PlanRun
{
    public string StartNode { get; set; } = "Start";

    public List<PlanStep> Steps { get; } = [];
}

/// <summary>
/// Reads the test plans of Yarn Spinner's own tests (the grammar <c>YarnSpinnerTestPlan.g4</c> of its test project).
/// </summary>
internal static partial class YarnTestPlan
{
    public static List<PlanRun> Parse(string text)
    {
        var tokens = new Queue<Match>(Tokens().Matches(text).Where(m => !m.Groups["skip"].Success));
        var runs = new List<PlanRun>();
        var run = new PlanRun();
        runs.Add(run);
        while (tokens.TryDequeue(out var token))
        {
            if (token.Groups["separator"].Success)
            {
                run = new PlanRun();
                runs.Add(run);
                continue;
            }

            switch (token.Value)
            {
                case "line:":
                    var line = Next("a text or *", "text", "star");
                    run.Steps.Add(new ExpectLine(line.Groups["text"].Success ? Unquote(line) : null));
                    break;
                case "option:":
                    var option = Unquote(Next("a text", "text"));
                    var disabled = tokens.TryPeek(out var after) && after.Groups["disabled"].Success;
                    if (disabled)
                        tokens.Dequeue();
                    run.Steps.Add(new ExpectOption(option, !disabled));
                    break;
                case "command:":
                    run.Steps.Add(new ExpectCommand(Unquote(Next("a text", "text"))));
                    break;
                case "stop":
                    run.Steps.Add(new ExpectStop());
                    break;
                case "select:":
                    run.Steps.Add(new SelectOption(int.Parse(Next("a number", "number").Value, CultureInfo.InvariantCulture)));
                    break;
                case "set:":
                    var name = Next("a variable", "variable").Value;
                    Next("=", "equals");
                    var value = Next("true, false or a number", "word", "number");
                    run.Steps.Add(new SetVariable(name, value.Groups["number"].Success ? float.Parse(value.Value, CultureInfo.InvariantCulture) : bool.Parse(value.Value)));
                    break;
                case "saliency:":
                    run.Steps.Add(new SetSaliency(Next("a mode", "word").Value));
                    break;
                case "node:":
                    if (run.Steps.Count > 0)
                        throw new FormatException("A test plan names its node at the start of a run.");
                    run.StartNode = Next("a node", "word").Value;
                    break;
                default:
                    throw new FormatException($"Unexpected '{token.Value}' in the test plan.");
            }
        }

        return runs;

        Match Next(string what, params string[] kinds)
        {
            if (tokens.TryDequeue(out var next) && kinds.Any(k => next.Groups[k].Success))
                return next;

            throw new FormatException($"The test plan has '{next?.Value}' where {what} was expected.");
        }
    }

    private static string Unquote(Match text) => text.Value.Trim('`');

    [GeneratedRegex("""
        (?<text>`[^`]*`)
        |(?<skip>\#[^\r\n]*|\s+)
        |(?<separator>---)
        |(?<keyword>(line|option|command|select|set|saliency|node):)
        |(?<disabled>\[disabled\])
        |(?<variable>\$[A-Za-z_][A-Za-z0-9_]*)
        |(?<word>[A-Za-z_][A-Za-z0-9_]*)
        |(?<number>[0-9]+)
        |(?<star>\*)
        |(?<equals>=)
        |(?<other>.)
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Tokens();
}
