using System.Collections.Generic;

namespace AngryGuy.Core
{
    public sealed class ScoredOption
    {
        public SmartObject Object;
        public Affordance Affordance;
        public float Score;
        public NeedType Motive;
        public string Explain = "";

        public override string ToString()
        {
            return string.Format("{0:0.000} {1} {2} ({3})",
                Score, Affordance != null ? Affordance.Verb : "?",
                Object != null ? Object.Name : "?", Explain);
        }
    }

    /// <summary>
    /// The decision layer. Every tick an NPC asks every reachable object
    /// "what do you offer me?", scores the answers against its own needs and
    /// personality, and commits to the best one.
    ///
    /// No behaviour trees, no per-NPC scripts. Designers add behaviour by adding
    /// objects with affordances, and the NPCs immediately know what to do with them.
    /// </summary>
    public static class UtilityAi
    {
        /// <summary>Below this, nothing is worth walking to; the NPC idles or wanders.</summary>
        public const float ActionThreshold = 0.035f;

        public static List<ScoredOption> ScoreAll(Npc npc, World world, float now)
        {
            List<ScoredOption> options = new List<ScoredOption>();

            for (int i = 0; i < world.Objects.Count; i++)
            {
                SmartObject obj = world.Objects[i];
                if (obj.HeldBy.Length > 0 && obj.HeldBy != npc.Id) continue;
                if (obj.Concealed) continue;

                float avoid = AvoidanceFactor(npc, obj, now);
                if (avoid <= 0f) continue;

                for (int a = 0; a < obj.Affordances.Count; a++)
                {
                    Affordance aff = obj.Affordances[a];
                    if (aff.IsSabotage) continue;

                    // Deliberately the distance check, not the full one. NPCs plan
                    // on what they can tell from here; the truth is discovered on
                    // arrival, and the gap between the two is the whole game.
                    if (!aff.AdvertisedTo(obj, npc)) continue;

                    ScoredOption option = Score(npc, obj, aff);
                    option.Score *= avoid;
                    if (option.Score > 0f) options.Add(option);
                }
            }

            options.Sort((x, y) => y.Score.CompareTo(x.Score));
            return options;
        }

        public static ScoredOption Choose(Npc npc, World world, Rng rng, float now)
        {
            List<ScoredOption> options = ScoreAll(npc, world, now);
            if (options.Count == 0) return null;

            // Soft selection: pick among the top few weighted by score, so two NPCs
            // with identical needs do not lockstep, and repeat playthroughs differ.
            // Weighted by score SQUARED. Plain score-weighting is far too loose:
            // a clearly best option loses a third of the time, NPCs look aimless,
            // and the player cannot predict a routine well enough to exploit it.
            // Squaring keeps genuine ties varied while making strong preferences win.
            int considered = options.Count < 4 ? options.Count : 4;
            float total = 0f;
            for (int i = 0; i < considered; i++) total += options[i].Score * options[i].Score;
            if (total <= 0f) return null;

            float roll = rng.NextFloat() * total;
            float running = 0f;
            for (int i = 0; i < considered; i++)
            {
                running += options[i].Score * options[i].Score;
                if (roll <= running)
                {
                    return options[i].Score >= ActionThreshold ? options[i] : null;
                }
            }

            ScoredOption best = options[0];
            return best.Score >= ActionThreshold ? best : null;
        }

        public static ScoredOption Score(Npc npc, SmartObject obj, Affordance aff)
        {
            float needScore = 0f;
            NeedType motive = NeedType.Comfort;
            float bestNeedContribution = 0f;

            for (int i = 0; i < aff.Satisfies.Count; i++)
            {
                NeedDelta delta = aff.Satisfies[i];
                float current = npc.Needs.Get(delta.Need);

                // Diminishing returns: filling a need that is already full is worthless.
                float headroom = delta.Amount >= 0f ? (1f - current) : 1f;
                float usable = delta.Amount >= 0f
                    ? (delta.Amount < headroom ? delta.Amount : headroom)
                    : delta.Amount;

                float contribution = npc.Needs.Urgency(delta.Need)
                                     * usable
                                     * TraitWeight(npc.Personality, delta.Need);

                needScore += contribution;
                if (contribution > bestNeedContribution)
                {
                    bestNeedContribution = contribution;
                    motive = delta.Need;
                }
            }

            if (needScore <= 0f)
            {
                return new ScoredOption
                {
                    Object = obj,
                    Affordance = aff,
                    Score = 0f,
                    Motive = motive,
                    Explain = "no need"
                };
            }

            float distance = Vec3.FlatDistance(npc.Position, obj.Position);
            float distanceFactor = Mathx.DistanceFalloff(distance, 4.5f);
            float ownership = OwnershipFactor(npc, obj);

            float score = needScore * aff.BaseAppeal * distanceFactor * ownership;

            return new ScoredOption
            {
                Object = obj,
                Affordance = aff,
                Score = score,
                Motive = motive,
                Explain = string.Format("{0} urgency {1:0.00} x dist {2:0.00} x own {3:0.00}",
                    motive, npc.Needs.Urgency(motive), distanceFactor, ownership)
            };
        }

        /// <summary>Personality bends what an NPC finds appealing.</summary>
        private static float TraitWeight(Personality p, NeedType need)
        {
            switch (need)
            {
                case NeedType.Hunger: return Mathx.Lerp(0.6f, 1.7f, p.Gluttony);
                case NeedType.Order: return Mathx.Lerp(0.3f, 1.9f, p.Tidiness);
                case NeedType.Social: return Mathx.Lerp(0.4f, 1.8f, p.Sociability);
                case NeedType.Comfort: return Mathx.Lerp(0.7f, 1.3f, 1f - p.Temper);
                default: return 1f;
            }
        }

        /// <summary>
        /// NPCs prefer their own things and are reluctant to use someone else's.
        /// This is what makes "whose stove is this" meaningful.
        /// </summary>
        private static float OwnershipFactor(Npc npc, SmartObject obj)
        {
            if (obj.OwnerId.Length == 0) return 1f;
            if (obj.OwnerId == npc.Id) return 1.3f;
            float relationship = npc.RelationshipWith(obj.OwnerId);
            return Mathx.Lerp(0.35f, 0.8f, Mathx.Clamp01((relationship + 1f) * 0.5f));
        }

        /// <summary>
        /// Recently failed here? Back off for a while, then try again.
        /// Without this, a sabotaged object turns into an infinite retry loop,
        /// which reads as a bug rather than as frustration.
        /// </summary>
        private static float AvoidanceFactor(Npc npc, SmartObject obj, float now)
        {
            float until;
            if (!npc.AvoidUntil.TryGetValue(obj.Id, out until)) return 1f;
            if (now >= until) return 1f;
            float remaining = until - now;
            return Mathx.Clamp01(1f - remaining / 20f) * 0.4f;
        }
    }
}
