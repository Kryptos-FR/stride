# Skein: Yarn Spinner for Stride games, design of the first version

**Approved by Nicolas on 2026-11-15.** Built on the four decisions of the design questions D1 to D4, which he accepted as recommended the same day (D1 A, D2 A, D3 A, D4 A). "Skein" is the name of D4 A. The implementation plan is [plan.md](plan.md).

Marks: **(N)** from Nicolas's request, **(D)** decided by Nicolas in D1 to D4, **(C)** my call, open to correction.

**My calls to check while reading** (each is marked **(C)** in its place):

1. Built in `extensions/ColorRise.Skein/` on a new branch `feature/colorrise-skein`, on top of `feature/xplat/plugin-follow-ups`, Stride through packages only (section 5).
2. Five packages, with the UI presenters apart from the runtime (section 5).
3. The project is a Stride asset (`.sdyarn`) that writes its keys into a `.yarnproject` for Yarn's tools, and preserves what VS Code keeps there (section 6).
4. Commands find their target through a `DialogueActorComponent` first, then by entity name; registration by reflection in this version (section 7).
5. One undo history for typing, graph and property grid edits (section 8).
6. The work starts with spikes on seven risks, each with a fallback; a fallback that loses a goal comes back to you (section 11).
7. The sample story "Brinemoor": three acts, the scenarios and quests of the table, English and French (section 12).
8. Not in this version: voice-over, completion in the text pane, rich text inside a line, saving in the middle of a dialogue, a source generator, WPF views (section 2).

A fresh reader checked this draft against Yarn Spinner 3.2.2 and the fork branch as a builder would, and had no question left for you; the points it raised are folded in as my calls.

## 1. Intent

Skein brings Yarn Spinner, the dialogue and story engine used in Unity and Godot games, to Stride. It is a third-party extension by Color-Rise, built on Stride's plugin model as Saga was.

The reasons, in Nicolas's words and order:

1. **"Their dialogue/story engine."** Yarn's language, compiler and virtual machine, unchanged, so that what a writer learns or reads about Yarn Spinner applies. **(N)**
2. **"Our plugin integration, including the node/graph editor, and support through our property grid."** What Saga proved the cross-platform Game Studio can host. **(N)**
3. **"Professional quality."** Something a studio could adopt, not a prototype: complete enough to ship a game with, tested, documented, and honest about its limits. **(N)**
4. **A larger sample story** with several acts, several scenarios per act and several quests, "to try out all that Yarn Spinner can offer". **(N)**

Saga stays as it is on its branches. Skein takes its lessons, not its runtime.

## 2. Goals, non-goals, anti-goals

**Goals of the first version**

- Write Yarn scripts in Game Studio (graph and text) or in VS Code with Yarn's own extension, on the same files, and see the same layout in both.
- Play a dialogue in Game Studio before the game exists, with rewind, replay after an edit, and a live highlight in the editor.
- Run dialogues in a Stride game with a few components: a runner, presenters, variable storage, commands and functions written in C#.
- Ship text in several languages with Yarn's string tables.
- A sample game whose story uses every Yarn language feature at least once.
- Record every gap met in the plugin model as a finding.

**Non-goals of the first version** (candidates for later, section 15)

- Voice-over and other assets per line.
- A graph at the level of lines and options (D1 C), and a story map (D2 C).
- Completion, hover and go-to-definition in the text pane. VS Code has them.
- Registration of commands by a source generator (needed for trimmed or AOT builds).
- Rich text inside a line (bold or coloured words): Stride's `TextBlock` draws one style.
- Saving in the middle of a dialogue. Yarn's virtual machine cannot be saved mid-node; Unity and Godot have the same limit.
- Views in the WPF Game Studio.
- **Visual scripting, avoided as much as possible (N).** The graph shows and arranges Yarn nodes, and game logic stays in C#. This is a preference, not a ban: if a piece of visual scripting turns out to be what makes a Yarn feature supportable, it is revisited case by case and comes back to Nicolas as a decision.

**Anti-goals** (a failure even if it works)

- **A fork or a dialect of Yarn.** Skein takes Yarn Spinner from nuget.org and never changes it. Every file Skein writes must compile with the stock Yarn tools.
- **Patching Stride to make Skein work.** A gap is a finding (section 13).
- **An extension that only works inside Stride's tree** (project references to `sources/`, internal APIs, build files of the repository). Skein may move to its own repository later; a temporary workaround is allowed when recorded (section 4).
- **Breaking the WPF Game Studio.** It must still open the sample.
- **Implying endorsement by the Yarn Spinner team** (their trademark rules, section 4).

