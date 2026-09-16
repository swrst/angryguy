using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// One thing the player has done to the world that has not paid off yet.
    ///
    /// The prototype's worst legibility problem was that a player could set nine
    /// traps across a level and have no way to know which were still live. Half
    /// of them had already been tidied away, walked past, or quietly reversed,
    /// and the player was still waiting on them. A sandbox about setting things
    /// up has to tell you what you have set up.
    ///
    /// Armed-ness is worked out generically, by remembering exactly which state
    /// keys the sabotage changed and checking whether they still hold those
    /// values. No per-item bookkeeping, so a new sabotage in the catalogue is
    /// tracked automatically.
    /// </summary>
    public sealed class Trap
    {
        public string ObjectId = "";
        public string ObjectName = "";
        public string Label = "";
        public float SetAt;

        /// <summary>State the sabotage left behind: key -> the value it set.</summary>
        public readonly Dictionary<string, float> Signature = new Dictionary<string, float>();

        /// <summary>Set when the sabotage was "this thing is no longer here".</summary>
        public bool MovedIt;
        public Vec3 MovedTo;

        /// <summary>Who has a habit of using this object, for "waiting for X".</summary>
        public string WaitingForName = "";

        /// <summary>True once someone has been caught out by it.</summary>
        public bool Sprung;

        /// <summary>True once somebody undid it.</summary>
        public bool Defused;

        public string StatusWord
        {
            get
            {
                if (Sprung) return "went off";
                if (Defused) return "undone";
                return "armed";
            }
        }
    }

    /// <summary>Tracks the player's outstanding sabotage.</summary>
    public sealed class TrapBoard
    {
        private readonly List<Trap> _traps = new List<Trap>();

        public IReadOnlyList<Trap> All
        {
            get { return _traps; }
        }

        public void Add(Trap trap)
        {
            // Re-doing the same sabotage on the same object replaces the entry
            // rather than stacking a second identical line.
            for (int i = 0; i < _traps.Count; i++)
            {
                if (_traps[i].ObjectId != trap.ObjectId || _traps[i].Label != trap.Label) continue;
                _traps[i] = trap;
                return;
            }
            _traps.Add(trap);
        }

        public void NoteSprung(string objectId)
        {
            for (int i = 0; i < _traps.Count; i++)
            {
                if (_traps[i].ObjectId == objectId && !_traps[i].Defused) _traps[i].Sprung = true;
            }
        }

        /// <summary>Re-check every outstanding trap against the world.</summary>
        public void Refresh(World world)
        {
            for (int i = 0; i < _traps.Count; i++)
            {
                Trap trap = _traps[i];
                if (trap.Sprung || trap.Defused) continue;

                SmartObject obj = world.GetObject(trap.ObjectId);
                if (obj == null) continue;

                bool stillSet = false;

                foreach (KeyValuePair<string, float> kv in trap.Signature)
                {
                    float now = obj.GetState(kv.Key);
                    if (System.Math.Abs(now - kv.Value) < 0.01f) { stillSet = true; break; }
                }

                if (!stillSet && trap.MovedIt)
                {
                    stillSet = Vec3.FlatDistance(obj.Position, obj.HomePosition) > 1.2f
                               || obj.HeldBy.Length > 0
                               || obj.Concealed;
                }

                if (!stillSet) trap.Defused = true;
            }
        }

        public int ArmedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _traps.Count; i++)
                {
                    if (!_traps[i].Sprung && !_traps[i].Defused) n++;
                }
                return n;
            }
        }
    }
}
