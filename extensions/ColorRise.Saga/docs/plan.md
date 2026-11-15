# ColorRise.Saga v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first version of ColorRise.Saga, a story system (plot, acts, scenarios, quests, dialogues) for Stride games with Game Studio (cross-platform) support, as a third-party extension that exercises the new plugin model.

**Architecture:** Four packages in `extensions/ColorRise.Saga/` that reach Stride only through packages from the local dev feed: a runtime (expressions, compiled content, story runtime, game system), an `Assets` companion (assets, content builder, compilers, templates), a neutral `Editor` companion (view models, story player, commands) and an `.Avalonia` companion (views). A sandbox game in the same folder references the runtime project.

**Tech Stack:** C# / .NET 10 (`net10.0`), Stride 4.5.3 dev packages, Avalonia (the version Stride's editor uses), xUnit (as Stride's tests).

**Spec:** `extensions/ColorRise.Saga/docs/design.md` (copy of /mnt/project-files/dialogue-extension/saga-design.md). Read it before any task.

## Global Constraints

- Nothing outside `extensions/ColorRise.Saga/` changes. A gap in Stride's plugin model goes to `docs/plugin-findings.md` (what was tried, what happened, workaround kept inside Saga, change suggested); the feature waits rather than Stride being patched.
- Stride is referenced by `PackageReference` only, never `ProjectReference` into `sources/`. The version is never committed: `Directory.Build.props` reads `$(SagaStrideVersion)` from a git-ignored `Directory.Build.local.props`, falling back to the version of the engine checkout the folder sits in (`MajorMinor.Patch` of `sources/shared/SharedAssemblyInfo.cs` plus the `-devN` suffix the dev build used).
- Target `net10.0`; not .NET 11.
- Explicit access modifiers; classes `sealed` by default. Nullable enabled.
- Avalonia code: no mention of WPF (comments, docs, test names); `MultiBinding` only inline `{sd:MultiBinding {Binding A}, {Binding B}, Converter=...}`.
- Experimental Stride APIs: opt in per project with `<NoWarn>$(NoWarn);STREXP001;STREXP003;STREXP006</NoWarn>` (only the ids used).
- Ids: commands `ColorRise.Saga.<Name>`, panel `ColorRise.Saga.StoryPlayer`. Asset files `.sdsaga` (story), `.sdsagadialog` (dialogue). Asset format version key `ColorRise.Saga`, starting at `1.0.0.0`.
- Expression symbols: quest states `not_started`, `available`, `active`, `succeeded`, `failed`; scenario states `pending`, `running`, `done`; built-ins `raised("event")`, `visited("name")`, `visits("name")`.
- Tests start nothing they don't stop and clean up (games, editors, temp folders).
- Git: branch `feature/colorrise-saga` from the fork's `master`; `git config user.name "Nicolas Musset"`, `user.email "musset.nicolas@gmail.com"` before the first commit; conventional titles `feat(saga): ...`, `test(saga): ...`, `fix(saga): ...`; no `Co-Authored-By` line, no Claude link.
- Sandbox setup: reuse an existing .NET SDK, never `curl | bash` or downloaded scripts, one plain step per command, no `&` background jobs.

## Review Focus

1. **A story that never settles** (an act link and an action that undo each other): settling stops after 100 passes and throws `SagaException` naming the cards that changed in the last pass. Test in Task 6.
2. **A choices node whose choices are all hidden or disabled**: the session doesn't hang; it ends the dialogue (`IsEnded == true`) and logs a warning naming the dialogue and node. Test in Task 7.
3. **Restoring a saved state into an edited story** (a variable or quest removed, another added): unknown entries are dropped with a warning, new ones take their defaults, nothing throws. Test in Task 6.
4. **A declared function the game never registered**: evaluation logs an error naming the function once and returns `false` / `0` / `""` by return type. Test in Task 4.
5. **A broken expression in an asset**: the compiler fails with the asset URL, the card (act, scenario, quest, node) and the position; the story player shows the same message and does not start. Tests in Tasks 9 and 13.

---

### Task 0: Workspace and engine build (no commit)

**Files:** none in git.

- [ ] **Step 1:** Install a .NET 10 SDK with no approval prompt. Read `https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json`, download the latest `dotnet-sdk-<v>-linux-x64.tar.gz` to the scratchpad, check its sha512 against the json, extract to `/root/.dotnet`, use `PATH=/root/.dotnet:$PATH DOTNET_ROOT=/root/.dotnet`. If that host is blocked: `apt-get install -y dotnet-sdk-10.0` plus Roslyn 5.3 from api.nuget.org (see project memory `linux-build-setup`).
- [ ] **Step 2:** Pull LFS: `git -c lfs.url=https://github.com/Kryptos-FR/stride.git/info/lfs lfs pull --include='deps/**'`, then the same for `sources/**`.
- [ ] **Step 3:** `dotnet build sources/Stride.Editor.Avalonia.slnf` (dev package redirect is on by default for local builds).
- [ ] **Step 4:** Verify the dev feed (`~/.local/share/stride/nugetdev/`) has stubs for `Stride.Engine`, `Stride.Core.Assets`, `Stride.AssetCompiler`, `Stride.Assets.Editor`, `Stride.Assets.Editor.Avalonia`, `Stride.Core.Presentation.Avalonia`, and note the `-devN` version. A missing package is a finding (Task 1 records it).

