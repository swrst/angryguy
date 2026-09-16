using System.Collections.Generic;

namespace AngryGuy.Core
{
    public enum Emotion
    {
        Composed,
        Afraid,
        Amused,
        Embarrassed,
        Proud,
        Bored,
        Suspicious
    }

    /// <summary>
    /// What an NPC feels, and what it believes about itself.
    ///
    /// Anger alone makes a target, not a character. This is the layer that makes
    /// four NPCs behave in noticeably different ways from the same events: one
    /// finds a colleague falling over hilarious, one finds it frightening, one is
    /// mortified that it happened to them in front of witnesses, and one quietly
    /// concludes that too much has gone wrong today for it to be chance.
    ///
    /// Every field here feeds back into behaviour. Nothing is decoration.
    /// </summary>
    public sealed class Mind
    {
        // ---- secondary emotions (0..1, all decay) -------------------------

        /// <summary>Unexplained loud things, alone in a room where stuff keeps breaking.</summary>
        public float Fear;

        /// <summary>Schadenfreude. Rises when someone they dislike has a bad time.</summary>
        public float Amusement;

        /// <summary>Their own failure, in public. Scales hard with how many people saw.</summary>
        public float Embarrassment;

        /// <summary>Work done well. A proud NPC is harder to wind up.</summary>
        public float Pride;

        /// <summary>Nothing has happened for a while. Bored NPCs go looking for something.</summary>
        public float Boredom;

        // ---- self-model ---------------------------------------------------

        /// <summary>How they think today is going. Low means a short fuse.</summary>
        public float DayQuality = 0.65f;

        /// <summary>"People think it was me." Rises with every accusation and sideways look.</summary>
        public float FeelsBlamed;

        /// <summary>How many personal misfortunes they have racked up.</summary>
        public int ThingsGoneWrong;

        /// <summary>
        /// The important one. Their own conviction that today's run of bad luck
        /// is not bad luck at all.
        ///
        /// This is the difficulty curve, expressed from inside the fiction: the
        /// more the player breaks, the more the cast starts actually looking for
        /// a culprit rather than shrugging and getting on with the shift.
        /// </summary>
        public float Wariness;

        /// <summary>Set once they have said the quiet part out loud, so they only do it once.</summary>
        public bool VoicedSuspicionOfSabotage;

        public void Tick(float dt, Personality personality)
        {
            Fear = Mathx.Clamp01(Fear - 0.030f * dt);
            Amusement = Mathx.Clamp01(Amusement - 0.055f * dt);
            Embarrassment = Mathx.Clamp01(Embarrassment - 0.028f * dt);
            Pride = Mathx.Clamp01(Pride - 0.020f * dt);
            Wariness = Mathx.Clamp01(Wariness - 0.006f * dt);
            FeelsBlamed = Mathx.Clamp01(FeelsBlamed - 0.012f * dt);

            // Boredom is the only one that grows on its own.
            Boredom = Mathx.Clamp01(Boredom + 0.012f * dt);

            // The day drifts back toward "fine" if nothing else happens, more
            // slowly for people who hold a grudge.
            float recovery = Mathx.Lerp(0.020f, 0.006f, personality.Grudge);
            DayQuality = Mathx.Clamp01(DayQuality + (0.65f - DayQuality) * recovery * dt);
        }

        /// <summary>Something went wrong for them personally.</summary>
        public void Misfortune(float severity, Personality personality)
        {
            ThingsGoneWrong++;
            DayQuality = Mathx.Clamp01(DayQuality - 0.12f * severity);
            Boredom = 0f;

            // Each new mishap makes "this is deliberate" a little more plausible,
            // and paranoid people get there much faster.
            float leap = 0.10f * severity * Mathx.Lerp(0.5f, 2.0f, personality.Paranoia);
            Wariness = Mathx.Clamp01(Wariness + leap);
        }

        /// <summary>Work completed successfully.</summary>
        public void Satisfaction(Personality personality)
        {
            Pride = Mathx.Clamp01(Pride + 0.18f);
            DayQuality = Mathx.Clamp01(DayQuality + 0.05f);
            Boredom = Mathx.Clamp01(Boredom - 0.35f);
        }

        /// <summary>
        /// Strongest current feeling, for the HUD and for speech. Anger is
        /// deliberately not in here - it has its own meter.
        /// </summary>
        public Emotion Dominant
        {
            get
            {
                float best = 0.25f;
                Emotion winner = Emotion.Composed;

                if (Fear > best) { best = Fear; winner = Emotion.Afraid; }
                if (Amusement > best) { best = Amusement; winner = Emotion.Amused; }
                if (Embarrassment > best) { best = Embarrassment; winner = Emotion.Embarrassed; }
                if (Wariness > best) { best = Wariness; winner = Emotion.Suspicious; }
                if (Pride > best) { best = Pride; winner = Emotion.Proud; }
                if (Boredom > 0.8f && best < Boredom) winner = Emotion.Bored;

                return winner;
            }
        }

