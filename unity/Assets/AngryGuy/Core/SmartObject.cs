using System;
using System.Collections.Generic;

namespace AngryGuy.Core
{
    [Flags]
    public enum ActorKind
    {
        None = 0,
        Npc = 1,
        Player = 2,
        Both = Npc | Player
    }

    /// <summary>How much an affordance moves a need.</summary>
    public struct NeedDelta
    {
        public NeedType Need;
        public float Amount;

        public NeedDelta(NeedType need, float amount)
        {
            Need = need;
            Amount = amount;
        }
    }

    /// <summary>Everything an affordance's effect is allowed to touch.</summary>
    public sealed class AffordanceContext
    {
        public Simulation Sim;
        public SmartObject Object;

        /// <summary>Null when the player is the one using it.</summary>
        public Npc Npc;

        public string ActorId = "";

        public bool IsPlayer
        {
            get { return Npc == null; }
        }
    }

    /// <summary>
    /// An interaction an object advertises to the world: "you can Cook on me,
    /// it takes 12 seconds, it will fill 0.7 of your Hunger, but only if I am
    /// not broken and I have contents."
    ///
    /// NPCs never contain object-specific logic. They read advertisements and
    /// score them. Adding a new object to the game therefore adds new NPC
    /// behaviour with zero changes to the AI.
    /// </summary>
    public sealed class Affordance
    {
        public string Id = "";

        /// <summary>Shown in the interaction prompt: "Take", "Cook", "Swap salt for sugar".</summary>
        public string Verb = "";

        public ActorKind Actors = ActorKind.Both;

        /// <summary>What using this does for the actor's needs.</summary>
        public List<NeedDelta> Satisfies = new List<NeedDelta>();

        /// <summary>Seconds of use before the effect lands.</summary>
        public float Duration = 2f;

        /// <summary>Flat multiplier for designers to nudge an option up or down.</summary>
        public float BaseAppeal = 1f;

        /// <summary>Noise made while using it. Attracts attention.</summary>
        public float Noise;

        /// <summary>Player-side sabotage. Drawn differently in the UI and never chosen by NPCs.</summary>
        public bool IsSabotage;

        /// <summary>
        /// Using this in view of someone is inherently incriminating.
        /// Scales how much suspicion a witness gains.
        /// </summary>
        public float Incrimination = 0.5f;

        /// <summary>
        /// Availability an NPC can judge from across the room: a bin is visibly
        /// full, a radio is audibly blaring. Checked when choosing a plan.
        /// Npc is null when evaluated for the player.
        /// </summary>
        public Func<SmartObject, Npc, bool> Precondition;

        /// <summary>
        /// Availability that can only be established by turning up: the pan is
        /// missing, the stove is dead, the fridge is empty.
        ///
        /// This split is what makes sabotage feel like sabotage. An NPC commits
        /// to a plan believing it will work, walks the length of the kitchen,
        /// and only then finds out. That walk is the setup and the failure is
        /// the punchline - and neither happens if NPCs can read world state
        /// from anywhere.
        /// </summary>
        public Func<SmartObject, Npc, bool> ArrivalPrecondition;

        /// <summary>
        /// A precondition that needs to look at the player rather than at this
        /// object - "am I carrying something", say. Only consulted when the
        /// player is the actor; NPCs never see these affordances.
        /// </summary>
        public Func<SmartObject, Simulation, bool> PlayerPrecondition;

        /// <summary>What actually happens when the interaction completes.</summary>
        public Action<AffordanceContext> Effect;

        /// <summary>Full check. Used on arrival, and for the player, who is standing right there.</summary>
        public bool AvailableFor(SmartObject obj, Npc npc)
        {
            ActorKind kind = npc == null ? ActorKind.Player : ActorKind.Npc;
            if ((Actors & kind) == 0) return false;
            if (Precondition != null && !Precondition(obj, npc)) return false;
            if (ArrivalPrecondition != null && !ArrivalPrecondition(obj, npc)) return false;
            return true;
        }

        /// <summary>What this looks like it offers from a distance. Used when planning.</summary>
        public bool AdvertisedTo(SmartObject obj, Npc npc)
        {
            if ((Actors & ActorKind.Npc) == 0) return false;
            if (Precondition != null && !Precondition(obj, npc)) return false;
            return true;
        }
    }

