using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// The locked palette. Nothing in the game uses a colour that is not here.
    ///
    /// This one rule is what produces a coherent look without an art director.
    /// Untitled Goose Game shipped on flat-shaded geometry with hand-tuned
    /// colour; what was expensive there was the taste, not the technique. The
    /// discipline of a fixed list is how two programmers get most of the way to
    /// the taste without being able to draw.
    ///
    /// Sixteen swatches. Adding a seventeenth is a design decision, not a
    /// convenience, and should feel like one.
    /// </summary>
    public static class Palette
    {
        // ---- grounds and structure ----------------------------------------

        /// <summary>Walls, and the colour the whole game sits on.</summary>
        public static readonly Color Shell = Hex("D9D6C8");

        /// <summary>Institutional paint, half way up every school wall.</summary>
        public static readonly Color Dado = Hex("8C9C8E");

        public static readonly Color Floor = Hex("B9A88C");
        public static readonly Color FloorAlt = Hex("A8977C");

        /// <summary>Doors, skirting, window frames.</summary>
        public static readonly Color Trim = Hex("F2EFE4");

        // ---- furniture -----------------------------------------------------

        public static readonly Color Wood = Hex("A9764A");
        public static readonly Color WoodDark = Hex("7C5230");
        public static readonly Color Metal = Hex("9AA3A8");
        public static readonly Color Plastic = Hex("5E6B72");

        // ---- the things that matter ----------------------------------------

        /// <summary>Anger, and the red pen. Used for almost nothing else.</summary>
        public static readonly Color Signal = Hex("C0392B");

        /// <summary>Suspicion. The other half of the HUD.</summary>
        public static readonly Color Watch = Hex("D9A441");

        /// <summary>Contract complete.</summary>
        public static readonly Color Good = Hex("5C8C5A");

        // ---- people --------------------------------------------------------

        public static readonly Color Tweed = Hex("6B6250");
        public static readonly Color Shirt = Hex("E8E4D8");
        public static readonly Color SkinLight = Hex("E8BC94");
        public static readonly Color SkinDark = Hex("7A5136");

        /// <summary>
        /// A colour the player is allowed to be, distinct from every NPC so you
        /// can always pick yourself out of a room at a glance.
        /// </summary>
        public static readonly Color Player = Hex("3D5A80");

        // ---- lighting ------------------------------------------------------

        /// <summary>Late-afternoon key light. Warm, low, and a bit tired.</summary>
        public static readonly Color KeyLight = Hex("FFF1DC");

        /// <summary>Sky fill. Cool, so shadows read blue against the warm key.</summary>
        public static readonly Color Ambient = Hex("8FA0B5");

        public static Color Hex(string rgb)
        {
            int value = int.Parse(rgb, System.Globalization.NumberStyles.HexNumber);
            return new Color(
                ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f,
                (value & 0xFF) / 255f);
        }

        /// <summary>
        /// Nudge a palette colour without leaving the palette. For the small
        /// variations that stop a room of identical objects looking tiled.
        /// </summary>
        public static Color Shade(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r * (1f + amount)),
                Mathf.Clamp01(c.g * (1f + amount)),
                Mathf.Clamp01(c.b * (1f + amount)),
                c.a);
        }
    }
}
