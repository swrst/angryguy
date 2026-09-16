using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>A sight-blocking, walk-blocking segment on the XZ plane.</summary>
    public struct Wall
    {
        public Vec3 A;
        public Vec3 B;
        public float Height;

        public Wall(float x1, float z1, float x2, float z2, float height = 2.5f)
        {
            A = new Vec3(x1, 0f, z1);
            B = new Vec3(x2, 0f, z2);
            Height = height;
        }
    }

    /// <summary>A named region, used for "the kitchen", "the dining room", "the exit".</summary>
    public sealed class Zone
    {
        public string Id = "";
        public string Name = "";
        public Vec3 Center;
        public float Radius = 3f;

        /// <summary>
        /// Somewhere the public has no business being. Standing here in the wrong
        /// clothes is quietly incriminating all by itself, which is what makes a
        /// stolen uniform worth having.
        /// </summary>
        public bool StaffOnly;

        public bool Contains(Vec3 p)
        {
            return Vec3.FlatDistance(p, Center) <= Radius;
        }
    }

    /// <summary>
    /// A door: a wall segment that only blocks while it is shut. Doors are what
    /// turn a fixed floorplan into something the player can reshape.
    /// </summary>
    public sealed class DoorBlocker
    {
        public Wall Segment;
        public SmartObject Object;

        public bool IsClosed
        {
            get { return Object != null && Object.GetState("open") <= 0f; }
        }
    }

    public sealed class World
    {
        public readonly List<SmartObject> Objects = new List<SmartObject>();
        public readonly List<DoorBlocker> Doors = new List<DoorBlocker>();
        public readonly List<Npc> Npcs = new List<Npc>();
        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<Zone> Zones = new List<Zone>();

        /// <summary>
        /// Doorways and gaps worth steering through. Movement is straight-line
        /// with wall sliding, which has no way around a corner: an NPC whose
        /// target sits behind a wall slides until it stops making progress and
        /// then stands there for the rest of the level. Routing via a portal
        /// first is the cheapest thing that makes the geometry navigable.
        /// </summary>
        public readonly List<Vec3> Portals = new List<Vec3>();

        private readonly Dictionary<string, SmartObject> _objectsById =
            new Dictionary<string, SmartObject>();

        private readonly Dictionary<string, Npc> _npcsById = new Dictionary<string, Npc>();

        /// <summary>
        /// 1 = lit, lower = gloom. Scales how far anyone can see, so killing the
        /// lights changes the whole level at once instead of one object.
        /// </summary>
        public float LightLevel = 1f;

        public Vec3 FloorMin = new Vec3(-10f, 0f, -10f);
        public Vec3 FloorMax = new Vec3(10f, 0f, 10f);

        public void Add(SmartObject obj)
        {
            Objects.Add(obj);
            _objectsById[obj.Id] = obj;
        }

        public void Add(Npc npc)
        {
            Npcs.Add(npc);
            _npcsById[npc.Id] = npc;
        }

        public SmartObject GetObject(string id)
        {
            SmartObject o;
            return _objectsById.TryGetValue(id, out o) ? o : null;
        }

        public Npc GetNpc(string id)
        {
            Npc n;
            return _npcsById.TryGetValue(id, out n) ? n : null;
        }

        public Zone GetZone(string id)
        {
            for (int i = 0; i < Zones.Count; i++)
            {
                if (Zones[i].Id == id) return Zones[i];
            }
            return null;
        }

        public List<SmartObject> ObjectsNear(Vec3 position, float radius)
        {
            List<SmartObject> result = new List<SmartObject>();
            for (int i = 0; i < Objects.Count; i++)
            {
                SmartObject o = Objects[i];
                if (o.HeldBy.Length > 0) continue;
                if (Vec3.FlatDistance(o.Position, position) <= radius) result.Add(o);
            }
            return result;
        }

        /// <summary>Straight-line visibility on the XZ plane, blocked by walls and shut doors.</summary>
        public bool HasLineOfSight(Vec3 from, Vec3 to)
        {
            for (int i = 0; i < Walls.Count; i++)
            {
                if (SegmentsIntersect(from, to, Walls[i].A, Walls[i].B)) return false;
            }

            for (int i = 0; i < Doors.Count; i++)
            {
                DoorBlocker door = Doors[i];
                if (!door.IsClosed) continue;
                if (SegmentsIntersect(from, to, door.Segment.A, door.Segment.B)) return false;
            }

            return true;
        }

        /// <summary>The shut door standing between two points, if there is one.</summary>
        public SmartObject BlockingDoor(Vec3 from, Vec3 to)
        {
            for (int i = 0; i < Doors.Count; i++)
            {
                DoorBlocker door = Doors[i];
                if (!door.IsClosed) continue;
                if (SegmentsIntersect(from, to, door.Segment.A, door.Segment.B)) return door.Object;
            }
            return null;
        }

        /// <summary>
        /// A doorway to aim for when the direct line is blocked: reachable from
        /// here, and genuinely closer to the goal than standing still.
        /// </summary>
        public bool TryFindPortal(Vec3 from, Vec3 goal, out Vec3 portal)
        {
            portal = Vec3.Zero;

            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < Portals.Count; i++)
            {
                Vec3 candidate = Portals[i];

                // No good steering toward a doorway we also cannot see.
                if (!HasLineOfSight(from, candidate)) continue;

                float cost = Vec3.FlatDistance(from, candidate) + Vec3.FlatDistance(candidate, goal);
                if (cost < best)
                {
                    best = cost;
                    portal = candidate;
                    found = true;
                }
            }

            return found;
        }

        public Vec3 Clamp(Vec3 p)
        {
            return new Vec3(
                Mathx.Clamp(p.X, FloorMin.X, FloorMax.X),
                p.Y,
                Mathx.Clamp(p.Z, FloorMin.Z, FloorMax.Z));
        }

        private static float Cross2(Vec3 o, Vec3 a, Vec3 b)
        {
            return (a.X - o.X) * (b.Z - o.Z) - (a.Z - o.Z) * (b.X - o.X);
        }

        private static bool SegmentsIntersect(Vec3 p1, Vec3 p2, Vec3 p3, Vec3 p4)
        {
            float d1 = Cross2(p3, p4, p1);
            float d2 = Cross2(p3, p4, p2);
            float d3 = Cross2(p1, p2, p3);
            float d4 = Cross2(p1, p2, p4);

            if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
                ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
            {
                return true;
            }
            return false;
        }
    }
}
