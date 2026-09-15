using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// Movement, camera and input. Third person by default because a game about
    /// not being seen needs you to see yourself and your surroundings; press V
    /// for first person if you prefer it.
    ///
    /// Holds no game state. It moves a capsule and reports where it ended up.
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
        private Transform _body;

        private float _yaw;
        private float _pitch = 14f;
        private float _verticalVelocity;
        private bool _moving;
        private bool _sneaking;

        public bool Sneaking
        {
            get { return _sneaking; }
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

            if (_body == null)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(transform, false);
                body.transform.localScale = new Vector3(0.7f, 0.85f, 0.7f);
                body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                Destroy(body.GetComponent<Collider>());

                Renderer renderer = body.GetComponent<Renderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material material = new Material(shader);
                Color colour = new Color(0.25f, 0.45f, 0.85f);
                material.color = colour;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
                renderer.sharedMaterial = material;

                _body = body.transform;
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
                _camera.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
                _camera.nearClipPlane = 0.05f;
            }

            // Drop the capsule where the level put the player.
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
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            Vector3 head = transform.position + Vector3.up * 1.6f;

            if (FirstPerson)
            {
                _camera.transform.position = head + _camera.transform.forward * 0.1f;
                _camera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                if (_body != null) _body.gameObject.SetActive(false);
                return;
            }

            if (_body != null && !_body.gameObject.activeSelf) _body.gameObject.SetActive(true);

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 offset = orbit * new Vector3(0f, 0f, -5.2f);
            Vector3 desired = head + offset + Vector3.up * 1.1f;

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
                    transform.rotation, target, 540f * Time.deltaTime);
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

            var interactions = _runner.Interactions;
            if (interactions.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                _runner.BeginInteraction(interactions[0]);
                return;
            }

            // Number keys pick straight off the prompt list.
            for (int i = 0; i < interactions.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _runner.BeginInteraction(interactions[i]);
                    return;
                }
            }
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

        public Camera Camera
        {
            get { return _camera; }
        }
    }
}
