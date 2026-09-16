using System.Collections.Generic;

namespace AngryGuy.Core
{
    public sealed class BlameResult
    {
        /// <summary>Empty means "nobody — must have been an accident".</summary>
        public string SuspectId = "";

        public float Confidence;
        public string Reason = "";

        public bool HasSuspect
        {
            get { return SuspectId.Length > 0; }
        }
    }

    /// <summary>
    /// Turns "something is wrong here" into "and I think YOU did it".
    ///
    /// The NPC never reads the event's true actor. It reasons only from what it
    /// personally saw, what it already believed, and what it was told. That gap
    /// between truth and belief is the entire stealth game.
    /// </summary>
    public static class BlameResolver
    {
        private const float OpportunityRadius = 5.5f;
        private const float LookBackSeconds = 25f;
        private const float LookForwardSeconds = 6f;

        public static BlameResult Resolve(Npc detective, WorldEvent anomaly, Simulation sim)
        {
            Dictionary<string, float> scores = new Dictionary<string, float>();
            Dictionary<string, string> reasons = new Dictionary<string, string>();

            // 1. Opportunity: who did I see near the scene around the time?
            List<Sighting> nearby = detective.Memory.WhoWasNear(
                anomaly.Position,
                OpportunityRadius,
                anomaly.Time - LookBackSeconds,
                anomaly.Time + LookForwardSeconds);

            for (int i = 0; i < nearby.Count; i++)
            {
                Sighting s = nearby[i];
                if (s.ActorId == detective.Id) continue;

                float recency = 1f - Mathx.Clamp01((anomaly.Time - s.When) / LookBackSeconds);
                float proximity = 1f - Mathx.Clamp01(
                    Vec3.FlatDistance(s.Where, anomaly.Position) / OpportunityRadius);

                float weight = 0.42f * (0.45f + 0.55f * recency) * (0.5f + 0.5f * proximity);
                weight += s.Incriminating * 0.55f;

                Bump(scores, reasons, s.ActorId, weight,
                    s.Incriminating > 0.3f ? "caught acting suspiciously nearby" : "was seen nearby");
            }

            // 2. Prior suspicion: this is how gossip and past incidents bite.
            foreach (KeyValuePair<string, float> kv in detective.Suspicion)
            {
                if (kv.Key == detective.Id || kv.Value <= 0.05f) continue;
                Bump(scores, reasons, kv.Key, kv.Value * 0.5f, "already under suspicion");
            }

            // 3. Motive: people I dislike are easier to blame.
            foreach (KeyValuePair<string, float> kv in detective.Relationships)
            {
                if (kv.Value >= 0f) continue;
                float dislike = -kv.Value;
                Bump(scores, reasons, kv.Key,
                    dislike * 0.28f * Mathx.Lerp(0.4f, 1.6f, detective.EffectiveParanoia),
                    "never liked them anyway");
            }

            // 4. Personal stake: if it was MY property, I care enough to find someone.
            bool personal = anomaly.VictimId == detective.Id;

            // Pick the winner.
            string bestId = "";
            float bestScore = 0f;
            foreach (KeyValuePair<string, float> kv in scores)
            {
                float jittered = kv.Value * (0.9f + sim.Rng.NextFloat() * 0.2f);
                if (jittered > bestScore)
                {
                    bestScore = jittered;
                    bestId = kv.Key;
                }
            }

            // Paranoid NPCs need almost no evidence. Trusting ones assume accidents.
            float threshold = Mathx.Lerp(0.52f, 0.14f, detective.EffectiveParanoia);
            if (personal) threshold *= 0.8f;

            if (bestId.Length == 0 || bestScore < threshold)
            {
                return new BlameResult
                {
                    SuspectId = "",
                    Confidence = 0f,
                    Reason = "no idea who"
                };
            }

            return new BlameResult
            {
                SuspectId = bestId,
                Confidence = Mathx.Clamp01(bestScore),
                Reason = reasons.ContainsKey(bestId) ? reasons[bestId] : "a hunch"
            };
        }

        private static void Bump(
            Dictionary<string, float> scores,
            Dictionary<string, string> reasons,
            string actorId,
            float amount,
            string reason)
        {
            if (actorId.Length == 0 || amount <= 0f) return;

            float current;
            scores.TryGetValue(actorId, out current);

            // Strongest single reason wins the label, so the HUD shows the real driver.
            if (!reasons.ContainsKey(actorId) || amount > current * 0.5f) reasons[actorId] = reason;

            scores[actorId] = current + amount;
        }
    }
}
