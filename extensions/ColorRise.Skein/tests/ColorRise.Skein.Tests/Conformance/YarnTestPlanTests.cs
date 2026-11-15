using ColorRise.Skein.Assets.Compilation;
using ColorRise.Skein.Tests.Session;
using Stride.Core.Diagnostics;
using Xunit.Sdk;

namespace ColorRise.Skein.Tests.Conformance;

/// <summary>
/// Yarn Spinner's own test scripts, run by Skein's session against Yarn Spinner's own test plans: what Yarn Spinner's
/// runner says, Skein's says.
/// </summary>
public sealed class YarnTestPlanTests
{
    private static readonly string TestCases = Path.Combine(AppContext.BaseDirectory, "Conformance", "TestCases");

    /// <summary>
    /// The scripts with a test plan.
    /// </summary>
    public static TheoryData<string> Plans() => [.. Directory.GetFiles(TestCases, "*.testplan").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal)!];

    /// <summary>
    /// The scripts Yarn Spinner's tests expect the compiler to refuse: the ones without a test plan.
    /// </summary>
    public static TheoryData<string> Refused()
    {
        var withoutPlan = Directory.GetFiles(TestCases, "*.yarn").Where(f => !File.Exists(Path.ChangeExtension(f, ".testplan")));
        var failures = Directory.GetFiles(Path.Combine(TestCases, "ParseFailures"), "*.yarn");
        return [.. withoutPlan.Concat(failures).Select(f => Path.GetRelativePath(TestCases, f).Replace('\\', '/')).Order(StringComparer.Ordinal)];
    }

    [Fact]
    public void The_test_cases_are_all_there()
    {
        Assert.Equal(32, Plans().Count);
        Assert.Equal(34, Refused().Count);
    }

    [Theory]
    [MemberData(nameof(Plans))]
    public async Task A_script_of_Yarn_Spinner_runs_as_its_test_plan_says(string name)
    {
        var actions = Functions();
        var compilation = Compile(Path.Combine(TestCases, name + ".yarn"), actions);
        Assert.DoesNotContain(compilation.Diagnostics, d => d.Severity == YarnSeverity.Error);
        var program = compilation.Program;
        Assert.NotNull(program);

        // As in Yarn Spinner's tests: a script without a Start node is only compiled
        if (!program.Nodes.ContainsKey("Start"))
            return;

        var log = new LoggerResult();
        var variables = new VariableStorage(log);
        var responder = new PlanResponder();
        var session = new DialogueSession(program, variables, TestDialogue.LinesOf(compilation, log), actions, responder, new DialogueSessionOptions
        {
            Saliency = YarnSaliency.BestLeastRecentlyViewed,
            Log = log,
            Nodes = TestDialogue.NodesOf(compilation),
        });
        responder.Session = session;

        // The variables and the saliency stay from one run of the plan to the next
        foreach (var run in YarnTestPlan.Parse(File.ReadAllText(Path.Combine(TestCases, name + ".testplan"))))
        {
            await responder.RunAsync(run);
        }

        Assert.Empty(log.Messages.Where(m => m.Type >= LogMessageType.Warning).Select(m => m.Text));
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public void A_script_Yarn_Spinner_refuses_gives_errors_with_a_place(string name)
    {
        var compilation = Compile(Path.Combine(TestCases, name), Functions());

        Assert.Contains(compilation.Diagnostics, d => d.Severity == YarnSeverity.Error);
        Assert.Null(compilation.Program);
        Assert.All(compilation.Diagnostics, d =>
        {
            Assert.True(d.Line >= 1 && d.Column >= 1, d.ToString());
            Assert.True(d.EndLine > d.Line || (d.EndLine == d.Line && d.EndColumn >= d.Column), d.ToString());
        });
    }

    private static YarnCompilation Compile(string path, YarnActionRegistry actions) => YarnCompilerBridge.Compile(new YarnCompilationRequest
    {
        Files = [(Path.GetFileName(path), File.ReadAllText(path))],
        Functions = [.. actions.Describe().Where(a => a.Kind == YarnActionKind.Function).Select(YarnFunctionDeclaration.From)],
    });

    /// <summary>
    /// The functions Yarn Spinner's tests give their scripts.
    /// </summary>
    private static YarnActionRegistry Functions()
    {
        var actions = new YarnActionRegistry();
        actions.AddFunction("assert", (bool value) => value ? true : throw new XunitException("An assert of the script does not hold."));
        actions.AddFunction("dummy_bool", () => true);
        actions.AddFunction("dummy_number", () => 1f);
        actions.AddFunction("dummy_string", () => "string");
        actions.AddFunction("add_three_operands", (int a, int b, int c) => a + b + c);
        actions.AddFunction("set_objective_complete", (string objective) => true);
        actions.AddFunction("is_objective_active", (string objective) => true);
        actions.AddFunction("get_quest_status", (string questName) => "InProgress");
        return actions;
    }

    /// <summary>
    /// Answers the dialogue as a run of a test plan says, and fails the test where the dialogue says something else.
    /// </summary>
    private sealed class PlanResponder : IDialogueResponder
    {
        private List<PlanStep> steps = [];
        private int next;
        private bool stoppedByThePlan;

        public DialogueSession Session { get; set; } = null!;

        private PlanStep? Expected => next < steps.Count ? steps[next] : null;

        public async Task RunAsync(PlanRun run)
        {
            steps = run.Steps;
            next = 0;
            stoppedByThePlan = false;
            Act();

            // A plan that expects nothing more does not go on: Yarn Spinner's tests leave the dialogue there
            if (Expected is null)
                return;

            await Session.RunAsync(run.StartNode);
            if (stoppedByThePlan)
                return;

            if (Expected is not ExpectStop)
                throw new XunitException($"The plan expects {Expected}, and the dialogue ended.");
            next++;
            Assert.Null(Expected);
        }

        public ValueTask HandleLineAsync(LocalizedLine line, LineCancellation cancellation)
        {
            if (Expected is not ExpectLine expected)
                throw new XunitException($"The plan expects {Expected}, and the dialogue says the line \"{line.Text.Text}\".");

            if (expected.Text is not null)
                Assert.Equal(expected.Text, line.Text.Text);
            Advance();
            return default;
        }

        public ValueTask<int?> HandleOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
        {
            var offered = string.Join(" | ", options.Select(o => o.Line.Text.Text));
            var expected = new List<ExpectOption>();
            while (Expected is ExpectOption option)
            {
                expected.Add(option);
                next++;
            }

            if (Expected is not SelectOption select)
                throw new XunitException($"The plan expects {Expected}, and the dialogue offers the options {offered}.");

            Assert.Equal(expected.Select(e => e.Text), options.Select(o => o.Line.Text.Text));
            Assert.Equal(expected.Select(e => e.IsAvailable), options.Select(o => o.IsAvailable));
            if (options.Any(o => o.IsAvailable))
                Assert.Single(options, o => o.Index == select.Number - 1);
            else
                Assert.Equal(0, select.Number);

            Advance();
            return new ValueTask<int?>(select.Number == 0 ? null : select.Number - 1);
        }

        public ValueTask HandleCommandAsync(string commandText, CancellationToken cancellation)
        {
            if (Expected is not ExpectCommand expected)
                throw new XunitException($"The plan expects {Expected}, and the dialogue runs the command \"{commandText}\".");

            Assert.Equal(expected.Text, commandText);
            Advance();
            return default;
        }

        public ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds) => default;

        public ValueTask HandleNodeStartAsync(string node) => default;

        public ValueTask HandleNodeCompleteAsync(string node) => default;

        public ValueTask HandleDialogueCompleteAsync() => default;

        private void Advance()
        {
            next++;
            Act();
            if (Expected is null)
            {
                stoppedByThePlan = true;
                Session.Stop();
            }
        }

        /// <summary>
        /// Does what the plan does between two things the dialogue says.
        /// </summary>
        private void Act()
        {
            for (; ; next++)
            {
                switch (Expected)
                {
                    case SetVariable { Value: bool flag } set:
                        Session.Variables.SetValue(set.Name, flag);
                        break;
                    case SetVariable { Value: float number } set:
                        Session.Variables.SetValue(set.Name, number);
                        break;
                    case SetSaliency saliency:
                        Session.Saliency = saliency.Mode switch
                        {
                            "first" => YarnSaliency.First,
                            "best" => YarnSaliency.Best,
                            "best_least_recently_seen" => YarnSaliency.BestLeastRecentlyViewed,
                            _ => throw new FormatException($"The test plan asks for the saliency '{saliency.Mode}'."),
                        };
                        break;
                    default:
                        return;
                }
            }
        }
    }
}