### Task 1: Scaffold the extension

**Files:**
- Create: `extensions/ColorRise.Saga/ColorRise.Saga.slnx`, `Directory.Build.props`, `Directory.Build.local.props.example`, `.gitignore` (ignores `Directory.Build.local.props`, `bin/`, `obj/`), `nuget.config` (dev feed `~/.local/share/stride/nugetdev` then nuget.org), `README.md`
- Create: `docs/design.md` (the spec), `docs/plugin-findings.md` (empty table: Finding, What Saga tried, What happened, Workaround, Suggested change)
- Create: `src/ColorRise.Saga/ColorRise.Saga.csproj` (declares both companions with `StrideCompanionProject`), `src/ColorRise.Saga.Assets/ColorRise.Saga.Assets.csproj` (`StridePackageKind` Assets) + `ColorRise.Saga.Assets.sdpkg`, `src/ColorRise.Saga.Editor/ColorRise.Saga.Editor.csproj` (`Editor`, declares the `.Avalonia` companion) + `SagaEditorPlugin.cs`, `src/ColorRise.Saga.Editor.Avalonia/ColorRise.Saga.Editor.Avalonia.csproj` (`Editor`, `StrideEditorToolkit` Avalonia) + `SagaEditorAvaloniaPlugin.cs`, `tests/ColorRise.Saga.Tests/ColorRise.Saga.Tests.csproj`

**Interfaces:**
- Produces: `public sealed class SagaEditorPlugin : StrideAssetsPlugin` (`ColorRise.Saga.Editor`), `public sealed class SagaEditorAvaloniaPlugin : StrideAssetsAvaloniaPlugin` (`ColorRise.Saga.Editor.Avalonia`), both overriding `Initialize(IEditorPluginContext)` and calling the base. Project shapes follow `samples/Plugin/Plugin/*/*.csproj` and `sources/plugins/UI/Stride.UI.Editor.Avalonia/*.csproj`, with `PackageReference Version="$(SagaStrideVersion)"`.

- [ ] **Step 1:** Create the branch from `master` and set the git identity.
- [ ] **Step 2:** Write the files above; `dotnet build extensions/ColorRise.Saga/ColorRise.Saga.slnx` → 0 errors, and `dotnet pack` of the Editor package records `StrideEditorToolkit` neutral, of `.Avalonia` records Avalonia (check the generated `.nuspec`/props).
- [ ] **Step 3:** Commit `feat(saga): scaffold the ColorRise.Saga extension`.

### Task 2: Expression syntax and parser

**Files:**
- Create: `src/ColorRise.Saga/Expressions/SagaValue.cs`, `SagaType.cs`, `SagaExpression.cs` (nodes), `SagaStatement.cs` (statements), `SagaDiagnostic.cs`, `SagaLexer.cs`, `SagaParser.cs`
- Test: `tests/ColorRise.Saga.Tests/Expressions/SagaParserTests.cs`

**Interfaces:**
- Produces (namespace `ColorRise.Saga.Expressions`, all `[DataContract]` so content can store them):
  - `public enum SagaValueKind { Bool, Int, Float, String, Symbol }`; `public readonly struct SagaValue` with `Kind`, `static Bool(bool)`, `Int(int)`, `Float(float)`, `String(string)`, `Symbol(string)`, `AsBool()`, `AsInt()`, `AsFloat()`, `AsString()`, value equality.
  - `public enum SagaTypeKind { Bool, Int, Float, String, QuestState, ScenarioState, Outcome, Act }`
  - `public abstract class SagaExpression`; sealed `LiteralExpression(SagaValue Value)`, `PathExpression(string Path)` (dotted), `CallExpression(string Function, List<SagaExpression> Arguments)`, `UnaryExpression(SagaUnaryOperator, SagaExpression)`, `BinaryExpression(SagaBinaryOperator, SagaExpression, SagaExpression)`; each node keeps `int Position`, `int Length`.
  - `public abstract class SagaStatement`; sealed `AssignStatement(string Path, SagaExpression Value)`, `RaiseStatement(string Event)`, `EmitStatement(string Signal, List<SagaExpression> Arguments)`.
  - `public sealed record SagaDiagnostic(int Position, int Length, string Message)`
  - `public static class SagaParser { ParseResult<SagaExpression?> ParseCondition(string? source); ParseResult<List<SagaStatement>> ParseActions(string? source); }`, `public sealed record ParseResult<T>(T Value, IReadOnlyList<SagaDiagnostic> Diagnostics)`. Empty or whitespace source: condition `null` (means true), actions empty. Operators: `|| && ! == != < <= > >= + -`, parentheses; statements separated by `;` or new lines.

