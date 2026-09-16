using System.Collections.Generic;
using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// Renders the simulation. Still generated entirely from level data rather
    /// than a scene file, but no longer flat grey boxes: textured surfaces,
    /// articulated characters and animated doors, so the player can tell what
    /// they are looking at without reading the debug overlay.
    /// </summary>
    public sealed class LevelView
    {
        private readonly Dictionary<string, ObjectView> _objectViews = new Dictionary<string, ObjectView>();
        private readonly Dictionary<string, NpcView> _npcViews = new Dictionary<string, NpcView>();

        private Transform _root;
        private Materials _materials;

        public Materials Materials
        {
            get { return _materials; }
        }

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

            StageLighting.Apply(_root);
            BuildFloors(sim);
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
            RenderSettings.ambientLight = new Color(0.44f, 0.45f, 0.5f);

            if (Object.FindAnyObjectByType<Light>() != null) return;

            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(_root, false);
            sun.transform.rotation = Quaternion.Euler(52f, -40f, 0f);

            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.shadows = LightShadows.Soft;
        }

        /// <summary>
        /// Two floors, not one: tile on the kitchen side, boards on the dining
        /// side. It costs nothing and instantly tells the player which half of
        /// the building they are standing in.
        /// </summary>
        private void BuildFloors(Simulation sim)
        {
            float minX = sim.World.FloorMin.X;
            float maxX = sim.World.FloorMax.X;
            float minZ = sim.World.FloorMin.Z;
            float maxZ = sim.World.FloorMax.Z;
            float depth = maxZ - minZ;

            AddFloor("KitchenFloor",
                new Vector3((minX + 0f) * 0.5f, 0f, (minZ + maxZ) * 0.5f),
                new Vector3(-minX, 1f, depth),
                "kitchen_tile", new Color(0.86f, 0.86f, 0.84f), 3f);

            AddFloor("DiningFloor",
                new Vector3((0f + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX, 1f, depth),
                "wood_floor", new Color(0.88f, 0.84f, 0.78f), 2.5f);
        }

        private void AddFloor(string name, Vector3 centre, Vector3 size, string texture,
            Color tint, float tiling)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = name;
            floor.transform.SetParent(_root, false);
            floor.transform.localScale = new Vector3(size.x / 10f, 1f, size.z / 10f);
            floor.transform.position = centre;

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _materials.Textured(texture, tint,
                    new Vector2(size.x * tiling / 10f, size.z * tiling / 10f));
            }
        }

        private void BuildWalls(Simulation sim)
        {
            for (int i = 0; i < sim.World.Walls.Count; i++)
            {
                Wall wall = sim.World.Walls[i];
                Vector3 a = ToUnity(wall.A);
                Vector3 b = ToUnity(wall.B);
                float length = Vector3.Distance(a, b);

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Wall" + i;
                cube.transform.SetParent(_root, false);
                cube.transform.position = (a + b) * 0.5f + Vector3.up * (wall.Height * 0.5f);
                cube.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
                cube.transform.localScale = new Vector3(0.25f, wall.Height, length);

                Renderer renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _materials.Textured("wall_paint",
                        new Color(0.86f, 0.86f, 0.88f), new Vector2(length * 0.5f, wall.Height * 0.5f));
                }
            }

            BuildPerimeter(sim);
        }

        private void BuildPerimeter(Simulation sim)
        {
            float minX = sim.World.FloorMin.X;
            float maxX = sim.World.FloorMax.X;
            float minZ = sim.World.FloorMin.Z;
            float maxZ = sim.World.FloorMax.Z;

            AddPerimeterWall(new Vector3((minX + maxX) * 0.5f, 1.4f, minZ), new Vector3(maxX - minX, 2.8f, 0.3f));
            AddPerimeterWall(new Vector3((minX + maxX) * 0.5f, 1.4f, maxZ), new Vector3(maxX - minX, 2.8f, 0.3f));
            AddPerimeterWall(new Vector3(minX, 1.4f, (minZ + maxZ) * 0.5f), new Vector3(0.3f, 2.8f, maxZ - minZ));
            AddPerimeterWall(new Vector3(maxX, 1.4f, (minZ + maxZ) * 0.5f), new Vector3(0.3f, 2.8f, maxZ - minZ));
        }

        private void AddPerimeterWall(Vector3 position, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Perimeter";
            cube.transform.SetParent(_root, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _materials.Textured("wall_paint",
                    new Color(0.7f, 0.7f, 0.74f), new Vector2(scale.x + scale.z, scale.y * 0.5f));
            }
        }

        private void BuildZones(Simulation sim)
        {
            Zone exit = sim.World.GetZone(sim.ExitZoneId);
            if (exit == null) return;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "ExitZone";
            marker.transform.SetParent(_root, false);
            marker.transform.position = ToUnity(exit.Center) + Vector3.up * 0.03f;
            marker.transform.localScale = new Vector3(exit.Radius * 2f, 0.03f, exit.Radius * 2f);
            Object.Destroy(marker.GetComponent<Collider>());

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _materials.Unlit(new Color(0.25f, 0.9f, 0.45f));

            // A sign post so the exit reads as a way out rather than a green rug.
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "ExitSign";
            post.transform.SetParent(_root, false);
            post.transform.position = ToUnity(exit.Center) + Vector3.up * 2.2f;
            post.transform.localScale = new Vector3(1.1f, 0.35f, 0.1f);
            Object.Destroy(post.GetComponent<Collider>());
            Renderer signRenderer = post.GetComponent<Renderer>();
            if (signRenderer != null) signRenderer.sharedMaterial = _materials.Unlit(new Color(0.2f, 0.85f, 0.4f));
        }

        private void BuildObjects(Simulation sim)
        {
            for (int i = 0; i < sim.World.Objects.Count; i++)
            {
                CreateObjectView(sim, sim.World.Objects[i]);
            }
        }

        private ObjectView CreateObjectView(Simulation sim, SmartObject obj)
        {
            ObjectView view = new ObjectView();

            if (obj.HasTag(Tags.Door))
            {
                BuildDoorView(view, obj);
            }
            else
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Obj_" + obj.Id;
                cube.transform.SetParent(_root, false);
                cube.transform.localScale = new Vector3(obj.Size.X, Mathf.Max(0.05f, obj.Size.Y), obj.Size.Z);
                cube.transform.position = ToUnity(obj.Position) + Vector3.up * (obj.Size.Y * 0.5f);

                Collider collider = cube.GetComponent<Collider>();
                if (collider != null && obj.Size.Y < 0.6f) Object.Destroy(collider);

                view.Root = cube.transform;
                view.Body = cube.transform;
                view.Renderer = cube.GetComponent<Renderer>();
            }

            view.BaseColour = ObjectColour(obj);
            view.Texture = ObjectTexture(obj);
            ApplySurface(view, view.BaseColour);

            _objectViews[obj.Id] = view;
            return view;
        }

        private void BuildDoorView(ObjectView view, SmartObject obj)
        {
            GameObject pivot = new GameObject("Door_" + obj.Id);
            pivot.transform.SetParent(_root, false);
            pivot.transform.position = ToUnity(obj.Position) + new Vector3(0f, 0f, -obj.Size.Z * 0.5f);

            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf";
            leaf.transform.SetParent(pivot.transform, false);
            leaf.transform.localPosition = new Vector3(0f, obj.Size.Y * 0.5f, obj.Size.Z * 0.5f);
            leaf.transform.localScale = new Vector3(obj.Size.X, obj.Size.Y, obj.Size.Z);
            Object.Destroy(leaf.GetComponent<Collider>());

            view.Root = pivot.transform;
            view.Body = leaf.transform;
            view.Renderer = leaf.GetComponent<Renderer>();
            view.IsDoor = true;
        }

        private void BuildNpcs(Simulation sim)
        {
            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];

                CharacterRig rig = new CharacterRig();
                Color uniform = UniformFor(npc);
                Color skin = SkinFor(npc);
                rig.Build(_root, "Npc_" + npc.Id, uniform, skin, _materials,
                    CharacterLooks.For(npc.Id));

                _npcViews[npc.Id] = new NpcView
                {
                    Rig = rig,
                    Npc = npc,
                    Uniform = uniform,
                    Skin = skin,
                    Cone = BuildVisionCone(rig.Root)
                };
            }
        }

        /// <summary>
        /// Field of view drawn on the floor. In a stealth game this is a gameplay
        /// feature, not a debug gizmo: the player cannot plan around perception
        /// they cannot see.
        /// </summary>
        private LineRenderer BuildVisionCone(Transform parent)
        {
            GameObject coneObject = new GameObject("VisionCone");
            coneObject.transform.SetParent(parent, false);

            LineRenderer line = coneObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = 0.07f;
            line.positionCount = ConeSegments + 2;
            line.sharedMaterial = _materials.Unlit(Color.white);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private const int ConeSegments = 16;

        // ------------------------------------------------------------------
        // Per-frame sync
        // ------------------------------------------------------------------

        public void Sync(Simulation sim, float dt, string focusedObjectId)
        {
            for (int i = 0; i < sim.World.Objects.Count; i++)
            {
                SmartObject obj = sim.World.Objects[i];

                ObjectView view;
                if (!_objectViews.TryGetValue(obj.Id, out view))
                {
                    // Objects can appear mid-level - an oil slick, for instance.
                    view = CreateObjectView(sim, obj);
                }

                bool visible = !obj.Concealed && obj.HeldBy.Length == 0;
                if (view.Root.gameObject.activeSelf != visible) view.Root.gameObject.SetActive(visible);
                if (!visible) continue;

                if (view.IsDoor)
                {
                    float open = obj.GetState("open") > 0f ? 95f : 0f;
                    view.Root.localRotation = Quaternion.Slerp(
                        view.Root.localRotation, Quaternion.Euler(0f, open, 0f), 1f - Mathf.Exp(-9f * dt));
                }
                else
                {
                    view.Root.position = ToUnity(obj.Position) + Vector3.up * (obj.Size.Y * 0.5f);
                }

                Color colour = ObjectColour(obj);
                if (obj.Id == focusedObjectId)
                {
                    // Focused objects glow, so "what am I about to interact with"
                    // is never a guess.
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 6f);
                    colour = Color.Lerp(colour, new Color(1f, 0.95f, 0.5f), 0.35f + pulse * 0.35f);
                }

                ApplySurface(view, colour);
            }

            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];

                NpcView view;
                if (!_npcViews.TryGetValue(npc.Id, out view)) continue;

                bool sitting = npc.Activity == NpcActivity.Using
                               && npc.CurrentPlan != null
                               && npc.CurrentPlan.Target != null
                               && npc.CurrentPlan.Target.HasTag(Tags.Seat);

                view.Rig.Pose(npc.Position, npc.Facing, npc.Activity, npc.Anger, sitting, dt,
                    _materials, view.Uniform, view.Skin, npc);

                TriggerReactions(view, npc);
                UpdateCone(view.Cone, npc, sim);
            }
        }

        /// <summary>
        /// Fire a physical reaction when the simulation's state jumps.
        ///
        /// The rig cannot see events, only state, so this watches for the edges:
        /// the frame an NPC starts noticing something, and the frame their anger
        /// takes a real step up. Those are the two moments that deserve a
        /// double-take and a held pose, and they are the difference between a
        /// character reacting and a number changing.
        /// </summary>
        private void TriggerReactions(NpcView view, Npc npc)
        {
            bool noticing = npc.Attention.Noticing;

            if (noticing && !view.WasNoticing)
            {
                view.Rig.React(Reaction.Startle, 0.7f);
            }
            view.WasNoticing = noticing;

            float jump = npc.Anger - view.LastAnger;
            view.LastAnger = npc.Anger;

            if (jump > 0.12f)
            {
                // The bigger the grievance, the harder the reaction - and at the
                // boiling point it stops being a double-take and becomes rage.
                view.Rig.React(
                    npc.Anger >= AngerModel.BoilingPoint ? Reaction.Rage : Reaction.Dismay,
                    0.7f + jump * 2.2f);
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

            // Green: hasn't noticed you. Amber: something feels off. Red: on to you.
            float suspicion = npc.SuspicionOf(PlayerAvatar.PlayerId);
            Color colour = suspicion < 0.4f
                ? Color.Lerp(new Color(0.3f, 0.9f, 0.45f), new Color(0.95f, 0.8f, 0.2f), suspicion / 0.4f)
                : Color.Lerp(new Color(0.95f, 0.8f, 0.2f), new Color(1f, 0.2f, 0.16f), (suspicion - 0.4f) / 0.6f);

            line.startColor = colour;
            line.endColor = new Color(colour.r, colour.g, colour.b, 0.1f);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void ApplySurface(ObjectView view, Color colour)
        {
            if (view.Renderer == null) return;

            view.Renderer.sharedMaterial = view.Texture.Length > 0
                ? _materials.Textured(view.Texture, colour, new Vector2(1.5f, 1.5f))
                : _materials.Lit(colour);
        }

        private static string ObjectTexture(SmartObject obj)
        {
            if (obj.HasTag(Tags.Hiding)) return "fabric";
            if (obj.HasTag(Tags.Appliance) || obj.HasTag(Tags.Container)) return "worktop_steel";
            if (obj.HasTag(Tags.Door) || obj.HasTag(Tags.Seat)) return "wood_floor";
            return "";
        }

        private static Color ObjectColour(SmartObject obj)
        {
            if (obj.HasTag(Tags.Hazard)) return new Color(0.9f, 0.78f, 0.15f);
            if (obj.HasTag(Tags.Mess)) return new Color(0.45f, 0.4f, 0.2f);
            if (obj.IsBroken) return new Color(0.85f, 0.35f, 0.2f);
            if (obj.HasTag(Tags.Hiding)) return new Color(0.75f, 0.7f, 0.85f);
            if (obj.HasTag(Tags.Door)) return new Color(0.85f, 0.8f, 0.7f);

            // The loud, dangerous things read red so the player clocks them from
            // across the room and has to decide whether it is worth it.
            if (obj.HasTag(Tags.Noisy)) return new Color(0.92f, 0.3f, 0.28f);

            if (obj.OwnerId.Length > 0) return new Color(0.78f, 0.82f, 0.95f);
            return new Color(0.88f, 0.88f, 0.86f);
        }

        private static Color UniformFor(Npc npc)
        {
            switch (npc.Role)
            {
                case "head chef": return new Color(0.94f, 0.94f, 0.92f);
                case "waiter": return new Color(0.25f, 0.3f, 0.55f);
                case "dishwasher": return new Color(0.35f, 0.5f, 0.42f);
                case "sous chef": return new Color(0.88f, 0.89f, 0.93f);
                case "kitchen porter": return new Color(0.5f, 0.45f, 0.3f);
                default: return new Color(0.28f, 0.28f, 0.32f);
            }
        }

        private static Color SkinFor(Npc npc)
        {
            // Just enough variation that the cast is distinguishable at range.
            int hash = 0;
            for (int i = 0; i < npc.Id.Length; i++) hash = hash * 31 + npc.Id[i];
            float t = Mathf.Abs((hash % 100) / 100f);
            return Color.Lerp(new Color(0.96f, 0.80f, 0.68f), new Color(0.45f, 0.31f, 0.23f), t);
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

    public sealed class ObjectView
    {
        public Transform Root;
        public Transform Body;
        public Renderer Renderer;
        public Color BaseColour;
        public string Texture = "";
        public bool IsDoor;
    }

    public sealed class NpcView
    {
        public CharacterRig Rig;
        public LineRenderer Cone;
        public Color Uniform;
        public Color Skin;

        /// <summary>The simulation NPC, so the rig can read attention and mood.</summary>
        public Npc Npc;

        /// <summary>Last anger seen, to spot the jump that deserves a reaction.</summary>
        public float LastAnger;

        /// <summary>Whether they were mid-notice last frame.</summary>
        public bool WasNoticing;
    }

    /// <summary>
    /// Shared materials, cached by colour and texture.
    ///
    /// Shaders are resolved by name at runtime so the same code renders correctly
    /// under the Built-in pipeline or URP. Hard-coding "Standard" is the usual
    /// reason a generated scene comes out magenta.
    /// </summary>
    public sealed class Materials
    {
        private readonly Dictionary<int, Material> _lit = new Dictionary<int, Material>();
        private readonly Dictionary<int, Material> _unlit = new Dictionary<int, Material>();
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();

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
            return Get(_lit, _litShader, colour, null, Vector2.one);
        }

        public Material Unlit(Color colour)
        {
            return Get(_unlit, _unlitShader, colour, null, Vector2.one);
        }

        /// <summary>
        /// An unlit, alpha-cut material for the face atlas. Unlit on purpose:
        /// eyes that fall into shadow stop reading, and the expression is the
        /// most important thing on the character.
        /// </summary>
        public Material Cutout(Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Transparent Cutout")
                            ?? Shader.Find("Sprites/Default")
                            ?? _unlitShader;

            Material material = new Material(shader);
            material.mainTexture = texture;
            material.color = Color.white;

            // URP's Unlit needs telling; the built-in cutout shader already knows.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.4f);

            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = 2450;
            return material;
        }

        public Material Textured(string textureName, Color tint, Vector2 tiling)
        {
            Texture2D texture = LoadTexture(textureName);
            if (texture == null) return Lit(tint);

            int key = QuantiseKey(tint) * 397 + textureName.GetHashCode()
                      + Mathf.RoundToInt(tiling.x * 7f) * 31 + Mathf.RoundToInt(tiling.y * 13f);

            return Get(_lit, _litShader, tint, texture, tiling, key);
        }

        private Texture2D LoadTexture(string name)
        {
            Texture2D texture;
            if (_textures.TryGetValue(name, out texture)) return texture;

            texture = Resources.Load<Texture2D>("Textures/" + name);
            if (texture != null) texture.wrapMode = TextureWrapMode.Repeat;
            _textures[name] = texture;
            return texture;
        }

        private Material Get(Dictionary<int, Material> cache, Shader shader, Color colour,
            Texture2D texture, Vector2 tiling, int explicitKey = 0)
        {
            int key = explicitKey != 0 ? explicitKey : QuantiseKey(colour);

            Material material;
            if (cache.TryGetValue(key, out material) && material != null) return material;

            material = new Material(shader);
            material.color = colour;

            // URP's Lit uses _BaseColor/_BaseMap; setting .color/.mainTexture alone
            // is silently ignored there.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);

            if (texture != null)
            {
                material.mainTexture = texture;
                material.mainTextureScale = tiling;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetTextureScale("_BaseMap", tiling);
                }
            }

            cache[key] = material;
            return material;
        }

        private static int QuantiseKey(Color c)
        {
            int r = Mathf.RoundToInt(c.r * 32f);
            int g = Mathf.RoundToInt(c.g * 32f);
            int b = Mathf.RoundToInt(c.b * 32f);
            int a = Mathf.RoundToInt(c.a * 32f);
            return ((r * 33 + g) * 33 + b) * 33 + a + 1;
        }
    }
}