    /// <summary>
    /// A thing in the world that advertises what can be done with it and holds
    /// a bag of float state. Sabotage is almost always "change a number in this
    /// bag so an advertisement stops being true".
    /// </summary>
    public sealed class SmartObject
    {
        public string Id = "";
        public string Name = "";
        public Vec3 Position;

        /// <summary>Where it belongs. Away from home for too long = noticeable.</summary>
        public Vec3 HomePosition;

        /// <summary>NPC who considers this theirs. Touching it is personal.</summary>
        public string OwnerId = "";

        public readonly HashSet<string> Tags = new HashSet<string>();
        public readonly Dictionary<string, float> State = new Dictionary<string, float>();
        public readonly List<Affordance> Affordances = new List<Affordance>();

        public bool Portable;

        /// <summary>Actor id currently carrying it, empty if on the ground.</summary>
        public string HeldBy = "";

        /// <summary>Out of sight (in the bin, behind the crates). Cannot be found by casual looking.</summary>
        public bool Concealed;

        /// <summary>Visual size hint for the grey-box renderer.</summary>
        public Vec3 Size = new Vec3(0.6f, 0.6f, 0.6f);

        public float GetState(string key, float fallback = 0f)
        {
            float v;
            return State.TryGetValue(key, out v) ? v : fallback;
        }

        public void SetState(string key, float value)
        {
            State[key] = value;
        }

        public void AddState(string key, float delta)
        {
            State[key] = GetState(key) + delta;
        }

        public bool HasTag(string tag)
        {
            return Tags.Contains(tag);
        }

        public SmartObject WithTag(string tag)
        {
            Tags.Add(tag);
            return this;
        }

        public SmartObject WithState(string key, float value)
        {
            State[key] = value;
            return this;
        }

        public SmartObject WithAffordance(Affordance a)
        {
            Affordances.Add(a);
            return this;
        }

        public bool IsAwayFromHome
        {
            get { return Vec3.FlatDistance(Position, HomePosition) > 1.2f; }
        }

        /// <summary>Convention: "broken" >= 1 means unusable.</summary>
        public bool IsBroken
        {
            get { return GetState("broken") >= 1f; }
        }

        public List<Affordance> AvailableFor(Npc npc)
        {
            List<Affordance> list = new List<Affordance>();
            for (int i = 0; i < Affordances.Count; i++)
            {
                if (Affordances[i].AvailableFor(this, npc)) list.Add(Affordances[i]);
            }
            return list;
        }

        /// <summary>What this object appears to offer an NPC who is still walking over.</summary>
        public List<Affordance> AdvertisedTo(Npc npc)
        {
            List<Affordance> list = new List<Affordance>();
            for (int i = 0; i < Affordances.Count; i++)
            {
                if (Affordances[i].AdvertisedTo(this, npc)) list.Add(Affordances[i]);
            }
            return list;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>Tag vocabulary. Kept as constants so typos fail at compile time.</summary>
    public static class Tags
    {
        public const string Food = "food";
        public const string Tool = "tool";
        public const string Appliance = "appliance";
        public const string Fragile = "fragile";
        public const string Seat = "seat";
        public const string Toilet = "toilet";
        public const string Container = "container";
        public const string Mess = "mess";
        public const string Noisy = "noisy";
        public const string Ingredient = "ingredient";
        public const string Social = "social";

        /// <summary>Something an NPC can slip on or trip over.</summary>
        public const string Hazard = "hazard";

        /// <summary>Somewhere the player can tuck themselves out of sight.</summary>
        public const string Hiding = "hiding";

        /// <summary>A door: blocks sight and movement while shut.</summary>
        public const string Door = "door";
    }

    /// <summary>State-bag key vocabulary.</summary>
    public static class StateKeys
    {
        public const string Broken = "broken";
        public const string Contents = "contents";
        public const string Tampered = "tampered";
        public const string Heat = "heat";
        public const string Cooking = "cooking";
        public const string Dirty = "dirty";

        /// <summary>Tampering that cannot be seen, only discovered by use.</summary>
        public const string Subtle = "subtleTamper";

        public const string Volume = "volume";
    }
}