- [ ] **Step 1: Write the failing tests**: `ParsesComparisonAndLogic` (`quest.secure_position == failed && day >= 12` → Binary(And, Binary(Equal, Path, Path), Binary(GreaterOrEqual, Path, Literal 12))), `ParsesCallWithArguments` (`has_item("sword")`), `PrecedenceAndBeforeOr`, `EmptyConditionIsNull`, `ParsesActions` (`gold = gold + 10; raise mine_collapsed\nemit give_item("sword", 2)` → 3 statements), `ReportsPositionOfError` (`day >= ` → one diagnostic at position 7), `ReportsUnterminatedString`.
- [ ] **Step 2:** Run `dotnet test extensions/ColorRise.Saga/tests/ColorRise.Saga.Tests --filter SagaParserTests` → FAIL (types missing).
- [ ] **Step 3:** Implement a hand-written lexer and recursive-descent parser.
- [ ] **Step 4:** Same command → PASS.
- [ ] **Step 5:** Commit `feat(saga): parse conditions and actions`.

### Task 3: Declarations and type checking

**Files:**
- Create: `src/ColorRise.Saga/Content/StoryDeclarations.cs` (declaration classes), `src/ColorRise.Saga/Expressions/ISagaScope.cs`, `SagaTypeChecker.cs`
- Test: `tests/ColorRise.Saga.Tests/Expressions/SagaTypeCheckerTests.cs`

**Interfaces:**
- Produces (namespace `ColorRise.Saga.Content`, `[DataContract]`): `StoryDeclarations { List<VariableDeclaration> Variables; List<EventDeclaration> Events; List<FunctionDeclaration> Functions; List<SignalDeclaration> Signals; List<CharacterDeclaration> Characters; List<AttributeTypeDeclaration> AttributeTypes }`; `VariableDeclaration { string Name; SagaTypeKind Type; string Default }` (default as expression text, a literal), `EventDeclaration { string Name }`, `FunctionDeclaration { string Name; List<SagaTypeKind> Parameters; SagaTypeKind ReturnType }`, `SignalDeclaration { string Name; List<SagaTypeKind> Parameters }`, `CharacterDeclaration { string Id; string DisplayName }`, `AttributeTypeDeclaration { string Name; SagaTypeKind Type; SagaCardKinds AppliesTo }`, `[Flags] enum SagaCardKinds { Act = 1, Scenario = 2, Quest = 4, Dialogue = 8 }`, `AttributeValue { string Name; string Value }`.
- Produces (namespace `ColorRise.Saga.Expressions`): `public interface ISagaScope { bool TryGetPathType(string path, out SagaTypeKind type, out string? symbolSet); FunctionDeclaration? FindFunction(string name); bool IsEvent(string name); SignalDeclaration? FindSignal(string name); bool IsAssignable(string path); IReadOnlyCollection<string> GetSymbols(string symbolSet); }` and `public sealed class SagaTypeChecker(ISagaScope scope) { IReadOnlyList<SagaDiagnostic> CheckCondition(SagaExpression? expression); IReadOnlyList<SagaDiagnostic> CheckActions(IReadOnlyList<SagaStatement> statements); }`.
- Paths: declared variable names (may be dotted, e.g. `player.strength`), `act` (Act), `quest.<id>.state` (QuestState), `quest.<id>.stage` (Int), `scenario.<id>.state` (ScenarioState), `scenario.<id>.outcome` (Outcome of that scenario). A bare identifier compared with a symbol-typed side is a symbol of that set. Assignable: variables, `quest.<id>.state`, `quest.<id>.stage`. Int widens to Float.

- [ ] **Step 1: Write the failing tests** with a fake `ISagaScope`: `UnknownNameIsReported` (message contains the name), `SymbolMustBelongToSet` (`quest.q.state == finished` → error listing the five states), `ConditionMustBeBool` (`day + 1`), `FunctionArgumentsAreChecked` (count and types), `ScenarioStateIsNotAssignable`, `RaiseNeedsDeclaredEvent`, `EmitChecksSignalParameters`, `IntWidensToFloat` (`speed > 2` with `speed: Float` → no diagnostic).
- [ ] **Step 2:** Run `--filter SagaTypeCheckerTests` → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** → PASS. **Step 5:** Commit `feat(saga): check expressions against the story's declarations`.

### Task 4: Evaluation behind one interface

**Files:**
- Create: `src/ColorRise.Saga/Expressions/ISagaCondition.cs`, `ISagaAction.cs`, `ISagaEvaluationContext.cs`, `SagaCondition.cs`, `SagaActions.cs`, `SagaTreeEvaluator.cs`
- Test: `tests/ColorRise.Saga.Tests/Expressions/SagaEvaluatorTests.cs`

**Interfaces:**
- Produces: `public interface ISagaCondition { bool Evaluate(ISagaEvaluationContext context); }`, `public interface ISagaAction { void Execute(ISagaExecutionContext context); }`, `public interface ISagaEvaluationContext { SagaValue Resolve(string path); SagaValue Call(string function, IReadOnlyList<SagaValue> arguments); int EventCount(string name); int Visits(string name); }`, `public interface ISagaExecutionContext : ISagaEvaluationContext { void Assign(string path, SagaValue value); void Raise(string name); void Emit(string signal, IReadOnlyList<SagaValue> arguments); }`.
- Produces `[DataContract] public sealed class SagaCondition : ISagaCondition { string Source; SagaExpression? Tree; }` (null tree = true) and `[DataContract] public sealed class SagaActions : ISagaAction { string Source; List<SagaStatement> Statements; }`, both evaluated by `SagaTreeEvaluator` (internal). This is the seam a source generator replaces later.

