# Architecture

## The one rule

The simulation knows nothing about Unity, and Unity knows nothing about the game
rules.

`unity/Assets/AngryGuy/Core/` is plain C# with `noEngineReferences: true` in its
assembly definition. Unity will not compile it if anyone adds `using UnityEngine`.
`tools/sim/AngryGuy.Core.Net/` compiles the same files again for .NET, via a glob
include — there is no copy and no DLL to keep in sync.

Everything that follows exists because of that split. The AI can be tested,
replayed and tuned in a terminal in seconds, and the Unity layer stays small
enough to rewrite when the art pipeline arrives.

---

## Utility AI + Smart Objects, concretely

There are no behaviour trees and no per-NPC scripts. Every tick, an idle NPC asks
every object in the level what it offers, scores the answers, and commits to the
best one.

**Objects advertise.** A `SmartObject` holds a bag of float state and a list of
`Affordance`s. An affordance says what it does for your needs, how long it takes,
and under what conditions it works:

```csharp
new Affordance {
    Verb = "Cook on",
    Actors = ActorKind.Npc,
    Duration = 9f,
    Satisfies = { new NeedDelta(NeedType.Comfort, 0.55f) },
    ArrivalPrecondition = (obj, npc) => !obj.IsBroken && PanIsWhereItShouldBe(),
    Effect = ctx => { /* start cooking; schedule the payoff */ }
}
```

**NPCs score.** For each advertised affordance:

```
score = Σ over needs ( urgency(need) × usable_gain × personality_weight(need) )
        × affordance appeal
        × distance falloff
        × ownership factor
```

- `urgency` is quadratic: a need at 0.9 barely motivates, a need at 0.1 dominates.
- `usable_gain` is clipped to remaining headroom, so eating when full scores zero.
- `personality_weight` is where NPCs differ — a glutton weights Hunger 1.7×, a
  fussy NPC weights Order 1.9×. Same code, different people.
- `ownership` makes NPCs prefer their own things and hesitate over other people's.

The winner is chosen with weighting by **score squared**, so a clear best option
usually wins but genuine ties vary between runs. Plain score-weighting was tried
first and made NPCs look aimless — the player could not learn a routine well
enough to exploit it, which is fatal in a stealth game.

**Adding content adds behaviour.** A new object with sensible affordances is
immediately understood by every NPC, with no AI changes. That is the whole reason
for this architecture.

---

## Knowledge: the split that makes sabotage work

`Affordance` has two gates:

- `Precondition` — things anyone can judge from across the room (a bin is visibly
  full, a radio is audibly blaring). Checked when **choosing** a plan.
- `ArrivalPrecondition` — things you can only establish by turning up (the pan is
  missing, the stove is dead, the fridge is empty). Checked **on arrival**.

Sabotage almost always lives in the second category. Gordon commits to cooking,
walks the length of his kitchen, and only then finds out. The walk is the setup
and the failure is the punchline.

This was wrong in the first draft — every precondition was checked at selection
time, NPCs had perfect global knowledge of world state, and so they simply never
chose a plan the player had already broken. Sabotage silently did nothing. It is
the single most important detail in the codebase.

---

## Anger is never set by the player

The player has no verb that adds anger. The player invalidates plans, damages
property and creates social friction; anger is what the simulation does about it.
That is why solutions nobody designed still work.

Routes into `AngerModel`:

| Source | Scaled by |
|---|---|
| A committed plan fails on arrival | Temper × need urgency × escalating failure count |
| Their property taken, moved, broken | Territoriality × Temper × severity |
| Mess in their space | Tidiness |
| Being accused to their face | Temper × accuser's confidence |
| Witnessing a row | Temper |
| A need pinned at zero | Temper (deliberately tiny) |

Two feedback terms shape the curve:

- **Tension** — "having one of those days". Each grievance raises it, and it
  multiplies the next one by up to 1.4×, bleeding off over a couple of minutes.
  This is what makes *timing* a skill: four sabotages inside two minutes compound
  into fury, the same four spread over ten minutes are shrugged off one at a time.
- **Decay** — must comfortably out-pace the ambient sources, so an undisturbed
  restaurant settles back to calm and any anger the player sees is anger the
  player caused. Verified: 420 seconds with no player leaves everyone at 0.00.

