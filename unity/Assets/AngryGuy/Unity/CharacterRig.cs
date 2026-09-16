using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// A person built from boxes, animated entirely in code.
    ///
    /// No FBX, no Animator, no imported clips. That is a deliberate trade: for a
    /// prototype, a hand-written walk cycle on primitives reads as intentional
    /// and toy-like, whereas mismatched free character models with mismatched
    /// free animations read as broken. It also means an NPC's pose is driven
    /// directly by simulation state, so what you see is always what the AI is
    /// actually doing.
    ///
    /// When real characters arrive, this class is the only thing that changes.
    /// </summary>
    public sealed class CharacterRig
    {
        public Transform Root;

        private Transform _hips;
        private Transform _torso;
        private Transform _head;
        private Transform _shoulderL;
        private Transform _shoulderR;
        private Transform _hipL;
        private Transform _hipR;

        private Renderer _torsoRenderer;
        private Renderer _headRenderer;

        private float _walkPhase;
        private float _bobPhase;
        private float _lastYaw;
        private Vector3 _lastPosition;

        private const float HipHeight = 0.92f;

        private float _hipHeight = HipHeight;

        public void Build(Transform parent, string name, Color uniform, Color skin,
            Materials materials, bool wearsHat)
        {
            CharacterBuild build = CharacterBuild.Default;
            build.Hat = wearsHat ? HatKind.Toque : HatKind.None;
            Build(parent, name, uniform, skin, materials, build);
        }

        /// <summary>
        /// Build a body from a silhouette description.
        ///
        /// Six people in one room have to be telling apart at a glance and from
        /// behind, in a game where noticing who is where is the whole skill. So
        /// height, width, head size, headgear and hair all vary - the shape says
        /// who it is long before the colour does.
        /// </summary>
        public void Build(Transform parent, string name, Color uniform, Color skin,
            Materials materials, CharacterBuild build)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Root = root.transform;

            float h = build.Height;
            float w = build.Girth;

            _hipHeight = HipHeight * h;
            _hips = MakeNode(Root, "hips", new Vector3(0f, _hipHeight, 0f));

            _torso = MakeBox(_hips, "torso",
                new Vector3(0f, 0.28f * h, 0f),
                new Vector3(0.52f * w, 0.62f * h, 0.3f * w), uniform, materials);
            _torsoRenderer = _torso.GetComponent<Renderer>();

            if (build.WearsApron)
            {
                MakeBox(_torso, "apron",
                    new Vector3(0f, -0.12f, 0.55f), new Vector3(0.86f, 0.72f, 0.12f),
                    build.ApronColour, materials);
            }

            // The head is an UNSCALED pivot with the skull as a child. Parenting
            // meshes directly under a scaled cube multiplies their scale too - a
            // 0.1 nose under a 0.34 head renders at 0.034 and vanishes.
            _head = MakeNode(_hips, "head", new Vector3(0f, 0.76f * h, 0f));

            float s = 0.34f * build.HeadScale;
            Transform skull = MakeBox(_head, "skull", Vector3.zero, new Vector3(s, s, s), skin, materials);
            _headRenderer = skull.GetComponent<Renderer>();

            // A nose, so facing is unmistakable from any angle.
            MakeBox(_head, "nose",
                new Vector3(0f, -0.02f, s * 0.62f), new Vector3(0.1f, 0.1f, 0.14f),
                new Color(0.2f, 0.18f, 0.18f), materials);

            if (build.HasHair)
            {
                MakeBox(_head, "hair",
                    new Vector3(0f, s * 0.42f, -s * 0.08f),
                    new Vector3(s * 1.06f, s * 0.42f, s * 1.06f), build.HairColour, materials);
            }

            switch (build.Hat)
            {
                case HatKind.Toque:
                    MakeBox(_head, "toque",
                        new Vector3(0f, s * 0.5f, 0f), new Vector3(s * 1.24f, s * 0.24f, s * 1.24f),
                        Color.white, materials);
                    MakeBox(_head, "toque_top",
                        new Vector3(0f, s * 1.05f, 0f), new Vector3(s * 1.06f, s * 0.9f, s * 1.06f),
                        Color.white, materials);
                    break;

                case HatKind.Cap:
                    MakeBox(_head, "cap",
                        new Vector3(0f, s * 0.58f, 0f), new Vector3(s * 1.1f, s * 0.36f, s * 1.1f),
                        build.HatColour, materials);
                    MakeBox(_head, "peak",
                        new Vector3(0f, s * 0.46f, s * 0.78f), new Vector3(s * 1.0f, s * 0.1f, s * 0.6f),
                        build.HatColour, materials);
                    break;

                case HatKind.Bun:
                    MakeBox(_head, "bun",
                        new Vector3(0f, s * 0.52f, -s * 0.5f), new Vector3(s * 0.5f, s * 0.5f, s * 0.5f),
                        build.HairColour, materials);
                    break;
            }

            if (build.HasGlasses)
            {
                MakeBox(_head, "glasses",
                    new Vector3(0f, s * 0.14f, s * 0.52f), new Vector3(s * 1.02f, s * 0.16f, s * 0.08f),
                    new Color(0.1f, 0.1f, 0.12f), materials);
            }

            // Limbs hang from pivot nodes so rotation happens at the joint,
            // not through the middle of the limb.
            _shoulderL = MakeNode(_hips, "shoulderL", new Vector3(-0.33f * w, 0.52f * h, 0f));
            _shoulderR = MakeNode(_hips, "shoulderR", new Vector3(0.33f * w, 0.52f * h, 0f));
            Vector3 arm = new Vector3(0.14f * w, 0.5f * h, 0.14f * w);
            MakeBox(_shoulderL, "armL", new Vector3(0f, -0.24f * h, 0f), arm, skin, materials);
            MakeBox(_shoulderR, "armR", new Vector3(0f, -0.24f * h, 0f), arm, skin, materials);

            _hipL = MakeNode(_hips, "hipL", new Vector3(-0.14f * w, 0f, 0f));
            _hipR = MakeNode(_hips, "hipR", new Vector3(0.14f * w, 0f, 0f));
            Color trousers = new Color(uniform.r * 0.45f, uniform.g * 0.45f, uniform.b * 0.5f);
            Vector3 leg = new Vector3(0.18f * w, 0.88f * h, 0.18f * w);
            MakeBox(_hipL, "legL", new Vector3(0f, -0.44f * h, 0f), leg, trousers, materials);
            MakeBox(_hipR, "legR", new Vector3(0f, -0.44f * h, 0f), leg, trousers, materials);
        }

        private static Transform MakeNode(Transform parent, string name, Vector3 localPosition)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPosition;
            return node.transform;
        }

        private static Transform MakeBox(Transform parent, string name, Vector3 localPosition,
            Vector3 scale, Color colour, Materials materials)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = scale;

            Collider collider = box.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = materials.Lit(colour);

            return box.transform;
        }

        /// <summary>
        /// Pose the rig from simulation state. Called once per frame; everything
        /// is derived, so there is no animation state to get out of sync.
        /// </summary>
        public void Pose(Vec3 position, Vec3 facing, NpcActivity activity, float anger,
            bool sitting, float dt, Materials materials, Color uniform, Color skin)
        {
            Vector3 target = LevelView.ToUnity(position);

            float moved = Vector3.Distance(target, _lastPosition) / Mathf.Max(dt, 0.0001f);
            _lastPosition = target;

            Root.position = target;

            Vector3 forward = LevelView.ToUnity(facing);
            if (forward.sqrMagnitude > 0.0001f)
            {
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                _lastYaw = Mathf.LerpAngle(_lastYaw, yaw, 1f - Mathf.Exp(-12f * dt));
                Root.rotation = Quaternion.Euler(0f, _lastYaw, 0f);
            }

            float speed = Mathf.Clamp01(moved / 3f);
            _walkPhase += dt * (4f + speed * 9f) * (speed > 0.03f ? 1f : 0f);
            _bobPhase += dt * 2f;

            if (sitting)
            {
                PoseSitting();
            }
            else
            {
                PoseUpright(speed, anger, activity);
            }

            PoseHead(activity, anger, dt);
            Tint(anger, materials, uniform, skin);
        }

        private void PoseUpright(float speed, float anger, NpcActivity activity)
        {
            float swing = Mathf.Sin(_walkPhase) * (12f + speed * 30f);

            _hipL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            _hipR.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            // Furious NPCs throw their arms about instead of swinging them.
            if (anger >= 0.85f)
            {
                float flail = Mathf.Sin(_walkPhase * 3.5f) * 35f;
                _shoulderL.localRotation = Quaternion.Euler(-150f + flail, 0f, 18f);
                _shoulderR.localRotation = Quaternion.Euler(-150f - flail, 0f, -18f);
            }
            else if (activity == NpcActivity.Confronting)
            {
                // Pointing an accusing finger.
                _shoulderR.localRotation = Quaternion.Euler(-85f, 0f, -12f);
                _shoulderL.localRotation = Quaternion.Euler(-swing * 0.6f, 0f, 0f);
            }
            else
            {
                _shoulderL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 6f);
                _shoulderR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, -6f);
            }

            float bob = Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.05f * speed;
            float breathe = Mathf.Sin(_bobPhase) * 0.012f;
            float rage = anger >= 0.85f ? Mathf.Sin(Time.time * 40f) * 0.02f : 0f;

            _hips.localPosition = new Vector3(rage, _hipHeight + bob + breathe, 0f);
            _hips.localRotation = Quaternion.Euler(speed * 6f, 0f, 0f);
            _torso.localRotation = Quaternion.Euler(0f, Mathf.Sin(_walkPhase) * 4f, 0f);
        }

        private void PoseSitting()
        {
            _hips.localPosition = new Vector3(0f, _hipHeight - 0.34f, 0f);
            _hips.localRotation = Quaternion.identity;
            _hipL.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            _hipR.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            _shoulderL.localRotation = Quaternion.Euler(-20f, 0f, 6f);
            _shoulderR.localRotation = Quaternion.Euler(-20f, 0f, -6f);
            _torso.localRotation = Quaternion.Euler(6f, 0f, 0f);
        }

        private void PoseHead(NpcActivity activity, float anger, float dt)
        {
            // Scanning the room while investigating is the clearest possible
            // "this NPC is looking for something" signal.
            float scan = activity == NpcActivity.Investigating
                ? Mathf.Sin(Time.time * 2.4f) * 42f
                : Mathf.Sin(Time.time * 0.6f) * 6f;

            float nod = anger >= 0.6f ? Mathf.Sin(Time.time * 9f) * 5f : 0f;
            _head.localRotation = Quaternion.Euler(nod, scan, 0f);
        }

        private void Tint(float anger, Materials materials, Color uniform, Color skin)
        {
            // Faces redden before uniforms do, which keeps roles readable by colour
            // while still showing mood at a glance.
            Color face = Color.Lerp(skin, new Color(0.92f, 0.22f, 0.16f), Mathf.Clamp01(anger * 1.15f));
            Color body = Color.Lerp(uniform, new Color(0.75f, 0.2f, 0.18f), Mathf.Clamp01(anger - 0.45f));

            if (_headRenderer != null) _headRenderer.sharedMaterial = materials.Lit(face);
            if (_torsoRenderer != null) _torsoRenderer.sharedMaterial = materials.Lit(body);
        }

        public Vector3 HeadWorldPosition
        {
            get { return _head != null ? _head.position : Root.position; }
        }
    }
}
