using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// What the player is wearing, and therefore where they look like they belong.
    ///
    /// This turns "don't be seen" into the more interesting "don't be seen
    /// somewhere you have no business being". In your own clothes the kitchen is
    /// hostile ground and every glance costs you; in chef's whites you can stand
    /// at the stove while the actual chef watches.
    ///
    /// It is not a free pass: a uniform hides your role, not your face, and the
    /// people who know the staff best will work it out given long enough.
    /// </summary>
    public sealed class Outfit
    {
        public string Id = "civilian";
        public string Name = "your own clothes";

        /// <summary>Zone ids this outfit makes you look native to.</summary>
        public readonly HashSet<string> BelongsIn = new HashSet<string>();

        /// <summary>
        /// How well it holds up to a proper look. Lower quality means observant
        /// people see through it sooner.
        /// </summary>
        public float Quality = 1f;

        public bool BelongsInZone(string zoneId)
        {
            return zoneId.Length > 0 && BelongsIn.Contains(zoneId);
        }

        public static Outfit Civilian()
        {
            Outfit outfit = new Outfit { Id = "civilian", Name = "your own clothes", Quality = 1f };
            // A customer belongs out front and nowhere else.
            outfit.BelongsIn.Add("dining");
            outfit.BelongsIn.Add("floor");
            return outfit;
        }

        public static Outfit ChefWhites()
        {
            Outfit outfit = new Outfit { Id = "whites", Name = "chef's whites", Quality = 0.75f };
            outfit.BelongsIn.Add("kitchen");
            outfit.BelongsIn.Add("washup");
            return outfit;
        }

        public static Outfit WaiterApron()
        {
            Outfit outfit = new Outfit { Id = "apron", Name = "waiter's apron", Quality = 0.8f };
            outfit.BelongsIn.Add("dining");
            outfit.BelongsIn.Add("floor");
            outfit.BelongsIn.Add("kitchen");
            return outfit;
        }

        /// <summary>
        /// Overalls get you anywhere, because nobody looks twice at a cleaner -
        /// but they are conspicuous enough that anyone who does look remembers.
        /// </summary>
        public static Outfit CleanerOveralls()
        {
            Outfit outfit = new Outfit { Id = "overalls", Name = "cleaner's overalls", Quality = 0.55f };
            outfit.BelongsIn.Add("kitchen");
            outfit.BelongsIn.Add("washup");
            outfit.BelongsIn.Add("dining");
            outfit.BelongsIn.Add("floor");
            return outfit;
        }

        public static Outfit ById(string id)
        {
            switch (id)
            {
                case "whites": return ChefWhites();
                case "apron": return WaiterApron();
                case "overalls": return CleanerOveralls();
                default: return Civilian();
            }
        }
    }
}
