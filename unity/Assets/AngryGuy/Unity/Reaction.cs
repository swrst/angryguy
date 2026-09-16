using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// A comedy reaction, as four numbers.
    ///
    /// Animation principles, but written as curves rather than keyframes,
    /// because that turns an art problem into a programming one - which is the
    /// only reason two programmers can ship expressive characters at all.
    ///
    /// The four things a reaction needs, in order:
    ///
    ///   ANTICIPATION  wind up AWAY from the thing first. About 6-10 frames.
    ///                 Skipping this is the single most common reason a
    ///                 reaction reads as a glitch rather than a performance.
    ///   EXAGGERATION  two to three times past what a real person would do.
    ///   THE HOLD      stop dead at the peak for 0.3-0.5s. Continuous motion
    ///                 has no punchline; the held pose IS the punchline.
    ///   SETTLE        overshoot back, wobble, resolve.
    ///
    /// Everything reads a 0..1 phase and returns a multiplier, so the rig can
    /// apply them to any joint without knowing what the reaction is.
    /// </summary>
    public struct Reaction
    {
        public float Anticipate;
        public float Strike;
        public float Hold;
        public float Settle;

        public float Total
        {
            get { return Anticipate + Strike + Hold + Settle; }
        }

        /// <summary>A double-take: sharp, held, and slow to recover.</summary>
        public static Reaction Startle
        {
            get { return new Reaction { Anticipate = 0.12f, Strike = 0.09f, Hold = 0.42f, Settle = 0.55f }; }
        }

        /// <summary>Fury. Barely winds up, all strike, holds a long time.</summary>
        public static Reaction Rage
        {
            get { return new Reaction { Anticipate = 0.07f, Strike = 0.06f, Hold = 0.68f, Settle = 0.9f }; }
        }

        /// <summary>Dawning realisation. Slow in, long hold, no recovery to speak of.</summary>
        public static Reaction Dismay
        {
            get { return new Reaction { Anticipate = 0.3f, Strike = 0.22f, Hold = 0.55f, Settle = 0.7f }; }
        }

        /// <summary>A small "hm?" - a notice, not a reaction.</summary>
        public static Reaction Glance
        {
            get { return new Reaction { Anticipate = 0.05f, Strike = 0.12f, Hold = 0.2f, Settle = 0.35f }; }
        }

        /// <summary>
        /// Where the pose is at time t. Negative during anticipation (wound the
        /// wrong way), overshoots past 1 on the strike, sits at 1 through the
        /// hold, then wobbles home.
        /// </summary>
        public float Evaluate(float t)
        {
            if (t <= 0f) return 0f;

            if (t < Anticipate)
            {
                // Wind up away from the target. This is the whole trick.
                float k = t / Anticipate;
                return -0.28f * Mathf.Sin(k * Mathf.PI);
            }

            t -= Anticipate;

            if (t < Strike)
            {
                // Fast, and overshooting well past the resting pose.
                float k = t / Strike;
                return Mathf.Lerp(-0.28f, 1.22f, k * k);
            }

            t -= Strike;

            if (t < Hold)
            {
                // Dead still. Resist the urge to put a breath on this.
                return 1f;
            }

            t -= Hold;

            if (t < Settle)
            {
                // Damped wobble back to neutral.
                float k = t / Settle;
                return Mathf.Cos(k * Mathf.PI * 2.2f) * (1f - k) * 0.35f;
            }

            return 0f;
        }
    }

    /// <summary>
    /// Plays one reaction at a time on a character, and holds the 12fps clock.
    /// </summary>
    public sealed class ReactionPlayer
    {
        private Reaction _current;
        private float _time = -1f;

        /// <summary>Strength multiplier, so an angrier NPC reacts harder.</summary>
        private float _force = 1f;

        public bool Playing
        {
            get { return _time >= 0f && _time < _current.Total; }
        }

        public void Play(Reaction reaction, float force = 1f)
        {
            _current = reaction;
            _force = Mathf.Clamp(force, 0.4f, 1.8f);
            _time = 0f;
        }

        public float Tick(float dt)
        {
            if (_time < 0f) return 0f;

            _time += dt;
            if (_time >= _current.Total)
            {
                _time = -1f;
                return 0f;
            }

            return _current.Evaluate(_time) * _force;
        }
    }

    /// <summary>
    /// Step-keying. Quantises time to 12 frames a second.
    ///
    /// One function, applied to every animation clock in the game, and the
    /// result reads as stop-motion rather than as cheap interpolation. It suits
    /// the register we picked, it costs nothing, and it hides a great many
    /// small crimes in procedural animation - a pose that pops between steps
    /// looks deliberate, where the same pose sliding smoothly looks broken.
    /// </summary>
    public static class StepKey
    {
        public const float Fps = 12f;

        public static float Quantise(float t)
        {
            return Mathf.Floor(t * Fps) / Fps;
        }

        /// <summary>Quantised sine, for walk cycles that read as animated rather than driven.</summary>
        public static float Sin(float phase)
        {
            return Mathf.Sin(Quantise(phase * (1f / (Mathf.PI * 2f))) * Mathf.PI * 2f);
        }
    }
}
