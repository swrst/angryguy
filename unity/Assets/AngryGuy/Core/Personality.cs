using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// Personality traits, all 0..1. These are multipliers on existing systems
    /// rather than special-case behaviour, which is what keeps NPCs feeling
    /// different without every NPC needing its own code path.
    /// </summary>
    public sealed class Personality
    {
        /// <summary>How hard anger spikes and how loudly it is expressed.</summary>
        public float Temper = 0.5f;

        /// <summary>Cares about mess. Drives the Order need and cleanup behaviour.</summary>
        public float Tidiness = 0.5f;

        /// <summary>Assumes malice over accident. Raises blame scores on people.</summary>
        public float Paranoia = 0.5f;

        /// <summary>Seeks company, starts conversations, spreads rumours faster.</summary>
        public float Sociability = 0.5f;

        /// <summary>Food matters more. Hunger-satisfying affordances score higher.</summary>
        public float Gluttony = 0.5f;

        /// <summary>Anger multiplier when *their own* property is touched.</summary>
        public float Territoriality = 0.5f;

        /// <summary>Perception quality: sight range, FOV, chance to notice anomalies.</summary>
        public float Observance = 0.5f;

        /// <summary>How readily they believe someone else's accusation.</summary>
        public float Gullibility = 0.5f;

        /// <summary>Slows anger and suspicion decay. High grudge = never lets it go.</summary>
        public float Grudge = 0.5f;

        public Personality Clone()
        {
            return new Personality
            {
                Temper = Temper,
                Tidiness = Tidiness,
                Paranoia = Paranoia,
                Sociability = Sociability,
                Gluttony = Gluttony,
                Territoriality = Territoriality,
                Observance = Observance,
                Gullibility = Gullibility,
                Grudge = Grudge
            };
        }

        /// <summary>Human-readable summary for debug HUDs.</summary>
        public string Describe()
        {
            List<string> parts = new List<string>();
            if (Temper > 0.7f) parts.Add("hot-headed");
            if (Temper < 0.3f) parts.Add("placid");
            if (Tidiness > 0.7f) parts.Add("fussy");
            if (Paranoia > 0.7f) parts.Add("paranoid");
            if (Sociability > 0.7f) parts.Add("chatty");
            if (Gluttony > 0.7f) parts.Add("greedy");
            if (Territoriality > 0.7f) parts.Add("possessive");
            if (Observance > 0.7f) parts.Add("sharp-eyed");
            if (Observance < 0.3f) parts.Add("oblivious");
            if (Gullibility > 0.7f) parts.Add("credulous");
            if (Grudge > 0.7f) parts.Add("holds grudges");
            return parts.Count == 0 ? "unremarkable" : string.Join(", ", parts.ToArray());
        }

        // ---- Archetypes used by the kitchen prototype -------------------------

        /// <summary>The target. Explosive, owns the kitchen, not especially suspicious of people.</summary>
        public static Personality Chef()
        {
            return new Personality
            {
                Temper = 0.92f,
                Tidiness = 0.75f,
                Paranoia = 0.35f,
                Sociability = 0.30f,
                Gluttony = 0.70f,
                Territoriality = 0.95f,
                Observance = 0.55f,
                Gullibility = 0.60f,
                Grudge = 0.80f
            };
        }

        /// <summary>The gossip hub. Moves information (and blame) around the level.</summary>
        public static Personality Waiter()
        {
            return new Personality
            {
                Temper = 0.40f,
                Tidiness = 0.50f,
                Paranoia = 0.45f,
                Sociability = 0.95f,
                Gluttony = 0.35f,
                Territoriality = 0.30f,
                Observance = 0.60f,
                Gullibility = 0.75f,
                Grudge = 0.30f
            };
        }

        /// <summary>The natural scapegoat. Low standing, oblivious, believes anything.</summary>
        public static Personality Dishwasher()
        {
            return new Personality
            {
                Temper = 0.25f,
                Tidiness = 0.30f,
                Paranoia = 0.20f,
                Sociability = 0.45f,
                Gluttony = 0.55f,
                Territoriality = 0.20f,
                Observance = 0.20f,
                Gullibility = 0.90f,
                Grudge = 0.20f
            };
        }

        /// <summary>The threat. Watches everything, suspects people first. The player's real obstacle.</summary>
        public static Personality Manager()
        {
            return new Personality
            {
                Temper = 0.50f,
                Tidiness = 0.80f,
                Paranoia = 0.90f,
                Sociability = 0.50f,
                Gluttony = 0.25f,
                Territoriality = 0.60f,
                Observance = 0.95f,
                Gullibility = 0.25f,
                Grudge = 0.70f
            };
        }
    }
}