        public string DominantWord
        {
            get
            {
                switch (Dominant)
                {
                    case Emotion.Afraid: return "rattled";
                    case Emotion.Amused: return "amused";
                    case Emotion.Embarrassed: return "mortified";
                    case Emotion.Proud: return "pleased";
                    case Emotion.Bored: return "bored";
                    case Emotion.Suspicious: return "wary";
                    default: return "";
                }
            }
        }

        /// <summary>
        /// Multiplier on how carefully they are watching the room. A wary,
        /// frightened NPC misses very little; a proud, bored one misses plenty.
        /// </summary>
        public float Alertness
        {
            get
            {
                return Mathx.Clamp(
                    1f + Wariness * 0.9f + Fear * 0.4f - Pride * 0.2f - Boredom * 0.15f,
                    0.6f, 2.4f);
            }
        }

        /// <summary>Convinced enough to start actively looking for who is doing this.</summary>
        public bool SuspectsSabotage
        {
            get { return Wariness >= 0.55f; }
        }
    }

    /// <summary>
    /// Lines NPCs say. Kept as data so the same event produces different words
    /// from different people, and so writing new dialogue needs no code.
    /// </summary>
    public static class Lines
    {
        public static string Misfortune(Npc npc, string what)
        {
            if (npc.Mind.SuspectsSabotage) return "Again?! That's not an accident.";
            if (npc.Anger > 0.7f) return "WHO TOUCHED MY " + what.ToUpperInvariant() + "?!";
            if (npc.Personality.Temper > 0.7f) return "Oh, you have GOT to be joking.";
            if (npc.Mind.Fear > 0.4f) return "...that's the third thing today.";
            return "Hm? The " + what + " isn't right...";
        }

        public static string Amused(Npc npc, string victimName)
        {
            if (npc.Personality.Sociability > 0.7f) return "Ha! Did everyone see that?";
            if (npc.RelationshipWith(victimName) < -0.2f) return "Serves them right.";
            return "Heh.";
        }

        public static string Embarrassed(Npc npc, int witnesses)
        {
            if (witnesses >= 2) return "Nobody saw that. NOBODY SAW THAT.";
            if (npc.Personality.Temper > 0.6f) return "Right. Who left that there?";
            return "...I'm fine.";
        }

        public static string Afraid(Npc npc)
        {
            if (npc.Personality.Observance > 0.7f) return "Something's going on in here.";
            return "Is somebody there?";
        }

        public static string RealisesSabotage(Npc npc)
        {
            if (npc.Personality.Paranoia > 0.7f) return "Someone is doing this on purpose. I know it.";
            if (npc.Personality.Temper > 0.7f) return "This is NOT bad luck. Somebody's at it.";
            return "This can't all be coincidence, can it?";
        }

        public static string FeelsBlamed(Npc npc)
        {
            if (npc.Personality.Temper > 0.6f) return "Why is it always ME?";
            return "I didn't do anything, I swear.";
        }

        public static string Bored(Npc npc)
        {
            if (npc.Personality.Sociability > 0.7f) return "Quiet today, isn't it?";
            return "*sighs*";
        }

        public static string Proud(Npc npc)
        {
            if (npc.Personality.Temper > 0.8f) return "THAT is how it's done.";
            return "Not bad, that.";
        }

        public static string StartsFixing(Npc npc, SmartObject obj)
        {
            if (npc.Mind.SuspectsSabotage) return "Right. Again. I'll sort it. Again.";
            if (npc.Personality.Tidiness > 0.85f) return "That's not staying like that.";
            return "I'll deal with the " + obj.Name + ".";
        }

        public static string WorkingOnIt(Npc npc, SmartObject obj)
        {
            if (obj.IsBroken) return "Right, this is going to take me a minute.";
            return "Where does this even live...";
        }

        public static string NotesForLater(Npc npc, SmartObject obj)
        {
            if (npc.Personality.Temper > 0.6f) return "The " + obj.Name + ". I'll be having words.";
            return "I'll get to the " + obj.Name + " in a minute.";
        }

        public static string FinishedFixing(Npc npc)
        {
            if (npc.Mind.Wariness > 0.4f) return "There. And if it happens again I'm asking questions.";
            if (npc.Personality.Sociability > 0.6f) return "Good as new. You're welcome.";
            return "Sorted.";
        }

        public static string Fumbles(Npc npc)
        {
            if (npc.Personality.Sociability > 0.6f) return "Whoops! That was me, sorry!";
            if (npc.Mind.Embarrassment > 0.4f) return "...nobody needs to know about that.";
            return "Ah. Butterfingers.";
        }

        public static string Pick(Rng rng, List<string> options)
        {
            return options.Count == 0 ? "" : options[rng.NextInt(options.Count)];
        }
    }
}