## 3. Concepts

Yarn Spinner's own terms are kept, so their documentation applies.

| Concept | Meaning |
|---|---|
| **Yarn script** | A `.yarn` text file holding nodes. |
| **Node** | A titled passage: headers (`title:`, `tags:`, `when:`, free `key: value` pairs), then a body of lines, options and statements. |
| **Line** | One thing said, usually `Speaker: text`. It can carry a line id (`#line:a1b2`), tags, markup and substitutions (`{$gold}`). |
| **Options** | Choices offered to the player (`-> text`), each with an optional condition and a body. |
| **Jump, detour** | `<<jump Node>>` goes to a node; `<<detour Node>>` goes and comes back. |
| **Variables** | Declared with a type and a default (`<<declare $gold = 5>>`): number, string, bool or an enum. A **smart variable** is declared with an expression and computed on each read. |
| **Node group** | Several nodes with the same title and different `when:` conditions. Running the title picks one by **saliency** (most specific first, by default avoiding repeats). **Line groups** (`=>`) do the same for single lines. |
| **Command** | `<<walk Odile pier>>`: an instruction to the game. The dialogue waits for it if the handler is asynchronous. |
| **Function** | Called in expressions: built in (`visited("Node")`, `dice(6)`) or provided by the game (`has_item("oil")`). |
| **Yarn project** | The set of scripts compiled together, with its languages and settings. |
| **Dialogue runner** | The component that runs nodes of a project in a game and talks to the presenters. |
| **Presenter** | A component that shows lines or options: a text box, a list of buttons, a bubble over a character. Unity called them dialogue views. |
| **Line provider** | Turns a line id into the text of the current language. |
| **Variable storage** | Where the variables live. It is what a game saves. |

## 4. Approaches considered

### D1. The source of truth **(D: A)**

- **A. Plain `.yarn` files** (picked). The editor is a graph of nodes with a text pane; the property grid edits node headers. All Yarn features work by construction; files stay editable in VS Code and portable to other engines. Cost: lines are text, not cards, and graph edits must patch text carefully.
- B. Structured Stride assets as in Saga, generating Yarn. The property grid reaches every line, but a second authoring model must chase Yarn's language, and Yarn's tools no longer apply.
- C. A plus a graph of lines and options. The hardest round trip; possible later on the same files.

### D2. Acts, scenarios and quests **(D: A)**

- **A. By convention, in Yarn** (picked): enums and variables for states, smart variables for derived conditions, node groups for "what does this character say now", free headers (`act:`, `scenario:`, `quest:`) that the editor can group and filter by. The sample defines the convention (section 12).
- B. Saga's story layer on top of Yarn: a second runtime to design, save and debug.
- C. An editor-only story map that generates Yarn declarations. Decided after the sample, with evidence.

### D3. The Yarn Spinner version **(D: A)**

- **A. 3.2.2 from nuget.org** (picked), as the Godot plugin does, behind one adapter shaped like 3.3's `IDialogueResponder`.
- B. The unreleased 3.3 built from source and embedded: an API that still moves, and a private build inside our packages.

### D4. The name **(D: A)**

Yarn Spinner's branding rules for "a non-game product or tool that supports Yarn Spinner" forbid "Yarn Spinner" or "Yarn" in the product name without written permission, and allow referential phrases.

- **A. A name of our own** (picked): **ColorRise.Skein**, described as "Yarn Spinner support for Stride games" and "built with Yarn Spinner", never as an official integration.
- B. Ask them for "Yarn Spinner for Stride"; rename on a yes.
- C. Use their name now and settle later.

What the name does not change **(C)**: types that are Yarn's own concepts keep Yarn's names (`YarnProject`, `[YarnCommand]`, `[YarnFunction]`, `DialogueRunner`), as in the Unity and Godot APIs, so that their documentation and samples read the same. The file formats `.yarn` and `.yarnproject` are theirs and keep their names. The README carries their MIT license, the notices of the dependencies and the credit line they suggest.

### How Skein is built against Stride **(C, as Saga's A)**

Its own folder and solution on a branch of the fork, Stride through packages of a local engine build only. Yarn Spinner comes from nuget.org as a normal `PackageReference`, which is what the Godot plugin does. Unity's way (prebuilt DLLs inside the package) exists only because Unity has no NuGet.

