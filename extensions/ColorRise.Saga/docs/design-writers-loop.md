# ColorRise.Saga: design of the writer's loop (version 2)

Status: built, 2026-11-15. The README's "What v2 adds" section says what it does.

2026-11-15. Nicolas picked group A, "the writer's loop", from the candidates in /mnt/project-files/dialogue-extension/saga-next-steps.md (section 4), then said "go ahead with your own calls". Every choice below that he did not make is marked **(C)**, Claude's call, open to correction; **(N)** marks his decisions.

## 1. Intent

A writer writes the story before the game exists, so the story player in Game Studio is their only playtest. Version 2 turns it into a tool for trying branches: see what happened step by step, go back to any step and take the other branch, replay the same run after editing the story, and see in the story and dialogue editors where the story is. A validation pass reports the mistakes that v1 lets through silently. **(N: group A)**

It also exercises plugin points v1 barely touched: a service of the session that several editors and panels share (`ISessionPluginContext.Services`), editors that react to another view's state, a second panel, and a custom-drawn control. **(C)**

## 2. Goals, non-goals, anti-goals

Goals:
- **A story timeline** of the play: one column per step (each start, raise, set, answer, talk, continue or choice), with lanes for the acts, the scenarios, the quests, the dialogues, and markers for the game's inputs and the story's signals.
- **Rewind:** go back to any step, and forward again, like undo and redo. A new step taken from an earlier one drops the steps after it.
- **Replay after edits:** the player notices that the story or one of its dialogues changed, and replays the same steps on the new version, showing which steps now play differently and where the run can no longer go on.
- **Live highlight:** the story editor marks the current act, the running and ended scenarios, the state of each quest, the dialogue playing; the dialogue editor marks the current node and the path the dialogue took.
- **Validation warnings:** checks that don't stop the build or the player but point at likely mistakes (section 7).

Non-goals (later): saved runs as files or unit tests; a tree of branches (the dropped steps are gone); watching a running game from Game Studio; checking scenes (a character component naming an undeclared character); warnings shown on the graph nodes; views for the WPF Game Studio.

Anti-goals:
- **The player changes the assets.** It only reads them.
- **Replaying on every keystroke.** Replay is a button: a writer in the middle of an edit has a broken story more often than not.
- **Patching Stride.** Gaps in the plugin model go to docs/plugin-findings.md. **(N, standing rule)**
- **Visual scripting.** Nothing here moves game logic into the story. **(N, standing rule)**

## 3. Approaches

**How rewinding works** **(C: A)**
- **A. Record the steps and replay them on a fresh runtime** (picked). A step is the input the writer gave (raise `met_mara`, set `day = 4`, choose "Leave"). Going back to step *k* builds a new runtime from the same compiled story and applies steps 0 to *k*. The runtime is deterministic once the answers of the functions are steps too, and stories are small, so replaying is instant. It restores the dialogue in progress, which the story state doesn't hold, and the same mechanism replays a run on an edited story.
- B. Restore a snapshot of the story state. Exact and simple for the story, but a dialogue in progress can't be restored (a v1 limit: the state holds no dialogue position), and it can't replay after an edit.
- C. Put the dialogue position in the state, then restore snapshots. It is the "save during a dialogue" feature, a bigger runtime change that the research put later.

