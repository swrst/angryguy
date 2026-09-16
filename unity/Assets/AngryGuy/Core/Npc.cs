using System.Collections.Generic;

namespace AngryGuy.Core
{
    public enum NpcActivity
    {
        Idle,
        Walking,
        Using,
        Investigating,
        Confronting,
        Chatting,
        Watching,
        Furious,

        /// <summary>Walking over to put something right, then putting it right.</summary>
        Repairing
    }

    /// <summary>
    /// A committed intention: "walk to the stove and cook".
    /// Plans matter because a plan that fails on arrival is the single
    /// biggest source of anger in the game.
    /// </summary>
    public sealed class Plan
    {
        public SmartObject Target;
        public Affordance Affordance;
        public float Elapsed;
        public bool InProgress;

        /// <summary>Need that motivated this plan, for frustration weighting.</summary>
        public NeedType Motive;

        public string Describe()
        {
            if (Target == null || Affordance == null) return "nothing";
            return Affordance.Verb + " " + Target.Name;
        }
    }

    public sealed class Npc
    {
        public string Id = "";
        public string Name = "";
        public string Role = "";

        public Vec3 Position;
        public Vec3 Facing = new Vec3(0f, 0f, 1f);
        public float MoveSpeed = 2.6f;

        public Personality Personality = new Personality();
        public Mind Mind = new Mind();
        public Needs Needs = new Needs();
        public NpcMemory Memory = new NpcMemory();
        public PerceptionModel Perception = new PerceptionModel();

        /// <summary>What they are looking at, and the beat before they react.</summary>
        public readonly Attention Attention = new Attention();

        /// <summary>Set by World.Add. Lets a precondition look up another object.</summary>
        public World World;

        /// <summary>0..1. The thing the player is trying to raise.</summary>
        public float Anger;

        /// <summary>Peak anger reached, so a brief explosion still counts as a win.</summary>
        public float PeakAnger;

        /// <summary>
        /// "Having one of those days." Rises with every grievance and multiplies
        /// the next one, then bleeds off over a couple of minutes.
        ///
        /// This is what makes timing a skill: four sabotages inside two minutes
        /// compound into fury, while the same four spread over ten minutes are
        /// shrugged off one at a time.
        /// </summary>
        public float Tension;

        /// <summary>Per-actor belief that they are behind recent trouble. 0..1.</summary>
        public readonly Dictionary<string, float> Suspicion = new Dictionary<string, float>();

        /// <summary>-1 (enemy) .. +1 (friend). Drives motive and trust in gossip.</summary>
        public readonly Dictionary<string, float> Relationships = new Dictionary<string, float>();

        public NpcActivity Activity = NpcActivity.Idle;
        public Plan CurrentPlan;
        public Vec3 MoveTarget;
        public float ActivityTimer;

        /// <summary>How close they have ever got to the current target, for stuck detection.</summary>
        public float ClosestApproach = float.MaxValue;

        /// <summary>How long they have been failing to make progress toward it.</summary>
        public float StuckTimer;

        /// <summary>Set while investigating: where they are heading to look.</summary>
        public Vec3 InvestigationPoint;

        /// <summary>Object this NPC is on their way to put right, if any.</summary>
        public string RepairTargetId = "";

        /// <summary>Seconds of work left on the current repair.</summary>
        public float RepairTimer;

        /// <summary>
        /// Something they have clocked but will not down tools for. They will go
        /// and deal with it the moment they finish what they are doing, which is
        /// the player's window: catch the fixer mid-task and your trap survives
        /// long enough to land.
        /// </summary>
        public string PendingRepairId = "";

        /// <summary>Stops a diligent NPC re-fixing the same thing in a loop.</summary>
        public readonly Dictionary<string, float> RepairCooldown = new Dictionary<string, float>();

        /// <summary>Owned things they have already gone and fetched the owner for.</summary>
        public readonly HashSet<string> ToldOwnerAbout = new HashSet<string>();

        /// <summary>Time until this NPC can fumble something again.</summary>
        public float FumbleCooldown;

        public string ConfrontTargetId = "";

        /// <summary>Zone they gravitate to when idle.</summary>
        public string HomeZoneId = "";

        public float GossipCooldown;
        public float ConfrontCooldown;
        public float OutburstCooldown;
        public float DecisionCooldown;

        /// <summary>
        /// Object id -> time until which this NPC stops trying that object.
        /// Set when a plan fails there, so sabotage produces frustration and a
        /// change of behaviour instead of an infinite retry loop.
        /// </summary>
        public readonly Dictionary<string, float> AvoidUntil = new Dictionary<string, float>();

