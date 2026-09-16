using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// A thing a person was seen or heard <em>doing</em>.
    ///
    /// This is the channel the prototype was missing, and its absence was a real
    /// hole rather than a tuning problem: pulling a fire alarm in front of four
    /// people cost nothing, because the only thing published was the
    /// consequence - "the alarm is going off" - which carries no attribution.
    /// Nobody ever learned that a person had pulled it.
    ///
    /// The two channels are deliberately different shapes:
    ///
    ///   Act          carries WHO. Witnessed at the moment it happens.  -> SUSPICION
    ///   Consequence  carries no one. Discovered whenever someone looks. -> ANGER
    ///
    /// Keeping them apart is what makes the stealth honest. An NPC who walks in
    /// on a wrecked kitchen is furious at nobody in particular; an NPC who
    /// watched you wreck it is furious at you. Those must never be the same code
    /// path, or every sabotage silently incriminates the player and the whole
    /// game collapses into "don't be in the room".
    /// </summary>
    public sealed class Act
    {
        /// <summary>Who did it. Ground truth - only witnesses get told.</summary>
        public string ActorId = "";

        /// <summary>Short id for the kind of thing done, e.g. "pull_alarm".</summary>
        public string Id = "";

        /// <summary>Plain language, from a witness's point of view.</summary>
        public string Description = "";

        public Vec3 Position;

        /// <summary>What it is worth to see this happen. 0..1.</summary>
        public float Incrimination = 0.5f;

        /// <summary>
        /// How loud the act itself is. An act can be heard without being seen,
        /// which tells an NPC that somebody is doing something over there
        /// without telling them who - a much more interesting state than either
        /// extreme.
        /// </summary>
        public float Loudness;

        /// <summary>
        /// True when the act is unmistakably wrong to watch: pulling an alarm,
        /// breaking something, going through someone's bag. A brazen act is
        /// worth reacting to even at a glance, and even by someone who likes you.
        /// </summary>
        public bool Brazen;

        /// <summary>Object it was done to, when there is one.</summary>
        public string ObjectId = "";
    }

    /// <summary>
    /// How clearly one NPC perceived one act. Produced by perception, consumed
    /// by suspicion - nothing else is allowed to decide this.
    /// </summary>
    public enum Witnessing
    {
        /// <summary>Saw nothing, heard nothing.</summary>
        Missed,

        /// <summary>Heard it, but could not see who. Draws them over.</summary>
        HeardOnly,

        /// <summary>Caught it in peripheral vision. Enough to wonder.</summary>
        Glimpsed,

        /// <summary>Watched it happen, facing it, in the open.</summary>
        Saw
    }

    public static class Witnessed
    {
        /// <summary>
        /// Multiplier on an act's incrimination. A glimpse should nag rather
        /// than convict: being half-seen once must never end a run, or careful
        /// play stops being rewarded.
        /// </summary>
        public static float Weight(Witnessing w)
        {
            switch (w)
            {
                case Witnessing.Saw: return 1f;
                case Witnessing.Glimpsed: return 0.35f;
                default: return 0f;
            }
        }

        public static string Reason(Witnessing w)
        {
            switch (w)
            {
                case Witnessing.Saw: return "watched you do it";
                case Witnessing.Glimpsed: return "half-saw you do something";
                case Witnessing.HeardOnly: return "heard you at it";
                default: return "";
            }
        }
    }

    /// <summary>
    /// Everything the player can be seen doing, in one place, so that adding a
    /// verb cannot accidentally ship without an act attached to it.
    /// </summary>
    public static class Acts
    {
        public const string Take = "take";
        public const string Drop = "drop";
        public const string Throw = "throw";
        public const string Plant = "plant";
        public const string Break = "break";
        public const string Tamper = "tamper";
        public const string Rummage = "rummage";
        public const string PullAlarm = "pull_alarm";
        public const string Lights = "lights";
        public const string Dress = "dress";
        public const string Hide = "hide";

        /// <summary>
        /// Acts that are obviously wrong to watch, whoever is doing them. These
        /// bypass the "people mostly aren't paying attention" discount.
        /// </summary>
        private static readonly HashSet<string> BrazenActs = new HashSet<string>
        {
            PullAlarm, Break, Lights, Rummage, Plant
        };

        public static bool IsBrazen(string actId)
        {
            return BrazenActs.Contains(actId);
        }
    }
}
