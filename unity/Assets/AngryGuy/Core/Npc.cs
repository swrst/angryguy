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
        Furious
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
        public Needs Needs = new Needs();
        public NpcMemory Memory = new NpcMemory();
        public PerceptionModel Perception = new PerceptionModel();

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

        /// <summary>Set while investigating: where they are heading to look.</summary>
        public Vec3 InvestigationPoint;

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

        /// <summary>Last thing they said, surfaced in the HUD as a speech bubble.</summary>
        public string Speech = "";
        public float SpeechTimer;

        public bool IsTarget;

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
