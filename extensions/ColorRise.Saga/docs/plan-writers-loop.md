# Saga writer's loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the story player into a writer's tool: a recorded run with rewind and replay after edits, a story timeline panel, play marks in the story and dialogue editors, and validation warnings.

**Architecture:** A pure `SagaRun` applies recorded `SagaStep`s to a `SagaRuntime` and records what each did. A session service, `SagaPlaybackService`, registered by the editor plugin through `ISessionPluginContext.Services`, owns the run, the cursor, rewind and replay; the player panel drives it, the new timeline panel and the graph editors follow its `Changed` event. Warnings come from `SagaContentBuilder` with a severity on `SagaBuildDiagnostic`.

**Tech Stack:** C# / .NET 10, Stride editor view models (toolkit-free), Avalonia views, xUnit.

**Spec:** extensions/ColorRise.Saga/docs/design-writers-loop.md

## Global Constraints

- Target `net10.0`; don't upgrade anything.
- Explicit access modifiers; classes sealed unless meant as bases. No copyright header in Saga files.
- No WPF mentions in Avalonia code; `MultiBinding` only inline as `{sd:MultiBinding ...}`.
- Never read or change XML or YAML with regular expressions.
- Stride is never patched: a plugin-model gap goes to `docs/plugin-findings.md` as a numbered finding.
- Commits: conventional titles `feat(saga):`, `fix(saga):`, `test(saga):`, `docs(saga):`; no Co-Authored-By line, no session link. Push `feature/colorrise-saga-writers-loop` to the fork after each task.
- Tests clean up what they start (the session fixture does; a test that opens an editor closes it).
- Commands (from `extensions/ColorRise.Saga`, with `PATH=/root/.dotnet:$PATH DOTNET_ROOT=/root/.dotnet`): runtime and assets tests `dotnet test tests/ColorRise.Saga.Tests`; editor tests `dotnet test tests/ColorRise.Saga.Editor.Tests`; whole suite `dotnet test ColorRise.Saga.slnx`.

## Review Focus

1. A rewind into the middle of a dialogue: the player must show that dialogue's line or choices, and the next Continue or Choose must work.
2. Replay after a choice was reworded, reordered or removed: matched by text, then index; a removed one stops the replay with a reason, and the cursor sits on the last step that applied.
3. An edit while a run is on screen: the run keeps its build until Replay; Replay with errors keeps the old run and shows the errors.
4. An editor opened after the run started, or a story editor for another story: marks follow the playing story only, and clear on Reset.
5. Warnings on the existing tests' stories and on the sandbox: "no problems" checks must become "no errors" where the stories carry unused names, and every warning on the sandbox must be a real one.

---

### Task 1: Dialogue session path

**Files:**
- Modify: `src/ColorRise.Saga/DialogueSession.cs`
- Test: `tests/ColorRise.Saga.Tests/Runtime/DialogueTests.cs`

**Interfaces:**
- Produces: `public string? DialogueSession.CurrentNodeId { get; }` (the node shown now, null once ended); `public IReadOnlyList<string> DialogueSession.Path { get; }` (ids of every node entered, in order: lines, choices, branches, the end node when one is reached).

- [ ] **Step 1:** Write `SessionKnowsItsNodeAndThePathItTook`: a dialogue line `hello` → branch `check` (link `day > 1` → `late`, else → `early`) → line `early` → choices `hub` → choice to end node `bye`. Assert after Talk: `CurrentNodeId == "hello"`, `Path == ["hello"]`; after Continue: `CurrentNodeId == "early"`, `Path == ["hello", "check", "early"]`; after Continue: `"hub"`; after Choose(0): `CurrentNodeId == null`, `Path == ["hello", "check", "early", "hub", "bye"]`, `IsEnded`.
- [ ] **Step 2:** Run it: FAIL (no `CurrentNodeId`/`Path`).
- [ ] **Step 3:** Expose `CurrentNodeId` (cleared in `End`), add a `List<string>` path appended in `MoveTo` for every node found, the end node included.
- [ ] **Step 4:** Run the runtime tests: PASS.
- [ ] **Step 5:** Commit `feat(saga): say which node a dialogue is at and which way it came`.

### Task 2: Warnings in the builder, logged by the compilers

