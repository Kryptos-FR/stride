# ColorRise.Saga: design of the first version

Version 2, 2026-11-15 (answers of 00:49 folded in), from the brainstorm with Nicolas in the thread "Design a story and dialogue extension". Diagrams: [Story System Sketch](https://claude.ai/artifact/QQmudJVgdCdx6jspoc4rmM). Research: /mnt/project-files/dialogue-extension/research-dialogue-systems.md.

Marks: **(N)** decided by Nicolas, **(C)** Claude's call, open to correction.

## 1. Intent

Saga is a story system for Stride games: a branching main plot cut into acts, scenarios and quests inside the acts, and dialogues between the player and NPCs (or between NPCs) that become available when the story state allows it. Gameplay code talks to it through named elements.

Two reasons, in this order:
1. **Validate the new plugin model** of the engine and of the cross-platform Game Studio (Avalonia) as a third party would: packages, companions, asset types and compilers, an asset editor with its own views, panels, commands, services. **(N)**
2. **Be used in a real game** of Color-Rise, not a proof of concept. No game code exists yet: Nicolas wants to write the story with Saga first. **(N)**

The first version is deliberately small: its job is to reach every plugin integration point and surface the problems there. The story features grow afterwards. **(N)**

## 2. Goals, non-goals, anti-goals

Goals of v1:
- Write a story in Game Studio (plot, acts, scenarios, quests, dialogues) and play it there, before any game code exists.
- A runtime a game drives with a few calls, and whose state it can save.
- Touch every plugin point listed in section 9, and record each problem met in the plugin model.

Non-goals of v1 (later): sub-plots as timelines of their own running in parallel (a love story or a family feud across the acts of a war; see section 7 for how v1 keeps the door open) **(N)**, localization and voice-over, barks and ambient lines, typed attribute classes from game code, typed constants for the declared names, a C# source generator for conditions, a built-in dialogue UI, the WPF Game Studio's views.

Anti-goals (a failure even if it works):
- **A no-code or visual scripting engine.** **(N)** The story holds data and conditions; what an event or signal means in the world is game code.
- **Patching Stride to make Saga work.** A gap in the plugin model is a finding to report (section 10), not a change to core code made on the side.
- An extension that only works because it lives in Stride's tree (project references to `sources/`, internal APIs).
- Breaking the WPF Game Studio: it must still open the sandbox; Saga assets show there with the generic property grid.

## 3. Concepts

| Concept | Meaning |
|---|---|
| **Story** | The whole narrative of a game: what it declares and its plot. One per game in v1. |
| **Act** | A node of the plot (the main timeline). The plot is a tree or a DAG: no cycles at this level. **(N)** A branch is picked by conditions on the story state; gameplay forces one by setting a variable or raising an event. **(N)** |
| **Scenario** | An event or intrigue of the main plot or a sub-plot. It happens when its conditions hold, whatever the player is doing right now; player actions and quest outcomes only change those conditions or its branch. **(N)** It belongs to the act it starts in and can be marked as continuing into the next acts. When an act ends, its other running scenarios end, and their active quests fail. **(N)** It has named outcomes (`easier`, `harder`, `lost`), each with a condition; the first that holds when it ends is kept, and anything later can test it (`scenario.overlord.outcome == harder`). No sub-graph inside a scenario in v1. **(N)** |
| **Quest** | Something the player does: optional, or it tips a scenario's branch (easier, harder, lost), or it gives tokens, objects, skill values. **(N)** A quest usually belongs to a scenario (Overlord offers "take and secure a position"). **(C)** Its life: not started → available (its offer condition holds) → active (accepted by an action, usually a dialogue choice, or automatically if marked) → succeeded or failed (by conditions checked like a scenario's, or by an action). Each stage has a "done when" condition. The game only reports facts (events, variables); the story moves the quests, and the game has no direct quest call. **(N)** |
| **Dialogue** | Lines and choices between participants (the player and NPCs, or NPCs only). Branches, with small loops back to a hub for convenience. **(N)** It belongs to a scenario or a quest, and has an activation: the character it is attached to, a condition, a priority. **(N)** By default its owner limits it: a scenario's dialogues play only while the scenario runs; a quest's dialogues play from "available" until its outcome, so a quest giver works. A dialogue can opt out and rely on its own condition only. **(N)** |
| **Character** | A speaker declared in the story. An NPC entity points at one with a component: the link between scenes and the story. |
| **Story state** | The current act, the state of each scenario and quest, the variables, the events raised, the visits. One object the game saves with its save files. |
| **Declared names** | Variables, events, functions, signals, characters and attribute types, declared in the story so that the editor can list and check them and the game uses the same names. **(N: "named elements in an API that gameplay code calls")** |
| **Attributes** | Typed values attached to any card (act, scenario, quest, dialogue), from attribute types the story declares, e.g. `starts_on_day: int`. The game reads them. This is how time works: Saga has no clock; a game that needs one reports it as a variable or an event and reads attributes. **(N)** |

