# Angry Guy

A systemic stealth-comedy prototype. You make one NPC furious without anyone
working out that you caused it, then you leave.

One level, four NPCs with their own routines, personalities, memories and
opinions about each other. Everything they do is simulated rather than scripted,
so the solutions you find are not ones anyone designed.

---

## What is here

```
unity/Assets/AngryGuy/Core/       the whole game: AI, anger, suspicion, blame, gossip
unity/Assets/AngryGuy/Unity/      rendering, animation, camera, input, HUD, audio
unity/Assets/AngryGuy/Resources/  generated textures and sound effects
unity/Assets/AngryGuy/Editor/     editor conveniences
tools/sim/                        run and test the game without Unity
tools/assets/                     scripts that generate the art and audio
docs/ARCHITECTURE.md              how it all fits together and why
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

You need the .NET SDK (8 or newer).

```bash
cd tools/sim

dotnet run --project AngryGuy.Tests                 # 29 tests over the AI and social systems
dotnet run --project AngryGuy.Headless -- watch 300 # watch the restaurant with no player at all
dotnet run --project AngryGuy.Headless              # play it in the terminal
dotnet run --project AngryGuy.Headless -- crime 240 # scripted sabotage run, for tuning
```

`watch` is the one to try first. It shows four NPCs running their own routines
and ends with a breakdown of where their time went — the fastest way to tell
whether a tuning change made someone stop behaving like their job title.

Terminal commands while playing: `map`, `look`, `use <n>`, `go <name>`,
`wait <sec>`, `throw`, `hide`, `who`, `log`, `quit`.

The terminal build prints the same feedback stream the Unity HUD turns into
floating numbers, so it is a fair preview of whether the game reads clearly.

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
| Shift | sneak — quiet and much harder to spot at range, but looks shifty up close |
| Mouse | look |
| V | first / third person |
| E | interact with whatever you are facing |
| 1–9 | pick a different option on that same object |
| Q | cancel an interaction |
| T | throw what you are holding — the noise happens where it lands |
| F | hide in a hiding spot you are standing next to |
| Tab | AI debug overlay |
| H | show/hide the controls |
| R / T | after the level ends: new seed / same seed again |

Orange entries in the prompt are sabotage. Cones on the floor are what each NPC
can see: green means they have not noticed you, amber means something feels off,
red means they are on to you.

### Reading what is happening

Everything the simulation does surfaces on screen:

- Numbers float off whoever it happened to — `+18 ANGER (couldn't Cook on stove)`,
  `+55 SUSPICION (saw you do it)`.
- Each NPC is labelled with their mood and what they are currently doing
  (`irritated - off to stove`, `calm - investigating`), plus a `?` or `!` bubble
  when they have noticed something.
- A banner across the top names anyone who can see you right now.
- The end-of-level screen replays everything you did, flags which actions were
  witnessed, and shows peak anger and highest suspicion.

If you still cannot tell why something happened, Tab opens the AI debug overlay
with raw needs, tension and per-NPC suspicion.

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

The tools you have beyond direct sabotage:

- **The swing door** between kitchen and dining room. Shutting it costs nothing,
  looks like nothing, and blinds half the cast.
- **Throwing** (T) puts a loud noise somewhere you are not, and people go and
  look at it.
- **The service bell** does the same thing even more bluntly.
- **Two hiding spots** — the pantry and the corner booth — where nobody can see
  you at all, at the cost of being unable to do anything while you are in there.
- **Delay.** Sugar in the salt does nothing until Gordon next cooks, minutes
  later, while you are demonstrably in the dining room.

---

## Art and audio

Everything you see and hear is generated by the two scripts in `tools/assets/`:

```bash
python3 tools/assets/generate_audio.py      # 17 sound effects
python3 tools/assets/generate_textures.py   # 5 seamless textures
```

They are synthesised from scratch rather than downloaded, which means no licence
to track, nothing to attribute, and no risk of shipping something we cannot
legally sell. They are also tweakable: if the bell is too shrill, change a number
and re-run.

Characters are boxes animated entirely in code (`CharacterRig.cs`) — walk cycles,
sitting, head-scanning while investigating, arm-flailing when furious. For a
prototype that reads as intentional, whereas mismatched free models with
mismatched free animations read as broken.

**When you want real art**, the obvious free sources are
[Kenney](https://kenney.nl) (CC0 models and audio),
[Quaternius](https://quaternius.com) (CC0 characters and props) and
[Mixamo](https://mixamo.com) (free rigged animations, Adobe account required).
Drop models into `unity/Assets/AngryGuy/Art/` and replace the `CreatePrimitive`
calls in `LevelView.cs` with `Instantiate(prefab)`. Nothing in the simulation
changes.

## What is deliberately missing

A main menu, saving, more levels, real character models. All of it is cheap to
add later and none of it tells you whether the game works.

`docs/ARCHITECTURE.md` explains how to add objects, NPCs and levels, and lists
the things most likely to break as this grows.
