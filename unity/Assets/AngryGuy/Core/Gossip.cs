using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// Information moves between NPCs when they talk. Suspicion is contagious,
    /// memories get copied with lower confidence and no first-hand status, and
    /// after three hops the story is confidently wrong.
    ///
    /// This is the cheapest emergent-comedy generator in the whole design: no
    /// authored dialogue, just a weighted copy of one NPC's beliefs into another.
    /// </summary>
    public static class Gossip
    {
        public static void Exchange(Npc a, Npc b, Simulation sim)
        {
            SpreadOneWay(a, b, sim);
            SpreadOneWay(b, a, sim);

            a.Needs.Add(NeedType.Social, 0.30f);
            b.Needs.Add(NeedType.Social, 0.30f);

            // Talking to someone is mildly bonding, which later changes who they
            // are willing to believe and who they are willing to blame.
            a.AddRelationship(b.Id, 0.04f);
            b.AddRelationship(a.Id, 0.04f);

            a.GossipCooldown = 18f;
            b.GossipCooldown = 18f;
        }

        private static void SpreadOneWay(Npc from, Npc to, Simulation sim)
        {
            float trust = Mathx.Clamp01((from.RelationshipWith(to.Id) + 1f) * 0.5f);
            float credulity = to.Personality.Gullibility;
            float transfer = Mathx.Lerp(0.12f, 0.55f, credulity) * Mathx.Lerp(0.5f, 1.2f, trust);

            // Pass on the juiciest suspicion.
            string topSuspect = "";
            float topValue = 0f;
            foreach (KeyValuePair<string, float> kv in from.Suspicion)
            {
                if (kv.Key == to.Id) continue;
                if (kv.Value > topValue)
                {
                    topValue = kv.Value;
                    topSuspect = kv.Key;
                }
            }

            if (topSuspect.Length > 0 && topValue > 0.18f)
            {
                sim.RaiseSuspicion(to, topSuspect, topValue * transfer,
                    "heard it from " + from.Name);
                to.AddRelationship(topSuspect, -0.06f * transfer);

                string suspectName = sim.DisplayName(topSuspect);
                from.Say("...and I reckon it was " + suspectName + ".");
                sim.Publish(new WorldEvent
                {
                    Kind = EventKind.Noise,
                    Position = from.Position,
                    TrueActorId = from.Id,
                    Loudness = 0.12f,
                    Severity = 0.05f,
                    Description = from.Name + " tells " + to.Name + " that " + suspectName + " is behind it"
                });
            }

            // Pass on a couple of remembered incidents, degraded.
            int shared = 0;
            for (int i = from.Memory.Entries.Count - 1; i >= 0 && shared < 2; i--)
            {
                MemoryEntry e = from.Memory.Entries[i];
                if (e.Confidence < 0.35f) continue;
                if (to.Memory.KnowsAbout(e.Kind, e.ObjectId)) continue;

                to.Memory.Remember(new MemoryEntry
                {
                    Kind = e.Kind,
                    ObjectId = e.ObjectId,
                    BelievedActorId = e.BelievedActorId,
                    Where = e.Where,
                    When = e.When,
                    Confidence = e.Confidence * Mathx.Lerp(0.35f, 0.8f, credulity),
                    FirstHand = false,
                    SourceId = from.Id,
                    Description = "heard from " + from.Name + ": " + e.Description
                });

                shared++;
            }
        }
    }
}
