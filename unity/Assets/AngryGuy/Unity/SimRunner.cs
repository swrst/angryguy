using System.Collections.Generic;
using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// The bridge between the engine-agnostic simulation and Unity.
    ///
    /// Unity supplies a clock, input and rendering. It holds no game rules: no
    /// anger, no suspicion, no AI. Everything that decides what happens lives in
    /// AngryGuy.Core, which is why the whole design can be unit tested and
    /// replayed headlessly.
    /// </summary>
    public sealed class SimRunner : MonoBehaviour
    {
        [Tooltip("0 uses a random seed. Any other value reproduces that exact run.")]
        public int Seed;

        [Tooltip("Speeds the world up for testing. 1 is normal.")]
        [Range(0.25f, 8f)]
        public float TimeScale = 1f;

        public Simulation Sim { get; private set; }
        public LevelView View { get; private set; }
        public PlayerController Player { get; private set; }

        /// <summary>The interaction currently being performed, if any.</summary>
        public InteractionOption Channelling { get; private set; }

        public float ChannelProgress { get; private set; }

        private List<InteractionOption> _interactions = new List<InteractionOption>();
        private float _interactionRefresh;

        public IReadOnlyList<InteractionOption> Interactions
        {
            get { return _interactions; }
        }

        private void Awake()
        {
            StartLevel(Seed == 0 ? Random.Range(1, int.MaxValue) : Seed);
        }

        public void StartLevel(int seed)
        {
            Seed = seed;

            if (View != null) View.Teardown();

            Sim = KitchenLevel.Build(seed);

            View = new LevelView();
            View.Build(Sim, transform);

            if (Player == null)
            {
                GameObject playerObject = new GameObject("Player");
                playerObject.transform.SetParent(transform, false);
                Player = playerObject.AddComponent<PlayerController>();
            }

            Player.Attach(this);

            if (GetComponent<GameHud>() == null) gameObject.AddComponent<GameHud>();
        }

        private void Update()
        {
            if (Sim == null) return;

            float dt = Time.deltaTime * TimeScale;

            // The player's transform is authoritative for position; the sim is told
            // where they ended up. Everything else is driven by the sim.
            Player.WriteInto(Sim);

            Sim.Tick(dt);

            TickChannel(dt);
            View.Sync(Sim);

            _interactionRefresh -= dt;
            if (_interactionRefresh <= 0f)
            {
                _interactionRefresh = 0.15f;
                _interactions = Sim.GetPlayerInteractions();
            }
        }

        // ------------------------------------------------------------------
        // Interactions
        // ------------------------------------------------------------------

        /// <summary>
        /// Interactions take time and must be finished in place. That window is
        /// where the stealth actually happens: anyone who walks in while you are
        /// halfway through emptying a fridge has seen you do it.
        /// </summary>
        public void BeginInteraction(InteractionOption option)
        {
            if (option == null) return;
            if (Sim.Outcome != GameOutcome.InProgress) return;

            Channelling = option;
            ChannelProgress = 0f;
        }

        public void CancelInteraction()
        {
            Channelling = null;
            ChannelProgress = 0f;
        }

        private void TickChannel(float dt)
        {
            if (Channelling == null) return;

            // Walked away, or someone else changed the world underneath us.
            float distance = Vec3.FlatDistance(Sim.Player.Position, Channelling.Object.Position);
            if (distance > 2.6f || !Channelling.Affordance.AvailableFor(Channelling.Object, null))
            {
                CancelInteraction();
                return;
            }

            ChannelProgress += dt;
            if (ChannelProgress < Mathf.Max(0.35f, Channelling.Affordance.Duration)) return;

            InteractionOption option = Channelling;
            CancelInteraction();
            Sim.PlayerInteract(option);
            _interactionRefresh = 0f;
        }

        public void Restart()
        {
            StartLevel(Random.Range(1, int.MaxValue));
        }

        public void Retry()
        {
            StartLevel(Seed);
        }
    }
}
