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

        /// <summary>
        /// The urge to put things right rather than just be annoyed about them.
        /// A diligent NPC walks over and repairs, cleans or re-shelves whatever
        /// they find wrong, which puts a clock on every trap the player sets.
        /// </summary>
        public float Diligence = 0.15f;

        /// <summary>
        /// Chance of knocking things over, dropping what they carry and generally
        /// generating background chaos. Someone clumsy in the building is cover:
        /// blame has somewhere innocent to land.
        /// </summary>
        public float Clumsiness = 0.05f;

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
                Grudge = Grudge,
                Diligence = Diligence,
                Clumsiness = Clumsiness
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
            if (Diligence > 0.7f) parts.Add("tidies up after everyone");
            if (Clumsiness > 0.55f) parts.Add("clumsy");
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

        /// <summary>
        /// The fixer. Loyal to the chef, and constitutionally unable to walk past
        /// a broken thing. He is the reason sabotage has a shelf life: set a trap
        /// and he may well have tidied it away before the target ever reaches it.
        /// The player's counter-play is to keep him busy, keep him out, or set
        /// traps where he does not patrol.
        /// </summary>
        public static Personality SousChef()
        {
            return new Personality
            {
                Temper = 0.55f,
                Tidiness = 0.90f,
                Paranoia = 0.55f,
                Sociability = 0.40f,
                Gluttony = 0.45f,
                Territoriality = 0.55f,
                Observance = 0.75f,
                Gullibility = 0.35f,
                Grudge = 0.45f,
                Diligence = 0.95f,
                Clumsiness = 0.05f
            };
        }

        /// <summary>
        /// The wildcard. Drops things, moves things, makes noise. He generates
        /// genuine background anomalies, which is what gives the player
        /// deniability: in a building where things go wrong on their own, an
        /// accusation is a much harder sell.
        /// </summary>
        public static Personality Porter()
        {
            return new Personality
            {
                Temper = 0.30f,
                Tidiness = 0.20f,
                Paranoia = 0.15f,
                Sociability = 0.70f,
                Gluttony = 0.80f,
                Territoriality = 0.15f,
                Observance = 0.25f,
                Gullibility = 0.85f,
                Grudge = 0.15f,
                Diligence = 0.10f,
                Clumsiness = 0.85f
            };
        }
    }
}
