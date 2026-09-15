using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>"I saw X over there, a while ago." The raw material of alibis and accusations.</summary>
    public struct Sighting
    {
        public string ActorId;
        public Vec3 Where;
        public float When;

        /// <summary>Was the actor doing something that looked wrong?</summary>
        public float Incriminating;
    }

    /// <summary>
    /// A remembered event. Confidence decays, and second-hand memories start
    /// weaker than first-hand ones, so a rumour that has passed through three
    /// NPCs is faint and distorted.
    /// </summary>
    public sealed class MemoryEntry
    {
        public EventKind Kind;
        public string ObjectId = "";

        /// <summary>Who this NPC *believes* did it. Not necessarily the truth.</summary>
        public string BelievedActorId = "";

        public Vec3 Where;
        public float When;
        public float Confidence = 1f;
        public bool FirstHand = true;

        /// <summary>Who passed this on, when second-hand.</summary>
        public string SourceId = "";

        public string Description = "";
    }

    public sealed class NpcMemory
    {
        public const int MaxSightings = 48;
        public const int MaxEntries = 32;

        private readonly List<Sighting> _sightings = new List<Sighting>();
        private readonly List<MemoryEntry> _entries = new List<MemoryEntry>();

        public IReadOnlyList<Sighting> Sightings
        {
            get { return _sightings; }
        }

        public IReadOnlyList<MemoryEntry> Entries
        {
            get { return _entries; }
        }

        public void RecordSighting(string actorId, Vec3 where, float when, float incriminating)
        {
            // Collapse repeated sightings of the same actor in the same place so a
            // stationary NPC does not flood the buffer and push out real evidence.
            for (int i = _sightings.Count - 1; i >= 0; i--)
            {
                Sighting s = _sightings[i];
                if (s.ActorId != actorId) continue;
                if (when - s.When > 2.5f) break;
                if (Vec3.FlatDistance(s.Where, where) < 2f)
                {
                    s.When = when;
                    s.Where = where;
                    if (incriminating > s.Incriminating) s.Incriminating = incriminating;
                    _sightings[i] = s;
                    return;
                }
            }

            _sightings.Add(new Sighting
            {
                ActorId = actorId,
                Where = where,
                When = when,
                Incriminating = incriminating
            });

            if (_sightings.Count > MaxSightings) _sightings.RemoveAt(0);
        }

        public void Remember(MemoryEntry entry)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries) _entries.RemoveAt(0);
        }

        public bool KnowsAbout(EventKind kind, string objectId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Kind == kind && _entries[i].ObjectId == objectId) return true;
            }
            return false;
        }

        /// <summary>
        /// Everyone this NPC saw near a place around a time. This is the
        /// opportunity signal that drives blame.
        /// </summary>
        public List<Sighting> WhoWasNear(Vec3 place, float radius, float fromTime, float toTime)
        {
            List<Sighting> result = new List<Sighting>();
            for (int i = 0; i < _sightings.Count; i++)
            {
                Sighting s = _sightings[i];
                if (s.When < fromTime || s.When > toTime) continue;
                if (Vec3.FlatDistance(s.Where, place) <= radius) result.Add(s);
            }
            return result;
        }

        /// <summary>Forgetting. High-grudge NPCs forget much more slowly.</summary>
        public void Tick(float dt, float grudge, float now)
        {
            float sightingLifetime = Mathx.Lerp(35f, 140f, grudge);
            for (int i = _sightings.Count - 1; i >= 0; i--)
            {
                if (now - _sightings[i].When > sightingLifetime) _sightings.RemoveAt(i);
            }

            float decayPerSecond = Mathx.Lerp(0.030f, 0.004f, grudge);
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                MemoryEntry e = _entries[i];
                e.Confidence -= decayPerSecond * dt;
                if (e.Confidence <= 0.05f) _entries.RemoveAt(i);
            }
        }
    }
}
