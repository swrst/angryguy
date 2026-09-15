using System;
using System.Collections.Generic;

namespace AngryGuy.Core
{
    public enum EventKind
    {
        /// <summary>A sound. Heard, not seen. Useful as a lure.</summary>
        Noise,

        /// <summary>An object left its home position.</summary>
        ObjectMoved,

        /// <summary>An object was picked up and carried off.</summary>
        ObjectTaken,

        /// <summary>An object stopped working.</summary>
        ObjectBroken,

        /// <summary>Contents tampered with: salt swapped for sugar, soup over-salted.</summary>
        ObjectTampered,

        /// <summary>Something on the floor that shouldn't be there.</summary>
        SpillCreated,

        /// <summary>An NPC hit a spill and went down. Loud and funny.</summary>
        Slipped,

        /// <summary>Cooking went wrong on its own schedule. Delayed payoff.</summary>
        FoodRuined,

        /// <summary>An NPC walked to something and it wasn't usable. The core frustration beat.</summary>
        PlanFailed,

        /// <summary>An NPC discovered evidence of an anomaly after the fact.</summary>
        Discovery,

        /// <summary>An NPC accused someone to their face.</summary>
        Accusation,

        /// <summary>Two NPCs arguing. Witnessable, escalates both.</summary>
        Argument,

        /// <summary>Anger crossed the boiling point. Very loud.</summary>
        Outburst,

        /// <summary>An NPC tidied something away, possibly destroying the player's setup.</summary>
        Cleanup,

        /// <summary>Bookkeeping only: objective satisfied / level over.</summary>
        Objective
    }

    /// <summary>
    /// Something that happened, somewhere, at some time. Events are the only
    /// currency between systems: perception turns them into memories, memories
    /// turn into blame, blame turns into anger.
    /// </summary>
    public sealed class WorldEvent
    {
        public int Id;
        public EventKind Kind;
        public float Time;
        public Vec3 Position;

        /// <summary>Ground truth of who caused it. NPCs do not get to read this.</summary>
        public string TrueActorId = "";

        /// <summary>Object the event happened to, if any.</summary>
        public string ObjectId = "";

        /// <summary>Whose interests were harmed, if anyone.</summary>
        public string VictimId = "";

        /// <summary>0 = silent, 1 = audible across the whole level.</summary>
        public float Loudness;

        /// <summary>
        /// True if the aftermath is still visible later (a broken stove, a spill).
        /// Evidence can be discovered long after the act, which is what lets the
        /// player be somewhere else when it lands.
        /// </summary>
        public bool LeavesEvidence;

        /// <summary>How much this matters to the victim. Scales anger.</summary>
        public float Severity = 0.5f;

        public string Description = "";

        public override string ToString()
        {
            return string.Format("[{0:0.0}s] {1}: {2}", Time, Kind, Description);
        }
    }

    /// <summary>
    /// An effect scheduled to fire later. This is the mechanical backbone of the
    /// whole stealth design: the player tampers now, the consequence lands in 40
    /// seconds, and by then the player is visibly somewhere else.
    /// </summary>
    public sealed class PendingEffect
    {
        public float FireAtTime;
        public string Label = "";
        public Action<Simulation> Apply;
        public bool Cancelled;
    }

    public sealed class EventBus
    {
        private readonly List<WorldEvent> _log = new List<WorldEvent>();
        private int _nextId = 1;

        public event Action<WorldEvent> OnEvent;

        public IReadOnlyList<WorldEvent> Log
        {
            get { return _log; }
        }

        public WorldEvent Publish(WorldEvent e)
        {
            e.Id = _nextId++;
            _log.Add(e);
            if (OnEvent != null) OnEvent(e);
            return e;
        }

        /// <summary>Events still physically present as evidence, for late discovery.</summary>
        public List<WorldEvent> EvidenceNear(Vec3 position, float radius, float notOlderThan, float now)
        {
            List<WorldEvent> found = new List<WorldEvent>();
            for (int i = _log.Count - 1; i >= 0; i--)
            {
                WorldEvent e = _log[i];
                if (now - e.Time > notOlderThan) break;
                if (!e.LeavesEvidence) continue;
                if (Vec3.FlatDistance(e.Position, position) <= radius) found.Add(e);
            }
            return found;
        }

        public List<WorldEvent> Recent(int count)
        {
            int start = Math.Max(0, _log.Count - count);
            return _log.GetRange(start, _log.Count - start);
        }
    }
}