- [ ] **Step 1: Write the failing tests** with a dictionary-backed fake context: `EvaluatesLogicAndComparison`, `ShortCircuits` (`false && f()` doesn't call `f`), `BuiltInsReadEventsAndVisits` (`raised("x")`, `visits("d") >= 2`), `ActionsAssignRaiseEmitInOrder`, `UnregisteredFunctionReturnsDefault` (Review Focus 4: context returns default and logs once; assert result `false` and one log entry for two calls), `NullTreeIsTrue`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS. **Step 5:** Commit `feat(saga): evaluate conditions and actions`.

### Task 5: Compiled content and story state

**Files:**
- Create: `src/ColorRise.Saga/Content/StoryDefinition.cs`, `PlotDefinition.cs`, `ActDefinition.cs`, `ScenarioDefinition.cs`, `QuestDefinition.cs`, `DialogueDefinition.cs`, `DialogueNodes.cs`; `src/ColorRise.Saga/SagaState.cs`; `src/ColorRise.Saga/AssemblyInfo.cs` (`[assembly: AssetFileExtension(".sdsaga", typeof(StoryDefinition))]`, `(".sdsagadialog", typeof(DialogueDefinition))`)
- Test: `tests/ColorRise.Saga.Tests/Content/SerializationTests.cs`

**Interfaces:**
- Produces (namespace `ColorRise.Saga.Content`, `[DataContract]`):
  - `StoryDefinition` (content type: `[ContentSerializer(typeof(DataContentSerializer<StoryDefinition>))]`, `[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<StoryDefinition>), Profile = "Content")]`): `string Name; StoryDeclarations Declarations; PlotDefinition Plot`.
  - `PlotDefinition { string FirstActId; List<ActDefinition> Acts }` (an object of its own so sub-plots can come later).
  - `ActDefinition { string Id; string Title; SagaCondition EndWhen; List<ActLink> Next; List<ScenarioDefinition> Scenarios; List<QuestDefinition> Quests; List<AttributeValue> Attributes }`, `ActLink { string TargetActId; SagaCondition When }`.
  - `ScenarioDefinition { string Id; string Title; SagaCondition StartWhen; SagaCondition EndWhen; bool ContinuesAcrossActs; SagaActions OnStart; SagaActions OnEnd; List<ScenarioOutcome> Outcomes; List<DialogueDefinition> Dialogues; List<AttributeValue> Attributes }`, `ScenarioOutcome { string Name; SagaCondition When }`.
  - `QuestDefinition { string Id; string Title; string? ScenarioId; SagaCondition OfferWhen; bool StartsAutomatically; List<QuestStage> Stages; SagaCondition SucceedWhen; SagaCondition FailWhen; SagaActions OnActivate; SagaActions OnSucceed; SagaActions OnFail; List<DialogueDefinition> Dialogues; List<AttributeValue> Attributes }`, `QuestStage { string Id; string Title; SagaCondition DoneWhen; SagaActions OnDone }`.
  - `DialogueDefinition` (content type, same attributes as `StoryDefinition`): `string Name; string CharacterId; SagaCondition When; int Priority; bool Once; bool OnlyWhileOwnerLive = true; List<string> Participants; string EntryNodeId; List<DialogueNode> Nodes; List<AttributeValue> Attributes`. No reference back to the story.
  - `abstract DialogueNode { string Id; string? Name }`; sealed `LineNode { string SpeakerId; string Text; SagaActions Actions; string? NextId }`, `ChoicesNode { List<DialogueChoice> Choices }`, `DialogueChoice { string Text; SagaCondition When; bool HideWhenFalse = true; bool Once; SagaActions Actions; string? TargetId }`, `BranchNode { List<DialogueLink> Links }`, `DialogueLink { SagaCondition When; string TargetId }`, `EndNode`.
- Produces (namespace `ColorRise.Saga`, `[DataContract]`): `public enum QuestState { NotStarted, Available, Active, Succeeded, Failed }`, `public enum ScenarioState { Pending, Running, Done }`, `SagaState { string? CurrentActId; List<string> CompletedActs; Dictionary<string, SagaValue> Variables; Dictionary<string, QuestProgress> Quests; Dictionary<string, ScenarioProgress> Scenarios; Dictionary<string, int> Events; Dictionary<string, int> Visits; List<string> UsedOnce }`, `QuestProgress { QuestState State; int Stage }`, `ScenarioProgress { ScenarioState State; string? Outcome }`.

- [ ] **Step 1: Write the failing tests**: `StoryDefinitionRoundTrips` and `SagaStateRoundTrips` through Stride's binary serializer (`BinarySerialization.Write/Read` on a `MemoryStream`), checking a condition tree, a choices node and a state with every dictionary filled.
- [ ] **Step 2:** FAIL. **Step 3:** Implement the data types. **Step 4:** PASS. **Step 5:** Commit `feat(saga): story content and saveable state`.

### Task 6: Story runtime: hooks and settling

**Files:**
- Create: `src/ColorRise.Saga/SagaRuntime.cs`, `SagaRuntime.Settling.cs`, `SagaSignal.cs`, `SagaChangedEventArgs.cs`, `SagaException.cs`
- Test: `tests/ColorRise.Saga.Tests/Runtime/SagaRuntimeTests.cs`, `tests/ColorRise.Saga.Tests/Runtime/StoryBuilder.cs` (a small fluent helper that builds `StoryDefinition`s in tests by parsing condition text)

**Interfaces:**
- Consumes: Tasks 2-5.
- Produces (namespace `ColorRise.Saga`): `public sealed class SagaRuntime { SagaRuntime(StoryDefinition story, SagaState? state = null, ILogger? logger = null); StoryDefinition Story { get; } SagaState State { get; } void Start(); void Raise(string eventName); void Set(string variable, SagaValue value); SagaValue Get(string variable); void RegisterFunction(string name, Func<IReadOnlyList<SagaValue>, SagaValue> function); IDisposable On(string signal, Action<SagaSignal> handler); SagaState CaptureState(); event EventHandler<SagaChangedEventArgs>? Changed; }`, `public sealed record SagaSignal(string Name, IReadOnlyList<SagaValue> Arguments)`, `public sealed record SagaChangedEventArgs(SagaChangeKind Kind, string Id)` with `enum SagaChangeKind { ActEntered, ActEnded, ScenarioStarted, ScenarioEnded, QuestAvailable, QuestActivated, QuestStageDone, QuestSucceeded, QuestFailed }`. Unknown names in hooks throw `ArgumentException`. `ILogger` is Stride's `Stride.Core.Diagnostics.ILogger`.
- Settling (design §8): repeat until a pass changes nothing, at most 100 passes (then `SagaException`, Review Focus 1): start pending scenarios whose `StartWhen` holds; end running ones whose `EndWhen` holds, keeping the first outcome whose condition holds; quests `NotStarted → Available` on `OfferWhen`, `→ Active` if `StartsAutomatically`; active quests finish stages in order on `DoneWhen`, then `Succeeded`/`Failed` on their conditions; when the act's `EndWhen` holds: end its scenarios without `ContinuesAcrossActs`, fail their active quests, enter the first `Next` link whose condition holds (none: the story is over, `CurrentActId` null). Actions run at each change. `raise` inside an action counts the event and continues the same settling.

- [ ] **Step 1: Write the failing tests**: `StartEntersFirstAct`, `RaiseStartsScenario`, `ScenarioEndKeepsFirstMatchingOutcome`, `QuestOfferedThenAcceptedByAction`, `QuestStagesAdvanceInOrder`, `QuestFailsWhenItsScenarioEndsWithTheAct`, `ContinuingScenarioSurvivesActChange`, `ActBranchFollowsFirstTrueLink` (`sided_with == "guild"`), `SignalsReachHandlersInOrder`, `UnknownVariableThrows`, `SettlingThatNeverStopsThrows` (Review Focus 1; message names the cards), `RestoreDropsUnknownAndDefaultsNew` (Review Focus 3), `CaptureThenRestoreGivesSameBehavior`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS. **Step 5:** Commit `feat(saga): run the story: hooks, settling, saving`.

### Task 7: Dialogues at runtime

**Files:**
- Create: `src/ColorRise.Saga/DialogueSession.cs`, `DialogueLine.cs`, `DialogueChoiceView.cs`; modify `src/ColorRise.Saga/SagaRuntime.cs` (add `Talk`)
- Test: `tests/ColorRise.Saga.Tests/Runtime/DialogueTests.cs`

**Interfaces:**
- Produces: `SagaRuntime.Talk(string characterId) : DialogueSession?` (highest `Priority` among dialogues of that character whose `When` holds, not used up if `Once`, and whose owner is live unless `OnlyWhileOwnerLive` is false: a scenario's while `Running`, a quest's while `Available` or `Active`; ties: declaration order). `public sealed class DialogueSession { DialogueDefinition Dialogue { get; } DialogueLine? CurrentLine { get; } IReadOnlyList<DialogueChoiceView> Choices { get; } bool IsEnded { get; } void Continue(); void Choose(int index); }`, `public sealed record DialogueLine(string SpeakerId, string Text)` (text with `{variable}` substituted), `public sealed record DialogueChoiceView(int Index, string Text, bool IsEnabled)`. Visits count `<dialogue name>` and `<dialogue name>.<node name>` for named nodes; each action settles the story.

- [ ] **Step 1: Write the failing tests**: `TalkPicksHighestPriorityActive`, `QuestGiverDialogueAvailableBeforeQuestIsActive`, `OwnerGateCanBeTurnedOff`, `OnceDialogueIsUsedUp`, `HubLoopsAndOnceChoiceDisappears`, `DisabledChoiceShownButNotChoosable` (`Choose` on it throws `InvalidOperationException`), `AllChoicesHiddenEndsDialogue` (Review Focus 2), `BranchTakesFirstTrueLink`, `ChoiceActionAdvancesQuest`, `TextSubstitutesVariables`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS. **Step 5:** Commit `feat(saga): play dialogues`.

### Task 8: Game system and character component

**Files:**
- Create: `src/ColorRise.Saga/SagaSystem.cs`, `src/ColorRise.Saga/SagaCharacterComponent.cs`; Create: `src/ColorRise.Saga.Assets/EditorImages.cs` + `Resources/SagaCharacterComponent.png` (`[assembly: TypeImage(typeof(SagaCharacterComponent), "SagaCharacterComponent.png")]`)
- Test: `tests/ColorRise.Saga.Tests/Runtime/SagaSystemTests.cs`

**Interfaces:**
- Produces: `[GameSystem] public sealed class SagaSystem : GameSystemBase { SagaSystem(IServiceRegistry registry); SagaRuntime? Runtime { get; } SagaRuntime Load(StoryDefinition story, SagaState? state = null); }` (logs through `GlobalLogger.GetLogger("Saga")`). `[DataContract("SagaCharacterComponent")] [ComponentCategory("Saga")] public sealed class SagaCharacterComponent : EntityComponent { string CharacterId { get; set; } DialogueSession? Talk(); }` (`Talk` finds `SagaSystem` in the entity's game services; null when no story is loaded).

- [ ] **Step 1: Write the failing test** `LoadReplacesRuntime` (a `SagaSystem` built on a bare `ServiceRegistry`, no game started).
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS. **Step 5:** Commit `feat(saga): game system and character component`.

### Task 9: Assets and the content builder

**Files:**
- Create: `src/ColorRise.Saga.Assets/StoryAsset.cs`, `StoryCards.cs`, `DialogueAsset.cs`, `DialogueCards.cs`, `SagaContentBuilder.cs`, `StoryAssetScope.cs`, `SagaBuildDiagnostic.cs`
- Test: `tests/ColorRise.Saga.Tests/Assets/SagaContentBuilderTests.cs` (the test project references the Assets project)

**Interfaces:**
- Produces (namespace `ColorRise.Saga.Assets`):
  - `[DataContract("SagaStoryAsset")] [AssetDescription(".sdsaga")] [AssetContentType(typeof(StoryDefinition))] [AssetFormatVersion("ColorRise.Saga", "1.0.0.0")] [Display(1000, "Saga story")] public sealed class StoryAsset : Asset { StoryDeclarations Declarations; string FirstActId; List<ActCard> Acts; }`. Cards mirror the definitions with conditions and actions as `string`, plus `Vector2 Position` on every card: `ActCard`, `ActLinkCard`, `ScenarioCard`, `ScenarioOutcomeCard`, `QuestCard`, `QuestStageCard`. `ScenarioCard.Dialogues` and `QuestCard.Dialogues` are `List<DialogueDefinition>` (content references, edited with the asset picker).
  - `[DataContract("SagaDialogueAsset")] [AssetDescription(".sdsagadialog")] [AssetContentType(typeof(DialogueDefinition))] [AssetFormatVersion("ColorRise.Saga", "1.0.0.0")] [Display(1001, "Saga dialogue")] public sealed class DialogueAsset : Asset { AssetReference? Story; string CharacterId; string When; int Priority; bool Once; bool OnlyWhileOwnerLive = true; List<string> Participants; string EntryNodeId; List<DialogueNodeCard> Nodes; List<AttributeValue> Attributes; }` with node cards mirroring Task 5's nodes plus `Vector2 Position`. `Story` is an asset reference, not a content reference, so that compiled content has no cycle; if the property grid can't pick it, record a finding and expose it as a read-only field set by the story editor.
  - `public sealed record SagaBuildDiagnostic(string Asset, string Card, int Position, string Message)`; `public sealed class StoryAssetScope : ISagaScope` (from a `StoryAsset`).
  - `public static class SagaContentBuilder { StoryDefinition BuildStory(StoryAsset story, string assetUrl, ICollection<SagaBuildDiagnostic> diagnostics); DialogueDefinition BuildDialogue(DialogueAsset dialogue, string assetUrl, StoryAsset story, ICollection<SagaBuildDiagnostic> diagnostics); }` Card ids are checked for duplicates and for being identifiers; dialogue references stay as given (references when compiling, built definitions for the player).

- [ ] **Step 1: Write the failing tests**: `BuildsPlotWithParsedConditions`, `BrokenConditionNamesAssetCardAndPosition` (Review Focus 5: `Asset == "Story/Main"`, `Card == "act act_2 / scenario overlord / StartWhen"`, position of the bad token), `UnknownCharacterInDialogueIsReported`, `DuplicateIdsAreReported`, `DialogueChecksAgainstItsStory`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS. **Step 5:** Commit `feat(saga): story and dialogue assets`.

### Task 10: Compilers, templates and the sandbox game

**Files:**
- Create: `src/ColorRise.Saga.Assets/StoryAssetCompiler.cs`, `DialogueAssetCompiler.cs`, `Templates/Assets/SagaStory.sdtpl`, `Templates/Assets/SagaDialogue.sdtpl`; modify `ColorRise.Saga.Assets.sdpkg` (`TemplateFolders`)
- Create: `sandbox/SagaSandbox/` (a minimal Stride game laid out like `samples/NewGame`, Linux and Windows heads optional, `SagaSandbox.Game` referencing `../../src/ColorRise.Saga/ColorRise.Saga.csproj`), with assets `Story/Main.sdsaga` (two acts with a branch, one scenario with two outcomes, one quest with two stages), `Dialogues/Foreman.sdsagadialog` (hub with a loop, a once choice, a quest action), `Dialogues/Guards.sdsagadialog` (NPC to NPC), a scene with two entities holding `SagaCharacterComponent`, and a startup script that loads the story and logs a short playthrough; add to the slnx
- Test: building the sandbox

**Interfaces:**
- Consumes: Task 9's builder. `[AssetCompiler(typeof(StoryAsset), typeof(AssetCompilationContext))] public sealed class StoryAssetCompiler : AssetCompilerBase` (and the dialogue one), following `samples/Plugin/Plugin/MyTemplate.Assets/MyTemplateAssetCompiler.cs`; diagnostics become errors of the build step (asset, card, position, message). The dialogue compiler declares the story asset as an input so a change to the declarations recompiles the dialogues; if the compiler API can't express it, record a finding.

- [ ] **Step 1:** `dotnet build extensions/ColorRise.Saga/sandbox/SagaSandbox/SagaSandbox.Game` (with the asset compiler run the game build does) → succeeds, `StoryDefinition` and both `DialogueDefinition`s in the output database.
- [ ] **Step 2:** Break one condition in a scratch copy of the sandbox (not committed) → build fails with the asset URL, card and position (Review Focus 5); restore.
- [ ] **Step 3:** Run the sandbox's game head under Xvfb if the platform allows, check the playthrough log; otherwise note why.
- [ ] **Step 4:** Commit `feat(saga): compile stories and add the sandbox game`.

### Task 11: Editor view models

**Files:**
- Create in `src/ColorRise.Saga.Editor/`: `ViewModels/StoryViewModel.cs` (`[AssetViewModel<StoryAsset>]`), `ViewModels/DialogueViewModel.cs` (`[AssetViewModel<DialogueAsset>]`), `ViewModels/StoryEditorViewModel.cs` (`[AssetEditorViewModel<StoryViewModel>]`), `ViewModels/StoryGraphLevel.cs`, `ViewModels/SagaGraphNodeViewModel.cs`, `ViewModels/SagaGraphLinkViewModel.cs`, `ViewModels/DialogueEditorViewModel.cs` (`[AssetEditorViewModel<DialogueViewModel>]`)
- Test: `tests/ColorRise.Saga.Editor.Tests/` if Stride's editor test helpers are reachable as packages; otherwise the checks of Task 15, and a finding.

**Interfaces:**
- Produces: `StoryEditorViewModel : AssetEditorViewModel<StoryViewModel>` with `ObservableList<SagaGraphNodeViewModel> Nodes`, `ObservableList<SagaGraphLinkViewModel> Links`, `ObservableList<StoryGraphLevel> Breadcrumb`, `StoryGraphLevel CurrentLevel`, commands `OpenCommand(node)` (an act opens its scenarios and quests, a scenario or quest opens its dialogues, a dialogue opens its asset editor), `GoToLevelCommand(level)`, `AddActCommand`, `AddScenarioCommand`, `AddQuestCommand`, `AddDialogueCommand` (creates a `.sdsagadialog` next to the story, sets its `Story`, adds the reference to the owner), `LinkCommand(NodeGraphLinkRequest-like source/target ids)`, `DeleteSelectionCommand`, `SelectedNodes` (feeds the property grid). `StoryGraphLevel { StoryGraphDepth Depth (Plot, Act, Owner); string? ActId; string? OwnerId; string Title }`. `DialogueEditorViewModel` the same shape for nodes (line, choices, branch, end) and their links. Every change goes through the asset's property graph (one undo step per user action, dirty flag), as `GraphicsCompositorEditorViewModel` does; node moves write `Position`.

- [ ] **Step 1:** Write the editor tests that the harness allows (`OpeningAnActShowsItsScenariosAndQuests`, `BreadcrumbGoesBack`, `AddActIsOneUndoStep`, `LinkingActsAddsANextLink`); run → FAIL.
- [ ] **Step 2:** Implement. **Step 3:** PASS (or the Task 15 check recorded). **Step 4:** Commit `feat(saga): story and dialogue editor view models`.

### Task 12: Editor views (Avalonia)

**Files:**
- Create in `src/ColorRise.Saga.Editor.Avalonia/`: `Views/StoryEditorView.axaml(.cs)` (`[AssetEditorView<StoryEditorViewModel>]`, `IAssetEditorView`), `Views/DialogueEditorView.axaml(.cs)`, `Themes/SagaStyles.axaml`; modify `SagaEditorAvaloniaPlugin.cs` (register styles)

**Interfaces:**
- Consumes: Task 11. Uses `sd:NodeGraphControl` (`Stride.Core.Presentation.Avalonia.Controls`: `Nodes`, `Links`, `LinkResolver`, `NodeTemplate`, `SelectedNodes`, `LinkCommand`, `UnlinkCommand`, `AutoLayout=False`, `SetNodePosition`, `GetNodePosition`) with node templates per card kind (colors: plot indigo, act/quest teal, dialogue orange, as in the sketch), the breadcrumb above the graph, the property grid as in the other asset editors. Double-click a node runs `OpenCommand`.

- [ ] **Step 1:** Build → 0 errors. **Step 2:** Commit `feat(saga): story and dialogue editor views`.

### Task 13: Story player panel

**Files:**
- Create in `src/ColorRise.Saga.Editor/`: `Player/StoryPlayerViewModel.cs` (`: PanelViewModel`), `Player/SagaPlayback.cs` (builds a playable `StoryDefinition` from the session's assets with `SagaContentBuilder`, resolving dialogue references to built definitions, and runs a `SagaRuntime`), `Player/ManualFunctionViewModel.cs`; modify `SagaEditorPlugin.cs` (panel descriptor, `context.ViewTypes` comes from the Avalonia plugin)
- Create in `src/ColorRise.Saga.Editor.Avalonia/`: `Views/StoryPlayerView.axaml(.cs)`; modify `SagaEditorAvaloniaPlugin.cs` (`context.ViewTypes.Register(typeof(StoryPlayerViewModel), typeof(StoryPlayerView))` or the registry's actual method)
- Test: `tests/ColorRise.Saga.Tests/Player/SagaPlaybackTests.cs` (no editor needed)

**Interfaces:**
- Produces: panel `ColorRise.Saga.StoryPlayer`, title "Story player", hidden by default, placement right. `StoryPlayerViewModel(PanelContext context)` with `Stories` (story assets of the session), `SelectedStory`, `StartCommand`, `ResetCommand`, `Diagnostics` (when not empty, Start is disabled: Review Focus 5), `CurrentAct`, `Scenarios`, `Quests` (id, state, stage), `Variables` (editable), `Events` (each with `RaiseCommand`), `Functions` (each with a manual answer of its return type), `Characters` (each with `TalkCommand`), `Transcript` (lines), `Choices` (with `ChooseCommand`), `ContinueCommand`. `SagaPlayback { static SagaPlayback? Create(StoryAsset story, string url, Func<DialogueDefinition, (DialogueAsset Asset, string Url)?> resolveDialogue, ICollection<SagaBuildDiagnostic> diagnostics); SagaRuntime Runtime { get; } }`.

- [ ] **Step 1: Write the failing tests**: `PlaybackResolvesDialogueReferences`, `PlaybackWithBrokenExpressionReturnsDiagnostics` (null result, diagnostic as in Task 9).
- [ ] **Step 2:** FAIL. **Step 3:** Implement playback, panel and view. **Step 4:** PASS and build. **Step 5:** Commit `feat(saga): story player panel`.

### Task 14: Commands

**Files:**
- Create: `src/ColorRise.Saga.Editor/SagaCommands.cs`; modify `SagaEditorPlugin.cs` (register)

**Interfaces:**
- Produces: `public static class SagaCommands { const string PlayStory = "ColorRise.Saga.PlayStory"; const string OpenDialogue = "ColorRise.Saga.OpenDialogue"; static IEnumerable<CommandDescriptor> Create(); }`. PlayStory: title "Play story", category "Saga", placement `CommandLocations.AssetViewContext`, enabled when the selection is one story asset; shows the story player with that story selected and started. OpenDialogue: title "Open dialogue", placement `SceneLocations.EntityTreeContext`, enabled on one entity with a `SagaCharacterComponent`; opens the editor of that character's highest-priority dialogue in the session (a list to pick from if several). Both in the palette.

- [ ] **Step 1:** Unit-test what doesn't need the editor (`PlayStoryOnlyForOneStoryAsset` with a fake selection) → FAIL, implement, PASS.
- [ ] **Step 2:** Commit `feat(saga): commands to play a story and open a dialogue`.

### Task 15: Game Studio check, findings and README

**Files:**
- Modify: `docs/plugin-findings.md`, `README.md`
- Create (project files, not git): `/mnt/project-files/dialogue-extension/screens/*.png`, `/mnt/project-files/dialogue-extension/saga-v1-report.md`

- [ ] **Step 1:** Start the cross-platform Game Studio of the dev build under Xvfb on `sandbox/SagaSandbox`; check, with a screenshot each: the Saga plugins listed as joined in the Plugins panel (with their toolkit), "Add asset" shows both templates, the story editor at its three depths with the breadcrumb, the dialogue editor (move a node, undo it), the property grid editing a condition, the story player (raise an event, talk to the foreman, follow the hub loop), "Play story" from the asset view, "Open dialogue" from the entity tree, the command palette finding both commands, an edit of a Saga editor file reloading the plugin.
- [ ] **Step 2:** Run the full extension test suite once: `dotnet test extensions/ColorRise.Saga/ColorRise.Saga.slnx` → all pass.
- [ ] **Step 3:** Fill `docs/plugin-findings.md` with every finding met (or "none met" for each plugin point of the design's table) and the README (build, dev feed, sandbox, what v1 does and doesn't).
- [ ] **Step 4:** Commit `docs(saga): plugin findings and readme`; push the branch to the fork.
- [ ] **Step 5:** Write the report for Nicolas: what works, screenshots, findings with suggested changes, what was left out.