**Files:**
- Modify: `src/ColorRise.Saga.Assets/SagaBuildDiagnostic.cs`, `SagaContentBuilder.cs`, `StoryAssetCompiler.cs` (`SagaCompilation`), `src/ColorRise.Saga.Editor/Player/SagaPlayback.cs`
- Test: `tests/ColorRise.Saga.Tests/Assets/SagaWarningsTests.cs` (new), `tests/ColorRise.Saga.Editor.Tests/Player/SagaPlaybackTests.cs`

**Interfaces:**
- Produces: `public enum SagaDiagnosticSeverity { Error, Warning }`; `SagaBuildDiagnostic(string Asset, string Card, int Position, string Message, SagaDiagnosticSeverity Severity = SagaDiagnosticSeverity.Error)` with `ToString()` unchanged; `public static bool SagaCompilation.Report(IReadOnlyCollection<SagaBuildDiagnostic>, ILogger)` returns whether any is an error (logs errors with `Error`, warnings with `Warning`), made public so a test can call it. `SagaPlayback.Create` returns a playback when the diagnostics hold warnings only.

- [ ] **Step 1:** Write tests, each building a small asset and asserting the single warning's card and severity (messages as in the spec, section 7):
  - `UnreachableActIsAWarning`: acts `a` (first) → `b`, and `c` with no link to it: warning on card `act c`.
  - `UnreachableDialogueNodeIsAWarning`: entry `hello` → end `bye`, plus line `orphan`: warning on `node orphan`; no warning when the entry is missing (that's an error already).
  - `BranchWithoutFallbackIsAWarning`: branch whose links both have `When`: warning on `node <id>`; none when the last link's `When` is empty.
  - `ChoicesThatCanRunOutAreAWarning`: every choice has `When` or `Once`: warning; one plain choice: none.
  - `CompilationLogsWarningsWithoutFailing`: `SagaCompilation.Report([warning], logger)` is false and logs a warning; with an error it is true and logs an error.
  - In `SagaPlaybackTests`, `PlaybackStartsWithWarnings`: a story with an unreachable act plays, and its diagnostics hold the warning.
- [ ] **Step 2:** Run them: FAIL to compile (no severity).
- [ ] **Step 3:** Add the severity; a `Builder.Warn(card, message)`; the plot reachability walk from `FirstActId` (only when it names an act); the dialogue reachability walk from the entry through `NextId`, choice targets and branch targets (only when the entry exists); the branch and choices checks. `Report` takes the logger (`commandContext.Logger` at the call sites). `SagaPlayback.Create` fails only on errors.
- [ ] **Step 4:** Run both test projects: PASS. Existing tests that assert an empty diagnostics list on stories that now warn: switch them to "no errors" (ledger the list).
- [ ] **Step 5:** Commit `feat(saga): warn about acts and dialogue nodes the story never reaches`.

### Task 3: Checks across the story and its dialogues

**Files:**
- Create: `src/ColorRise.Saga.Assets/SagaUsage.cs` (internal collector)
- Modify: `SagaContentBuilder.cs`, `StoryAssetCompiler.cs`, `src/ColorRise.Saga.Editor/Player/SagaPlayback.cs`
- Test: `tests/ColorRise.Saga.Tests/Assets/SagaWarningsTests.cs`, `tests/ColorRise.Saga.Editor.Tests/Player/SagaPlaybackTests.cs`

**Interfaces:**
- Consumes: Task 2's `Warn` and severity.
- Produces: `public static void SagaContentBuilder.CheckStory(StoryAsset story, string url, IReadOnlyList<(DialogueAsset Asset, string Url)> dialogues, ICollection<SagaBuildDiagnostic> diagnostics)`: builds the story and the dialogues again with a usage collector, discards their own diagnostics, and adds only the warnings below when none of them had an error. `SagaPlayback.Create` calls it with the dialogues it resolved; the story compiler calls it with the dialogues found through `AssetFinder`, and hashes their content in `ComputeParameterHash`.

- [ ] **Step 1:** Write tests (one story with one or two dialogues each):
  - `VisitedKeyThatNamesNothingIsAWarning`: `visited("Mara.gretting")` in a scenario's `StartWhen` with a node named `greeting`: warning on that condition's card, with its position; `visited("Mara")`, `visits("Mara.greeting")` give none.
  - `UnusedDeclarationsAreWarnings`: a variable shown only in a line's text (`{day}`) is used; an unused variable, an event never checked by `raised`, an uncalled function, a signal never emitted, a character without dialogue or line each give one warning on `declarations / <kind> <name>`.
  - `QuestNothingStartsIsAWarning`: no `StartsAutomatically` and no `quest.<id>.state = ...` action: warning on `act <a> / quest <id>`; an action in a dialogue choice that sets it clears it.
  - `NoCrossChecksOnABrokenStory`: a condition that doesn't parse: `CheckStory` adds nothing.
  - In `SagaPlaybackTests`, `PlaybackReportsWarningsAcrossDialogues`: an unused event appears in the playback's diagnostics as a warning.
- [ ] **Step 2:** Run them: FAIL.
- [ ] **Step 3:** The collector walks parsed conditions and actions (paths, calls, `raised`/`visited`/`visits` string arguments with card and position, assignments, `raise`, `emit`) and `{name}` in line and choice texts; characters from `CharacterId`, participants and speakers; dialogue names and `name.nodeName` keys from the dialogues given.
- [ ] **Step 4:** Run both test projects: PASS.
- [ ] **Step 5:** Commit `feat(saga): warn about names the story declares but never uses, and visited keys that name nothing`.

### Task 4: The recorded run

**Files:**
- Create: `src/ColorRise.Saga.Editor/Player/SagaStep.cs`, `SagaRunStep.cs`, `SagaRun.cs`
- Test: `tests/ColorRise.Saga.Editor.Tests/Player/SagaRunTests.cs`

**Interfaces:**
- Consumes: Task 1's `CurrentNodeId`, `Path`.
- Produces:
  - `public enum SagaStepKind { Start, Raise, Set, Answer, Talk, Continue, Choose }`
  - `public sealed record SagaStep(SagaStepKind Kind, string Name = "", string Value = "", int ChoiceIndex = -1)` with factories `Start()`, `Raise(string)`, `Set(string, string)`, `Answer(string, string)`, `Talk(string)`, `Continue()`, `Choose(int, string)`, and `ToString()` in words (`Raise met_mara`, `Set day = 4`, `Answer has_pickaxe() → true`, `Talk to mara`, `Continue`, `Choose "Leave"`).
  - `public sealed class SagaRunStep` with `Step`, `State` (copy), `Changes`, `Signals`, `Lines`, `Messages`, `DialogueName`, `DialoguePath`, `IsDialogueOpen`, `PlaysDifferently` (settable by the service), `Signature` (lines, changes and signals joined, for comparing replays).
  - `public sealed class SagaRun : IDisposable`: `SagaRun(StoryDefinition story)`; `Runtime`; `Dialogue` (open session or null); `Answer(string function)`; `string? CanApply(SagaStep)` (null or the reason); `SagaRunStep Apply(SagaStep)` (throws `InvalidOperationException` with the reason when it can't apply; returns the step it applied, a `Choose` carrying the text and index actually taken).
- Transcript lines keep v1's wording (`Talking to Mara`, `Mara: You came back.`, `> Leave`, `(the dialogue ends)`, `(nothing to say now)`, `Error: ...`, `Warning: ...` for runtime messages).

- [ ] **Step 1:** Write tests on a story built with `SagaPlayback.Create` (Mara's dialogue as in `StoryPlayerTests`, plus a signal emitted by the choice):
  - `RunRecordsWhatEachStepDid`: Start records `ActEntered`/`ScenarioStarted`; Talk records the lines, `DialogueName == "Mara"`, `DialoguePath == ["hello"]`, open; Continue then Choose "Leave" records `> Leave`, `(the dialogue ends)`, the `QuestAvailable` change, the signal, closed path with the end node.
  - `StepsThatCantApplySayWhy`: Raise of an undeclared event, Set with a bad literal, Continue with no dialogue, Choose of a text not offered and an index out of range: `CanApply` gives a reason and `Apply` throws.
  - `ChoiceIsMatchedByTextThenIndex`: `Choose(1, "Leave")` takes "Leave" wherever it is; `Choose(0, "Gone")` takes the enabled choice at index 0 and returns its new text.
  - `AnswersAreSteps`: before `Answer("has_pickaxe", "true")` the choice is hidden, after it shown.
  - `ReplayingTheSameStepsGivesTheSameRun`: applying the recorded steps to a new `SagaRun` gives equal `Signature`s.
- [ ] **Step 2:** Run: FAIL.
- [ ] **Step 3:** Implement; subscribe to `Runtime.Changed` and to each declared signal with `On`, collect the runtime logger's messages per step; release the subscriptions in `Dispose`.
- [ ] **Step 4:** Run the editor tests: PASS.
- [ ] **Step 5:** Commit `feat(saga): record a story run step by step`.

### Task 5: The playback service

**Files:**
- Create: `src/ColorRise.Saga.Editor/Player/SagaPlaybackService.cs`
- Modify: `src/ColorRise.Saga.Editor/SagaEditorPlugin.cs` (override `InitializeSession`, base first, `context.Services.Register(service)`, `context.OnClose(service.Dispose)`)
- Test: `tests/ColorRise.Saga.Editor.Tests/Player/SagaPlaybackServiceTests.cs`

**Interfaces:**
- Consumes: Tasks 2-4.
- Produces: `public sealed class SagaPlaybackService : IDisposable` with `SagaPlaybackService(ISessionViewModel session)`; `event EventHandler? Changed`; `StoryViewModel? Story`; `IReadOnlyList<SagaBuildDiagnostic> Diagnostics`; `bool HasErrors`; `bool IsPlaying`; `SagaRun? Run`; `IReadOnlyList<SagaRunStep> Steps`; `int Cursor` (-1 when not playing); `SagaRunStep? CurrentStep`; `bool IsStale`; `int? DivergedAt`; `string Divergence` (reason or empty); `void Select(StoryViewModel? story)` (stops and checks); `void Reset()`; `bool Start()`; `string? Do(SagaStep step)` (null when applied, else the reason; drops the steps after the cursor); `void GoTo(int index)`; `bool Replay()`; `bool Plays(AssetId dialogue)`; `(IReadOnlyList<string> Path, bool IsOpen)? DialoguePath(string dialogueName)` (its last play up to the cursor).

- [ ] **Step 1:** Write tests in the session fixture (the player's test story):
  - `ServiceIsAServiceOfTheSession`: `fixture.Session.ServiceProvider.Get<SagaPlaybackService>()` is there; an opened story editor's `ServiceProvider` finds the same instance.
  - `GoingBackAndForthRestoresTheDialogue`: Start, Talk, Continue (choices shown), Choose "Leave"; `GoTo(2)`: the run's dialogue shows the choices again, `Steps.Count == 4`; `GoTo(3)`: the dialogue ended again and the quest is available; `GoTo(1)` then `Do(Raise)`: the steps after 1 are gone.
  - `EditingTheStoryMarksTheRunStale`: an edit of a scenario's title through the property graph sets `IsStale`; so does an edit of the dialogue; Replay clears it.
  - `ReplayFollowsTheEditedStory`: reword the "Leave" line's next text, Replay: the step that shows it `PlaysDifferently`; remove the "Leave" choice, Replay: `DivergedAt == 3`, `Cursor == 2`, `Divergence` names the choice.
  - `ReplayWithErrorsKeepsTheRun`: break a condition, Replay returns false, `HasErrors`, the steps and cursor are unchanged.
  - `ResetForgetsTheRun`.
  - `ChangedIsRaisedOnEachChange` (counts on Start, Do, GoTo, Replay, Reset, stale).
- [ ] **Step 2:** Run: FAIL.
- [ ] **Step 3:** Implement (the build moves here from the player: `SagaPlayback.Create` with the session's dialogues; follow `PropertyGraph.Changed` and `ItemChanged` of the story and of every resolved dialogue asset view model, unhooked on stop and dispose).
- [ ] **Step 4:** Run the editor tests: PASS.
- [ ] **Step 5:** Commit `feat(saga): a playback service of the session with rewind and replay`.

### Task 6: The player panel on the service

**Files:**
- Modify: `src/ColorRise.Saga.Editor/Player/StoryPlayerViewModel.cs`, `StoryPlayerItems.cs`, `ManualFunctionViewModel.cs`, `src/ColorRise.Saga.Editor.Avalonia/Views/StoryPlayerView.axaml`
- Test: `tests/ColorRise.Saga.Editor.Tests/Player/StoryPlayerTests.cs`, `SagaCommandsTests.cs`

**Interfaces:**
- Consumes: Task 5.
- Produces: `StoryPlayerViewModel.Diagnostics` becomes `ObservableList<SagaBuildDiagnostic>`; `ShowTimelineCommand` (shows `StoryTimelineViewModel.PanelId` through `IPanelService`); `Playback` is replaced by `Service` (`SagaPlaybackService`). Function answers and variable values go through `Do` and follow the cursor (`ManualFunctionViewModel.Update(SagaValue)`).

- [ ] **Step 1:** Update the existing tests for the new types (`Playback!.Runtime` → `Service.Run!.Runtime`, diagnostics as text) and add `RewindingShowsTheDialogueAtThatStep`: after Choose, `Service.GoTo(2)`: `Choices` shows "Leave" again, the transcript stops at Mara's line, the quest is `not_started`; `WarningsDontBlockStart`: a story with an unused event lists one warning and starts.
- [ ] **Step 2:** Run: FAIL.
- [ ] **Step 3:** Rewrite the panel as a view of the service (lists rebuilt when the story's build changes, values and transcript refreshed on `Changed`); the view styles warnings apart and gets a Timeline button.
- [ ] **Step 4:** Run the editor tests: PASS.
- [ ] **Step 5:** Commit `feat(saga): the story player drives the playback service`.

### Task 7: The timeline model and panel

**Files:**
- Create: `src/ColorRise.Saga.Editor/Player/SagaTimeline.cs` (lanes from steps), `StoryTimelineViewModel.cs`
- Modify: `SagaEditorPlugin.cs` (register the panel), `SagaCommands.cs` (Play story shows the timeline too)
- Test: `tests/ColorRise.Saga.Editor.Tests/Player/SagaTimelineTests.cs`, `StoryTimelineTests.cs`

**Interfaces:**
- Consumes: Tasks 4-5.
- Produces:
  - `public enum TimelineItemKind { Act, Running, Ended, Offered, Active, Succeeded, Failed, Stage, Dialogue, Choice, Event, Variable, Answer, Signal }`
  - `public sealed record TimelineItem(int Start, int End, string Label, TimelineItemKind Kind, string Tip, bool IsMark, bool IsOpen)`
  - `public sealed record TimelineLane(string Title, IReadOnlyList<TimelineItem> Items)`
  - `public static IReadOnlyList<TimelineLane> SagaTimeline.Build(IReadOnlyList<SagaRunStep> steps)`: lanes Acts; one per scenario started and quest offered, in order of first appearance; Dialogues; Game; Signals (the last three always present).
  - `public sealed class StoryTimelineViewModel : PanelViewModel` (`PanelId = "ColorRise.Saga.StoryTimeline"`, Bottom, hidden): `Steps` (`TimelineStepViewModel`: `Index`, `Glyph`, `Tip`, `IsFuture`, `IsDiverged`, `PlaysDifferently`), `Lanes`, `Cursor`, `Status`, `Details` (lines of the cursor step), `GoToStepCommand` (int), `BackCommand`, `ForwardCommand`, `ReplayCommand` (enabled when stale).

- [ ] **Step 1:** Write `SagaTimelineTests` on a run of the player's story (Start, Talk, Continue, Choose, Raise): the act span starts at 0 and is open; the scenario lane runs from 0; the quest lane has an offered span from 3; the dialogue span covers 1-3 with a choice mark at 3; the Game lane has an event mark at 4; a stage-done tick and a succeeded mark on a quest with one stage. `StoryTimelineTests` (fixture): the panel follows the service (steps, cursor, future steps after `GoTo`, the divergence after a broken Replay, `Status` text, Details of the cursor step), and its commands go back, forward, replay; "Play story" shows both panels (extend `SagaCommandsTests` with a recording `IPanelService` if the fixture has none, else assert on the panel service).
- [ ] **Step 2:** Run: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the editor tests: PASS.
- [ ] **Step 5:** Commit `feat(saga): a story timeline panel`.

### Task 8: The timeline view

**Files:**
- Create: `src/ColorRise.Saga.Editor.Avalonia/Views/SagaTimelineControl.cs`, `StoryTimelineView.axaml`, `.axaml.cs`
- Modify: `SagaEditorAvaloniaPlugin.cs` (register the view), `Themes/SagaStyles.axaml` (timeline colors)

**Interfaces:**
- Consumes: Task 7's `StoryTimelineViewModel`, `TimelineLane`, `TimelineItem`, `TimelineStepViewModel`.
- Produces: `SagaTimelineControl : Control` with styled properties `Steps`, `Lanes`, `Cursor`, `GoToStepCommand`; draws the title column, the header (number and glyph), lanes (spans as rounded bars with clipped labels, marks as shapes), the cursor column, greyed future, red divergence, a dot on steps that play differently; a click runs the command with the column's index; the tooltip shows the column's `Tip`.

- [ ] **Step 1:** Build the Avalonia project: no warnings from the new files.
- [ ] **Step 2:** Check it in Game Studio under Xvfb on the sandbox (Task 10 takes the screenshots).
- [ ] **Step 3:** Commit `feat(saga): draw the story timeline`.

### Task 9: Play marks in the editors

**Files:**
- Modify: `src/ColorRise.Saga.Editor/ViewModels/SagaGraphNodeViewModel.cs`, `SagaGraphEditorViewModel.cs`, `StoryEditorViewModel.cs`, `DialogueEditorViewModel.cs`; `src/ColorRise.Saga.Editor.Avalonia/Themes/SagaStyles.axaml`; create `Views/SagaPlayBrushes.cs`
- Test: `tests/ColorRise.Saga.Editor.Tests/StoryEditorTests.cs`, `DialogueEditorTests.cs`

**Interfaces:**
- Consumes: Task 5's service (`Story`, `Run.Runtime.State`, `Plays`, `DialoguePath`, `Changed`).
- Produces: `public enum SagaPlayMark { None, Current, Visited, Running, Offered, Done, Failed }`; `SagaGraphNodeViewModel.PlayMark` and `PlayNote` (internal setters); `protected abstract void SagaGraphEditorViewModel.UpdatePlayMarks(SagaPlaybackService? playback)` called after each `Refresh` and on `Changed`.

- [ ] **Step 1:** Write tests: `StoryEditorMarksWhereThePlayIs` (plot: current act `Current`; act level after a step: scenario `Running`, quest `Offered`, and after the quest goes active `Running` with note `stage 1/2`; scenario level: the dialogue `Current` while it plays, `Visited` with `played ×1` after; another story's editor: all `None`; Reset: all `None`); `DialogueEditorMarksTheNodeAndThePath` (after Talk and Continue: `hub` `Current`, `hello` `Visited`; after the dialogue ends: no `Current`, the end node `Visited`; `GoTo(1)`: `hello` `Current` again).
- [ ] **Step 2:** Run: FAIL.
- [ ] **Step 3:** Implement; the node template gets an outline and a badge bound to `PlayMark` and `PlayNote` through `SagaPlayBrushes`.
- [ ] **Step 4:** Run the editor tests, build the Avalonia project: PASS.
- [ ] **Step 5:** Commit `feat(saga): show in the story and dialogue editors where the play is`.

### Task 10: Docs, findings, walkthrough

**Files:**
- Modify: `README.md` (what v2 does, the timeline and warnings), `docs/plugin-findings.md` (new findings, numbered after 15), `docs/design-writers-loop.md` (status line)
- Create: /mnt/project-files/dialogue-extension/saga-writers-loop-report.md, screenshots in /mnt/project-files/dialogue-extension/screens/ (`loop-01-...`)

- [ ] **Step 1:** Run the whole suite: all green.
- [ ] **Step 2:** Game Studio under Xvfb on the sandbox: play the story, rewind, edit a dialogue, replay, open the editors; take screenshots; note every warning the sandbox story gets and judge each (fix the sandbox if it's a real mistake, ledger if the warning is wrong).
- [ ] **Step 3:** Write the findings, the README and the report (rulings, deferred minors, screenshots).
- [ ] **Step 4:** Commit `docs(saga): the writer's loop in the README, findings 16+` and push.
