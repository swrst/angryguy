using System.Collections.Generic;
using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// Renders the simulation as grey-box primitives. No art, no prefabs, no
    /// scene file: the level you see is generated from the same data the AI
    /// reasons about, so the two can never drift apart.
    ///
    /// Swapping in real models later means replacing CreatePrimitive calls with
    /// Instantiate(prefab). The simulation does not know or care.
    /// </summary>
    public sealed class LevelView
    {
        private readonly Dictionary<string, Transform> _objectViews = new Dictionary<string, Transform>();
        private readonly Dictionary<string, NpcView> _npcViews = new Dictionary<string, NpcView>();

        private Transform _root;
        private Materials _materials;

        public IReadOnlyDictionary<string, NpcView> NpcViews
        {
            get { return _npcViews; }
        }

        public void Build(Simulation sim, Transform parent)
        {
            _materials = new Materials();

            GameObject root = new GameObject("Level");
            root.transform.SetParent(parent, false);
            _root = root.transform;

            BuildLighting();
            BuildFloor(sim);
            BuildWalls(sim);
            BuildZones(sim);
            BuildObjects(sim);
            BuildNpcs(sim);
        }

        public void Teardown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _objectViews.Clear();
            _npcViews.Clear();
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private void BuildLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);

            // Unity's default scene already ships a directional light. Only add one
            // if the scene is genuinely empty, or everything ends up double-lit.
            if (Object.FindAnyObjectByType<Light>() != null) return;

            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(_root, false);
            sun.transform.rotation = Quaternion.Euler(52f, -40f, 0f);

            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.shadows = LightShadows.Soft;
        }

        private void BuildFloor(Simulation sim)
        {
            float width = sim.World.FloorMax.X - sim.World.FloorMin.X;
            float depth = sim.World.FloorMax.Z - sim.World.FloorMin.Z;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(_root, false);

            // A Unity plane is 10x10 units at scale 1.
            floor.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);
            floor.transform.position = new Vector3(
                (sim.World.FloorMin.X + sim.World.FloorMax.X) * 0.5f,
                0f,
                (sim.World.FloorMin.Z + sim.World.FloorMax.Z) * 0.5f);

            Paint(floor, _materials.Lit(new Color(0.62f, 0.60f, 0.57f)));
        }

        private void BuildWalls(Simulation sim)
        {
            for (int i = 0; i < sim.World.Walls.Count; i++)
            {
                Wall wall = sim.World.Walls[i];

                Vector3 a = ToUnity(wall.A);
                Vector3 b = ToUnity(wall.B);
                Vector3 mid = (a + b) * 0.5f;
                float length = Vector3.Distance(a, b);

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Wall" + i;
                cube.transform.SetParent(_root, false);
                cube.transform.position = mid + Vector3.up * (wall.Height * 0.5f);
                cube.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
                cube.transform.localScale = new Vector3(0.25f, wall.Height, length);

                Paint(cube, _materials.Lit(new Color(0.48f, 0.47f, 0.5f)));
            }

            BuildPerimeter(sim);
        }

        private void BuildPerimeter(Simulation sim)
        {
            float minX = sim.World.FloorMin.X;
            float maxX = sim.World.FloorMax.X;
            float minZ = sim.World.FloorMin.Z;
            float maxZ = sim.World.FloorMax.Z;

            AddPerimeterWall(new Vector3((minX + maxX) * 0.5f, 1.25f, minZ), new Vector3(maxX - minX, 2.5f, 0.25f));
            AddPerimeterWall(new Vector3((minX + maxX) * 0.5f, 1.25f, maxZ), new Vector3(maxX - minX, 2.5f, 0.25f));
            AddPerimeterWall(new Vector3(minX, 1.25f, (minZ + maxZ) * 0.5f), new Vector3(0.25f, 2.5f, maxZ - minZ));
            AddPerimeterWall(new Vector3(maxX, 1.25f, (minZ + maxZ) * 0.5f), new Vector3(0.25f, 2.5f, maxZ - minZ));
        }

        private void AddPerimeterWall(Vector3 position, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Perimeter";
            cube.transform.SetParent(_root, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            Paint(cube, _materials.Lit(new Color(0.35f, 0.34f, 0.36f)));
        }

        private void BuildZones(Simulation sim)
        {
            Zone exit = sim.World.GetZone(sim.ExitZoneId);
            if (exit == null) return;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "ExitZone";
            marker.transform.SetParent(_root, false);
            marker.transform.position = ToUnity(exit.Center) + Vector3.up * 0.02f;
            marker.transform.localScale = new Vector3(exit.Radius * 2f, 0.02f, exit.Radius * 2f);
            Object.Destroy(marker.GetComponent<Collider>());
            Paint(marker, _materials.Unlit(new Color(0.25f, 0.85f, 0.4f)));
        }

        private void BuildObjects(Simulation sim)
        {
            for (int i = 0; i < sim.World.Objects.Count; i++)
            {
                CreateObjectView(sim.World.Objects[i]);
            }
        }

        private Transform CreateObjectView(SmartObject obj)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Obj_" + obj.Id;
            cube.transform.SetParent(_root, false);
            cube.transform.localScale = new Vector3(obj.Size.X, Mathf.Max(0.05f, obj.Size.Y), obj.Size.Z);
            cube.transform.position = ToUnity(obj.Position) + Vector3.up * (obj.Size.Y * 0.5f);

            // Small props must not shove the player around.
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null && obj.Size.Y < 0.6f) Object.Destroy(collider);

            Paint(cube, _materials.Lit(ObjectColour(obj)));

            _objectViews[obj.Id] = cube.transform;
            return cube.transform;
        }

        private void BuildNpcs(Simulation sim)
        {
            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Npc_" + npc.Id;
                body.transform.SetParent(_root, false);
                body.transform.localScale = new Vector3(0.75f, 0.85f, 0.75f);
                Object.Destroy(body.GetComponent<Collider>());

                // A nose, so you can tell at a glance which way they are facing.
                GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nose.name = "Facing";
                nose.transform.SetParent(body.transform, false);
                nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.45f);
                nose.transform.localPosition = new Vector3(0f, 0.25f, 0.55f);
                Object.Destroy(nose.GetComponent<Collider>());
                Paint(nose, _materials.Lit(new Color(0.15f, 0.15f, 0.18f)));

                // Target gets a hat so the objective is unmistakable.
                if (npc.IsTarget)
                {
                    GameObject hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    hat.name = "Hat";
                    hat.transform.SetParent(body.transform, false);
                    hat.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);
                    hat.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                    Object.Destroy(hat.GetComponent<Collider>());
                    Paint(hat, _materials.Lit(Color.white));
                }

                NpcView view = new NpcView
                {
                    Body = body.transform,
                    Renderer = body.GetComponent<Renderer>(),
                    Material = _materials.Lit(Color.green),
                    Cone = BuildVisionCone(body.transform)
                };
                view.Renderer.sharedMaterial = view.Material;

                _npcViews[npc.Id] = view;
            }
        }

        /// <summary>
        /// Draws each NPC's field of view on the floor. In a stealth game the
        /// player cannot plan around perception they cannot see, so this is a
        /// gameplay feature during the prototype, not a debug gizmo.
        /// </summary>
        private LineRenderer BuildVisionCone(Transform parent)
        {
            GameObject coneObject = new GameObject("VisionCone");
            coneObject.transform.SetParent(parent, false);

            LineRenderer line = coneObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = 0.06f;
            line.positionCount = ConeSegments + 2;
            line.sharedMaterial = _materials.Unlit(Color.white);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private const int ConeSegments = 14;

        // ------------------------------------------------------------------
        // Per-frame sync
        // ------------------------------------------------------------------

        public void Sync(Simulation sim)
        {
            for (int i = 0; i < sim.World.Objects.Count; i++)
            {
                SmartObject obj = sim.World.Objects[i];

                Transform view;
                if (!_objectViews.TryGetValue(obj.Id, out view))
                {
                    // Objects can appear mid-level (an oil slick, for instance).
                    view = CreateObjectView(obj);
                }

                bool visible = !obj.Concealed && obj.HeldBy.Length == 0;
                if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
                if (!visible) continue;

                view.position = ToUnity(obj.Position) + Vector3.up * (obj.Size.Y * 0.5f);

                Renderer renderer = view.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = _materials.Lit(ObjectColour(obj));
            }

            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];

                NpcView view;
                if (!_npcViews.TryGetValue(npc.Id, out view)) continue;

                view.Body.position = ToUnity(npc.Position) + Vector3.up * 0.85f;

                Vector3 facing = ToUnity(npc.Facing);
                if (facing.sqrMagnitude > 0.001f)
                {
                    view.Body.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                }

                view.Renderer.sharedMaterial = _materials.Lit(AngerColour(npc.Anger));
                UpdateCone(view.Cone, npc, sim);
            }
        }

        private void UpdateCone(LineRenderer line, Npc npc, Simulation sim)
        {
            float range = npc.Perception.SightRange;
            float half = npc.Perception.FovDegrees * 0.5f;

            Vector3 origin = ToUnity(npc.Position) + Vector3.up * 0.05f;
            Vector3 forward = ToUnity(npc.Facing);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward = forward.normalized;

            line.SetPosition(0, origin);
            for (int i = 0; i <= ConeSegments; i++)
            {
                float t = i / (float)ConeSegments;
                float angle = Mathf.Lerp(-half, half, t);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
                line.SetPosition(i + 1, origin + direction * range);
            }

            // Green = has not noticed you. Red = is fairly sure it was you.
            float suspicion = npc.SuspicionOf(PlayerAvatar.PlayerId);
            Color colour = Color.Lerp(new Color(0.3f, 0.9f, 0.45f), new Color(1f, 0.25f, 0.2f), suspicion);
            colour.a = 0.55f;
            line.startColor = colour;
            line.endColor = new Color(colour.r, colour.g, colour.b, 0.12f);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private Color ObjectColour(SmartObject obj)
        {
            if (obj.HasTag(Tags.Hazard)) return new Color(0.85f, 0.75f, 0.15f);
            if (obj.HasTag(Tags.Mess)) return new Color(0.45f, 0.4f, 0.2f);
            if (obj.IsBroken) return new Color(0.85f, 0.35f, 0.2f);
            if (obj.OwnerId.Length > 0) return new Color(0.55f, 0.6f, 0.72f);
            return new Color(0.72f, 0.72f, 0.7f);
        }

        private static Color AngerColour(float anger)
        {
            return anger < 0.5f
                ? Color.Lerp(new Color(0.45f, 0.75f, 0.5f), new Color(0.95f, 0.85f, 0.3f), anger / 0.5f)
                : Color.Lerp(new Color(0.95f, 0.85f, 0.3f), new Color(0.9f, 0.18f, 0.15f), (anger - 0.5f) / 0.5f);
        }

        private void Paint(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        public static Vector3 ToUnity(Vec3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Vec3 ToSim(Vector3 v)
        {
            return new Vec3(v.x, v.y, v.z);
        }
    }

    public sealed class NpcView
    {
        public Transform Body;
        public Renderer Renderer;
        public Material Material;
        public LineRenderer Cone;
    }

    /// <summary>
    /// Shared materials, cached by colour.
    ///
    /// Resolves shaders by name at runtime so the same code renders correctly
    /// whether the project uses the Built-in pipeline or URP. Hard-coding
    /// "Standard" is the usual reason a generated scene comes out magenta.
    /// </summary>
    public sealed class Materials
    {
        private readonly Dictionary<int, Material> _lit = new Dictionary<int, Material>();
        private readonly Dictionary<int, Material> _unlit = new Dictionary<int, Material>();

        private readonly Shader _litShader;
        private readonly Shader _unlitShader;

        public Materials()
        {
            _litShader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Diffuse");

            _unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Unlit/Color")
                           ?? _litShader;
        }

        public Material Lit(Color colour)
        {
            return Get(_lit, _litShader, colour);
        }

        public Material Unlit(Color colour)
        {
            return Get(_unlit, _unlitShader, colour);
        }

        private Material Get(Dictionary<int, Material> cache, Shader shader, Color colour)
        {
            int key = QuantiseKey(colour);

            Material material;
            if (cache.TryGetValue(key, out material) && material != null) return material;

            material = new Material(shader);
            material.color = colour;

            // URP's Lit uses _BaseColor; setting .color alone can be ignored.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);

            cache[key] = material;
            return material;
        }

        private static int QuantiseKey(Color c)
        {
            int r = Mathf.RoundToInt(c.r * 32f);
            int g = Mathf.RoundToInt(c.g * 32f);
            int b = Mathf.RoundToInt(c.b * 32f);
            int a = Mathf.RoundToInt(c.a * 32f);
            return ((r * 33 + g) * 33 + b) * 33 + a;
        }
    }
}
