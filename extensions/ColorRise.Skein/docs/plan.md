# ColorRise.Skein v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first version of ColorRise.Skein: Yarn Spinner's dialogue engine in Stride games, with a graph and text editor, the property grid and a dialogue player in the cross-platform Game Studio, and the sample game "Brinemoor".

**Architecture:** Five packages in `extensions/ColorRise.Skein/` that reach Stride through packages only and Yarn Spinner 3.2.2 through nuget.org: a runtime (engine-free dialogue session behind a 3.3-shaped responder, plus its Stride shell), a UI package of presenters, an `Assets` companion (the `.yarn` and `.sdyarn` assets, the compiler bridge, the text patcher), a toolkit-neutral `Editor` companion (view models, analysis and player services, commands) and its `.Avalonia` companion (views). Plain `.yarn` files are the truth; every editor change is a text patch.

**Tech Stack:** C# / .NET 10 (`net10.0`), Stride 4.5.3 dev packages of `feature/xplat/plugin-follow-ups`, `YarnSpinner` and `YarnSpinner.Compiler` 3.2.2, Avalonia 12.1.1 and AvaloniaEdit 12.0.0 (the versions Game Studio ships), xUnit 2.9.3.

**Spec:** [design.md](design.md). Read it before any task.

**Execution:** native, in the design thread, on Fable (Nicolas, 2026-11-15), then one whole-branch review by a fresh reviewer (Task 25).

## Global Constraints

- Nothing outside `extensions/ColorRise.Skein/` changes. A gap in Stride's plugin model goes to `docs/plugin-findings.md` (what Skein tried, what happened, the workaround kept inside Skein, the change suggested to Stride); the feature waits rather than Stride being patched.
- **Ready to leave the fork.** Only what a third-party plugin gets from Stride's packages: no `ProjectReference` into `sources/`, no import of a `.props` or `.targets` of Stride's tree, no read of a file of the repository outside the folder. Skein's own `Directory.Build.props`, `Directory.Build.targets` and `nuget.config` cut it off from the repository's. A temporary relaxation is allowed only when written in `docs/plugin-findings.md` under "Tied to the fork" with what Stride must publish to remove it, and listed in the README. Known from the start: the `stride-dev` feed of `nuget.config` points at `../../bin/packages`.
- The Stride version is `$(SkeinStrideVersion)`, default `4.5.3-dev` committed in `Directory.Build.props`, overridden in a git-ignored `Directory.Build.local.props`. It is not read from Stride's tree.
- Yarn Spinner is never changed or embedded: `PackageReference` to `YarnSpinner` and `YarnSpinner.Compiler` `3.2.2` only. Every file Skein writes (`.yarn`, `.yarnproject`, `.ysls.json`, strings CSV) must be accepted by the stock Yarn tools.
- Yarn 3.2's handler API is used in one internal class, `Yarn32Adapter`. Everything else talks to `IDialogueResponder`.
- Names: the product is "Skein", described as "Yarn Spinner support for Stride games" and "built with Yarn Spinner"; never "official" or "Yarn Spinner for Stride". Types that are Yarn's concepts keep Yarn's names (`YarnProject`, `[YarnCommand]`, `[YarnFunction]`, `[YarnNode]`, `DialogueRunner...`).
- Target `net10.0`, not .NET 11. Explicit access modifiers, classes `sealed` by default, nullable enabled.
- Avalonia code: no mention of WPF (comments, docs, test names); `MultiBinding` only inline `{sd:MultiBinding {Binding A}, {Binding B}, Converter=...}`.
- Experimental Stride API: opt in per project with `<NoWarn>` for the ids used only (`STREXP001` panels and views, `STREXP003` commands, `STREXP006` services, `STREXP010` node graph).
- Ids: commands `ColorRise.Skein.<Name>`; panels `ColorRise.Skein.DialoguePlayer`, `ColorRise.Skein.DialogueTimeline`. Files: `.yarn` (script), `.sdyarn` (project asset). Asset format version key `ColorRise.Skein`, starting at `1.0.0.0`. Layout headers are Yarn's: `position: x,y`, `color:`, `cluster:`, `style: note`.
- Visual scripting is avoided as much as possible; if a Yarn feature seems to need some, stop and bring it to Nicolas as a decision.
- Tests start nothing they don't stop and clean up (games, sessions, temp folders).
- Git: branch `feature/colorrise-skein` from the tip of `origin/feature/xplat/plugin-follow-ups` (17f6ee7d7 on 2026-11-15; fetch again first). `git config user.name "Nicolas Musset"`, `user.email "musset.nicolas@gmail.com"` before the first commit. Conventional titles `feat(skein): ...`, `test(skein): ...`, `fix(skein): ...`, `docs(skein): ...`. No `Co-Authored-By` line, no Claude or session link. A force-push of this branch needs no approval. Never touch the Saga branches or other stacks.
- **Commit and push often, so Nicolas can try the work while it progresses** (Nicolas, 2026-11-15): at least once per task, more often inside a long one. Every push builds (`dotnet build` of the solution) and leaves the sample opening in Game Studio once it exists. The README's section "Try it" (build, open the sample, what works today, what does not yet) is updated in the same push whenever what can be tried changes.
- Workspace setup: reuse an existing .NET SDK, never `curl | bash` or a downloaded script, one plain step per command, no `&` background jobs.

## Review Focus

1. **A file with CRLF line endings, a byte order mark, tabs, comments between nodes or trailing spaces.** A graph or header edit changes only the lines it owns; every other byte of the file is the same after the patch. Tests in Task 14.
2. **A half-typed script while its graph is open.** The graph keeps a card for every node a tolerant scan finds (title and position at least), shows the errors, and never empties or throws; graph edits still apply. Tests in Tasks 14 and 15.
3. **A script that belongs to no project, or to two.** With none, the editor opens it, compiles it alone and says "This script is in no Yarn project" above the panes; the build ignores it with a warning naming it. With two, both compile it, and the editor uses the first project by URL and says so. Tests in Tasks 11 and 15.
4. **Stop in the middle of a line, of options or of a command.** The presenters' tokens are cancelled, "dialogue completed" is raised once, nothing is left awaiting, and the same runner starts another dialogue. Tests in Tasks 8 and 9.
5. **Renaming a node to a title that exists, or to one Yarn refuses.** The rename is refused with the reason and nothing is patched; adding a node with the title of another makes a node group only when asked ("Add to group"). Tests in Tasks 14 and 15.

---

### Task 0: Workspace and engine build (no commit)

**Files:** none in git.

- [x] **Step 1:** Install a .NET 10 SDK with no approval prompt: read `https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json`, download the latest `dotnet-sdk-<v>-linux-x64.tar.gz` to the scratchpad, check its sha512 against the json, extract to `/root/.dotnet`, use `PATH=/root/.dotnet:$PATH DOTNET_ROOT=/root/.dotnet`. If that host is blocked, `apt-get install -y dotnet-sdk-10.0` and the Roslyn 5.3 toolset from nuget.org (project memory `linux-build-setup`).
- [x] **Step 2:** `git fetch origin feature/xplat/plugin-follow-ups`, create `feature/colorrise-skein` on its tip, set the git identity.
- [x] **Step 3:** Pull LFS from the fork: `git -c lfs.url=https://github.com/Kryptos-FR/stride.git/info/lfs lfs pull --include='deps/**'`, then the include list of project memory `container-lfs-setup`.
- [x] **Step 4:** `dotnet build sources/Stride.Editor.Avalonia.slnf`, then `dotnet build sources/assets/Stride.AssetCompiler/Stride.AssetCompiler.csproj` (Debug).
- [x] **Step 5:** Check `bin/packages` holds `Stride.Engine`, `Stride.UI`, `Stride.Core.Assets`, `Stride.AssetCompiler`, `Stride.Assets.Editor`, `Stride.Assets.Editor.Avalonia`, `Stride.Core.Assets.Editor.Testing`, `Stride.Assets.Editor.Testing` at `4.5.3-dev`. A missing one is the first finding.

### Task 1: Scaffold the extension