        /// <summary>How many times a plan of theirs has collapsed. Escalates reactions.</summary>
        public int FrustrationCount;

        /// <summary>
        /// When this NPC last gained suspicion of the player. One action can trip
        /// several code paths at once (the act is seen AND the resulting event is
        /// witnessed); without this they stack and a single slip ends the run.
        /// </summary>
        public float LastPlayerSuspicionTime = -99f;

        /// <summary>Anger level at their last public explosion, so they need a fresh reason to do it again.</summary>
        public float AngerAtLastOutburst = -1f;

        /// <summary>
        /// Drip-fed suspicion banked up until it is worth telling the player
        /// about. Without this, standing in the wrong room produces a "+1" every
        /// tick and drowns out the events that matter.
        /// </summary>
        public float PendingSuspicionReport;

        /// <summary>Last thing they said, surfaced in the HUD as a speech bubble.</summary>
        public string Speech = "";
        public float SpeechTimer;

        public bool IsTarget;

        /// <summary>
        /// Paranoia as it actually applies right now: their baseline plus however
        /// convinced they have become that today's mishaps are deliberate. This
        /// is what makes a level get harder the more you break.
        /// </summary>
        public float EffectiveParanoia
        {
            get { return Mathx.Clamp01(Personality.Paranoia + Mind.Wariness * 0.45f); }
        }

        /// <summary>Signature actions this NPC does more often than the numbers alone suggest.</summary>
        public readonly Dictionary<string, float> Habits = new Dictionary<string, float>();

        public float HabitWeight(string objectId, string affordanceId)
        {
            float weight;
            return Habits.TryGetValue(objectId + ":" + affordanceId, out weight) ? weight : 1f;
        }

        public void AddHabit(string objectId, string affordanceId, float weight)
        {
            Habits[objectId + ":" + affordanceId] = weight;
        }

        public float SuspicionOf(string actorId)
        {
            float v;
            return Suspicion.TryGetValue(actorId, out v) ? v : 0f;
        }

        public void AddSuspicion(string actorId, float amount)
        {
            if (actorId.Length == 0) return;
            Suspicion[actorId] = Mathx.Clamp01(SuspicionOf(actorId) + amount);
        }

        public float RelationshipWith(string npcId)
        {
            float v;
            return Relationships.TryGetValue(npcId, out v) ? v : 0f;
        }

        public void AddRelationship(string npcId, float delta)
        {
            if (npcId.Length == 0) return;
            Relationships[npcId] = Mathx.Clamp(RelationshipWith(npcId) + delta, -1f, 1f);
        }

        public void Say(string line, float duration = 3.5f)
        {
            Speech = line;
            SpeechTimer = duration;
        }

        public void AbandonPlan()
        {
            CurrentPlan = null;
            Activity = NpcActivity.Idle;
        }

        public bool IsFurious
        {
            get { return Anger >= 0.85f; }
        }

        public string MoodWord
        {
            get
            {
                if (Anger >= 0.85f) return "FURIOUS";
                if (Anger >= 0.6f) return "angry";
                if (Anger >= 0.35f) return "irritated";

                // Below real anger, what they are feeling is more interesting than
                // "calm" - and it is what tells the player which lever to pull.
                string emotion = Mind.DominantWord;
                if (emotion.Length > 0) return emotion;

                if (Anger >= 0.15f) return "bothered";
                return "calm";
            }
        }

        /// <summary>
        /// What this NPC is visibly doing, in words the player can act on.
        /// Shown above their head, because "Investigating" on screen is the
        /// difference between a readable stealth game and a confusing one.
        /// </summary>
        public string StatusLabel
        {
            get
            {
                switch (Activity)
                {
                    case NpcActivity.Investigating: return "investigating";
                    case NpcActivity.Confronting: return "confronting " + ConfrontTargetId;
                    case NpcActivity.Chatting: return "chatting";
                    case NpcActivity.Using:
                        return CurrentPlan != null ? CurrentPlan.Describe().ToLowerInvariant() : "busy";
                    case NpcActivity.Walking:
                        return CurrentPlan != null
                            ? "off to " + CurrentPlan.Target.Name
                            : "wandering";
                    case NpcActivity.Watching: return "looking around";
                    default: return "idle";
                }
            }
        }

        /// <summary>Short glyph for the bubble over their head. ASCII only - no font dependency.</summary>
        public string StatusGlyph
        {
            get
            {
                if (Anger >= 0.85f) return "!!!";
                if (Activity == NpcActivity.Confronting) return "!!";
                if (Activity == NpcActivity.Investigating) return "?";
                if (Anger >= 0.5f) return "!";
                if (Activity == NpcActivity.Chatting) return "...";
                return "";
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
