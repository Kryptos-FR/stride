# Skein for writers

How to write, arrange, play and translate a story in Game Studio with Skein. The programmers' side (the dialogue runner, presenters, commands and functions in C#, saving) is in [guide-developers.md](guide-developers.md).

Skein does not teach the Yarn language: [Yarn Spinner's documentation](https://docs.yarnspinner.dev) does, and all of Yarn Spinner 3 works here. A script of Skein is a plain `.yarn` file. What this guide adds is what Game Studio does with those files.

The sample game `sample/Brinemoor` has a story of five scripts and 70 nodes to try all of this on. The Yarn of this guide is compiled with Skein's tests.

## 1. A project and its scripts

A story is made of two kinds of assets:

- A **Yarn script** is a `.yarn` file in an asset folder of the game. It holds nodes.
- A **Yarn project** (`.sdyarn`) says which scripts make one story, the language they are written in, and the languages they are translated to. By default it takes every script of its folder and of the folders under it.

To start a story, pick a folder in the asset view and choose *Add asset > Dialogue > Yarn project*. In a folder that has no script yet, the project comes with a first script `Start` that has a node `Start`. *Add asset > Dialogue > Yarn script* adds more scripts. How to split a story into scripts is up to you: Brinemoor has one script per act, one for the fishers' small talk, and one for what the whole story shares.

Select the project in the asset view to see its settings in the property grid:

| Setting | What it does |
|---|---|
| Source files, Excluded files | Which scripts are in the project: glob patterns from the folder of the project. `**/*.yarn` is every script; `Chatter/*.yarn` is one folder. |
| Base language | The language the scripts are written in (`en`, `fr`, `pt-BR`). |
| Localisations | The other languages, each with its strings file (section 8). |
| Line id style | How *Add line ids* names the ids: random (`line:0a1b2c3`) or descriptive (`line:Tavern_0100_Odile`). |
| Saliency | How the game picks among the lines of a line group or the nodes of a node group (section 4). |
| Severities | How serious a message of the compiler is, by its code. In a new project, a jump to a node that does not exist (`YS0012`) is an error. |
| Yarn project file | Keeps the files Yarn Spinner's own tools read beside the project (section 9). |

## 2. Write

A double click on a script opens the script editor: the graph of its nodes on the left, the text on the right, the messages of the compiler below.

```yarn
title: Start
position: 0,0
---
Tam: Welcome to the Gull. A bed, or a bowl of stew?
-> A bed.
    <<jump Bed>>
-> Stew.
    Tam: Sit anywhere.
===
title: Bed
position: 300,0
---
Tam: Up the stairs, second door.
===
```

- **The text** shows the nodes selected on the graph. *Whole file* shows the file as it is on the disk, headers and all. The text has Yarn's colours, and the compiler reads it as you type: an error is underlined at its place and listed below, with the same message the build would give. A double click on a message shows its place. Ctrl+click on a node name of a `<<jump>>` goes to that node.
- **The graph** has a card for each node: its title, who speaks in it, its first lines, its options and its tags. A jump or a detour is a link between two cards. A node of another script is a grey stub (a double click opens its script), and a node that does not exist is a red stub.
- **A new node**: *+ Node* in the toolbar, or right click on the graph and *Add node here*. Type its text in the text pane.
- **A new jump**: drag from the "jump" or "detour" connector of a card to another card. The `<<jump>>` or `<<detour>>` is added at the end of the node's text; move it where it belongs.
- **Rename**: double click on a title, or F2. Every jump to the node follows, in all the scripts of the project.
- **Duplicate, delete**: in the menu of a card. Delete also works with the Delete key when the graph has the focus.

Every change is a step of Game Studio's undo history: the *Edit history* panel lists them ("Typing", "Move node", "Rename node 'A' to 'B'"), and Ctrl+Z and Ctrl+Y undo and redo them, across the graph, the text and the property grid. *File > Save* writes the files.

**The property grid** shows the headers of the nodes selected on the graph: title, tags, colour, cluster, the `when:` conditions, and the other headers as a list. A change there changes the text. A header of your own (`act: 2`, `quest: lamp`) is just a line between the title and the `---`.

## 3. Arrange

How the graph looks is written in the script itself, with headers that Yarn Spinner's own tools also read. Nothing about the layout is kept elsewhere.

| What | How | In the file |
|---|---|---|
| Place a card | Drag it | `position: 300,0` |
| Place every card along the jumps | *Layout* | new `position:` headers |
| Colour a card | Menu of the card, *Colour* | `color: red` (red, orange, yellow, green, blue, purple) |
| A note for the writers | *+ Note*, or *Add note here* | a node with `style: note` that nothing jumps to |
| See everything | Right click, *Show all the nodes* | |

The search box dims the cards that do not match: it looks in the titles, the headers and the text.

## 4. Group

A story of some size needs more than one flat graph. Skein has three ways to group nodes, and they can be mixed.

**Clusters.** A node with a `cluster:` header is drawn in a frame with the other nodes of the same cluster. Set it in the property grid. A cluster is only a drawing: the story does not know it.

**Group by a header.** *Group by* in the toolbar frames the nodes by the value of any header: pick `act`, and each act has its frame. This is how a story is cut in acts, scenarios and quests without any feature of the language: give the nodes the headers that fit your story, and look at the graph by one or the other.

**Node groups.** Several nodes with the same title and a `when:` header each are a node group, a feature of Yarn Spinner 3: a jump to the title plays the best node whose conditions are true. The graph stacks them in one frame under their title. *Add a node to its node group* in the menu of a card adds one.

```yarn
title: Start
---
<<declare $lamp_lit = false>>
<<jump Gossip>>
===
title: Gossip
when: $lamp_lit
---
Tam: They lit the lamp at last.
===
title: Gossip
when: always
---
Tam: Quiet night.
===
```

A **line group** is the same idea for lines, inside a node:

```yarn
=> Tam: The tide is late today.
=> Tam: Mind the third stair. <<if $stairs_fixed == false>>
=> Tam: You again! <<once>>
```

How the game picks is the project's *Saliency*. With the default one, the node or the line with the most conditions wins; among equals, the one seen the least often; among those, one at random. So a node group with two nodes that are both always true does not play at random: it plays them in turn.

## 5. Play

The **dialogue player** plays the story inside Game Studio, from the scripts as they are in memory. Nothing needs saving or building, and the game does not need to exist.

- *▶ Play* in the toolbar of the script editor (Ctrl+F5) plays the selected node. *Play from here* in the menu of a card plays that card.
- *Play dialogue* in the menu of the asset view, on a project, plays its node `Start` (or its first node by name); on a script, its first node.
- *View > Dialogue player* opens the panel, where a project and a node can be picked.

The panel shows what the dialogue says. It waits after each line (*Continue*) and at the options. An option whose condition is false is greyed, as the game may show it. Commands (`<<walk Tam door>>`) show in the transcript and go on at once: the player has no game to run them. *Waits* says whether a `<<wait>>` takes its time.

While the dialogue plays, the script editor follows: the card of the current node has a blue border, the nodes the run went through a paler one, and the text lights the current line.

**What the game would answer.** When the scripts call a function of the game, the player has no game to ask:

```yarn
<<if has_item("oil")>>
    Maud: You found it. Good.
<<else>>
    Maud: Come back with the oil.
<<endif>>
```

The panel stops and asks for the answer of `has_item("oil")`, then remembers it. The answers are listed in the panel, where each can be changed for the next run.

**Variables.** The panel lists the variables of the project with their values. Change one to try another state of the story without playing up to it. A smart variable shows its value, and cannot be set.

**Why this one?** When a node group or a line group picks, the transcript tells why: each candidate with its score, or the condition that ruled it out.

## 6. Rewind

*Timeline* in the player opens the **dialogue timeline**: one column per step of the run, with lanes for the nodes, the lines, the choices, what the game was told to do, and the variables that changed.

- Click a step, or use the arrows, to go back and forth. The player, the variables and the script editor show the run as it was at that step.
- Going on from an earlier step with another choice starts a new branch: what followed the old choice is dropped.

This is the fast way to try every option of a list: play one, go back one step, play the next.

## 7. Replay after an edit

A run is the list of what you did: the choices, the answers given for the game, the variables set by hand. After an edit of the scripts, *Replay* plays the same list on the new scripts.

- The steps that are new, or that read differently, have a dot in the timeline.
- When the edit removed the option a choice took, the replay stops there and says why.

So a fix of a line deep in the story is checked in one click: play up to it once, edit, replay.

## 8. Line ids and languages

A line is translated by its id: the tag `#line:` at its end.

```yarn
Tam: Welcome to the Gull. #line:0a1b2c3
-> A bed. #line:0c4d5e6
```

The scripts are written in the base language, and each other language has a **strings file**: a CSV file in Yarn Spinner's format, with a row per line. A double click on the project opens its **overview**, which has the buttons and the counts.

1. **Add the language.** In the property grid of the project, add a localisation and type its language code.
2. ***Add line ids*** gives an id to each line that has none, in all the scripts, as one step of the undo history. Do it whenever lines were added.
3. ***Export strings*** writes the strings file of each language that has none: `Strings/<project>.<language>.csv` beside the project, with every line in the base language. It never writes over a file that exists.
4. **Translate.** The translator changes the `text` column in place, in a spreadsheet or a text editor, and keeps the other columns. The file is an asset of the game like the scripts, and goes in version control with them.
5. ***Update strings files***, after the scripts changed: new lines are added without a text, lines that are gone are removed, and the translation of a line whose text changed gets `(NEEDS UPDATE)` in front. The translator fixes it and removes the mark.

The overview counts, for each language, the lines, the translated ones, those to update and the missing ones. *Read the files again* counts anew after a translator changed a file. The build of the game says how many lines have no translation; in the game, such a line shows in the base language.

The dialogue player has a box for the language: it plays the story in a translation, from the strings files as they are on the disk.

Things to know:

- Keep the ids once a line is translated. A line whose id is removed is a new line for the translation.
- *Descriptive* ids need a node name that is used once: in a node group, where several nodes have the same title, Skein gives random ids to the lines.
- `[select]`, `[plural]` and `[ordinal]` work in every language, with the rules of the language of the line. A translation can give other cases than the original: French has no "few", and its rules call "many" what English calls "other". A case that the marker does not give falls back to its `other` text, so `other` is the one to always write.

## 9. With Visual Studio Code, on the same files

Yarn Spinner has an extension for Visual Studio Code, with completion, hover, and a graph of its own. It works on the files of a Skein project as they are:

- Saving the project in Game Studio writes a `.yarnproject` file beside it, with the scripts, the base language and the localisations. The extension finds it when the folder is opened. What the extension keeps in that file stays when Skein writes it again.
- Once the game is built, Skein also writes a `.ysls.json` file with the commands and the functions of the game and their descriptions: the extension completes them and checks their parameters.
- Yarn Spinner's command line reads the same file: `ysc compile <project>.yarnproject` compiles the story as Skein's build does, without the game. It does not read the definitions file: it finds the types of the game's functions from how the scripts use them.
- The layout is in the scripts (`position:`, `color:`, `cluster:`, `style: note`), so a graph arranged in one tool looks the same in the other.
- A script that another program changes while Game Studio is open is read again. When it has no unsaved change in Game Studio, this happens at once, and one undo gives the text of before back. When it has, Game Studio asks first.

The same holds for a pull from version control, and for any text editor. Scripts are text: they merge and compare line by line.

## 10. Good to know

Things that surprised us while writing Brinemoor. All are how Yarn Spinner works, not rules of Skein.

```yarn
<<declare $coins = 3>>
The sign reads\: no credit.
Tam: One room left. [pause=500 /]It has a view.
You have {$coins} [plural value={$coins} one="coin" other="coins" /].
```

- **A colon in a line that nobody says.** What is before the first colon of a line is the name of the speaker. A line of narration that has a colon must write it `\:` (every colon of the line), or half of it becomes a very long name. The backslash is not shown.
- **A space around a marker.** A marker that stands alone, such as `[pause=500 /]`, takes the space that follows it. Write the space before the marker and none after.
- **`when: once` and `<<once>>`** remember that they ran in a variable of the story: they are saved with the game, and the player's *Variables* does not list them. *Stop* in the player forgets them.
- **A jump to a node that does not exist** is an error in a new project, at the build and in the editor. Yarn Spinner alone only warns, and the dialogue would stop there in the game.
- **A command the game does not have** does not fail the build: the scripts are allowed to be ahead of the code. In the game, the log has a warning and the dialogue goes on. The descriptions of the commands that exist are in the `.ysls.json` file.

## 11. The commands

All are in the command palette (Ctrl+Shift+P, category *Dialogue*).

| Command | Where | What it does |
|---|---|---|
| Play from here | Script editor (Ctrl+F5), menu of a card | Plays the node of the card, else the selected node, else the first node of the script. |
| Play dialogue | Menu of the asset view, on a project or a script | Opens the dialogue player and starts. |
| Open dialogue player | Palette, *View* menu | Shows the panel. |
| Add line ids | Menu of the asset view on a project, overview | Gives an id to each line that has none. |
| Export strings | Menu of the asset view on a project | Writes the strings file of each language that has none. |
| Update strings files | Menu of the asset view on a project, overview | Brings the strings files up to date with the scripts. |
