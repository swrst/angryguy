using System;
using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// Minimal 3D vector. Deliberately NOT UnityEngine.Vector3 so the whole
    /// simulation compiles and runs outside Unity (tests, headless sim, CI).
    /// The Unity layer converts at the boundary.
    /// </summary>
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 Zero
        {
            get { return new Vec3(0f, 0f, 0f); }
        }

        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vec3 operator *(Vec3 a, float s)
        {
            return new Vec3(a.X * s, a.Y * s, a.Z * s);
        }

        public float SqrMagnitude
        {
            get { return X * X + Y * Y + Z * Z; }
        }

        public float Magnitude
        {
            get { return (float)Math.Sqrt(SqrMagnitude); }
        }

        public Vec3 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 0.00001f ? new Vec3(X / m, Y / m, Z / m) : Zero;
            }
        }

        /// <summary>Distance ignoring height. The sim is effectively 2D on the XZ plane.</summary>
        public static float FlatDistance(Vec3 a, Vec3 b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static float Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vec3 MoveTowards(Vec3 from, Vec3 to, float maxStep)
        {
            Vec3 delta = to - from;
            float dist = delta.Magnitude;
            if (dist <= maxStep || dist < 0.00001f) return to;
            return from + delta * (maxStep / dist);
        }

        public bool Equals(Vec3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is Vec3 && Equals((Vec3)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("({0:0.0}, {1:0.0}, {2:0.0})", X, Y, Z);
        }
    }

    /// <summary>
    /// Deterministic RNG. Seeded runs reproduce exactly, which is what makes
    /// "that hilarious thing that happened" reproducible as a bug report or a test.
    /// </summary>
    public sealed class Rng
    {
        private uint _state;

        public Rng(int seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            // xorshift32
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>Uniform in [0,1).</summary>
        public float NextFloat()
        {
            return (NextUInt() & 0xFFFFFF) / 16777216f;
        }

        public float Range(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) return 0;
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public T Pick<T>(IList<T> items)
        {
            if (items == null || items.Count == 0) return default(T);
            return items[NextInt(items.Count)];
        }
    }

    public static class Mathx
    {
        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        public static float Clamp(float v, float min, float max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        /// <summary>
        /// Urgency curve for needs. A need at 0.9 barely motivates; a need at 0.1
        /// dominates everything. Quadratic falloff is the classic Sims-style shape.
        /// </summary>
        public static float Urgency(float needLevel)
        {
            float deficit = 1f - Clamp01(needLevel);
            return deficit * deficit;
        }

        /// <summary>Distance penalty: 1.0 at the NPC's feet, falling off smoothly.</summary>
        public static float DistanceFalloff(float distance, float halfLife)
        {
            if (halfLife <= 0f) return 1f;
            return halfLife / (halfLife + Math.Max(0f, distance));
        }
    }
}