**Files:**
- Create: `extensions/ColorRise.Skein/ColorRise.Skein.slnx`, `Directory.Build.props` (version, `net10.0`, package metadata, `SkeinAvaloniaVersion` 12.1.1, `SkeinAvaloniaEditVersion` 12.0.0, `SkeinYarnVersion` 3.2.2, `StrideGraphicsApi` Vulkan off Windows), `Directory.Build.targets` (empty unless Saga's finding 2 still holds), `Directory.Build.local.props.example`, `nuget.config` (nuget.org, plus `stride-dev` → `../../bin/packages` mapped to `Stride`, `Stride.*`), `.gitignore`
- Create: `README.md` (what it is, not affiliated with Stride nor with Yarn Spinner, build, credits "Dialogue powered by Yarn Spinner — https://yarnspinner.dev"), `THIRD-PARTY-NOTICES.md` (Yarn Spinner MIT, ANTLR 4 BSD-3, Protobuf BSD-3, CsvHelper MS-PL/Apache-2.0), `docs/design.md`, `docs/plan.md`, `docs/plugin-findings.md` (sections "Findings", "Tied to the fork", "Saga findings fixed")
- Create: `src/ColorRise.Skein/ColorRise.Skein.csproj` + `.sdpkg` (declares the Assets and Editor companions), `src/ColorRise.Skein.UI/` (references the runtime and `Stride.UI`), `src/ColorRise.Skein.Assets/` (`StridePackageKind` Assets) + `.sdpkg`, `src/ColorRise.Skein.Editor/` (`Editor`, declares the `.Avalonia` companion) + `SkeinEditorPlugin.cs`, `src/ColorRise.Skein.Editor.Avalonia/` + `SkeinEditorAvaloniaPlugin.cs`, `tests/ColorRise.Skein.Tests/`, `tests/ColorRise.Skein.Editor.Tests/` (references `Stride.Assets.Editor.Testing`)

**Interfaces:**
- Produces: `public sealed class SkeinEditorPlugin : StrideAssetsPlugin` (`ColorRise.Skein.Editor`), `public sealed class SkeinEditorAvaloniaPlugin : StrideAssetsAvaloniaPlugin` (`ColorRise.Skein.Editor.Avalonia`), both overriding `Initialize(IEditorPluginContext)` and calling the base. Project shapes follow Saga's on `origin/feature/colorrise-saga-writers-loop` (read with `git show`, never check out), without its read of `sources/shared/SharedAssemblyInfo.cs`.

- [x] **Step 1:** Write the files. `dotnet build extensions/ColorRise.Skein/ColorRise.Skein.slnx` → 0 errors, 0 warnings from Skein's projects.
- [x] **Step 2:** `dotnet pack` → five `.nupkg` in `extensions/ColorRise.Skein/bin/packages`; the Editor package records a neutral toolkit, `.Avalonia` records Avalonia; the runtime's packed `.sdpkg` lists both companions.
- [x] **Step 3:** `grep -rn "sources/\|build/" --include=*.props --include=*.targets --include=*.csproj extensions/ColorRise.Skein` → no hit. Commit `feat(skein): scaffold the ColorRise.Skein extension`, push.

### Task 2: Spike R4 and R1 — Yarn Spinner's packages and a `.yarn` asset

Settles whether Yarn's dependencies load beside Stride's (tests, asset compiler, Game Studio) and whether a third party's `SourceCodeAsset` works in an asset folder. What works stays; the outcome goes at the end of this plan under "Spike results".

**Files:**
- Create: `src/ColorRise.Skein.Assets/YarnScriptAsset.cs`, `src/ColorRise.Skein.Assets/Templates/Assets/YarnScript.sdtpl`, `tests/ColorRise.Skein.Tests/Spikes/YarnPackagesTests.cs`, `tests/ColorRise.Skein.Editor.Tests/Spikes/YarnScriptAssetTests.cs`, `sample/Brinemoor/` (a game from Stride's template with one `Assets/Dialogue/Hello.yarn`)

**Interfaces:**
- Produces: `[DataContract("YarnScriptAsset")] [AssetDescription(".yarn")] public sealed class YarnScriptAsset : SourceCodeAsset` with `public const string FileExtension = ".yarn"`.

- [x] **Step 1:** Test `Compiles_and_runs_with_the_nuget_packages`: compile `title: Start\n---\nOdile: Hello.\n===\n` with `Yarn.Compiler.Compiler.Compile`, run it with `Yarn.Dialogue`, assert one line whose text in the string table is `Odile: Hello.` and no diagnostics.
- [x] **Step 2:** Tests in a kit session (`StrideEditorTestSession.OpenAsync`) with `Hello.yarn` on disk in the package's asset folder: the asset loads as `YarnScriptAsset` with the file's text; its id equals `SourceCodeAsset.GenerateIdFromLocation(package, location)`; changing `Text` marks it dirty and saving writes exactly the text (no YAML); renaming the asset moves the file and the session still finds it.
- [x] **Step 3:** Game Studio under Xvfb on `sample/Brinemoor`: the script shows in the asset view, *Add asset* offers "Yarn script", the Plugins panel lists Skein as joined, no load error. Then a game build with a throwaway compiler that only logs the script's text length, to see Yarn's assemblies and the asset load in the asset compiler's process.
- [x] **Step 4:** Decide R1 and R4. If `SourceCodeAsset` fails one of: load, save, create, rename, build dependency → fallback (a normal `YarnScriptAsset : Asset` with `Source` pointing at the `.yarn`), which costs the goal "the file is the asset" and so goes to Nicolas as a decision before going on. Record findings. Commit `feat(skein): the Yarn script asset`, push.

### Task 3: Spike R2 — the property grid on something that is not an asset

**Files:**
- Create: `src/ColorRise.Skein.Editor/ViewModels/YarnNodeHeadersViewModel.cs`, `tests/ColorRise.Skein.Editor.Tests/Spikes/NodeHeadersGridTests.cs`

**Interfaces:**
- Produces: `public sealed class YarnNodeHeaders` (plain object: `Title`, `Tags`, `Color`, `Cluster`, `When` list, `Others` list of key and value) and `public sealed class YarnNodeHeadersViewModel : IPropertyProviderViewModel` giving the session's property grid a Quantum graph of a `YarnNodeHeaders`, with `event Action<YarnNodeHeaders>? Changed`.

- [x] **Step 1:** Test: selecting a `YarnNodeHeadersViewModel` in the session's active properties shows nodes `Title`, `Tags`, `Color`, `Cluster`, `When`, `Others`; setting `Title` through its node presenter raises `Changed` once with the new title, and is one step of `IUndoRedoService`.
- [x] **Step 2:** Game Studio check with a hard-coded object selected from the script editor stub.
- [x] **Step 3:** Decide R2: works → Task 17 builds on it; else fallback (a header form inside the script editor), which is not the design's "support through our property grid" for node headers, so a decision for Nicolas. Record findings. Commit, push.

### Task 4: Spike R3 — typing as steps of the session's undo history

**Files:**
- Create: `src/ColorRise.Skein.Editor/Text/YarnTextBuffer.cs`, `tests/ColorRise.Skein.Editor.Tests/Spikes/TextUndoTests.cs`; in `.Avalonia`: `Views/YarnTextPane.cs` (AvaloniaEdit `TextEditor` bound to the buffer, its own undo stack off)

**Interfaces:**
- Produces: `public sealed class YarnTextBuffer` over a script asset's text: `string Text { get; }`, `void Replace(int offset, int length, string text, string actionName)` (one undo step; consecutive typing within 1 s merges into one step named "Typing"), `event Action<YarnTextChange>? Changed` (`Offset`, `RemovedLength`, `InsertedText`), `void ReloadFromDisk(string text)` (a step named "Reload from disk" that leaves the script clean). The script owns its buffer: `YarnScriptViewModel.TextBuffer`.

- [x] **Step 1:** Tests: two `Replace` then undo twice gives the original text and a clean asset; undo then redo gives the text after the second; typing "abc" as three changes in a row is one step; a graph-style replace between typing steps keeps the order in the history.
- [x] **Step 2:** Game Studio check: type in the pane, Ctrl+Z in the pane and Edit > Undo both undo, the history panel lists "Typing", the caret stays where the change was.
- [x] **Step 3:** Decide R3: works → kept; else the fallback of the design (the pane's own history, graph edits as its steps). Record findings. Commit, push.

### Task 5: Compiler bridge

**Files:**
- Create: `src/ColorRise.Skein.Assets/Compilation/YarnCompilerBridge.cs`, `YarnCompilation.cs`, `YarnDiagnostic.cs`, `YarnNodeData.cs`, `YarnFunctionDeclaration.cs`
- Test: `tests/ColorRise.Skein.Tests/Compilation/YarnCompilerBridgeTests.cs`

**Interfaces:**
- Produces: `public static YarnCompilation YarnCompilerBridge.Compile(YarnCompilationRequest request)`. `YarnCompilationRequest`: `IReadOnlyList<(string FileName, string Text)> Files`, `IReadOnlyList<YarnFunctionDeclaration> Functions`, `IReadOnlyDictionary<string, YarnSeverity> Severities`, `bool StringsOnly`, `CancellationToken Cancellation`. `YarnCompilation`: `Yarn.Program? Program`, `IReadOnlyList<YarnNodeData> Nodes` (`Title`, `UniqueTitle` (the node's name in the program: the nodes of a node group share `Title`), `File`, `Headers` as ordered `YarnHeaderData(Key, Value)`, `Tags`, `NodeGroup`, `Complexity`, `Speakers`, `Preview`, `Options` (`Text`, `Line`, `Group`), `Jumps` with `Target`, `IsDetour`, `Line`, `Column`; `HeaderStartLine`, `BodyStartLine` (the line after `---`), `BodyEndLine` (the line of `===`)), `IReadOnlyDictionary<string, YarnLineData> Lines` (`Text`, `Node` (the title), `File`, `Line`, `IsImplicitId`, `Metadata` (without the `line:` and `shadow:` tags), `ShadowOf`), `IReadOnlyList<YarnVariableData> Variables` (`Name`, `Type`, `Default`, `Description`, `File`, `Line`, `IsSmart`, `IsImplicit`, `IsInternal` (the compiler's own `$Yarn.Internal.` variables)), `IReadOnlyList<YarnDiagnostic> Diagnostics`, `bool HasErrors`. `YarnDiagnostic`: `File`, `Node` (the title of the node the place is in, or null), `Line`, `Column`, `EndLine`, `EndColumn`, `Code`, `YarnSeverity Severity` (`Error`, `Warning`, `Info`; `None` only as a project's setting, it drops the diagnostic), `Message`; `ToString()` is `"{File}({Line},{Column}): {severity} {Code}: [{Node}] {Message}"` (no brackets without a node). `YarnFunctionDeclaration`: `Name`, `ParameterTypes`, `ReturnType` (each `number`, `string` or `bool`; another type throws `ArgumentException`).
- Every line and column of the bridge's types counts from 1.
- As built: Yarn Spinner 3.2.2 reports a jump to a missing node (`YS0012`) and a variable used without a declaration (`YS0003`) as warnings, so both leave a program; a project that wants them to stop the build sets their severity to `Error`, and Skein then gives no program. What the compiler itself reports as an error stays one whatever the project sets.

- [x] **Step 1:** Tests: two files with a jump across them give `Jumps[0].Target == "Wreck"` and both nodes; a jump to `Odile_Kye` gives one diagnostic at the jump's line with code `YS0012`, `Node == "Odile"`, a warning that leaves `Program` non-null; a severity override of that code to `Error` leaves `Program` null and to `None` drops it; `<<declare $gold = 5 as number>>` with `/// The purse` gives a variable `$gold`, `number`, default `5`, description `The purse`; a declared function `has_item(string) -> bool` type-checks `<<if has_item("oil")>>`, and with a `number` argument gives an error. Also: headers in order, node groups, line metadata, a syntax error, Windows line ends, an unclosed node, empty files.
- [x] **Step 2:** Implement; severities are applied to the result with `WithSeverityOverrides`. Run the tests → pass. Commit `feat(skein): the compiler bridge`, push.

### Task 6: Runtime data, variable storage, lines

**Files:**
- Create in `src/ColorRise.Skein/`: `Content/YarnProject.cs`, `Content/YarnStringTable.cs`, `Content/YarnNodeInfo.cs`, `YarnSaliency.cs`, `VariableStorage.cs`, `VariableSnapshot.cs`, `ILineProvider.cs`, `TextLineProvider.cs`, `LocalizedLine.cs`, `AssemblyInfo.cs` (`[assembly: AssetFileExtension(".sdyarn", typeof(YarnProject))]`)
- Test: `tests/ColorRise.Skein.Tests/Runtime/VariableStorageTests.cs`, `LineProviderTests.cs`, `Content/SerializationTests.cs`

**Interfaces:**
- Produces: `YarnProject` (content: `byte[] Program`, `List<YarnNodeInfo> Nodes` with `Title`, `Headers`, `Tags`; `Dictionary<string, List<string>> LineMetadata`; `string BaseLanguage`; `List<string> Languages`; `YarnSaliency Saliency`; `static string StringTableUrl(string projectUrl, string language)` = `"{projectUrl}/strings/{language}"`). `YarnStringTable` (content: `Language`, `Dictionary<string, string> Strings`). `enum YarnSaliency { First, Best, BestLeastRecentlyViewed, RandomBestLeastRecentlyViewed }`.
- Produces: `public sealed class VariableStorage : Yarn.IVariableStorage` with `event Action<string, object?>? VariableChanged`, `VariableSnapshot Save()`, `IReadOnlyList<string> Load(VariableSnapshot snapshot)` (returns the names dropped). `VariableSnapshot`: a `[DataContract]` of three dictionaries (numbers, strings, bools).
- Produces: `interface ILineProvider { string Language { get; set; } LocalizedLine GetLine(string lineId, IReadOnlyList<string> substitutions); }`, `TextLineProvider(Func<string, YarnStringTable?> load, string baseLanguage, ILogger log)`, `LocalizedLine` (`Id`, `RawText`, `Yarn.Markup.MarkupParseResult Text`, `string? CharacterName`, `Yarn.Markup.MarkupParseResult TextWithoutCharacterName`, `IReadOnlyList<string> Metadata`).

- [ ] **Step 1:** Tests: a set then `Save`, a new storage and `Load` gives the same values and `VariableChanged` once per name; a snapshot holding `$old` loaded into a storage whose program doesn't declare it returns `["$old"]` and logs one warning; a number in the snapshot for a variable now declared `bool` is dropped and the default kept; a smart variable is never in a snapshot.
- [ ] **Step 2:** Tests: `Odile: You have {0} letter[plural value={0} one="" other="s"/].` with substitution `3` gives `Text.Text == "Odile: You have 3 letters."`, `CharacterName == "Odile"`, `TextWithoutCharacterName.Text == "You have 3 letters."`; language `fr` with no entry for the line returns the base text and logs one warning for that line, once however many times it is asked; `YarnProject` and `YarnStringTable` round-trip through Stride's binary serializer.
- [ ] **Step 3:** Implement, run → pass. Commit `feat(skein): project content, variable storage and line provider`, push.

### Task 7: Commands and functions

**Files:**
- Create in `src/ColorRise.Skein/Actions/`: `YarnCommandAttribute.cs`, `YarnFunctionAttribute.cs`, `YarnNodeAttribute.cs`, `YarnActionRegistry.cs`, `YarnCommandText.cs`, `IYarnTargetResolver.cs`, `YarnActionDescription.cs`
- Test: `tests/ColorRise.Skein.Tests/Actions/YarnActionRegistryTests.cs`

**Interfaces:**
- Produces: `[YarnCommand("name")]`, `[YarnFunction("name")]` on methods; `[YarnNode(string projectMember)]` on string members. `public sealed class YarnActionRegistry`: `void AddCommandHandler(string name, Delegate handler)`, `void AddFunction(string name, Delegate function)`, `void RegisterAssembly(Assembly assembly)`, `IYarnTargetResolver? Targets { get; set; }`, `ValueTask<bool> RunCommandAsync(string commandText, CancellationToken cancellation)` (false when no handler), `IReadOnlyList<YarnActionDescription> Describe()` (name, parameters with type and default, return type, documentation: the source of the compiler's function declarations and of `.ysls.json`), `void CopyFunctionsTo(Yarn.Library library)`. `interface IYarnTargetResolver { object? Find(string name, Type type); }`. `static IReadOnlyList<string> YarnCommandText.Split(string text)` (quotes group words).
- The built-in commands `wait <seconds>` and `stop` are not in the registry: `DialogueSession` handles them itself (Task 8).

- [ ] **Step 1:** Tests: `give oil 2` calls a static `Give(string item, int count = 1)` with `("oil", 2)` and `give oil` with `("oil", 1)`; `open CellarDoor` calls the instance method on the object the resolver returns for `("CellarDoor", typeof(Door))`; a `Task`-returning command is awaited before `RunCommandAsync` completes; `say "two words"` splits into `["say", "two words"]`; `params string[]` takes the rest; an enum parameter converts case-insensitively; a target the resolver doesn't find, or a parameter that doesn't convert (`give oil many`), throws `YarnCommandException` whose message holds the command text and the reason; two handlers of one name throw at registration naming both methods; `Describe()` of `bool HasItem(string item)` gives `has_item`, one `string` parameter, returns `bool`.
- [ ] **Step 2:** Implement, run → pass. Commit `feat(skein): commands and functions`, push.

### Task 8: The engine-free dialogue session

**Files:**
- Create in `src/ColorRise.Skein/Session/`: `DialogueSession.cs`, `IDialogueResponder.cs`, `DialogueOption.cs`, `LineCancellation.cs`, `Yarn32Adapter.cs` (internal), `SaliencyPick.cs`, `SeededSaliencyStrategy.cs`, `IDialogueClock.cs`
- Create: `tests/ColorRise.Skein.Tests/Session/DialogueSessionTests.cs`, `RecordingResponder.cs`, `Conformance/YarnTestPlanTests.cs`, `Conformance/TestCases/*.yarn` and `*.testplan` (copied from YarnSpinner v3.2.2 `Tests/TestCases`, with `Conformance/LICENSE-YarnSpinner.md`)

**Interfaces:**
- Consumes: `VariableStorage`, `ILineProvider`, `LocalizedLine`, `YarnActionRegistry`.
- Produces: `interface IDialogueResponder`: `ValueTask PrepareForLinesAsync(IReadOnlyList<string> lineIds)`, `ValueTask HandleLineAsync(LocalizedLine line, LineCancellation cancellation)`, `ValueTask<int?> HandleOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)` (null: no choice, fall through), `ValueTask HandleCommandAsync(string commandText, CancellationToken cancellation)`, `ValueTask HandleNodeStartAsync(string node)`, `ValueTask HandleNodeCompleteAsync(string node)`, `ValueTask HandleDialogueCompleteAsync()`. `readonly record struct LineCancellation(CancellationToken HurryUp, CancellationToken NextLine)`. `sealed record DialogueOption(int Index, LocalizedLine Line, bool IsAvailable)`.
- Produces: `public sealed class DialogueSession(Yarn.Program program, VariableStorage variables, ILineProvider lines, YarnActionRegistry actions, IDialogueResponder responder, DialogueSessionOptions options)`: `Task RunAsync(string node, CancellationToken cancellation = default)`, `void Stop()`, `void RequestHurryUp()`, `void RequestNextLine()`, `bool IsRunning`, `string? CurrentNode`, `bool HasSalientContent(string nodeGroup)`, `event Action<SaliencyPick>? SaliencyPicked` (`Group`, candidates each with `Name`, `Complexity`, `Passed`, `FailedCondition`; `Chosen`). `DialogueSessionOptions`: `YarnSaliency Saliency`, `int? RandomSeed` (seeded `random`, `random_range`, `dice` and saliency when set), `IDialogueClock Clock`, `ILogger Log`.

- [ ] **Step 1:** Tests with `RecordingResponder`: lines, options and commands arrive in script order; picking option 1 runs its body; an unavailable option arrives with `IsAvailable == false`; all options unavailable and the responder returning null continues after the options; `RunAsync` twice in a row on one session works.
- [ ] **Step 2:** Review Focus 4: `Stop()` while `HandleLineAsync`, `HandleOptionsAsync` or `HandleCommandAsync` is awaiting cancels its token, `RunAsync` completes without throwing, `HandleDialogueCompleteAsync` is called exactly once, `IsRunning` is false, and the next `RunAsync` runs.
- [ ] **Step 3:** Tests: with `RandomSeed = 7` two runs of a script using `dice(6)` and a node group of equal candidates give the same transcript; `SaliencyPicked` reports the losers' failed `when:` text; a function the registry lacks logs one error naming it and yields the type's default; `wait 2` asks the clock for 2 seconds and `stop` ends the dialogue, neither reaching the responder.
- [ ] **Step 4:** `YarnTestPlanTests`: a theory over the copied plans; each transcript (lines, options, commands, stop) equals the plan's. Plans needing an upstream-only helper are listed in a skip list with the reason.
- [ ] **Step 5:** Implement (the adapter turns each 3.2 handler call into an awaited responder call, then `Continue()` or `SetSelectedOption`), run → pass. Commit `feat(skein): the dialogue session and the Yarn 3.2 adapter`, push.

### Task 9: The Stride runtime

**Files:**
- Create in `src/ColorRise.Skein/`: `DialogueSystem.cs`, `DialogueRunnerComponent.cs`, `DialoguePresenter.cs`, `DialogueActorComponent.cs`, `DialogueActorProcessor.cs`, `EntityTargetResolver.cs`, `GameDialogueClock.cs`
- Test: `tests/ColorRise.Skein.Tests/Runtime/DialogueRunnerTests.cs`, `EntityTargetResolverTests.cs`

**Interfaces:**
- Consumes: `DialogueSession`, `IDialogueResponder`, `YarnActionRegistry`, `YarnProject`.
- Produces: `[GameSystem] public sealed class DialogueSystem : GameSystemBase` (`YarnActionRegistry Actions`, `VariableStorage Variables`, `string Language { get; set; }`, `event Action<string>? LanguageChanged`). `public sealed class DialogueRunnerComponent : StartupScript` (`YarnProject? Project`, `[YarnNode(nameof(Project))] string StartNode`, `bool AutoStart`, `List<DialoguePresenter> Presenters`, `YarnSaliency? Saliency`, `bool OptionsFallThrough`, `VariableStorage? VariablesOverride`; `Task StartDialogue(string node)`, `void Stop()`, `void RequestNextLine()`, `void RequestHurryUp()`, `bool IsRunning`, `string? CurrentNode`, `bool HasSalientContent(string nodeGroup)`; events `DialogueStarted`, `DialogueCompleted`, `NodeStarted(string)`, `NodeCompleted(string)`, `UnhandledCommand(string)`). `public abstract class DialoguePresenter : StartupScript` with virtual `Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)`, `Task<int?> RunOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)`, `Task OnDialogueStartedAsync()`, `Task OnDialogueCompletedAsync()`, `Task OnNodeEnterAsync(string node)`, `Task OnNodeExitAsync(string node)`. `[DataContract] public sealed class DialogueActorComponent : EntityComponent` with `string ActorName`.
- The runner starts the session as a micro-thread of `Script.Scheduler`; a line goes to every presenter at once and the runner waits for all; options go to all and the first non-null answer wins, cancelling the others. Targets: actors by `ActorName` first, then an entity of the runner's scene by name, then a component of the method's type on it. `DialogueSystem` registers the `[YarnCommand]` and `[YarnFunction]` methods of the assemblies Stride has registered when the game starts, and gives each runner a `TextLineProvider` that loads the string table of `Language` and reloads on `LanguageChanged`.

- [ ] **Step 1:** Tests with a `ServiceRegistry`, a `SceneSystem` and a `ScriptSystem` stepped by hand, no window: a runner with two recording presenters delivers a line to both and continues only when both tasks end; two runners on two entities run at once without mixing lines; `Stop()` during options cancels the presenters (Review Focus 4 at this level); an actor named `Odile` on an entity named `NPC_3` is found before an entity named `Odile`; a command with no handler raises `UnhandledCommand` and logs one warning for that command name.
- [ ] **Step 2:** Implement, run → pass. If a hand-stepped game needs more than packages offer, cover these in the sample's smoke test and record a finding. Commit `feat(skein): dialogue system, runner, presenters and actors`, push.

### Task 10: The UI presenters

**Files:**
- Create in `src/ColorRise.Skein.UI/`: `LinePresenter.cs`, `OptionsPresenter.cs`, `LineAdvancer.cs`, `Typewriter.cs`, `DefaultDialogueLook.cs`
- Test: `tests/ColorRise.Skein.Tests/UI/TypewriterTests.cs`

**Interfaces:**
- Produces: `public sealed class Typewriter(Yarn.Markup.MarkupParseResult text, TypewriterMode mode, float unitsPerSecond)`: `string Advance(TimeSpan elapsed)` (visible text), `bool IsComplete`, `void Complete()`; honours `[pause=ms/]` attributes; `enum TypewriterMode { Instant, Letter, Word }`. `LinePresenter`, `OptionsPresenter : DialoguePresenter` with `UIComponent? Page` and element names `SkeinSpeaker`, `SkeinText`, `SkeinContinue`, `SkeinOptions`; with no page they build `DefaultDialogueLook`. `LinePresenter` has `event Action<Yarn.Markup.MarkupAttribute>? MarkerReached`, raised when the typewriter passes an attribute other than `pause`, for a game's own inline events. `OptionsPresenter.UnavailableOptions`: `Hidden` or `Disabled`. `LineAdvancer : SyncScript` with `Keys AdvanceKey = Keys.Space`, `GamePadButton AdvanceButton = GamePadButton.A`, `DialogueRunnerComponent Runner`: first press hurry up, second next line.

- [ ] **Step 1:** Tests: `"Hello"` by letter at 10 per second shows `"He"` after 0.2 s and is complete after 0.5 s; `"Wait[pause=500/] now"` shows `"Wait"` from 0.4 s to 0.9 s; by word, `"one two"` at 2 per second shows `"one"` after 0.5 s; `Complete()` shows all.
- [ ] **Step 2:** Implement the three presenters; they are checked in the sample (Task 22). Commit `feat(skein): UI presenters`, push.

### Task 11: The project asset and its compiler

**Files:**
- Create in `src/ColorRise.Skein.Assets/`: `YarnProjectAsset.cs`, `YarnProjectFiles.cs`, `YarnProjectAssetCompiler.cs`, `YarnProjectAssetFactory.cs`, `SkeinAssetFormat.cs`, `Templates/Assets/YarnProject.sdtpl`, `Resources/*.png` (asset and component icons), `EditorImages.cs`; update `.sdpkg` template list
- Test: `tests/ColorRise.Skein.Tests/Assets/YarnProjectFilesTests.cs`, `AssetCompilationTests.cs`, `AssetYamlTests.cs`

**Interfaces:**
- Consumes: `YarnCompilerBridge`, `YarnProject`, `YarnStringTable`, `YarnScriptAsset`.
- Produces: `[AssetDescription(".sdyarn")] [AssetContentType(typeof(YarnProject))] public sealed class YarnProjectAsset : Asset`: `List<string> SourceFiles` (default `["**/*.yarn"]`), `List<string> ExcludedFiles`, `string BaseLanguage` (default `"en"`), `List<YarnLocalisation> Localisations` (`Language`, `UFile Strings`), `YarnLineIdStyle LineIdStyle` (`Random`, `Descriptive`), `YarnSaliency Saliency` (default `RandomBestLeastRecentlyViewed`), `Dictionary<string, YarnSeverity> DiagnosticSeverities`, `bool WriteYarnProjectFile` (default true).
- Produces: `static IReadOnlyList<AssetItem> YarnProjectFiles.ScriptsOf(AssetItem project)` (globs relative to the project asset's folder, sorted by URL) and `static IReadOnlyList<AssetItem> YarnProjectFiles.ProjectsOf(AssetItem script)`.
- Produces: `YarnProjectAssetCompiler`: one `YarnProject` at the asset's URL and one `YarnStringTable` per language at `YarnProject.StringTableUrl`; every script it includes is an input.

- [ ] **Step 1:** Tests for the globs: `**/*.yarn` beside `Dialogue/Story.sdyarn` takes `Dialogue/Act1.yarn` and `Dialogue/Chatter/Pier.yarn`, not `Other/X.yarn`; an exclude `Chatter/**` drops the second; `ProjectsOf` returns two projects for a script both include, sorted by URL.
- [ ] **Step 2:** Compilation tests (as Saga's `AssetCompilationTests`): a project with two scripts builds a `YarnProject` whose `Nodes` hold both titles and a base string table; a script error fails the build with a message starting `Dialogue/Act1.yarn(` and holding the code; a warning passes; editing a script changes the command's hash; a localised project with a line without `#line:` warns once naming the count and the first three lines; a script in no project warns `is in no Yarn project` (Review Focus 3); the declared functions of the game's assemblies reach the compiler.
- [ ] **Step 3:** Implement, run → pass. Build `sample/Brinemoor` with a project asset → the game loads `YarnProject` by URL. Commit `feat(skein): the Yarn project asset and its compiler`, push.

### Task 12: Line ids, strings files and Yarn's project files

**Files:**
- Create in `src/ColorRise.Skein.Assets/Localisation/`: `YarnLineTagger.cs`, `YarnStringsCsv.cs`, `YarnStringsUpdate.cs`; in `Interop/`: `YarnProjectFileWriter.cs`, `YarnDefinitionsWriter.cs`
- Test: `tests/ColorRise.Skein.Tests/Localisation/*.cs`, `Interop/YarnProjectFileWriterTests.cs`

**Interfaces:**
- Produces: `static string YarnLineTagger.AddLineIds(string fileName, string text, ISet<string> existingIds, YarnLineIdStyle style)` (Yarn's `Utility.TagLines`). `YarnStringsCsv.Write(IEnumerable<YarnStringRow> rows)` / `Read(string csv)` with Yarn's columns `language,id,text,file,node,lineNumber,lock,comment`. `YarnStringsUpdate.Update(IReadOnlyList<YarnStringRow> existing, YarnCompilation baseLanguage, string language)` returns rows plus counts `Added`, `Removed`, `NeedsUpdate` (a row whose `lock` differs from the base text's hash gets the comment `(NEEDS UPDATE)`, as Yarn's tools do). `static string YarnProjectFileWriter.Merge(string? existingJson, YarnProjectAsset asset, string definitionsFile)`; `static string YarnDefinitionsWriter.Write(IReadOnlyList<YarnActionDescription> actions)`.

- [ ] **Step 1:** Tests: tagging a file with one tagged and two untagged lines adds two distinct ids and leaves every other character as it was; tagging twice changes nothing; a CSV row with a comma and a quote in its text round-trips; update after one new line, one deleted line and one changed line gives `Added == 1`, `Removed == 1`, `NeedsUpdate == 1`.
- [ ] **Step 2:** Tests: `Merge(null, …)` gives a file Yarn's `Project.LoadFromString` reads with the same source patterns and base language; `Merge` over a file holding an unknown key `"editorOptions": {"x": 1}` keeps it byte for byte in value and rewrites `sourceFiles`; the definitions file lists `has_item` as a function with its parameter.
- [ ] **Step 3:** Implement, run → pass. Commit `feat(skein): line ids, strings files and Yarn project files`, push.

### Task 13: Functions of the game for the compiler

**Files:**
- Create: `src/ColorRise.Skein.Assets/Compilation/GameActionScanner.cs`
- Test: `tests/ColorRise.Skein.Tests/Compilation/GameActionScannerTests.cs`

**Interfaces:**
- Produces: `static IReadOnlyList<YarnActionDescription> GameActionScanner.Scan(IEnumerable<Assembly> assemblies)` over `[YarnCommand]` and `[YarnFunction]`, used by the asset compiler (the game's assemblies as the build registers them), by Game Studio's analysis (the session's game assemblies, again after a reload) and by the definitions file.

- [ ] **Step 1:** Tests: an assembly with one command and one function gives both with parameter names and types; an unsupported function return type (`Vector3`) is reported as a diagnostic naming the method, not thrown.
- [ ] **Step 2:** Implement, with the mapping of a function's `YarnActionDescription` to the bridge's `YarnFunctionDeclaration`, and wire it into Task 11's compiler. Commit `feat(skein): the game's commands and functions for the compiler`, push.

### Task 14: The text patcher

**Files:**
- Create in `src/ColorRise.Skein.Assets/Text/`: `YarnDocument.cs`, `YarnNodeSpan.cs`, `YarnTextEdit.cs`, `YarnTextPatcher.cs`, `YarnTitles.cs`
- Test: `tests/ColorRise.Skein.Tests/Text/YarnDocumentTests.cs`, `YarnTextPatcherTests.cs`, `Text/Fixtures/*.yarn`

**Interfaces:**
- Produces: `public sealed class YarnDocument` = a tolerant scan of a script's text, independent of the compiler: `static YarnDocument Parse(string text)`, `IReadOnlyList<YarnNodeSpan> Nodes` (`Title`, `Headers` each with key, value and line span; `HeaderStart`, `BodyStart`, `End` offsets; `Index` among same-titled nodes), `string NewLine` (the file's own). `readonly record struct YarnTextEdit(int Offset, int Length, string Text)`.
- Produces: `static class YarnTextPatcher`, each returning the edits to apply (the caller applies them through `YarnTextBuffer`): `SetHeader(doc, node, key, value)`, `RemoveHeader(doc, node, key)`, `SetPosition(doc, node, x, y)`, `AddNode(doc, title, x, y, headers)`, `DeleteNode(doc, node)`, `DuplicateNode(doc, node, newTitle)`, `AppendJump(doc, node, target)`, `RenameNode(doc, node, newTitle)` (the title only), `RenameJumps(doc, oldTitle, newTitle)` (every `<<jump X>>` and `<<detour X>>` in the file, not inside comments or text). `YarnTitles.Validate(string title, IEnumerable<string> existing)` returns null or the reason.

- [ ] **Step 1:** Review Focus 1, one fixture each: CRLF, a byte order mark, tabs, `//` comments between nodes, trailing spaces, a node with no blank line before `===`. For each operation: applying the edits changes only the owned lines (assert the unchanged prefix and suffix byte for byte), new lines use the file's `NewLine`, and the patched text compiles.
- [ ] **Step 2:** Tests: `SetPosition` on a node without `position:` inserts the header after `title:`; on one that has it, replaces the value only; `SetHeader` keeps the order of the other headers; `RenameJumps("Odile_Key", "Odile_TheKey")` changes `<<jump Odile_Key>>` and `<<detour Odile_Key>>` and leaves `// <<jump Odile_Key>>` and a line `Odile: say Odile_Key` alone; `DeleteNode` of the second member of a node group removes that member only.
- [ ] **Step 3:** Review Focus 2 and 5: `Parse` of a file whose second node has an unterminated `<<if` still returns three nodes with their titles and positions; `Validate("Odile Key", …)` and `Validate("", …)` return a reason; `Validate("Wreck", ["Wreck"])` returns `A node named 'Wreck' already exists.`
- [ ] **Step 4:** Implement, run → pass. Commit `feat(skein): the Yarn text patcher`, push.

### Task 15: Editor services and view models

**Files:**
- Create in `src/ColorRise.Skein.Editor/`: `ViewModels/YarnScriptViewModel.cs`, `YarnProjectViewModel.cs`, `YarnScriptEditorViewModel.cs`, `YarnNodeCardViewModel.cs`, `YarnLinkViewModel.cs`, `YarnStubCardViewModel.cs`, `YarnGraphGrouping.cs`; `Services/YarnAnalysisService.cs`, `YarnProjectAnalysis.cs`, `YarnRenameService.cs`; `Text/YarnTextBuffer.cs` (from Task 4)
- Test: `tests/ColorRise.Skein.Editor.Tests/ScriptEditorTests.cs`, `AnalysisServiceTests.cs`, `RenameTests.cs`, `ExternalChangeTests.cs`

**Interfaces:**
- Consumes: `YarnDocument`, `YarnTextPatcher`, `YarnCompilerBridge`, `YarnProjectFiles`, `YarnTextBuffer`, `YarnNodeHeadersViewModel`.
- Produces: `public sealed class YarnAnalysisService` (session service, `YarnAnalysisService.Of(session)`): `YarnProjectAnalysis? AnalysisOf(AssetViewModel scriptOrProject)`, `event Action<YarnProjectAnalysis>? Changed`, `Task WhenIdle()`; compiles a project's scripts from the assets in memory on a background task, 300 ms after the last change, never two at once per project, cancelled and awaited when the session closes. `YarnProjectAnalysis`: `Project` (null for a script alone), `YarnCompilation Compilation`, `IReadOnlyList<YarnDiagnostic> Diagnostics`.
- Produces: `[AssetViewModel<YarnScriptAsset>] YarnScriptViewModel`, `[AssetViewModel<YarnProjectAsset>] YarnProjectViewModel`, `[AssetEditorViewModel<YarnScriptViewModel>] public sealed class YarnScriptEditorViewModel`: `ObservableList<YarnNodeCardViewModel> Cards`, `ObservableList<YarnLinkViewModel> Links` (`IsDetour`, `IsBroken`), `ObservableList<object> SelectedCards`, `YarnTextBuffer Text`, `IReadOnlyList<YarnDiagnostic> Diagnostics`, `string? Notice` (Review Focus 3), `string? GroupBy`, `IReadOnlyList<string> GroupableHeaders`, `string Search`; commands `AddNodeCommand(Point)`, `AddNoteCommand(Point)`, `AddToGroupCommand(card)`, `RenameCommand(card, title)`, `DuplicateCommand`, `DeleteSelectionCommand`, `SetColorCommand`, `LinkCommand(source, target)` (appends a jump), `MoveCommand(moves)`, `LayoutCommand`, `OpenTargetCommand(stub)`, `PlayFromHereCommand(card)`. `YarnNodeCardViewModel`: `Title`, `When`, `Complexity`, `Speakers`, `Preview`, `Options`, `Position`, `Color`, `Cluster`, `IsNote`, `GroupValue`, `IsFilteredOut`, `PlayMark`.
- Cards come from `YarnDocument` (always) enriched by the last analysis (when it compiles); links from the analysis, kept from the last good one while the script is broken. The members of a node group share a `Title` and carry `GroupIndex` and `GroupCount`, so the view stacks them under one title.

- [ ] **Step 1:** Tests in a kit session: opening a script with three nodes and one jump gives three cards and one link; a jump into another script gives a `YarnStubCardViewModel` naming file and node; a jump to nowhere gives a broken link; each command is one undo step that changes the asset's text as Task 14's patch does, and undo restores text and cards; moving two cards is one step writing two `position:` headers.
- [ ] **Step 2:** Review Focus 2, 3, 5: breaking the second node's body keeps three cards and lists the error; a script outside every project has `Notice == "This script is in no Yarn project."` and analyses alone; a script in two projects names the one used; `RenameCommand` to an existing title leaves the text unchanged and reports the reason.
- [ ] **Step 3:** `GroupBy = "quest"` gives each card its `quest:` value and `GroupableHeaders` lists the headers present; `Search = "lamp"` filters by title, tag, speaker and text.
- [ ] **Step 4:** Rename across files (R7): renaming `Odile_Key` patches its title and the jumps in a second, unopened script of the project in one undo step, both assets dirty; undo restores both. If patching an unopened asset proves unsafe, rename in open files only, report the others in the editor, and record it.
- [ ] **Step 5:** External change: rewriting the file on disk with no unsaved edit reloads text and cards without a history step; with unsaved edits the editor asks through the dialog service (a fake in the test) and keeps the user's choice.
- [ ] **Step 6:** Implement, run → pass. Commit `feat(skein): script editor view models and analysis service`, push.

### Task 16: Script editor views

**Files:**
- Create in `src/ColorRise.Skein.Editor.Avalonia/`: `Views/YarnScriptEditorView.axaml(.cs)`, `Views/YarnGraphBinding.cs`, `Views/YarnGraphDecorations.cs` (cluster frames, group tints, notes), `Views/YarnTextPane.cs`, `Text/YarnHighlighter.cs` (colours from `YarnSpinnerLexer` tokens), `Text/YarnDiagnosticRenderer.cs`, `Themes/SkeinStyles.axaml`

**Interfaces:**
- Consumes: `YarnScriptEditorViewModel`; `NodeGraphControl` with `NodeGraphNode.Position` bound two-way, `NodesMoved` → `MoveCommand`, `NodeDoubleClicked`, a node `ContextMenu` (Play from here, Rename, Duplicate, Delete, Colour, Add to group) and a graph menu (Add node, Add note, Layout).
- Layout: toolbar (Add node, Add note, Layout, Group by, Search, Play), graph left, text pane right with a "whole file" toggle, problems list below; a double click on a problem or Ctrl+click on a jump target selects the place. A file with no `position:` header at all is laid out as a tree when it opens, without touching the text until a card is moved. Theme brushes: `SystemControlBackgroundAltHighBrush` family (Saga's finding 18).

- [ ] **Step 1:** Implement. Headless view tests where the kit allows: the view opens for a script, the graph has three node containers, typing in the pane changes the asset.
- [ ] **Step 2:** Game Studio under Xvfb on the sample: screenshots of the editor, a drag, a rename, group by quest, an error underlined, into the project files (`yarn-spinner/screens/`). Fix what the screenshots show. Commit `feat(skein): script editor views`, push.

### Task 17: Property grid — node headers and the node picker

**Files:**
- Create in `src/ColorRise.Skein.Editor/`: `ViewModels/YarnNodeHeadersViewModel.cs` (from Task 3), `PropertyGrid/YarnNodeUpdater.cs`, `PropertyGrid/YarnNodeKeys.cs`; in `.Avalonia`: `Views/YarnNodeTemplateProvider.cs`, `Views/YarnNodePicker.axaml`
- Test: `tests/ColorRise.Skein.Editor.Tests/NodeHeadersTests.cs`, `NodePickerTests.cs`

**Interfaces:**
- Consumes: `YarnScriptEditorViewModel`, `YarnTextPatcher`, `[YarnNode]`, `YarnAnalysisService`.
- Produces: selecting cards selects their `YarnNodeHeadersViewModel` in the session's property grid; a change in the grid is the matching patch (`SetHeader`, `RemoveHeader`, rename through `YarnRenameService`). `YarnNodeUpdater` (a node presenter updater) attaches to a string member marked `[YarnNode(nameof(X))]` the node names of the project referenced by member `X` of the same object, under `YarnNodeKeys.NodeNames`; the Avalonia template shows a searchable drop-down.

- [ ] **Step 1:** Tests: selecting a card shows its headers; setting `Cluster` to `Pier` writes `cluster: Pier` in the text in one step; clearing it removes the header; changing `Title` renames across the project; the runner's `StartNode` presenter of an entity whose `Project` is set carries the project's node names, updated after a script edit, and none when `Project` is null.
- [ ] **Step 2:** Implement (or the fallback form decided in Task 3), run → pass, screenshot in Game Studio. Commit `feat(skein): property grid for node headers and node pickers`, push.

### Task 18: Project overview

**Files:**
- Create: `src/ColorRise.Skein.Editor/ViewModels/YarnProjectEditorViewModel.cs`, `Services/YarnLocalisationService.cs`; `.Avalonia/Views/YarnProjectEditorView.axaml(.cs)`
- Test: `tests/ColorRise.Skein.Editor.Tests/ProjectEditorTests.cs`

**Interfaces:**
- Produces: `[AssetEditorViewModel<YarnProjectViewModel>] YarnProjectEditorViewModel`: `Scripts` (name, node count), `Variables` (name, type, default, description, file, line; `GoToCommand`), `Diagnostics`, `Languages` (language, lines, translated, to update), commands `AddLineIdsCommand`, `ExportStringsCommand(language)`, `UpdateStringsCommand`. `YarnLocalisationService`: `Task<int> AddLineIds(YarnProjectViewModel project)` (one undo step over all scripts), `Task ExportStrings(...)`, `Task<YarnStringsUpdateResult> UpdateStrings(...)`; writes the `.yarnproject` and `.ysls.json` when the project asset is saved and `WriteYarnProjectFile` is on.

- [ ] **Step 1:** Tests: the overview of a two-script project lists both with their node counts and `$gold` with its description; `AddLineIds` tags every untagged line in one undo step and returns the count; `UpdateStrings` on the sample's French file after a new English line reports one added; saving the project writes a `.yarnproject` that Yarn's `Project.LoadFromFile` reads.
- [ ] **Step 2:** Implement, run → pass, screenshot. Commit `feat(skein): project overview and localisation commands`, push.

### Task 19: The dialogue player service

**Files:**
- Create in `src/ColorRise.Skein.Editor/Player/`: `DialoguePlayerService.cs`, `PlayerRun.cs`, `PlayerStep.cs`, `PlayerResponder.cs`, `PlayerTranscriptItems.cs`, `DialoguePlayerViewModel.cs`, `DialogueTimelineViewModel.cs`, `ManualFunctionViewModel.cs`
- Test: `tests/ColorRise.Skein.Editor.Tests/Player/*.cs`

**Interfaces:**
- Consumes: `DialogueSession` (with `RandomSeed`), `YarnAnalysisService`, `TextLineProvider` over the analysis' string tables.
- Produces: `public sealed class DialoguePlayerService` (session service, `Of(session)`; Saga's finding 19): `Task Start(YarnProjectViewModel project, string node)`, `Task Continue()`, `Task Choose(int option)`, `Task Answer(string function, object value)`, `void SetVariable(string name, object value)`, `Task GoTo(int step)`, `Task Replay()`, `void Reset()`, `string Language { get; set; }`, `bool HonourWait`, `IReadOnlyList<PlayerStep> Steps`, `int Cursor`, `bool IsStale` (a script changed since the run), `string? CurrentNode`, `int? CurrentLine`, `IReadOnlyList<string> NodePath`, `event Action? Changed`. `PlayerStep`: `Kind` (`Start`, `Line`, `Options`, `Choose`, `Command`, `Function`, `SetVariable`, `NodeEnter`, `Saliency`), `Node`, `Line`, `Text`, `SaliencyPick? Pick`, `Changed` (after a replay), `Variables` snapshot after the step.
- Rewind replays the recorded inputs (start node, seed, choices, answers, variable edits) from the start up to the step, with no delay. A function with no remembered answer stops the step, asks, and plays the step again once answered. Replay rebuilds from the current analysis and stops, with the reason, before the first input that no longer applies.

- [ ] **Step 1:** Tests: a run of two lines, an options step and a choice records the steps in order with the variables after each; `GoTo(1)` gives the state of step 1 and keeps the later steps until a new input drops them; a script calling `has_item("oil")` stops with a pending question, and after `Answer("has_item", true)` the same call does not ask again in the run; `SetVariable("$gold", 9)` is a step and survives a rewind past it.
- [ ] **Step 2:** Tests: editing a line's text marks the run stale; `Replay` marks that step `Changed` and reaches the cursor; deleting the chosen option makes `Replay` stop before it with a reason naming the option; a run with `dice(6)` replays to the same lines; a node group pick records a `Saliency` step with the losers' failed conditions; switching `Language` to `fr` shows the French text of the current line.
- [ ] **Step 3:** Implement, run → pass. Commit `feat(skein): the dialogue player service`, push.

### Task 20: Player and timeline views, live highlight

**Files:**
- Create in `.Avalonia/Views/`: `DialoguePlayerView.axaml(.cs)`, `DialogueTimelineView.axaml(.cs)`, `DialogueTimelineControl.cs` (Saga's `SagaTimelineControl` is the model), `PlayBrushes.cs`; update `YarnScriptEditorViewModel` (`PlayMark` of cards, current line) and `YarnTextPane` (current line marker)
- Test: `tests/ColorRise.Skein.Editor.Tests/Player/PlayMarksTests.cs`

- [ ] **Step 1:** Tests: while a run is at node `Odile`, the open editor's card `Odile` has `PlayMark == Current`, the nodes of `NodePath` have `Visited`, and `Reset()` clears them; the marks follow `GoTo`.
- [ ] **Step 2:** Implement the two panels (registered in the neutral plugin, views in the Avalonia one) and the marks. Screenshots under Xvfb: a run, a rewind, a replay after an edit, "why this node". Commit `feat(skein): dialogue player and timeline panels`, push.

### Task 21: Commands, templates, entity factory

**Files:**
- Create: `src/ColorRise.Skein.Editor/SkeinCommands.cs`, `EntityFactories/DialogueRunnerFactory.cs`; update the plugin classes and the `.sdtpl` files
- Test: `tests/ColorRise.Skein.Editor.Tests/SkeinCommandsTests.cs`

**Interfaces:**
- Produces: commands `ColorRise.Skein.PlayFromHere` (node menu), `ColorRise.Skein.PlayProject` (asset view menu, on a project or a script), `ColorRise.Skein.OpenDialoguePlayer`, `ColorRise.Skein.AddLineIds`, `ColorRise.Skein.ExportStrings`, `ColorRise.Skein.UpdateStrings` (category `Dialogue`). *Add asset* "Yarn project" creates the project and a first script `Start.yarn` holding a node `Start`. *Add entity* "Dialogue runner": the runner, and `LinePresenter`, `OptionsPresenter`, `LineAdvancer` when the game's package references `ColorRise.Skein.UI` (`PackageTypeScope`).

- [ ] **Step 1:** Tests: `PlayProject` can execute with a project or a script selected and not with a texture; executing it opens the player panel and starts at the project's first node; the "Yarn project" template creates both assets; the factory's entity has a runner, with presenters only when the UI package is referenced.
- [ ] **Step 2:** Implement, run → pass. Commit `feat(skein): commands, templates and the dialogue runner factory`, push.

### Task 22: The sample — Brinemoor's game

**Files:**
- Create in `sample/Brinemoor/`: `Brinemoor.slnx`, `Brinemoor.Game/` (`StoryDirector.cs`, `Journal.cs`, `Inventory.cs`, `DayClock.cs`, `SaveSlot.cs`: engine-free classes; `BrinemoorGame.cs`, `PlayerController.cs`, `Talkable.cs`, `Hud.cs`, `StoryCommands.cs`: Stride scripts), `Brinemoor.Linux/`, `Brinemoor.Windows/`, `Assets/` (scene of simple shapes, UI page, `Dialogue/Story.sdyarn`), `nuget.config`
- Test: `tests/ColorRise.Skein.Tests/Sample/` referencing `Brinemoor.Game`

**Interfaces:**
- Consumes: the runner, presenters, `DialogueSystem`, `[YarnCommand]`, `[YarnFunction]`.
- Produces: `StoryDirector` (engine-free): after each dialogue and each fact, runs the node group `Events` while `HasSalientContent("Events")` and nothing plays. Commands `walk`, `give`, `take`, `quest_changed`, `end_day`, `ending`; functions `has_item(string)`, `time_of_day()`. Keys: WASD move, T talk, E use, Space next, 1 to 9 choose, N end the day, L switch language, F5 save, F9 load, Esc quit.

- [ ] **Step 1:** Tests on the engine-free classes with a stub story of ten nodes: the director runs an `Events` node when its `when:` turns true and not while a dialogue plays; `quest_changed lamp` adds a journal entry; save then load restores variables, inventory and day.
- [ ] **Step 2:** Implement the game; `dotnet build` and a launch under Xvfb reach the scene with the dialogue box (screenshot). Commit `feat(skein): the Brinemoor sample game`, push.

### Task 23: The sample — Brinemoor's story

**Files:**
- Create: `sample/Brinemoor/Assets/Dialogue/Story.yarn`, `Act1.yarn`, `Act2.yarn`, `Act3.yarn`, `Chatter.yarn`, `Strings/fr.csv`
- Test: `tests/ColorRise.Skein.Tests/Sample/PlaythroughTests.cs`, `FeatureCoverageTests.cs`

**Interfaces:**
- The convention of the design's section 12: `enum Act`, `enum QuestState` (`NotStarted`, `Offered`, `Active`, `Succeeded`, `Failed`), `$act`, `$quest_<id>`, smart variables for derived facts, headers `act:`, `scenario:`, `quest:` on every node, one node group per character, the node group `Events`. Three acts, the scenarios and quests of the design's table, four endings. About 60 nodes.

- [ ] **Step 1:** `FeatureCoverageTests`, one assertion per row of the design's feature table, from the compilation: at least one detour, one smart variable, one enum, one node group with `when:` and one with `once`, one line group, one `once` block, `visited` and `visited_count`, `dice` and `format`, a game function and a command of each kind, the markup attributes `select`, `plural`, `ordinal` and a `pause`, a line with a `#mood:` tag, one shadow line, an options block that can fall through, `cluster:`, `color:` and a `style: note` node, a French string for every line id.
- [ ] **Step 2:** `PlaythroughTests`: four scripted playthroughs (choices by option text) on `DialogueSession` with the sample's director, clock and inventory each reach their `ending` command (`keeper`, `harbourmaster`, `stranger`, `dark`); one playthrough where night falls first fails **Light the lamp** and reaches Act II's wreck variant.
- [ ] **Step 3:** Write the story. Nicolas approved the outline, not the text: keep it short, original, and readable in both languages. Run → pass; play it in the game under Xvfb once per act (screenshots). Commit `feat(skein): the Brinemoor story`, push.

### Task 24: Guides, packages outside the tree, findings

**Files:**
- Create: `docs/guide-writers.md`, `docs/guide-developers.md`; update `README.md`, `THIRD-PARTY-NOTICES.md`, `docs/plugin-findings.md`

- [ ] **Step 1:** The writers' guide (write, arrange, group, play, rewind, replay, line ids, languages, working with VS Code on the same files) and the developers' guide (reference `ColorRise.Skein` directly, runner, presenters, commands and functions, targets, saves between dialogues, languages, what to keep in a trimmed build, limits). Every code sample compiles (kept as a test file).
- [ ] **Step 2:** Outside the tree: `dotnet pack`, then a small game in a scratch folder outside the checkout with a `nuget.config` of the two local feeds and `PackageReference` to `ColorRise.Skein` and `ColorRise.Skein.UI` only: restore, build with a `.yarn` and a `.sdyarn`, run one line. Record what fails as findings.
- [ ] **Step 3:** Open the sample's `.yarn` files with Yarn's command line or compiler package untouched (`ysc`-equivalent call on the written `.yarnproject`) → compiles; this is the anti-goal check "every file compiles with the stock tools".
- [ ] **Step 4:** Complete the findings: each with what was tried, what happened, the workaround, the suggested change; "Tied to the fork" lists every relaxation still open; Saga's findings fixed by the follow-ups are checked off. Commit `docs(skein): guides, notices and plugin findings`, push.

### Task 25: Game Studio walkthrough, review, report

- [ ] **Step 1:** Full suite once: `dotnet test extensions/ColorRise.Skein/ColorRise.Skein.slnx`.
- [ ] **Step 2:** Walkthrough of the cross-platform Game Studio under Xvfb on Brinemoor, following the writers' guide step by step, screenshots in the project files (`yarn-spinner/screens/`). The WPF Game Studio check (it opens the sample, no Skein views) is listed for Nicolas's Windows or the Wine thread; not run here unless he asks.
- [ ] **Step 3:** One whole-branch review by a fresh reviewer given the design, this plan and the diff (no conclusions of mine); fix what it finds, each fix in the commit of its task where the history allows, else a `fix(skein):` commit.
- [ ] **Step 4:** Report `yarn-spinner/skein-v1-report.md` in the project files: what was built against each goal of the design, the spike results, the findings, the limits, what is open for Nicolas (decisions in his format), how to try it. Update project memory. Push.

---

## Spike results

Filled in by Tasks 2 to 4: for each of R1 to R4, what was tried, the result, and whether the design's fallback was needed (a fallback that loses a goal is a decision for Nicolas before the work goes on).

### R4: Yarn Spinner's packages beside Stride's (Task 2): holds, no fallback

- Tried: compile and run a script with `YarnSpinner.Compiler` and `YarnSpinner` 3.2.2 in the test host; a throwaway asset compiler in `ColorRise.Skein.Assets` that compiled a script during a game build; Game Studio on the sample with the plugin loaded.
- Result: Yarn's compiler, Google.Protobuf 3.25.2 and Antlr4.Runtime.Standard 4.13.1 load and run in the test host and inside Stride's asset compiler process (loaded as the game's companion); a Yarn warning came out of MSBuild with the `.yarn` file's path. Game Studio loads the two editor assemblies (the Plugins panel lists them, no asset error) and offers to reload after a rebuild. Yarn's compiler running inside Game Studio itself is seen with the analysis service (Task 15).
- Learned for Task 8: in 3.2.2, `Dialogue.Continue()` called from inside a handler does nothing; the adapter calls it again once the handler has returned.

### R1: a `.yarn` file as a third party's `SourceCodeAsset` (Task 2): holds, no fallback

- Tried: load, text, id, change with undo, save, rename, create from a template, in a test session and in Game Studio; a game build with the script in the asset folder.
- Result: all work. The file is the asset and stays plain Yarn text.
- Facts later tasks rely on: the id comes from the location (`SourceCodeAsset.GenerateIdFromLocation(package, "/<namespace>/<location>")`), so a rename changes it and nothing may refer to a script by id (the Yarn project includes scripts by pattern); a text change is undoable and dirties the asset when it is pushed as an `AnonymousDirtyingOperation` on the script's `Dirtiables` in a transaction of `IUndoRedoService`.
- Two problems met and kept inside Skein (findings 3 and 4 of `plugin-findings.md`): Stride's save added a byte order mark (now `YarnScriptAsset.Save` writes UTF-8 with a mark only when the file had one), and a new asset whose default name starts with the folder's name lands at the root (the template's default name is now `YarnScript`).

### R2: the property grid on node headers (Task 3): holds, no fallback

- Tried: a plain object `YarnNodeHeaders` (title, tags, colour, cluster, `when:` conditions, free headers) given to the session's property grid through `YarnNodeHeadersViewModel : IPropertyProviderViewModel`, whose root node comes from the session's node container (`session.AssetNodeContainer.GetOrCreateNode(headers)`); in a test session and in Game Studio.
- Result: the grid shows the six headers with its standard editors (text boxes, lists with add and remove, a key and value pair per free header) under the name "Yarn node <title>". A change made in the grid reaches `Changed` once, inside the undo transaction the grid opens ("Update property Title"): what the handler pushes there is one step of the session's history, and a change that pushes nothing leaves no step. So the script editor rewrites the header in the text from that handler, and the text stays the truth.
- For Task 17: the headers object is a view of the text; an undo, typing or an outside change makes a new object and shows it again. The grid's "Open this asset in editor" button stayed visible from the selection before: check it with the real editor.

### R3: typing as steps of the session's undo history (Task 4): holds, no fallback

- Tried: `YarnTextBuffer`, one per script (`YarnScriptViewModel.TextBuffer`), whose `Replace` pushes a dirtying operation on the session's history; a text pane (`YarnTextPane`, AvaloniaEdit with its own history off) that types into the buffer and shows every change of it; in a test session and in Game Studio.
- Result: typing shows as "Typing" steps in the Edit history panel, Ctrl+Z and Ctrl+Y in the pane and Edit > Undo and Redo all move through the session's history, the script is dirty or clean as the history says, and the caret goes where the change was.
- How typing joins one step: Stride merges operations only inside one transaction, so the buffer adds a typing change to its last typing step while that step is still the last of the history, not saved, and less than a second old. Any other step, an undo, a redo or a save ends it.
- Changed from the plan: the buffer belongs to the script and not to the editor, so the steps still work after the editor closed; `ReloadFromDisk` is a step ("Reload from disk") that leaves the script without unsaved changes, so the steps made before an outside change still apply to the text they were made on.
- AvaloniaEdit's text area takes the keys of its own Undo and Redo even with nothing to undo: the pane removes those two bindings so that the keys reach the window. The Edit menu reaches the pane through `IEditCommandHandler`, which the pane implements for Cut, Copy, Paste and Delete only.
