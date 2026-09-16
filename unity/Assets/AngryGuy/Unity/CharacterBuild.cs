using UnityEngine;

namespace AngryGuy.UnityLayer
{
    public enum HatKind
    {
        None,
        Toque,
        Cap,
        Bun
    }

    /// <summary>
    /// The silhouette half of a character: everything about the body that does
    /// not change at runtime.
    ///
    /// This is deliberately separate from the rig. The rig knows how to animate
    /// a person; this knows what that person looks like. Adding a new character
    /// to a level means adding an entry to <see cref="CharacterLooks"/> and
    /// nothing else - no prefab, no import, no scene edit.
    ///
    /// Keep the shapes exaggerated. Six people in one kitchen have to be
    /// identifiable from behind, at distance, in peripheral vision, because
    /// "who is in this room and which way are they facing" is the entire skill
    /// the game asks of the player.
    /// </summary>
    public struct CharacterBuild
    {
        public float Height;
        public float Girth;
        public float HeadScale;

        public HatKind Hat;
        public Color HatColour;

        public bool HasHair;
        public Color HairColour;

        public bool HasGlasses;

        public bool WearsApron;
        public Color ApronColour;

        public static CharacterBuild Default
        {
            get
            {
                CharacterBuild b = new CharacterBuild();
                b.Height = 1f;
                b.Girth = 1f;
                b.HeadScale = 1f;
                b.Hat = HatKind.None;
                b.HatColour = new Color(0.2f, 0.22f, 0.26f);
                b.HairColour = new Color(0.18f, 0.13f, 0.1f);
                b.ApronColour = new Color(0.9f, 0.9f, 0.88f);
                return b;
            }
        }
    }

    /// <summary>
    /// The cast's appearances, keyed by NPC id. One place to look when a level
    /// gains a character.
    /// </summary>
    public static class CharacterLooks
    {
        public static CharacterBuild For(string npcId)
        {
            CharacterBuild b = CharacterBuild.Default;

            switch (npcId)
            {
                // Gordon: broad, heavy, tall hat. The biggest thing in the room,
                // which is the point - you should always know where he is.
                case "chef":
                    b.Height = 1.06f;
                    b.Girth = 1.22f;
                    b.HeadScale = 1.05f;
                    b.Hat = HatKind.Toque;
                    b.WearsApron = true;
                    b.ApronColour = new Color(0.94f, 0.93f, 0.9f);
                    return b;

                // Bruno: the same uniform, one size down and no swagger. Reading
                // him as "the other chef" at a glance is intentional - so is the
                // moment you realise which one just walked in.
                case "souschef":
                    b.Height = 0.97f;
                    b.Girth = 1.05f;
                    b.HeadScale = 0.96f;
                    b.Hat = HatKind.Toque;
                    b.WearsApron = true;
                    b.ApronColour = new Color(0.86f, 0.87f, 0.9f);
                    return b;

                // Marie: tall, slight, hair up. Fast-moving and hard to miss.
                case "waiter":
                    b.Height = 1.03f;
                    b.Girth = 0.86f;
                    b.HeadScale = 0.98f;
                    b.Hat = HatKind.Bun;
                    b.HairColour = new Color(0.32f, 0.2f, 0.12f);
                    b.HasHair = true;
                    b.WearsApron = true;
                    b.ApronColour = new Color(0.16f, 0.17f, 0.2f);
                    return b;

                // Terry: short, round, shapeless. Reads as harmless, which is
                // exactly why blame sticks to him.
                case "dishwasher":
                    b.Height = 0.9f;
                    b.Girth = 1.18f;
                    b.HeadScale = 1.08f;
                    b.HasHair = true;
                    b.HairColour = new Color(0.4f, 0.38f, 0.36f);
                    return b;

                // Eva: upright, narrow, glasses. The one silhouette you learn to
                // check for before you touch anything.
                case "manager":
                    b.Height = 1.04f;
                    b.Girth = 0.82f;
                    b.HeadScale = 0.94f;
                    b.HasHair = true;
                    b.HairColour = new Color(0.1f, 0.09f, 0.09f);
                    b.HasGlasses = true;
                    return b;

                // Pip: small, skinny, baseball cap, permanently in motion.
                case "porter":
                    b.Height = 0.88f;
                    b.Girth = 0.78f;
                    b.HeadScale = 1.06f;
                    b.Hat = HatKind.Cap;
                    b.HatColour = new Color(0.65f, 0.2f, 0.18f);
                    return b;

                default:
                    return b;
            }
        }
    }
}
