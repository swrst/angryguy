using System.Collections.Generic;
using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// Movement, camera and input.
    ///
    /// Third person by default: a game about not being seen needs you to see
    /// yourself and what is behind you. Press V for first person.
    ///
    /// Holds no game state. It moves a capsule, reads keys, and reports back.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public float WalkSpeed = 3.4f;
        public float SneakSpeed = 1.5f;
        public float MouseSensitivity = 2.6f;

        public bool FirstPerson;

        private SimRunner _runner;
        private CharacterController _controller;
        private Camera _camera;
        private CharacterRig _rig;
        private Transform _carried;
        private Renderer _carriedRenderer;

        private float _yaw;
        private float _pitch = 16f;
        private float _verticalVelocity;
        private bool _moving;
        private bool _sneaking;

        public bool Sneaking
        {
            get { return _sneaking; }
        }

        public bool IsMoving
        {
            get { return _moving; }
        }

        public Camera Camera
        {
            get { return _camera; }
        }

        public void Attach(SimRunner runner)
        {
            _runner = runner;

            if (_controller == null)
            {
                _controller = gameObject.AddComponent<CharacterController>();
                _controller.height = 1.8f;
                _controller.radius = 0.35f;
                _controller.center = new Vector3(0f, 0.9f, 0f);
                _controller.slopeLimit = 60f;
                _controller.stepOffset = 0.4f;
            }

            if (_rig == null)
            {
                _rig = new CharacterRig();
                _rig.Build(transform, "PlayerBody",
                    new Color(0.22f, 0.4f, 0.8f), new Color(0.9f, 0.74f, 0.62f),
                    runner.View.Materials, false);

                GameObject carried = GameObject.CreatePrimitive(PrimitiveType.Cube);
                carried.name = "Carried";
                carried.transform.SetParent(transform, false);
                carried.transform.localPosition = new Vector3(0f, 1.05f, 0.45f);
                carried.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
                Destroy(carried.GetComponent<Collider>());
                _carried = carried.transform;
                _carriedRenderer = carried.GetComponent<Renderer>();
                carried.SetActive(false);
            }

            if (_camera == null)
            {
                Camera existing = Camera.main;
                if (existing != null && existing.transform.parent == null)
                {
                    _camera = existing;
                }
                else
                {
                    GameObject cameraObject = new GameObject("PlayerCamera");
                    _camera = cameraObject.AddComponent<Camera>();
                    cameraObject.AddComponent<AudioListener>();
                    cameraObject.tag = "MainCamera";
                }

                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.14f, 0.16f, 0.2f);
                _camera.nearClipPlane = 0.05f;
            }

            _controller.enabled = false;
            transform.position = LevelView.ToUnity(runner.Sim.Player.Position);
            _controller.enabled = true;

            LockCursor(true);
        }

        private void Update()
        {
            if (_runner == null || _runner.Sim == null) return;

            HandleCursor();
            HandleLook();
            HandleMovement();
            HandleInteractionInput();
            UpdateBody();
        }

        private void UpdateBody()
        {
            if (_rig == null) return;

            Vec3 facing = LevelView.ToSim(transform.forward);
            _rig.Pose(LevelView.ToSim(transform.position), facing,
                _moving ? NpcActivity.Walking : NpcActivity.Idle, 0f, false,
                Time.deltaTime, _runner.View.Materials,
                new Color(0.22f, 0.4f, 0.8f), new Color(0.9f, 0.74f, 0.62f));

            // Hide our own body in first person, and while hidden in a cupboard.
            bool showBody = !FirstPerson && !_runner.Sim.Player.IsHidden;
            if (_rig.Root.gameObject.activeSelf != showBody) _rig.Root.gameObject.SetActive(showBody);

            bool carrying = _runner.Sim.Player.IsCarrying;
            if (_carried != null && _carried.gameObject.activeSelf != carrying)
            {
                _carried.gameObject.SetActive(carrying);
            }

            if (carrying && _carriedRenderer != null)
            {
                SmartObject held = _runner.Sim.World.GetObject(_runner.Sim.Player.CarryingObjectId);
                if (held != null)
                {
                    _carried.localScale = new Vector3(
                        Mathf.Clamp(held.Size.X, 0.15f, 0.5f),
                        Mathf.Clamp(held.Size.Y, 0.15f, 0.5f),
                        Mathf.Clamp(held.Size.Z, 0.15f, 0.5f));
                    _carriedRenderer.sharedMaterial =
                        _runner.View.Materials.Lit(new Color(0.9f, 0.85f, 0.5f));
                }
            }
        }

        // ------------------------------------------------------------------

        private void HandleCursor()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
            if (Input.GetKeyDown(KeyCode.V)) FirstPerson = !FirstPerson;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            _yaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity;
            _pitch -= Input.GetAxisRaw("Mouse Y") * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -35f, 70f);
        }

        private void HandleMovement()
        {
            // Hiding costs you all your agency until you step back out, which is
            // what stops it being a free win.
            if (_runner.Sim.Player.IsHidden)
            {
                _moving = false;
                return;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            _sneaking = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            Vector3 input = new Vector3(h, 0f, v);
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 move = Quaternion.Euler(0f, _yaw, 0f) * input;
            _moving = move.sqrMagnitude > 0.01f;

            float speed = _sneaking ? SneakSpeed : WalkSpeed;

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -1f;
            _verticalVelocity += Physics.gravity.y * Time.deltaTime;

            Vector3 velocity = move * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            if (_moving && !FirstPerson)
            {
                Quaternion target = Quaternion.LookRotation(move.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, 620f * Time.deltaTime);
            }
            else if (FirstPerson)
            {
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
        }

        private void HandleInteractionInput()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                _runner.CancelInteraction();
                return;
            }

            // Throwing is its own key because it is the one verb you want to use
            // while already running away.
            if (Input.GetKeyDown(KeyCode.T) && _runner.Sim.Player.IsCarrying)
            {
                _runner.Sim.PlayerThrow();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleHide();
                return;
            }

            List<InteractionOption> options = _runner.FocusedOptions();
            if (options.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                _runner.BeginInteraction(options[0]);
                return;
            }

            for (int i = 0; i < options.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _runner.BeginInteraction(options[i]);
                    return;
                }
            }
        }

        private void ToggleHide()
        {
            if (_runner.Sim.Player.IsHidden)
            {
                _runner.Sim.PlayerToggleHide(null);
                return;
            }

            SmartObject nearest = null;
            float best = float.MaxValue;
            List<SmartObject> objects = _runner.Sim.World.Objects;

            for (int i = 0; i < objects.Count; i++)
            {
                if (!objects[i].HasTag(Tags.Hiding)) continue;
                float distance = Vec3.FlatDistance(objects[i].Position, _runner.Sim.Player.Position);
                if (distance < best)
                {
                    best = distance;
                    nearest = objects[i];
                }
            }

            if (nearest != null && best <= 2.2f)
            {
                _runner.Sim.PlayerToggleHide(nearest);
                _controller.enabled = false;
                transform.position = LevelView.ToUnity(_runner.Sim.Player.Position);
                _controller.enabled = true;
            }
            else
            {
                _runner.PushToast("Nothing to hide in here.", new Color(0.8f, 0.8f, 0.85f), 2.5f);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            Vector3 head = transform.position + Vector3.up * 1.62f;

            if (FirstPerson)
            {
                _camera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                _camera.transform.position = head + _camera.transform.forward * 0.12f;
                return;
            }

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = head + orbit * new Vector3(0.45f, 0f, -5.0f) + Vector3.up * 1.0f;

            // Keep the camera out of walls without a full collision solve.
            Vector3 direction = desired - head;
            RaycastHit hit;
            if (Physics.Raycast(head, direction.normalized, out hit, direction.magnitude + 0.3f))
            {
                desired = hit.point - direction.normalized * 0.3f;
            }

            _camera.transform.position = desired;
            _camera.transform.rotation = Quaternion.LookRotation((head - desired).normalized, Vector3.up);
        }

        /// <summary>Tell the simulation where the player ended up this frame.</summary>
        public void WriteInto(Simulation sim)
        {
            sim.SyncPlayer(
                LevelView.ToSim(transform.position),
                LevelView.ToSim(FirstPerson
                    ? Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward
                    : transform.forward),
                _sneaking,
                _moving);

            sim.Player.WalkSpeed = WalkSpeed;
            sim.Player.SneakSpeed = SneakSpeed;
        }
    }
}
