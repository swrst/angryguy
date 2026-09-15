namespace AngryGuy.Core
{
    /// <summary>
    /// Every route from "something happened" to "someone is angrier".
    ///
    /// Design rule: the player never adds anger directly. The player invalidates
    /// plans, damages property and creates social friction; anger is what the
    /// simulation does about it. That is why solutions the designers never wrote
    /// still work.
    /// </summary>
    public static class AngerModel
    {
        /// <summary>Above this, the NPC explodes publicly.</summary>
        public const float BoilingPoint = 0.85f;

        /// <summary>A grievance this size or larger counts as "another thing going wrong".</summary>
        public const float TensionTrigger = 0.05f;

        public static bool Add(Npc npc, float amount, Simulation sim, string reason)
        {
            if (amount <= 0f) return false;

            // Each new problem lands harder while they are already wound up.
            float scaled = amount * (1f + npc.Tension * 1.4f);

            float before = npc.Anger;
            npc.Anger = Mathx.Clamp01(npc.Anger + scaled);
            if (npc.Anger > npc.PeakAnger) npc.PeakAnger = npc.Anger;

            if (amount >= TensionTrigger)
            {
                npc.Tension = Mathx.Clamp01(npc.Tension + 0.14f);
            }

            if (sim != null) sim.NoteAngerChange(npc, scaled, reason);

            return before < BoilingPoint && npc.Anger >= BoilingPoint;
        }

        /// <summary>
        /// The core beat: an NPC walked across the room to do something and it
        /// was not there / not working / already ruined.
        /// </summary>
        public static float PlanFailure(Npc npc, NeedType motive)
        {
            npc.FrustrationCount++;

            float temper = Mathx.Lerp(0.5f, 1.8f, npc.Personality.Temper);
            float urgency = 0.45f + npc.Needs.Urgency(motive);

            // Second and third failures hurt more than the first. NPCs having
            // "one of those days" is most of the comedy.
            float escalation = 1f + Mathx.Clamp(npc.FrustrationCount - 1, 0, 4) * 0.28f;

            return 0.085f * temper * urgency * escalation;
        }

        /// <summary>Their property was taken, moved, broken or messed with.</summary>
        public static float PropertyViolation(Npc npc, float severity)
        {
            float territorial = Mathx.Lerp(0.4f, 1.9f, npc.Personality.Territoriality);
            float temper = Mathx.Lerp(0.6f, 1.5f, npc.Personality.Temper);
            return 0.11f * severity * territorial * temper;
        }

        /// <summary>Mess in their space. Small on its own, corrosive in bulk.</summary>
        public static float DisorderSeen(Npc npc, float severity)
        {
            return 0.035f * severity * Mathx.Lerp(0.2f, 1.8f, npc.Personality.Tidiness);
        }

        /// <summary>Being accused to your face, deservedly or not.</summary>
        public static float Accused(Npc npc, float confidence)
        {
            float temper = Mathx.Lerp(0.6f, 1.7f, npc.Personality.Temper);
            return 0.13f * (0.5f + confidence) * temper;
        }

        /// <summary>Witnessing a row nearby. Raises the temperature of the room.</summary>
        public static float WitnessedConflict(Npc npc)
        {
            return 0.02f * Mathx.Lerp(0.5f, 1.5f, npc.Personality.Temper);
        }

        /// <summary>
        /// A need pinned at rock bottom grinds away at them continuously.
        /// Deliberately small: this is background pressure, not a driver. If it
        /// out-paces Decay, every NPC maxes out on their own and the player is
        /// irrelevant.
        /// </summary>
        public static float Starvation(Npc npc, float dt)
        {
            float total = 0f;
            for (int i = 0; i < Needs.Count; i++)
            {
                NeedType need = (NeedType)i;
                if (npc.Needs.IsStarved(need)) total += 0.0015f * dt;
            }
            return total * Mathx.Lerp(0.5f, 1.6f, npc.Personality.Temper);
        }

        /// <summary>
        /// Cooling off. Must comfortably out-pace the ambient sources above, so
        /// that an undisturbed restaurant settles back to calm and any anger the
        /// player sees is anger the player caused.
        /// </summary>
        public static void Decay(Npc npc, float dt)
        {
            float rate = Mathx.Lerp(0.012f, 0.0015f, npc.Personality.Grudge);
            npc.Anger = Mathx.Clamp01(npc.Anger - rate * dt);

            // Winding down takes a while, so a chain of incidents keeps its
            // momentum across a couple of minutes of play.
            npc.Tension = Mathx.Clamp01(npc.Tension - 0.004f * dt);

            // Suspicion fades too, otherwise one unlucky glance ends the level.
            float suspicionDecay = Mathx.Lerp(0.016f, 0.0035f, npc.Personality.Grudge) * dt;
            string[] keys = new string[npc.Suspicion.Count];
            npc.Suspicion.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                npc.Suspicion[keys[i]] = Mathx.Clamp01(npc.Suspicion[keys[i]] - suspicionDecay);
            }
        }
    }
}
