# Angry Guy

A systemic stealth-comedy prototype. You make one NPC furious without anyone
working out that you caused it, then you leave.

This repository is the **playable core**, not the pretty version. There is no
art, no audio and no animation, on purpose: the only question worth answering
first is whether the core loop is fun.

---

## What is here

```
unity/Assets/AngryGuy/Core/     the whole game: AI, anger, suspicion, blame, gossip
unity/Assets/AngryGuy/Unity/    rendering, camera, input, HUD
unity/Assets/AngryGuy/Editor/   editor conveniences
tools/sim/                      run and test the game without Unity
docs/ARCHITECTURE.md            how it all fits together and why
```

The important structural decision: **`Core` contains no Unity code at all.**
It is plain C# targeting the same language version Unity uses, and the
assembly definition has `noEngineReferences: true`, so Unity itself refuses to
compile the core if anyone ever adds `using UnityEngine` to it.

That means the entire AI can be run, tested, replayed and tuned in a terminal in
about two seconds, without opening an editor. `tools/sim` is a second compiler
pointed at exactly the same source files — not a copy, not a DLL. There is one
source of truth.

---

## Run it right now, without Unity

You need the .NET 8 SDK.

```bash
cd tools/sim

dotnet run --project AngryGuy.Tests                 # 24 tests over the AI and social systems
dotnet run --project AngryGuy.Headless -- watch 300 # watch the restaurant with no player at all
dotnet run --project AngryGuy.Headless              # play it in the terminal
dotnet run --project AngryGuy.Headless -- crime 240 # scripted sabotage run, for tuning
```

`watch` is the one to try first. It shows four NPCs running their own routines
and ends with a breakdown of where their time went — the fastest way to tell
whether a tuning change made someone stop behaving like their job title.

Terminal commands while playing: `map`, `look`, `use <n>`, `go <name>`,
`wait <sec>`, `who`, `log`, `quit`.

---

## Run it in Unity

1. Install **Unity 6 LTS** (free Personal licence).
2. Open the `unity/` folder as a project.
   If the Hub complains about the editor version, edit
   `unity/ProjectSettings/ProjectVersion.txt` to match what you have installed.
3. Press **Play** in whatever empty scene opens.

There is no scene file to open and nothing to drag into the hierarchy. The level
builds itself from code on play — see `Bootstrap.cs` for why.

If nothing responds to input, set
`Edit > Project Settings > Player > Active Input Handling` to
**Input Manager (Old)** or **Both** and restart Unity. The editor logs a clear
error about this on load.

### Controls

| | |
|---|---|
| WASD | move |
| Shift | sneak — quiet, but looks shifty if you are seen |
| Mouse | look |
| V | first / third person |
| E or 1–9 | interact with what is in reach |
| Q | cancel an interaction |
| Tab | AI debug overlay |
| R / T | after the level ends: new seed / same seed again |

Orange entries in the prompt are sabotage. Green cones are what NPCs can see;
a cone turns red as that NPC starts to suspect you.

---

## The level

A restaurant kitchen and dining room, split by a wall with one doorway.

- **Gordon**, head chef — the target. Hot-headed, possessive about his kitchen,
  holds a grudge, not especially suspicious of people.
- **Marie**, waiter — chatty and credulous. She is how information (and blame)
  travels across the level.
- **Terry**, dishwasher — placid, oblivious, believes anything. Gordon already
  half-blames him for everything, which is a crack you can widen.
- **Eva**, manager — paranoid and sharp-eyed. She is the actual threat.

Your objective is Gordon's anger, not everyone's. Spraying sabotage around the
level mildly annoys four people; the objective needs one person pushed over the
edge.

---

## What is deliberately missing

Animation, audio, real models, a main menu, saving, more levels. All of it is
cheap to add later and none of it tells you whether the game works.

`docs/ARCHITECTURE.md` explains how to add objects, NPCs and levels, and lists
the things most likely to break as this grows.