A representative targeted run: `0.24 → 0.37 → 0.45 → 0.59 → 0.90`, then he
explodes, while the other three NPCs are still at zero.

---

## Suspicion, blame and gossip

NPCs never read an event's true actor. They reason only from what they personally
saw, what they already believed, and what they were told. The gap between truth
and belief is the stealth game.

`BlameResolver` scores every candidate on:

1. **Opportunity** — who did I see near the scene, around the time, from my own
   sighting memory. Weighted by recency and proximity.
2. **Prior suspicion** — how gossip and past incidents bite.
3. **Motive** — people I dislike are easier to blame, scaled by Paranoia.

Then a threshold: `lerp(0.52, 0.14, Paranoia)`. Paranoid Eva nearly always finds a
culprit; trusting Terry usually concludes it was an accident. If nobody clears the
bar, the NPC decides it was bad luck — and the player stays invisible.

`Gossip` copies one NPC's beliefs into another when they talk, weighted by trust
and the listener's Gullibility, with memories degraded to second-hand. After three
hops the story is confidently wrong. No authored dialogue is involved.

When suspicion of another NPC crosses 0.45 they go and confront them, which
raises both NPCs' anger, damages the relationship, and lets the accused **deflect**
onto whoever *they* already suspect. That is where blame chains stop being a
two-person problem — and it is the comedy engine.

---

## Delayed consequences

`Simulation.Schedule(delay, label, apply)` is the backbone of the stealth design.
Swapping sugar into the salt does nothing visible; it detonates when Gordon next
cooks, minutes later, while the player is demonstrably in the dining room.

Subtle tampering also sets a `subtleTamper` flag so it is excluded from the
"looks wrong at a glance" check. It can only be discovered by *using* the object.

---

## Unity layer

| File | Job |
|---|---|
| `Bootstrap.cs` | builds the game on Play in any empty scene |
| `SimRunner.cs` | owns the `Simulation`, ticks it, handles interaction channelling |
| `LevelView.cs` | grey-box primitives, vision cones, colour by anger |
| `PlayerController.cs` | character controller, third/first person camera |
| `GameHud.cs` | IMGUI HUD, world-space labels, debug overlay |

**No scene file.** `.unity` files are large, opaque, merge horribly in git and
cannot be written by hand. Generating the level from code means the repo holds
only readable C#, two people can work on the same level without conflicts, and
pressing Play just works. When real art arrives, replace `CreatePrimitive` with
`Instantiate(prefab)` in `LevelView`; nothing else changes.

**No uGUI.** `OnGUI` needs no canvas, prefabs or font assets, so the prototype has
zero asset dependencies. Replace it once the design settles.

**Vision cones are a feature, not a gizmo.** A player cannot plan around
perception they cannot see.

---

## Adding things

**A new object** — add a builder to `KitchenLevel.cs`. Put player-facing sabotage
in `IsSabotage = true` affordances with an `Incrimination` value, and put anything
the player can break into `ArrivalPrecondition`. NPCs need no changes.

**A new NPC** — add a `Personality` (nine traits) and a home zone. Seed some
relationships; they are what give blame somewhere to land.

**A new level** — copy `KitchenLevel.cs`. It is ~700 lines of pure content with no
engine dependency, and it is the only file that should need to change.

---

## Known weak points

- **Affordances use C# delegates**, which are flexible and fast to iterate on but
  cannot be serialised or edited outside code. If the game grows past a handful of
  levels this should become data (ScriptableObjects or JSON) with the delegates
  reduced to a small set of named effect types.
- **Movement is straight-line with wall sliding**, not pathfinding. Fine for two
  rooms; the moment a level has a U-shaped corridor, swap in NavMesh (Unity side)
  or an A\* grid (core side, to keep it testable).
- **Time spent walking is high** (roughly two thirds for front-of-house staff).
  The knobs are `DistanceFalloff` half-life in `UtilityAi`, NPC `MoveSpeed`, and
  giving each zone its own facilities so NPCs are not crossing the level for every
  need.
- **One level's content lives in one static class.** Fine now, will not scale past
  three or four levels.
- **No save system, audio, or animation.** All deliberate.