**Ready to leave the fork (N).** Skein lives in the fork for convenience and experimentation, and may move to a repository of its own once the Stride features it needs are published. So it uses only what a third-party plugin gets from Stride's packages: no import of a `.props` or `.targets` file of Stride's tree, no path into `sources/` or `build/`, no file of the repository outside its own folder. Its own `Directory.Build.props`, `Directory.Build.targets` and `nuget.config` stop MSBuild from picking up the repository's. The rule can be relaxed for a time as a workaround; each relaxation is written in `docs/plugin-findings.md` with what Stride would have to publish to remove it, and the README lists the ones still open.

## 5. Packages and layout

Everything lives in `extensions/ColorRise.Skein/` on the branch `feature/colorrise-skein`, based on the tip of `feature/xplat/plugin-follow-ups` (node positions, `NodesMoved`, node double click and context menus, the editor test kit). Nothing outside that folder changes. **(C)**

```
extensions/ColorRise.Skein/
  ColorRise.Skein.slnx
  Directory.Build.props, Directory.Build.targets, nuget.config   its own, cutting it off from the repository's (section 4)
  README.md                  what it is, build, open the sample, credits and licenses
  THIRD-PARTY-NOTICES.md     Yarn Spinner (MIT), ANTLR 4, Protobuf, CsvHelper, ...
  docs/design.md             this document
  docs/guide-writers.md      writing and playing Yarn in Game Studio
  docs/guide-developers.md   runner, presenters, commands, saves, languages
  docs/plugin-findings.md    section 13
  src/ColorRise.Skein/                   runtime
  src/ColorRise.Skein.UI/                presenters on Stride's UI
  src/ColorRise.Skein.Assets/            Assets companion
  src/ColorRise.Skein.Editor/            Editor companion, toolkit-neutral
  src/ColorRise.Skein.Editor.Avalonia/   views for the cross-platform Game Studio
  tests/ColorRise.Skein.Tests/           runtime, compiler bridge, text patching
  tests/ColorRise.Skein.Editor.Tests/    view models in a real editor session (test kit)
  sample/Brinemoor/                      the sample game (section 12)
```

| Package | Kind | Holds | References |
|---|---|---|---|
| `ColorRise.Skein` | Runtime | compiled project content, dialogue runner, variable storage, line provider, presenter base, commands and functions, the engine-free dialogue session | `Stride.Engine`, `YarnSpinner` |
| `ColorRise.Skein.UI` | Runtime | line presenter, options presenter, line advancer, on Stride's UI | `ColorRise.Skein`, `Stride.UI` |
| `ColorRise.Skein.Assets` | `Assets` | the script and project assets, the compiler bridge, the text patcher, compilers, templates, icons, strings files | `Stride.Core.Assets`, the runtime, `YarnSpinner.Compiler` |
| `ColorRise.Skein.Editor` | `Editor`, neutral | plugin class, editor view models, player service, commands, property grid pieces | `Stride.Assets.Editor`, Runtime, UI and Assets |
| `ColorRise.Skein.Editor.Avalonia` | `Editor`, Avalonia | graph and text views, player and timeline views, node picker template | the Editor package, `Stride.Assets.Editor.Avalonia`, `AvaloniaEdit` |

The UI presenters are a package of their own so that a game with its own dialogue UI does not pull `Stride.UI` in. **(C)** A game references `ColorRise.Skein` directly, and `ColorRise.Skein.UI` too when it wants the provided presenters: Stride creates a plugin's game systems only for packages the game references directly, so the UI package alone would not bring the `DialogueSystem`. The guide and the asset template say so. All target `net10.0`. Repository conventions apply: explicit access modifiers, sealed by default, no WPF mention in Avalonia code, `MultiBinding` only inline.

## 6. Assets and data **(D: follows D1 A)**

### Yarn script asset

- A `.yarn` file in an asset folder of the game **is** the asset: `YarnScriptAsset` derives from Stride's `SourceCodeAsset`, as scripts and shaders do. The file holds Yarn text and nothing else, so any Yarn tool can read and write it.
- It has no compiled content of its own. Its project compiles it.
- Layout and looks are Yarn's own headers, the ones their VS Code extension reads and writes: `position: x,y`, `color:`, `cluster:` (a visual group) and `style: note` (a sticky note). A file arranged in Game Studio looks the same in VS Code, and the other way round.