Sub-plots, as Nicolas describes them, can run across acts and have timelines of their own, in parallel with each other (a love story between two characters, the player possibly one of them; a family feud; in *Saving Private Ryan* the sub-plot is the focus and WWII almost an ambient plot). v1 has one plot; continuing scenarios cover a thread that crosses acts. **(N)**

Example (not Nicolas's game): the plot is WWII, a scenario is Operation Overlord, a quest is "take one enemy position and secure it". The quest's success makes the campaign easier, its failure harder or lost; the war goes on either way.

## 4. Approaches considered

**How the extension is built against Stride** **(N: A)**
- **A. Its own folder and solution on the branch, Stride through packages only** (picked). Packages from the local dev feed an engine build fills (`redirect-packages`, NugetDev). A sandbox game in the same folder references the Saga runtime project, so Game Studio loads the companions as for any game, compiles the editor packages from the solution and reloads them on edits. Costs an engine build first and slower iterations, but it is what a third party lives through.
- B. Projects inside Stride's tree referencing `sources/`: fast, but skips the package path, toolkit detection and version check, and blurs "not affiliated".
- C. A separate repository on published packages: the new plugin API is in no released package yet.

**How conditions are written** **(N: A for v1)**
- **A. Short expressions over declared names, plus functions the game provides by name** (picked): `quest.secure_position == failed && day >= 12`, `has_item("sword")`.
- B. Condition objects in the property grid (compare variable, quest outcome, All/Any groups, later C# classes from the game).
- C. Both.
- Later (Nicolas's idea): replace the runtime evaluator with C# generated from the story assets. v1 keeps that door open (section 6).

**Scope of the system**: Nicolas widened it from dialogues to a story system with plot, acts, scenarios and quests. **(N)**

## 5. Packages and layout

Name **ColorRise.Saga** **(N)**. Everything lives in `extensions/ColorRise.Saga/` on the branch `feature/colorrise-saga` (from the fork's `master`) **(C)**; nothing outside that folder changes.

```
extensions/ColorRise.Saga/
  ColorRise.Saga.slnx
  Directory.Build.props        net10.0, nullable, conventions; imports the Stride version (below)
  nuget.config                 the local dev feed, then nuget.org
  README.md                    what it is, how to build it, how to open the sandbox
  docs/design.md               this document
  docs/plugin-findings.md      section 10
  src/ColorRise.Saga/                   runtime
  src/ColorRise.Saga.Assets/            Assets companion
  src/ColorRise.Saga.Editor/            Editor companion, toolkit-neutral
  src/ColorRise.Saga.Editor.Avalonia/   views for the cross-platform Game Studio
  tests/ColorRise.Saga.Tests/           runtime, expressions, asset-to-content
  sandbox/SagaSandbox/                  a small game: a story, two dialogues, a scene with NPC entities
```

| Package | Kind | Holds | References |
|---|---|---|---|
| `ColorRise.Saga` | Runtime | content types, story state, runtime, expression evaluator, character component | `Stride.Engine`; declares the two companions with `StrideCompanionProject` |
| `ColorRise.Saga.Assets` | `Assets` | story and dialogue assets, compilers, templates, icons, the asset-to-content builder | `Stride.Core.Assets`, the runtime |
| `ColorRise.Saga.Editor` | `Editor`, neutral | plugin class, editor view models, story player service and view model, commands | `Stride.Assets.Editor`, the runtime and Assets; declares the `.Avalonia` companion |
| `ColorRise.Saga.Editor.Avalonia` | `Editor`, Avalonia | graph views, story player view, its own plugin class registering the views | the Editor package, `Stride.Assets.Editor.Avalonia` (or what the docs name) |

The Stride version is never committed: a local engine build is `4.5.3-devN`, with N different per checkout, so `Directory.Build.props` takes it from a git-ignored local file, or reads it from the engine checkout the folder sits in. **(C)** All target `net10.0` (not .NET 11). Code conventions of the repo: explicit access modifiers, classes sealed by default, no WPF mention in Avalonia code, `MultiBinding` only inline.

## 6. Expressions

- **Conditions** are boolean expressions: literals, declared names, `.` paths (`quest.secure_position.stage`), comparison, `&&`, `||`, `!`, parentheses, calls to declared functions, and built-ins `raised("event")`, `visited("node")`, `visits("node")`. Values are bool, int, float, string and the states of quests and scenarios (`not_started`, `available`, `active`, `succeeded`, `failed`; `pending`, `running`, `done`) and scenario outcomes. **(C)**
- **Actions** (on lines, choices, scenario start and end, quest stages) are a short list of statements: assignment to a variable or to a quest's stage or outcome, `raise event`, `emit signal(args)`. **(C)** Still not a scripting language: no loops, no locals.
- **Compilation**: the asset compiler parses each expression into a small syntax tree, checks every name against the story's declarations and the types, and fails the build with the asset, the card and the position on an error. The compiled content stores the trees. **(C)**
- **Evaluation** goes through one interface (`ISagaCondition` / `ISagaAction`, names to settle), implemented in v1 by a tree walker, so a source generator can replace it later without changing the story files. **(N's later idea, C's shape)**

## 7. Data model

**Story asset** (`StoryAsset`, file `.sdsaga` **(C)**):
- Declarations: variables (name, type, default), events, functions (name, parameter types, return type), signals (name, parameters), characters (id, display name), attribute types (name, type, which card kinds).
- Plot: acts with their links to the next acts, each link with a condition; the first act; an act's end condition. The plot is an object of its own inside the story, so that sub-plots can come later as more plots without reshaping the rest. **(C)**
- In each act: scenarios (start and end conditions, "continues into the next acts", actions on start and end, named outcomes with their conditions, attributes) and quests (the scenario it belongs to, the offer condition, "starts automatically", stages with their "done when" conditions, success and failure conditions, actions on each change, attributes).
- The asset format has a version from day one, with Stride's asset upgraders for each change, since Nicolas writes his real story with it. **(C)**
- Positions of the nodes in each graph, so the layout the writer made is kept. **(C)**

**Dialogue asset** (`DialogueAsset`, file `.sdsagadialog` **(C)**), one per dialogue so that the story and its dialogues merge separately in git **(C, approved with the v1 cut)**:
- The story it belongs to (an asset reference) and its owner (a scenario or quest id).
- Activation: the character it is attached to, a condition, a priority, "once", and "only while its owner is live" (on by default).
- Participants (characters).
- Nodes: **line** (speaker, text, actions), **choices** (each: text, condition, hidden or shown disabled when false, "once", actions, target), **branch** (first link whose condition holds), **end**. Links may loop. Each node has a stable id, which localization will use later.
- Text substitution `{player_name}` over declared variables. **(C)**
- Node positions.

**Compiled content**: one `StoryDefinition` per story, holding its dialogues, loaded by URL. **(C)** The same builder makes it for the compiler and for the story player in Game Studio, so both read the assets the same way.

## 8. Runtime

- `SagaSystem`, a `[GameSystem]`, gives access to the runtime; `Load(StoryDefinition)` starts a story, `Restore(state)` resumes one.
- Hooks, all by declared name: `Raise(event)`, `Set(variable, value)`, `Get(variable)`, `RegisterFunction(name, delegate)`, `On(signal, handler)`. An unknown name throws; a declared function the game did not register logs an error and returns false/0.
- **Settling**: after each raise, set or action, the runtime re-evaluates until nothing changes (with a guard against endless back-and-forth): scenarios start and end (keeping their outcome), quests become available, start when automatic, finish their stages, succeed or fail; the act ends (ending its scenarios that don't continue, failing their active quests) and the next one is picked by the first link whose condition holds. Events changed: `ActChanged`, `ScenarioChanged`, `QuestChanged`. **(C)**
- **Dialogues**: `GetDialogue(character)` returns the dialogue with the highest priority whose condition holds and whose owner is live (unless it opted out) (ties: declaration order **(C)**), or none. A `DialogueSession` exposes the current line (speaker, text after substitution), the choices (text, enabled), `Continue()`, `Choose(index)`, `IsEnded`. The game's UI shows it; Saga has no UI.
- `SagaCharacterComponent` on an NPC entity names a character of the story; it has an icon in the editor.
- **Save**: `CaptureState()` returns a serializable `SagaState`; `Restore` takes it back.

## 9. Game Studio (cross-platform)

- **Story editor** (asset editor of the story, on the node graph control of `Stride.Core.Presentation.Avalonia`): one graph view with depths and a breadcrumb, `Story › Act II › Overlord`: the plot (acts and conditional links), then an act (its scenarios and quests, quests drawn inside or linked to their scenario), then a scenario or quest (its dialogues as nodes; opening one opens the dialogue editor). The property grid shows the selected card; expressions are text fields, checked as you type when cheap, and always by the compiler.
- **Dialogue editor**: the graph of lines, choices, branches and ends; add, link, delete; undo and redo through the asset's property graph.
- **Story player** panel (View menu): pick a story of the session, start or reset it, see the act, scenarios and quests, edit variables, raise events, answer the functions by hand, pick a character and talk: the transcript with the choices. Built on the runtime itself, from the assets in memory (no build needed). **(N: in v1, minimal)**
- **Commands**: "Play story" on a story asset (asset view context menu and palette), "Open dialogue" on an entity with a character component (entity tree), both with ids `ColorRise.Saga.*`.
- **Templates**: "Saga story" and "Saga dialogue" in *Add asset*.

| Plugin point exercised | By |
|---|---|
| Runtime package with companions, `[GameSystem]`, component with icon, content by URL | runtime |
| `Assets` companion: assets, compilers, templates, asset references, build errors | Assets |
| Neutral `Editor` + `.Avalonia` companion, toolkit detection, version check | Editor packages |
| Asset editors with views of a third party, public controls reused | story and dialogue editors |
| `context.Panels`, `context.ViewTypes`, `context.Services`, `CreateInstance` | story player |
| `context.Commands`, context menus of the asset view and the entity tree, palette | commands |
| Compiling and reloading editor packages from the game's solution | sandbox |
| Opting in to the experimental areas (`STREXP001`, `003`, `006`) | Editor packages |

The WPF Game Studio gets no Saga views: it must still open the sandbox, with Saga's assets in the generic property grid.

## 10. Plugin findings

`docs/plugin-findings.md` records every problem met in the plugin model: what Saga tried, what happened, the workaround kept inside the extension (if any), and the change suggested to Stride. A finding that blocks a feature leaves the feature out of v1 rather than changing Stride. Nicolas decides what goes upstream.

## 11. Testing and checks

- Unit tests (`tests/ColorRise.Saga.Tests`): the parser and the type checks, evaluation, settling, dialogue sessions (priorities, once, loops, hidden and disabled choices), save and restore, asset to content. Tests clean up anything they start.
- The sandbox builds with its story and dialogues; a broken expression fails the build with a clear message.
- Game Studio (cross-platform) on Linux under Xvfb: open the sandbox, edit a dialogue, play the story in the story player, screenshots in the project files.
- The WPF Game Studio is not run here; the sandbox must keep its generic behavior there (to check on Windows or Wine when Nicolas wants).

## 12. Where the real story is written

v1: in the sandbox on the branch (Claude's reading of Nicolas's answers; he writes his story before any game code exists). A separate Color-Rise game project later means packing the four packages at one version and a feed to get them from; the versioned asset format (section 7) is there so stories written now keep working. **(C)**

## 13. Left to the builder

Exact class and member names, the expression grammar details and its error messages, the graph styling, how the breadcrumb looks, how expression fields check as you type, the gizmo of the character component (an icon is enough), the content of the sandbox's sample story, and the order of the work as long as each step builds.