**What the timeline cursor is** **(C: A)**
- **A. One cursor: clicking a step goes there** (picked). Every position is live: the player panel, the timeline and the editors all show it, and the writer can act from it. The steps after it stay, greyed, until a new step drops them.
- B. A selected step to inspect and a separate cursor where the story is (Unreal's Rewind Debugger). Two positions on screen at once confuse more than they help when going back is instant.

**Where the play lives** **(C: A)**
- **A. A service of the session** (`SagaPlaybackService`), registered by the plugin in `InitializeSession` through `context.Services`. The player panel drives it; the timeline panel and the editors read it and follow its `Changed` event.
- B. Keep it in the player panel and let the others find the panel through the panel service. Ties the editors to a view, and the panel only exists once shown.

**Where the timeline goes** **(C: A)**
- **A. Its own panel, at the bottom, hidden at first**, shown by "Play story" and by a button of the player. Wide and short suits a timeline; the player keeps its column on the right.
- B. Inside the player panel. It would be cramped in a side panel.

**Where the warnings are found** **(C: A)**
- **A. In `SagaContentBuilder`, with a severity on its diagnostics.** The compilers log warnings without failing; the player lists them without blocking Start. The checks across the story and its dialogues (names used, `visited` keys) need every dialogue: a new `CheckStory` takes them, the player calls it with the assets in memory, and the story compiler finds the dialogues through its asset finder, with their content in its parameter hash so that a dialogue edit refreshes the warnings.
- B. Only in the player. The build would miss them, and the player would no longer report "what the compiler would".

## 4. The run

A **step** (`SagaStep`) is what the writer did, recorded as names and texts, never as object references, so that it can apply to another build of the story **(C)**:

| Kind | Recorded | Applies when |
|---|---|---|
| Start | | always first, once |
| Raise | the event | the story declares it |
| Set | the variable and the value as text | it is declared and the text is a literal of its type |
| Answer | the function and the answer as text | it is declared and the text is a literal of its return type |
| Talk | the character | the story declares it (having nothing to say is a result, not a failure) |
| Continue | | a dialogue shows a line and no choices |
| Choose | the choice's index and text, and the texts of the choices shown | a dialogue shows choices and one of them, enabled, has the same text, or else the same index when that one was reworded |

A **run step** (`SagaRunStep`) is a step with what it did: the story state after it (a copy), the changes of the story (`SagaChangedEventArgs`), the signals emitted, the transcript lines, the runtime's warnings and errors, and the dialogue open during or ended by the step (its name, the nodes it went through, whether it is still open).

Answering a function is a step **(C)**: the function's answer at each point of the run must be known to replay it, and it shows on the timeline. In v1 an answer was a field that only mattered at the next step; that stays true, the step changes nothing by itself.

A choice is matched by its text first, then by its index **(C)**: a writer who reorders choices keeps the run; one who rewords a choice keeps it too, as long as the order stays. The index counts only for a reword: the node shows as many choices as when the step was taken, and the text at the index is not one it showed then. A choice removed before others would otherwise hand its index to the next one.

What a step throws while the story settles (a story that doesn't settle) is recorded in its messages, as v1's transcript does, and replays the same way. Only a step that can't apply stops a replay.

## 5. Rewind and replay

- **Go to step k.** Before the cursor: a new runtime from the same build, steps 0 to k applied again. After it: the steps from the cursor to k applied in turn. The cursor lands on k, or on the step before the first one that no longer applies.
- **A new step at the cursor** drops the steps after it, and applies.
- **Changed since built.** The service follows the property graphs of the story and of each dialogue it built (any change, including undo and redo). The run keeps playing the build it has; the timeline says "changed since built" and Replay lights up. **(C)**
- **Replay** builds the story again from the assets in memory. With errors, nothing changes and the errors show in the player. Without, it applies steps 0 to the cursor on the new build:
  - a step whose transcript, changes or signals differ from before is marked "plays differently";
  - the first step that can't apply is the divergence: the cursor stops before it, and it and the steps after stay greyed with the reason ("the choice 'Leave' is no longer offered"); the writer can only go on by taking a new step.
- **Reset** stops, forgets the run, and checks the story again; **Start** starts a new run.

## 6. Showing it

**The player panel** keeps its layout and shows the cursor: where the story is, the API by hand, the transcript up to the cursor, the choices. It gains a Timeline button, and its problems list shows errors and warnings apart (warnings don't block Start). **(C)**

**The timeline panel** (bottom, hidden by default) **(C)**:

```
[◀] [▶] [Replay]  Step 7 of 12: choose "Leave" · changed since built
        | 0 1 2 3 4 5 6 7 8 9 10 11 |  Step 7: choose "Leave"
 Acts    [the_mine ··············][esc|  > Leave
 cave_in   [running ············ rescued  Quest find_the_lamp is available
 lamp            [offered][active |  ✓    Signal play_sound("door")
 Dialogue     [mara ·•·]  [guard]       |
 Game     S !    =      ?             |
 Signals          ★         ★         |
```

- Each column is a step; the header gives its number and a glyph of its kind; a tooltip gives what it did.
- Lanes: the acts; one per scenario that started (running, then ended with its outcome); one per quest that was offered (offered, active with a tick per stage done, then succeeded or failed); the dialogues (a bar from talking to the end, a tick per choice); the game's inputs (raise, set, answer); the signals the story emitted.
- The cursor's column is highlighted; the steps after it are greyed; the divergence is red; a step that plays differently since the last replay has a dot.
- Click a column to go there; ◀ and ▶ step back and forth.
- On the right, the cursor step in full: what was done, the lines, the changes of the story in words, the signals, the warnings.
- Drawn by a custom Avalonia control from a toolkit-free view model (lanes and items), so the logic is tested without a UI.

**Live highlight** **(C)**: each graph node gets a play mark (none, current, visited, running, offered, done, failed) and a short note, from the state at the cursor, drawn as a colored outline and a badge by the node template:
- story editor: acts (current, done), scenarios (running, ended with the outcome), quests (offered, active with "stage 2/3", succeeded, failed), the dialogues of a scenario or quest (playing, played ×n);
- dialogue editor: the node the dialogue is at, the nodes it went through in its last play up to the cursor (with how many times).

An editor marks nothing when the playing story isn't its story, or isn't the story its dialogue belongs to. The marks clear on Reset.

## 7. Validation warnings

All are warnings: the story builds and plays. **(C)**

| Where | Warning |
|---|---|
| Plot | An act no link leads to from the first act: the story never plays it. |
| Dialogue | A node nothing leads to from the entry. |
| Dialogue | A branch whose every link has a condition: when none holds, the dialogue ends there. |
| Dialogue | A choices node whose every choice has a condition or can be taken once: when none is left, the dialogue ends there. |
| Story and dialogues | `visited("k")` or `visits("k")` where `k` names no dialogue of the story and no named node of one (`dialogue.node`). |
| Story and dialogues | A declared variable nothing reads, sets or shows in a line (`{name}`); an event no condition checks with `raised`; a function nothing calls; a signal nothing emits; a character no dialogue belongs to, has as a participant, or gives a line. |
| Story and dialogues | A quest that doesn't start by itself and that no action makes active (`quest.<id>.state = ...`). |

The checks across the story and its dialogues run only when the story and its dialogues have no errors, so that a condition that doesn't parse doesn't make its names look unused. **(C)**

## 8. Runtime changes

- `DialogueSession.CurrentNodeId` and `DialogueSession.Path`, the ids of the nodes the session went through in order (lines, choices, branches, and the end node when it reaches one). The visits only count named nodes, so the editor can't rebuild the path from them. **(C)** A game can use them for a backlog.

## 9. What is left to the builder

Colors, glyphs, sizes and wording; the exact layout of the timeline panel; the names of private members; how the panels refresh their lists. Each plugin-model gap met goes in docs/plugin-findings.md.
