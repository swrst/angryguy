using System.Collections.Generic;
using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>Floating text that rises off an NPC's head and fades.</summary>
    public sealed class Popup
    {
        public Vector3 World;
        public string Text = "";
        public Color Colour = Color.white;
        public float Age;
        public float Life = 1.8f;
    }

    /// <summary>A short message along the bottom of the screen.</summary>
    public sealed class Toast
    {
        public string Text = "";
        public Color Colour = Color.white;
        public float Age;
        public float Life = 4.5f;
    }

    /// <summary>
    /// The bridge between the engine-agnostic simulation and Unity.
    ///
    /// Unity supplies a clock, input, rendering and sound. It holds no game
    /// rules: no anger, no suspicion, no AI. Everything that decides what
    /// happens lives in AngryGuy.Core.
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
        public AudioDirector Audio { get; private set; }

        /// <summary>Interaction the player is currently performing, if any.</summary>
        public InteractionOption Channelling { get; private set; }

        public float ChannelProgress { get; private set; }

        /// <summary>What the player is looking at and would interact with.</summary>
        public InteractionOption Focus { get; private set; }

        public readonly List<Popup> Popups = new List<Popup>();
        public readonly List<Toast> Toasts = new List<Toast>();

        private List<InteractionOption> _interactions = new List<InteractionOption>();
        private float _interactionRefresh;
        private int _hintStage;
        private float _hintTimer = 3f;

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
            Popups.Clear();
            Toasts.Clear();
            _hintStage = 0;
            _hintTimer = 3f;
            Channelling = null;
            Focus = null;

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

            if (Audio == null) Audio = gameObject.AddComponent<AudioDirector>();
            Audio.Attach(Sim);

            if (GetComponent<GameHud>() == null) gameObject.AddComponent<GameHud>();

            PushToast("Make Gordon furious. Don't let anyone work out it was you.",
                new Color(1f, 0.9f, 0.5f), 7f);
        }

        private void Update()
        {
            if (Sim == null) return;

            float dt = Time.deltaTime * TimeScale;
            GameOutcome before = Sim.Outcome;

            Player.WriteInto(Sim);
            Sim.Tick(dt);

            TickChannel(dt);
            DrainFeedback();
            TickPopups(dt);
            TickHints(dt);

            View.Sync(Sim, dt, Focus != null ? Focus.Object.Id : "");

            _interactionRefresh -= dt;
            if (_interactionRefresh <= 0f)
            {
                _interactionRefresh = 0.12f;
                _interactions = Sim.GetPlayerInteractions();
            }

            Focus = ChooseFocus();

            Audio.TickPlayer(Player.IsMoving, Player.Sneaking, dt, Player.transform.position);
            Audio.TickTension(HighestSuspicion(), dt);

            if (before == GameOutcome.InProgress && Sim.Outcome != GameOutcome.InProgress)
            {
                Audio.OnOutcome(Sim.Outcome);
            }
        }

        // ------------------------------------------------------------------
        // Feedback -> screen
        // ------------------------------------------------------------------

        /// <summary>
        /// Everything the simulation wants the player to know arrives here and
        /// is turned into something visible. This is the fix for the prototype's
        /// worst problem: plenty happening, none of it legible.
        /// </summary>
        private void DrainFeedback()
        {
            List<FeedbackEvent> events = Sim.Feedback.Drain();

            for (int i = 0; i < events.Count; i++)
            {
                FeedbackEvent e = events[i];
                Audio.OnFeedback(e);

                Vector3 head = HeadPosition(e.ActorId);

                switch (e.Kind)
                {
                    case FeedbackKind.Anger:
                        Popups.Add(new Popup
                        {
                            World = head,
                            Text = "+" + e.Amount + " ANGER",
                            Colour = new Color(1f, 0.42f, 0.3f)
                        });
                        if (e.Amount >= 5) PushToast(Sim.DisplayName(e.ActorId) + ": " + e.Text,
                            new Color(1f, 0.55f, 0.4f), 3.5f);
                        break;

                    case FeedbackKind.Suspicion:
                        Popups.Add(new Popup
                        {
                            World = head,
                            Text = "+" + e.Amount + " SUSPICION",
                            Colour = new Color(1f, 0.8f, 0.25f)
                        });
                        break;

                    case FeedbackKind.Alert:
                        PushToast(e.Text, new Color(1f, 0.75f, 0.2f), 5f);
                        break;

                    case FeedbackKind.Objective:
                        PushToast(e.Text, new Color(0.45f, 1f, 0.6f), 8f);
                        break;

                    case FeedbackKind.Payoff:
                        PushToast(e.Text, new Color(0.6f, 0.9f, 1f), 5f);
                        break;

                    default:
                        PushToast(e.Text, Color.white, 4f);
                        break;
                }
            }
        }

        private Vector3 HeadPosition(string actorId)
        {
            if (actorId.Length == 0) return Player.transform.position + Vector3.up * 2f;

            NpcView view;
            if (View.NpcViews.TryGetValue(actorId, out view) && view.Rig != null)
            {
                return view.Rig.HeadWorldPosition + Vector3.up * 0.45f;
            }

            return LevelView.ToUnity(Sim.ActorPosition(actorId)) + Vector3.up * 2.1f;
        }

        private void TickPopups(float dt)
        {
            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                Popups[i].Age += dt;
                Popups[i].World += Vector3.up * dt * 0.6f;
                if (Popups[i].Age >= Popups[i].Life) Popups.RemoveAt(i);
            }

            for (int i = Toasts.Count - 1; i >= 0; i--)
            {
                Toasts[i].Age += dt;
                if (Toasts[i].Age >= Toasts[i].Life) Toasts.RemoveAt(i);
            }
        }

        public void PushToast(string text, Color colour, float life)
        {
            if (text.Length == 0) return;

            // Collapse repeats so a chatty moment does not bury the screen.
            for (int i = 0; i < Toasts.Count; i++)
            {
                if (Toasts[i].Text == text)
                {
                    Toasts[i].Age = 0f;
                    return;
                }
            }

            Toasts.Add(new Toast { Text = text, Colour = colour, Life = life });
            if (Toasts.Count > 4) Toasts.RemoveAt(0);
        }

        /// <summary>
        /// Staged hints for the first minute. A systemic game is unreadable if
        /// nobody tells you what the verbs are; after that it gets out of the way.
        /// </summary>
        private void TickHints(float dt)
        {
            if (_hintStage > 3 || Sim.Outcome != GameOutcome.InProgress) return;

            _hintTimer -= dt;
            if (_hintTimer > 0f) return;

            switch (_hintStage)
            {
                case 0:
                    PushToast("Walk up to things and press E. Orange options are sabotage.",
                        new Color(0.8f, 0.9f, 1f), 7f);
                    _hintTimer = 14f;
                    break;
                case 1:
                    PushToast("Hold SHIFT to sneak. Green cones are what people can see.",
                        new Color(0.8f, 0.9f, 1f), 7f);
                    _hintTimer = 16f;
                    break;
                case 2:
                    PushToast("Most sabotage pays off later - set it up, then be somewhere else.",
                        new Color(0.8f, 0.9f, 1f), 8f);
                    _hintTimer = 18f;
                    break;
                case 3:
                    PushToast("Stuck? Ring the bell to pull people out of the kitchen.",
                        new Color(0.8f, 0.9f, 1f), 7f);
                    break;
            }

            _hintStage++;
        }

        public float HighestSuspicion()
        {
            float highest = 0f;
            for (int i = 0; i < Sim.World.Npcs.Count; i++)
            {
                float s = Sim.World.Npcs[i].SuspicionOf(PlayerAvatar.PlayerId);
                if (s > highest) highest = s;
            }
            return highest;
        }

        // ------------------------------------------------------------------
        // Interactions
        // ------------------------------------------------------------------

        /// <summary>
        /// What the player is about to act on: the nearest thing roughly in
        /// front of them. Picking a target for the player beats making them
        /// read a list every time.
        /// </summary>
        private InteractionOption ChooseFocus()
        {
            if (_interactions.Count == 0) return null;

            Vector3 forward = Player.transform.forward;
            InteractionOption best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < _interactions.Count; i++)
            {
                InteractionOption option = _interactions[i];
                Vector3 toObject = LevelView.ToUnity(option.Object.Position) - Player.transform.position;
                toObject.y = 0f;
                if (toObject.sqrMagnitude < 0.0001f) return option;

                float facing = Vector3.Dot(forward.normalized, toObject.normalized);
                float score = facing * 2f - option.Distance * 0.35f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = option;
                }
            }

            return best;
        }

        /// <summary>Every interaction available on whatever the player is focused on.</summary>
        public List<InteractionOption> FocusedOptions()
        {
            List<InteractionOption> options = new List<InteractionOption>();
            if (Focus == null) return options;

            for (int i = 0; i < _interactions.Count; i++)
            {
                if (_interactions[i].Object.Id == Focus.Object.Id) options.Add(_interactions[i]);
            }
            return options;
        }

        /// <summary>
        /// Interactions take time and must be finished in place. That window is
        /// where the stealth actually happens: anyone who walks in while you are
        /// halfway through emptying a fridge has seen you do it.
        /// </summary>
        public void BeginInteraction(InteractionOption option)
        {
            if (option == null || Sim.Outcome != GameOutcome.InProgress) return;
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
