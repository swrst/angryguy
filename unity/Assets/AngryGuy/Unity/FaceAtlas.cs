using System.Collections.Generic;
using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// The faces an NPC can pull, as one generated texture.
    ///
    /// Brows carry most of the readable emotion on a stylised character - far
    /// more than mouths, and enormously more than eyes on their own. So the
    /// whole expression system is a 4x4 atlas of eye-and-brow pairs, swapped by
    /// moving a UV offset. Sixteen expressions, one texture, one material
    /// property set from code, and no blend shapes, no rig, and no artist.
    ///
    /// Drawn procedurally rather than authored, for the same reason the audio is
    /// synthesised: no licence to track, no asset to lose, and we can retune the
    /// angry brow angle by changing a number.
    /// </summary>
    public enum Face
    {
        Neutral = 0,
        Working = 1,
        Puzzled = 2,
        Annoyed = 3,
        Cross = 4,
        Furious = 5,
        Suspicious = 6,
        Watching = 7,
        Startled = 8,
        Afraid = 9,
        Pleased = 10,
        Smug = 11,
        Embarrassed = 12,
        Weary = 13,
        Amused = 14,
        Blink = 15
    }

    public static class FaceAtlas
    {
        public const int Columns = 4;
        public const int Rows = 4;

        private const int CellSize = 64;

        private static Texture2D _texture;

        public static Texture2D Texture
        {
            get
            {
                if (_texture == null) _texture = Generate();
                return _texture;
            }
        }

        public static Vector2 Scale
        {
            get { return new Vector2(1f / Columns, 1f / Rows); }
        }

        public static Vector2 OffsetFor(Face face)
        {
            int index = (int)face;
            int column = index % Columns;

            // Texture space runs bottom-up; the atlas is authored top-down.
            int row = Rows - 1 - (index / Columns);

            return new Vector2(column / (float)Columns, row / (float)Rows);
        }

        /// <summary>
        /// Pick a face from what the simulation already knows. Nothing here is a
        /// new system - anger, wariness and the Mind's emotions are all being
        /// tracked anyway, and this is simply the first time any of it has been
        /// visible on a character's face.
        /// </summary>
        public static Face For(Npc npc)
        {
            if (npc.Anger >= AngerModel.BoilingPoint) return Face.Furious;
            if (npc.Attention.Noticing) return Face.Startled;

            if (npc.SuspicionOf(PlayerAvatar.PlayerId) > 0.55f) return Face.Watching;
            if (npc.Mind.SuspectsSabotage) return Face.Suspicious;

            if (npc.Anger > 0.55f) return Face.Cross;
            if (npc.Anger > 0.25f) return Face.Annoyed;

            switch (npc.Mind.Dominant)
            {
                case Emotion.Afraid: return Face.Afraid;
                case Emotion.Amused: return Face.Amused;
                case Emotion.Embarrassed: return Face.Embarrassed;
                case Emotion.Proud: return Face.Smug;
                case Emotion.Bored: return Face.Weary;
                case Emotion.Suspicious: return Face.Suspicious;
            }

            if (npc.Activity == NpcActivity.Investigating) return Face.Puzzled;
            if (npc.Activity == NpcActivity.Using) return Face.Working;

            return Face.Neutral;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Each cell: two eyes and two brows on a transparent ground. The brow
        /// angle and height do nearly all the work; the eyes mostly just change
        /// size.
        /// </summary>
        private static Texture2D Generate()
        {
            int width = Columns * CellSize;
            int height = Rows * CellSize;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "FaceAtlas";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

            foreach (KeyValuePair<Face, FaceSpec> entry in Specs())
            {
                int index = (int)entry.Key;
                int cellX = (index % Columns) * CellSize;
                int cellY = (Rows - 1 - index / Columns) * CellSize;
                DrawFace(pixels, width, cellX, cellY, entry.Value);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private struct FaceSpec
        {
            /// <summary>Inner end of the brow, in cell units above centre.</summary>
            public float BrowInner;

            /// <summary>Outer end. Inner lower than outer reads angry; the reverse reads worried.</summary>
            public float BrowOuter;

            /// <summary>Eye opening, 0 = shut, 1 = wide.</summary>
            public float EyeOpen;

            /// <summary>Pupils off centre, for a sidelong look.</summary>
            public float PupilShift;
        }

        private static Dictionary<Face, FaceSpec> Specs()
        {
            return new Dictionary<Face, FaceSpec>
            {
                { Face.Neutral,     Spec(0.30f, 0.30f, 0.70f,  0.00f) },
                { Face.Working,     Spec(0.24f, 0.28f, 0.52f,  0.00f) },
                { Face.Puzzled,     Spec(0.42f, 0.22f, 0.80f,  0.18f) },
                { Face.Annoyed,     Spec(0.16f, 0.34f, 0.58f,  0.00f) },
                { Face.Cross,       Spec(0.08f, 0.40f, 0.68f,  0.00f) },
                { Face.Furious,     Spec(0.00f, 0.46f, 0.95f,  0.00f) },
                { Face.Suspicious,  Spec(0.14f, 0.32f, 0.40f,  0.22f) },
                { Face.Watching,    Spec(0.12f, 0.36f, 0.48f, -0.24f) },
                { Face.Startled,    Spec(0.46f, 0.46f, 1.00f,  0.00f) },
                { Face.Afraid,      Spec(0.48f, 0.26f, 0.92f,  0.10f) },
                { Face.Pleased,     Spec(0.32f, 0.32f, 0.44f,  0.00f) },
                { Face.Smug,        Spec(0.26f, 0.38f, 0.36f,  0.16f) },
                { Face.Embarrassed, Spec(0.40f, 0.20f, 0.30f, -0.18f) },
                { Face.Weary,       Spec(0.22f, 0.20f, 0.26f,  0.00f) },
                { Face.Amused,      Spec(0.36f, 0.34f, 0.34f,  0.00f) },
                { Face.Blink,       Spec(0.30f, 0.30f, 0.05f,  0.00f) }
            };
        }

        private static FaceSpec Spec(float inner, float outer, float open, float shift)
        {
            return new FaceSpec
            {
                BrowInner = inner, BrowOuter = outer, EyeOpen = open, PupilShift = shift
            };
        }

        private static void DrawFace(Color32[] pixels, int stride, int cellX, int cellY, FaceSpec spec)
        {
            Color32 ink = new Color32(26, 22, 20, 255);
            Color32 white = new Color32(250, 248, 242, 255);

            // Two eyes, mirrored about the cell centre.
            DrawEye(pixels, stride, cellX, cellY, 0.32f, spec, ink, white, +1f);
            DrawEye(pixels, stride, cellX, cellY, 0.68f, spec, ink, white, -1f);
        }

        private static void DrawEye(Color32[] pixels, int stride, int cellX, int cellY,
            float centreU, FaceSpec spec, Color32 ink, Color32 white, float mirror)
        {
            float eyeCx = centreU * CellSize;
            float eyeCy = 0.46f * CellSize;

            float radius = CellSize * 0.115f;
            float openness = Mathf.Clamp01(spec.EyeOpen);

            // The eye itself: an ellipse squashed by how open it is.
            for (int y = -(int)radius - 2; y <= (int)radius + 2; y++)
            {
                for (int x = -(int)radius - 2; x <= (int)radius + 2; x++)
                {
                    float nx = x / radius;
                    float ny = y / (radius * Mathf.Max(0.08f, openness));
                    if (nx * nx + ny * ny > 1f) continue;

                    bool pupil = (x - spec.PupilShift * radius * mirror) *
                                 (x - spec.PupilShift * radius * mirror) + y * y
                                 < radius * radius * 0.28f;

                    Plot(pixels, stride, cellX + (int)eyeCx + x, cellY + (int)eyeCy + y,
                        pupil || openness < 0.12f ? ink : white);
                }
            }

            // The brow, which is what the expression actually lives in.
            float innerX = eyeCx - radius * 1.5f * mirror;
            float outerX = eyeCx + radius * 1.5f * mirror;
            float innerY = eyeCy + spec.BrowInner * CellSize * 0.5f;
            float outerY = eyeCy + spec.BrowOuter * CellSize * 0.5f;

            DrawThickLine(pixels, stride, cellX, cellY,
                innerX, innerY, outerX, outerY, CellSize * 0.055f, ink);
        }

        private static void DrawThickLine(Color32[] pixels, int stride, int cellX, int cellY,
            float x0, float y0, float x1, float y1, float thickness, Color32 colour)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2f) + 1;
            int half = Mathf.CeilToInt(thickness);

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float px = Mathf.Lerp(x0, x1, t);
                float py = Mathf.Lerp(y0, y1, t);

                for (int dy = -half; dy <= half; dy++)
                {
                    for (int dx = -half; dx <= half; dx++)
                    {
                        if (dx * dx + dy * dy > thickness * thickness) continue;
                        Plot(pixels, stride, cellX + (int)px + dx, cellY + (int)py + dy, colour);
                    }
                }
            }
        }

        private static void Plot(Color32[] pixels, int stride, int x, int y, Color32 colour)
        {
            if (x < 0 || y < 0 || x >= stride) return;
            int index = y * stride + x;
            if (index < 0 || index >= pixels.Length) return;
            pixels[index] = colour;
        }
    }
}
