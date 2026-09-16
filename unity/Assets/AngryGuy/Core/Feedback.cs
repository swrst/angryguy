using System.Collections.Generic;

namespace AngryGuy.Core
{
    public enum FeedbackKind
    {
        /// <summary>Anger went up on someone. Shows over their head.</summary>
        Anger,

        /// <summary>Someone got more suspicious of the player.</summary>
        Suspicion,

        /// <summary>Neutral information: a plan failed, a discovery was made.</summary>
        Info,

        /// <summary>Something the player should react to right now.</summary>
        Alert,

        /// <summary>Objective progress.</summary>
        Objective,

        /// <summary>A trap the player set has just gone off.</summary>
        Payoff
    }

    /// <summary>
    /// One piece of player-facing feedback.
    ///
    /// The prototype's biggest failure in playtest was "I have no idea what's
    /// happening": the simulation was doing plenty, but none of it was legible.
    /// Every meaningful state change now emits one of these, carrying a number
    /// on the 0-100 scale players actually read and a plain-language reason.
    /// </summary>
    public sealed class FeedbackEvent
    {
        public FeedbackKind Kind;

        /// <summary>Who this belongs to, for positioning it over their head. May be empty.</summary>
        public string ActorId = "";

        /// <summary>Signed, 0-100 scale. Zero for messages that carry no number.</summary>
        public int Amount;

        /// <summary>Short reason, written for the player: "couldn't cook", "saw you".</summary>
        public string Text = "";

        public float Time;

        public override string ToString()
        {
            if (Amount == 0) return Text;
            return string.Format("{0}{1} {2} - {3}",
                Amount > 0 ? "+" : "", Amount, Kind.ToString().ToUpperInvariant(), Text);
        }
    }

    /// <summary>
    /// Named bands for suspicion. Players cannot read 0.34; they can read
    /// "something feels off". The bands also give the NPC visible behaviour
    /// changes to hang off.
    /// </summary>
    public enum SuspicionTier
    {
        Calm,
        Unsettled,
        Suspicious,
        Watching,
        Certain
    }

    public static class Suspicion
    {
        public static SuspicionTier TierFor(float value)
        {
            if (value >= 0.85f) return SuspicionTier.Certain;
            if (value >= 0.6f) return SuspicionTier.Watching;
            if (value >= 0.4f) return SuspicionTier.Suspicious;
            if (value >= 0.2f) return SuspicionTier.Unsettled;
            return SuspicionTier.Calm;
        }

        /// <summary>Reads after a name: "Marie is ...".</summary>
        public static string Describe(SuspicionTier tier)
        {
            switch (tier)
            {
                case SuspicionTier.Certain: return "sure it was you";
                case SuspicionTier.Watching: return "watching you closely";
                case SuspicionTier.Suspicious: return "getting suspicious";
                case SuspicionTier.Unsettled: return "starting to wonder";
                default: return "not paying you any attention";
            }
        }

        /// <summary>Reads as a standalone label in the HUD.</summary>
        public static string Label(SuspicionTier tier)
        {
            switch (tier)
            {
                case SuspicionTier.Certain: return "certain it was you";
                case SuspicionTier.Watching: return "watching you";
                case SuspicionTier.Suspicious: return "suspicious";
                case SuspicionTier.Unsettled: return "something feels off";
                default: return "hasn't noticed you";
            }
        }
    }

    /// <summary>One thing the player did, kept for the end-of-level summary.</summary>
    public sealed class PlayerAction
    {
        public float Time;
        public string Label = "";
        public bool Sabotage;

        /// <summary>Whether anyone could see the player at the moment they did it.</summary>
        public bool Witnessed;
    }

    /// <summary>
    /// Queue the presentation layer drains each frame. Keeping it a plain list
    /// rather than C# events means the headless runner can read exactly the same
    /// feedback the Unity HUD does.
    /// </summary>
    public sealed class FeedbackQueue
    {
        private readonly List<FeedbackEvent> _pending = new List<FeedbackEvent>();
        private readonly List<FeedbackEvent> _history = new List<FeedbackEvent>();

        /// <summary>
        /// Everything that has happened, kept after draining.
        ///
        /// Drain() empties the queue as the HUD consumes it, which is right for
        /// display and useless for the caught screen - by the time the player is
        /// rumbled, the events that rumbled them have long since been shown and
        /// thrown away. The history is what lets us hand them the case file.
        /// </summary>
        public IReadOnlyList<FeedbackEvent> History
        {
            get { return _history; }
        }

        public void Push(FeedbackEvent e)
        {
            _pending.Add(e);
            if (_pending.Count > 64) _pending.RemoveAt(0);

            _history.Add(e);
            if (_history.Count > 256) _history.RemoveAt(0);
        }

        /// <summary>Takes everything queued and clears it.</summary>
        public List<FeedbackEvent> Drain()
        {
            List<FeedbackEvent> copy = new List<FeedbackEvent>(_pending);
            _pending.Clear();
            return copy;
        }

        public int Count
        {
            get { return _pending.Count; }
        }
    }
}
