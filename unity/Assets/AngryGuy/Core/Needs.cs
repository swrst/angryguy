using System;

namespace AngryGuy.Core
{
    public enum NeedType
    {
        Hunger = 0,
        Energy = 1,
        Bladder = 2,
        Social = 3,
        Order = 4,
        Comfort = 5
    }

    /// <summary>
    /// Motivation state of an NPC. 1 = fully satisfied, 0 = desperate.
    /// Needs are what make NPCs *want* things, which is what gives the player
    /// something to take away from them.
    /// </summary>
    public sealed class Needs
    {
        public const int Count = 6;

        private readonly float[] _values = new float[Count];
        private readonly float[] _decayPerSecond = new float[Count];

        public Needs()
        {
            for (int i = 0; i < Count; i++)
            {
                _values[i] = 0.8f;
                _decayPerSecond[i] = 0.01f;
            }

            // Tuned so that over a ~7 minute service an NPC cycles through several
            // wants and can actually keep up with them. If needs decay faster than
            // NPCs can satisfy them, everyone starves, everyone is permanently
            // furious, and the player has nothing left to do.
            _decayPerSecond[(int)NeedType.Hunger] = 0.0045f;
            _decayPerSecond[(int)NeedType.Energy] = 0.0018f;
            _decayPerSecond[(int)NeedType.Bladder] = 0.0035f;
            _decayPerSecond[(int)NeedType.Social] = 0.0028f;
            _decayPerSecond[(int)NeedType.Order] = 0.0016f;
            _decayPerSecond[(int)NeedType.Comfort] = 0.0030f;
        }

        public float Get(NeedType need)
        {
            return _values[(int)need];
        }

        public void Set(NeedType need, float value)
        {
            _values[(int)need] = Mathx.Clamp01(value);
        }

        public void Add(NeedType need, float delta)
        {
            Set(need, _values[(int)need] + delta);
        }

        public void SetDecay(NeedType need, float perSecond)
        {
            _decayPerSecond[(int)need] = perSecond;
        }

        public float GetDecay(NeedType need)
        {
            return _decayPerSecond[(int)need];
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < Count; i++)
            {
                _values[i] = Mathx.Clamp01(_values[i] - _decayPerSecond[i] * dt);
            }
        }

        /// <summary>How badly this need wants attention right now (0..1, non-linear).</summary>
        public float Urgency(NeedType need)
        {
            return Mathx.Urgency(_values[(int)need]);
        }

        /// <summary>The need currently screaming loudest. Used for debug readouts.</summary>
        public NeedType MostUrgent()
        {
            NeedType worst = NeedType.Hunger;
            float best = -1f;
            for (int i = 0; i < Count; i++)
            {
                float u = Mathx.Urgency(_values[i]);
                if (u > best)
                {
                    best = u;
                    worst = (NeedType)i;
                }
            }
            return worst;
        }

        /// <summary>A need pinned at zero is actively painful and feeds the anger model.</summary>
        public bool IsStarved(NeedType need)
        {
            return _values[(int)need] <= 0.05f;
        }

        public override string ToString()
        {
            return string.Format(
                "H{0:0.00} E{1:0.00} B{2:0.00} S{3:0.00} O{4:0.00} C{5:0.00}",
                _values[0], _values[1], _values[2], _values[3], _values[4], _values[5]);
        }
    }
}