### Yarn project asset

`YarnProjectAsset`, file `.sdyarn` **(C)**, a normal Stride asset, so the property grid edits it as any asset:

| Property | Meaning |
|---|---|
| Source files, excluded files | Glob patterns relative to the asset's folder, default `**/*.yarn`. Same meaning as in a `.yarnproject`. |
| Base language | The language the scripts are written in. |
| Localisations | For each other language, a strings file (CSV, Yarn's own format). |
| Line id style | How "Add line ids" names new ids: random or descriptive (Yarn's two generators). |
| Saliency strategy | The default of the project's runners (Yarn's four strategies). |
| Diagnostic severities | Per error code, as Yarn's `compilerOptions`. |
| Write `.yarnproject` | On by default: keeps a `.yarnproject` and a definitions file (`.ysls.json`, the commands and functions of the game) next to the asset, for VS Code and Yarn's command line. The asset is the truth for the keys Skein owns (source and excluded files, languages, definitions, compiler options). Yarn's VS Code extension keeps its own data in the same file (positions in its project view, character colours, spell checking), so Skein merges: it rewrites its keys and preserves every other. **(C)** |

Why a Stride asset and not the `.yarnproject` file itself **(C)**: the property grid, references from components, and build dependencies all need a real asset. Writing Skein's keys of the `.yarnproject` one way avoids two truths.

### Compiled content

One `YarnProject` content per project asset: the compiled program, the node names and headers, the line metadata, and one string table per language. Each language is a content of its own, loaded when that language is selected. The same bridge compiles for the build and for Game Studio, from the assets in memory.

### Build

- A Yarn error fails the build with the file, the node, the line and column, and Yarn's stable error code. Warnings pass.
- The project compiler depends on every script it includes: editing a script rebuilds the project.
- A project with localisations and lines that have no line id gets a warning naming them, as in Unity.
- The functions the game declares (section 7) are given to the compiler with their types, from the game's assemblies. On 3.2 this improves type checking; on 3.3 it is required.
- The diagnostic severities of the project are applied to the compiler's result (3.2.2 exposes that as a public step after compilation, not as a job setting).

### Line ids and languages

- "Add line ids" writes `#line:` tags into the scripts with Yarn's own tagger.
- "Export strings" writes Yarn's CSV (language, id, text, file, node, lineNumber, lock, comment) for a language; "Update strings files" adds new lines, removes dead ones and marks the lines whose source changed, as Unity's does.
- A missing translation falls back to the base language, with one warning per line and run.

## 7. Runtime

### The pieces

- **`DialogueSystem`**, a `[GameSystem]`: the registry of commands and functions, the game-wide variable storage, the current language. One per game.
- **`DialogueRunnerComponent`** on an entity: the project, an optional start node and auto start, its presenters, an optional storage and line provider of its own (the game-wide ones by default), the saliency strategy, and whether options with nothing available fall through. Calls: `StartDialogue(node)`, `Stop()`, `RequestNextLine()`, `RequestHurryUp()`. State and events: running, current node, node started and completed, dialogue started and completed, unhandled command.
- **Several runners can run at once**, for example a conversation and background chatter between two characters.
- **`DialoguePresenter`**, an abstract script a game derives from. All methods are virtual and asynchronous: run a line, run options (returns the choice, or none to let another presenter answer), dialogue started and completed, node entered and left. A line goes to every presenter of the runner at once, and the runner waits for all of them.
- **`LocalizedLine`**: the line id, the text with its markup parsed, the speaker name, the text without the name, the metadata tags.
- **Two requests, as in Yarn for Unity**: "hurry up" (finish the typewriter) and "next line" (cancel what remains). A presenter gets both as cancellation tokens.

### Running on Stride **(C)**

A dialogue runs as a micro-thread of the game's script scheduler, on the game thread. Presenters and command handlers are ordinary `async` code and can `await Script.NextFrame()`. Nothing runs on another thread.

### The engine-free session

The runner is a thin Stride shell around `DialogueSession`, a class with no Stride dependency: the Yarn `Dialogue`, an `IDialogueResponder`-shaped interface, the storage and the line provider. Game Studio's player and the tests drive the same class. The adapter between 3.2's callbacks and that interface is one internal class (D3). **(C)**

### Commands and functions

```csharp
public sealed class Door : SyncScript
{
    [YarnCommand("open")]            // <<open CellarDoor>> runs on the entity named CellarDoor
    public async Task Open() { ... } // the dialogue waits for the task

    [YarnFunction("has_item")]       // <<if has_item("oil")>>
    public static bool HasItem(string item) => Inventory.Contains(item);
}
```

- A command is a static method, or an instance method of a component. For an instance method, the first word after the command names the target.
- **Finding the target (C):** first among the entities that carry a `DialogueActorComponent` with that name, which registers itself and survives scene loading and spawning; then by entity name in the runner's scene, as Unity does with game objects. Saga's research found name lookups fragile, hence the actor component first.
- Parameters convert from text: string, bool, numbers, enums, and an entity or a component found as a target is. Optional parameters and `params` work.
- A command can return nothing or a task. A function is static and returns a bool, a number or a string.
- **Registration (C):** by the attributes, found by reflection in the assemblies Stride registers when the game starts, or by an explicit `AddCommandHandler` and `AddFunction`. A source generator replaces the reflection later (section 15). The guide says what to preserve in a trimmed build.
- Built in: `wait` and `stop`, and Yarn's standard library of functions.
- A command nothing handles raises the runner's event and logs a warning once. A function nothing provides is a build error when declared functions are known, else a logged error and the type's default.

### Variables and saves

- `VariableStorage` implements Yarn's `IVariableStorage`: typed get and set, a change event (a journal or a HUD listens to it), `Save()` to a serializable snapshot and `Load(snapshot)`. Smart variables are computed, never stored.
- A game saves the snapshot with its own save file. The guide states the limit: save between dialogues. Node visit counts and `once` flags are variables of the storage, so they are saved too.
- Loading a snapshot made with an older story drops what the project no longer declares, with one warning listing it (Saga's rule).

### Lines and markup

- `TextLineProvider` serves the string table of the current language and loads another when the language changes.
- Markup is parsed by Yarn's `LineParser`: the `character` attribute, the built-in `select`, `plural` and `ordinal` replacements, and any game attribute. A presenter receives the attributes with their positions.
- Yarn's inline events work in the provided presenter: `[pause=500/]`, and any attribute a game maps to an action.

### The UI package

- **Line presenter:** speaker name, text, typewriter by letter or by word with a speed, pauses from markup, an indicator when the line is complete.
- **Options presenter:** one button per option, unavailable options hidden or disabled by setting, keyboard and gamepad navigation.
- **Line advancer:** turns an input into "hurry up" then "next line".
- Each builds a default look in code when given no UI page, and binds to named elements of a page when given one, so it works at once and a game restyles it with a normal UI page. **(C)**

## 8. Game Studio (cross-platform)

### Script editor

Opening a `.yarn` asset shows two panes, side by side.

```
+-- Act1.yarn -----------------------------------------------------------+
| [+ Node] [+ Note] [Layout] [Group by: quest v] [Search....]   [Play >] |
|------------------------------------------+-----------------------------|
|  +- Odile ------------+                  | title: Odile                |
|  | when: $act == ...  |---> +- Odile_Key | when: $act == Act.Arrival   |
|  | Odile, Player      |     +------------| quest: lamp                 |
|  | "The lamp's out.." |                  | ---                         |
|  | -> I'll look.      |- -> (Act2.yarn   | Odile: The lamp's out again.|
|  | -> Not my problem. |      > Wreck)    | -> I'll look.               |
|  +--------------------+                  |     <<jump Odile_Key>>      |
|------------------------------------------+-----------------------------|
| 1 error: Act1.yarn (41,12) YS0012 jump to unknown node 'Odile_Kye'     |
+------------------------------------------------------------------------+
```

**The graph**

- One card per node: title, `when:` condition and its saliency score for a member of a node group, speakers, a preview of the first lines, the options.
- Links are jumps (solid) and detours (dashed), from the compiler's own metadata. A jump into another file ends on a small stub card naming the file and node; a double click opens that file there. A jump to a node that does not exist ends on a red stub.
- Members of a node group are stacked under one title. Clusters (`cluster:`) are framed. Notes (`style: note`) are drawn as notes.
- **Group by header**, which is what makes D2 A workable: pick any header (`act`, `scenario`, `quest`, `cluster`, `tags`) to frame or tint the cards by its value, and filter by value. The search box filters by title, tag, speaker and text.
- Editing: add a node or a note at the cursor, rename, duplicate, delete, set the colour, drag from a card to another to append a jump, drag to move. A file with no positions is laid out as a tree.
- **Renaming a node** updates the jumps and detours to it in every script of the project, in one undo step.
- Node context menu: Play from here, Rename, Duplicate, Delete, Colour.

**The text pane**

- The body and headers of the selected node, or the whole file (a toggle). Built on AvaloniaEdit.
- Colours come from the Yarn compiler's own lexer, so they are exact for the version in use. **(C)**
- Errors and warnings are underlined as you type, from a background compile of the project's scripts in memory, and listed under the panes with their code. A double click goes to the place.
- Ctrl+click on a jump target selects that node.

**One history.** Typing, graph edits and property grid edits are steps of the session's undo and redo, so the Edit menu and the history panel show them all. The script is dirty until saved, and saves with the session. **(C)**

**Edited outside.** When another program changes the file and Game Studio has no unsaved edit of it, the editor reloads it, as Game Studio's script editor does. With unsaved edits on both sides, it asks.

### Property grid

- **A selected node** shows its headers: title, tags, colour, cluster, the `when:` conditions, and the free headers as a key and value list. A change rewrites that header in the text.
- **The project asset** shows the properties of section 6.
- **A component that names a node** shows a searchable list of the project's node names instead of a text box. It works for Skein's runner and for any game component, through `[YarnNode(nameof(Project))]` on a string member, as in Unity.
- **The runner and the presenters** are edited as any component.

### Project overview

Opening a project asset shows a page, not a graph: the scripts with their node counts, the declared variables (name, type, default, description, where; a double click goes there), the problems of the whole project, and each language with its counts (lines, translated, to update) and the buttons Add line ids, Export strings, Update strings files.

### Dialogue player

A panel (View menu), on the engine-free session and the assets in memory, with no build:

- Pick a project and a node, or use "Play from here" on a card.
- The transcript with speakers and options. Unavailable options are shown greyed.
- **Variables**, live and editable; smart variables are shown with their expression, read-only.
- **Commands** appear in the transcript and continue at once. **Game functions** are answered by hand, with the answer remembered for the run. `wait` can be honoured or skipped. Yarn 3.2.2 calls functions synchronously, so the player does not block on the question: it stops the step, asks, and plays the step again from its recorded start with the answer known, which the replay machinery already does. No thread waits on the user. **(C)**
- **Why this node:** when a node group or line group picks, the transcript shows the candidates, their scores, and the condition that ruled each loser out.
- **Language:** switch to check a translation.
- **Live highlight:** the graph outlines the current node and the path taken; the text pane marks the current line.
- **Timeline, rewind and replay after an edit**, carried over from Saga's second version: one column per step, click to go back, and Replay plays the same choices on the edited scripts and stops where they no longer apply. Random functions are seeded per run so a replay is the same run: in the player, Skein registers its own seeded `random`, `random_range` and `dice` in place of Yarn's (theirs share a private generator) and uses a seeded copy of the random saliency strategy. A game keeps Yarn's own. **(C)**

### Commands, templates, the rest

- Commands, with ids `ColorRise.Skein.*`: Play from here (node menu), Play project (asset view menu), Open dialogue player, Add line ids, Export strings, Update strings files. All in the palette.
- *Add asset*: "Yarn project" (a project and a first script) and "Yarn script".
- *Add entity*: "Dialogue runner" (a runner with the two presenters and the advancer when the game references the UI package, the runner alone otherwise). **(C)**
- Icons for the assets and the components.

### WPF Game Studio

No Skein views. It must open the sample: the project asset shows in the generic property grid, scripts show as assets. To check on Windows or under Wine.

## 9. What Skein's editor adds to Yarn's own tools

Yarn's VS Code extension already has a graph view, a preview and error checking. Skein does not compete with it; the files are shared. What only an engine integration can give:

- The build, the runtime and the components.
- The property grid, and node pickers on components.
- Grouping and filtering the graph by a game's own headers.
- A player with a timeline, rewind, replay after edits, and the reason for each saliency pick.
- "Play from here" from a card, and later from an entity of a scene.

## 10. Plugin points exercised

| Plugin point | By | New since Saga |
|---|---|---|
| Runtime with companions, `[GameSystem]`, components, content by URL | runtime | |
| A second runtime package depending on another plugin (`Stride.UI`) | UI package | yes |
| NuGet dependencies of a plugin reaching the game, the asset compiler and Game Studio | Yarn Spinner packages | yes |
| A text file as a third party's asset (`SourceCodeAsset`) | script asset | yes |
| A compiler depending on a set of other assets found by pattern | project compiler | yes |
| Asset editor with a third party's views, the node graph with bound positions, `NodesMoved`, node menus, double click | script editor | the follow-ups |
| A text editor inside a plugin's asset editor | text pane | yes |
| The property grid on something that is not an asset's own graph | node headers | yes |
| A property template and a node presenter updater from a plugin | node picker | yes |
| A plugin's own steps in the session's undo history | script editor | yes |
| Reloading a file changed outside | script editor | yes |
| Panels, session services, one editor reacting to another | player, timeline | |
| Commands, context menus, the palette | commands | |
| Asset templates, entity factories, icons | templates | entity factory |
| The editor test kit | editor tests | yes |

## 11. Risks, and what is tried first

The plan starts with short spikes on the points nobody has exercised, each with its fallback. A fallback that loses a goal comes back to Nicolas as a decision.

| # | Risk | Fallback |
|---|---|---|
| R1 | A third party's `SourceCodeAsset` in an asset folder: loading, creating from a template, renaming (its id comes from its location), build dependencies, the WPF Game Studio. Stride's only precedent outside project files is the effect log. | A normal asset pointing at a `.yarn` source file, as a texture points at its image. Two files per script. |
| R2 | The property grid on node headers: a source code asset has no property graph, so the headers need a graph of their own that the session's grid accepts. | A header form inside the script editor. The standard grid still serves the project and the components. |
| R3 | One undo history for typing and graph edits, with AvaloniaEdit's own history out of the way. | The text pane keeps its own history, and graph edits are steps of it. |
| R4 | Yarn Spinner's dependencies (ANTLR, Protobuf, CsvHelper, System.Text.Json) loading beside Stride's in the asset compiler and Game Studio. | Finding, and a workaround inside the extension if one exists. |
| R5 | Stride's UI draws one style per text block. | Stated limit; the attributes are delivered, so a game's own presenter can do more. |
| R6 | Yarn Spinner 3.3 reaching nuget.org during the work. | Finish on 3.2.2, then move the adapter (D3). |
| R7 | Renaming a node across files that are not open. | Patch through their assets in memory, marked dirty; if that proves unsafe, rename only in open files and report the others. |

## 12. The sample: "Brinemoor" **(C, for approval)**

An original story. Brinemoor is a tidal island town reached by a causeway. The player is its new courier. The game is small on purpose, as Saga's demo was: simple shapes on an island, walk, talk, use, a dialogue box, a journal, a language switch (English and French) and one save slot.

### The story convention (D2 A made concrete)

- `Story.yarn` declares `enum Act`, `enum QuestState` (not started, offered, active, succeeded, failed), `$act`, one `$quest_<id>` per quest, and smart variables for derived facts (`$lamp_can_be_lit = $has_oil and $has_key`).
- Every node carries `act:`, and `scenario:` or `quest:` where it belongs to one. The editor groups by them.
- A character is a node group titled by the character's name. Talking runs the group, and saliency picks the most specific node whose `when:` holds.
- Scenario events are a node group `Events`. The game runs it whenever it has content and no dialogue plays. That is how a scenario "happens when its conditions hold, whatever the player is doing".
- The game reports facts with variables and reacts to commands (`<<quest_changed lamp>>` for the journal toast). About 100 lines of game code make the director and the journal; if they prove generic, they move into Skein later.

### Acts, scenarios, quests

| Act | Scenarios | Quests |
|---|---|---|
| **I. Arrival.** The causeway is open. | *The dark lamp:* the lighthouse is out and the keeper, Odile, is missing. *The harbour dues:* the harbourmaster, Brandt, raises the dues and the fishers grumble. | **Letters for Brinemoor** (three deliveries, in any order). **Light the lamp** (find oil and the key; fails if night falls first). **The cat on the roof** (optional, once). |
| **II. The wreck.** A ship reaches the island at night. Lit lamp: it anchors. Dark lamp: it runs aground. | *Salvage:* fishers and harbourmaster both claim the cargo. *Odile's trail:* clues point to a smugglers' cave. *The stranger:* a passenger, Wren, with a secret and a trust level. | **Salvage or seize** (two exclusive ways to finish). **Follow the trail** (three clues, any order). **A warm meal** (optional, raises Wren's trust). |
| **III. High tide.** The island is cut off. | *The cave:* the smugglers, and what Odile was doing there. *The vote:* who keeps the light. | **Free Odile**, or let her go. **Swing the vote** (four townsfolk, each moved by what the player did). Then one of four endings. |

Act II has two variants and Act III depends on the side taken and on Wren's trust, so the plot branches without duplicating the acts.

### Yarn features, and where the sample uses each

| Feature | Where |
|---|---|
| Nodes, lines, options with conditions, nested options | everywhere |
| Jumps and detours | the town meeting of Act II is a hub that detours into each topic |
| Declared variables with descriptions, enums, smart variables | `Story.yarn`, quest states, the derived facts |
| `if`, `elseif`, `else`, `set`, interpolation `{$name}` | deliveries, dues |
| Node groups, `when:`, `once`, saliency strategies | every character; the `Events` group |
| Line groups (`=>`) | greetings, the epilogue's summary lines |
| `once` blocks and `visited`, `visited_count` | the cat; "you again?" |
| Standard functions (`dice`, `random_range`, `round`, `format`) | haggling over the dues |
| Game functions | `has_item("oil")`, `time_of_day()` |
| Commands, static and on an actor, awaited | `<<walk Odile pier>>`, `<<give oil>>`, `<<wait 1>>` |
| Markup: character, `select`, `plural`, `ordinal`, inline events | letters count, "the 2nd time", pauses |
| Line ids, tags and metadata on lines | `#line:` everywhere, `#mood:angry` drives the speaker's colour |
| Shadow lines | a repeated farewell |
| Options that fall through | an option list with nothing left |
| Free headers, clusters, colours, notes | `act:`, `scenario:`, `quest:`; writer's notes |
| Languages | English and French string tables |
| Variable storage, save and load | the save slot, between dialogues |
| Two runners at once | fishers chatting on the pier while the player talks |

Size: about 60 nodes in five scripts (`Story`, `Act1`, `Act2`, `Act3`, `Chatter`).

## 13. Plugin findings

`docs/plugin-findings.md` records each problem met in the plugin model: what Skein tried, what happened, the workaround kept inside the extension, and the change suggested to Stride. A finding that blocks a feature leaves the feature out, and Nicolas decides what goes upstream. Saga's findings that the follow-ups branch fixed are checked off there when Skein uses the fix.

## 14. Testing and checks

- **Runtime and compiler bridge** (`ColorRise.Skein.Tests`): compile and run scripts through the engine-free session with recording presenters; options, saliency, commands and their parameter conversions, functions, storage save and load, languages and fallbacks, the 3.2 adapter.
- **Yarn's own test plans:** the upstream repository ships scripts with expected transcripts. A selection runs through Skein's session as a conformance suite, with their license notice. **(C)**
- **Text patching:** every graph and header edit is tested as text in, text out, including comments, blank lines and odd formatting. A patch must never change a line it does not own.
- **Editor view models** in a real session with the test kit: the graph model from scripts, each edit as one undo step, rename across files, the player, replay.
- **Game Studio under Xvfb** on the sample: the walkthrough with screenshots in the project files.
- **The sample game** runs on Linux. A scripted playthrough reaches each of the four endings at the level of the engine-free session, with the sample's director, clock and inventory written engine-free so the test drives them; a launch of the real game under Xvfb is the smoke test. In-game input is not automated. **(C)**
- **Outside the tree:** the packed packages are restored and used by a small game outside the extension's folder, as Saga's README did, which is what the anti-goal of section 2 asks for. **(C)**
- Tests clean up what they start. The WPF Game Studio is checked on Windows or Wine when Nicolas wants.

## 15. Later

In rough order of value: voice-over and line assets; completion and hover in the text pane; a source generator for commands (3.3's own, once on nuget.org); the move to Yarn Spinner 3.3; "Play from here" on an entity; the story map (D2 C) or Yarn's own quest graphs if they ship; a line-level graph (D1 C); bubbles over characters; watching a running game from Game Studio.

## 16. Left to the builder

Class and member names beyond those given; the graph's styling and the cards' exact content; how the text pane colours and underlines; the patcher's internals; the timeline's drawing (Saga's is the model); the default look of the presenters; the sample's dialogue text and its game code; the order of the work as long as each step builds, the spikes of section 11 come first, and each milestone is pushed to the fork.
