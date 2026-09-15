using System;

namespace AngryGuy.Core
{
    /// <summary>
    /// What an NPC can sense. Derived from the Observance trait so "sharp-eyed"
    /// is a real mechanical difference rather than flavour text.
    /// </summary>
    public sealed class PerceptionModel
    {
        public float SightRange = 9f;
        public float FovDegrees = 110f;
        public float HearingRange = 8f;

        /// <summary>Chance per check to spot out-of-place evidence nearby.</summary>
        public float NoticeChance = 0.4f;

        public static PerceptionModel FromPersonality(Personality p)
        {
            return new PerceptionModel
            {
                SightRange = Mathx.Lerp(6f, 14f, p.Observance),
                FovDegrees = Mathx.Lerp(85f, 140f, p.Observance),
                HearingRange = Mathx.Lerp(6f, 12f, p.Observance),
                NoticeChance = Mathx.Lerp(0.15f, 0.85f, p.Observance)
            };
        }

        public bool CanSee(Vec3 eye, Vec3 facing, Vec3 target, World world)
        {
            float distance = Vec3.FlatDistance(eye, target);
            if (distance > SightRange) return false;

            // Anything basically on top of them is seen regardless of facing.
            if (distance > 0.8f)
            {
                Vec3 toTarget = (target - eye).Normalized;
                Vec3 flatFacing = new Vec3(facing.X, 0f, facing.Z).Normalized;
                float dot = Vec3.Dot(flatFacing, toTarget);
                float cosHalfFov = (float)Math.Cos(FovDegrees * 0.5f * Math.PI / 180.0);
                if (dot < cosHalfFov) return false;
            }

            return world.HasLineOfSight(eye, target);
        }

        /// <summary>
        /// Sound ignores facing and passes through walls at reduced range.
        /// Loudness 1 is audible everywhere, which is what makes an Outburst
        /// a level-wide event.
        /// </summary>
        public bool CanHear(Vec3 ear, Vec3 source, float loudness, World world)
        {
            if (loudness <= 0f) return false;
            float effectiveRange = HearingRange * (0.4f + loudness * 2.2f);
            float distance = Vec3.FlatDistance(ear, source);
            if (distance > effectiveRange) return false;
            if (!world.HasLineOfSight(ear, source)) return distance <= effectiveRange * 0.55f;
            return true;
        }
    }
}
