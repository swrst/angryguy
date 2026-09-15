using System.Collections.Generic;
using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// IMGUI heads-up display. Deliberately not uGUI: OnGUI needs no canvas, no
    /// prefabs, no fonts and no scene setup, so the prototype has zero asset
    /// dependencies. Replace with uGUI/UI Toolkit once the design settles.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private SimRunner _runner;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _title;
        private GUIStyle _centred;
        private bool _showDebug;

        private void Awake()
        {
            _runner = GetComponent<SimRunner>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) _showDebug = !_showDebug;

            if (_runner != null && _runner.Sim != null &&
                _runner.Sim.Outcome != GameOutcome.InProgress)
            {
                if (Input.GetKeyDown(KeyCode.R)) _runner.Restart();
                if (Input.GetKeyDown(KeyCode.T)) _runner.Retry();
            }
        }

        private void EnsureStyles()
        {
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = 14;
            _label.normal.textColor = Color.white;

            _small = new GUIStyle(_label);
            _small.fontSize = 11;
            _small.normal.textColor = new Color(0.82f, 0.82f, 0.86f);

            _title = new GUIStyle(_label);
            _title.fontSize = 18;
            _title.fontStyle = FontStyle.Bold;

            _centred = new GUIStyle(_title);
            _centred.alignment = TextAnchor.MiddleCenter;
            _centred.fontSize = 26;
        }

        private void OnGUI()
        {
            if (_runner == null || _runner.Sim == null) return;

            EnsureStyles();

            Simulation sim = _runner.Sim;
            HudSnapshot hud = sim.BuildHud();

            DrawObjectivePanel(hud);
            DrawInteractionPanel();
            DrawFeed(hud);
            DrawWorldLabels(sim);

            if (_showDebug) DrawDebugPanel(sim);
            if (hud.Outcome != GameOutcome.InProgress) DrawOutcome(hud);
        }

        // ------------------------------------------------------------------

        private void DrawObjectivePanel(HudSnapshot hud)
        {
            GUI.Box(new Rect(12f, 12f, 300f, 132f), GUIContent.none);

            GUI.Label(new Rect(24f, 18f, 280f, 24f), "Target: " + hud.TargetName, _title);

            GUI.Label(new Rect(24f, 46f, 280f, 18f), "Anger", _small);
            Bar(new Rect(24f, 64f, 268f, 14f), hud.TargetAnger,
                new Color(0.35f, 0.7f, 0.4f), new Color(0.9f, 0.2f, 0.15f));
            Marker(new Rect(24f, 64f, 268f, 14f), hud.TargetPeakAnger);
            Marker(new Rect(24f, 64f, 268f, 14f), Simulation.AngerWinThreshold, new Color(1f, 1f, 1f, 0.55f));

            string suspicionLabel = hud.HighestSuspicionBy.Length > 0
                ? "Suspicion (" + hud.HighestSuspicionBy + ")"
                : "Suspicion";
            GUI.Label(new Rect(24f, 82f, 280f, 18f), suspicionLabel, _small);
            Bar(new Rect(24f, 100f, 268f, 14f), hud.HighestSuspicion,
                new Color(0.3f, 0.5f, 0.8f), new Color(0.95f, 0.55f, 0.1f));

            string status = hud.ObjectiveMet
                ? "He's lost it. Get to the back door."
                : string.Format("{0:0}s left", hud.TimeRemaining);
            GUI.Label(new Rect(24f, 118f, 280f, 18f), status, _small);
        }

        private void DrawInteractionPanel()
        {
            IReadOnlyList<InteractionOption> options = _runner.Interactions;

            if (_runner.Channelling != null)
            {
                float duration = Mathf.Max(0.35f, _runner.Channelling.Affordance.Duration);
                float t = Mathf.Clamp01(_runner.ChannelProgress / duration);

                float width = 320f;
                Rect box = new Rect((Screen.width - width) * 0.5f, Screen.height - 120f, width, 54f);
                GUI.Box(box, GUIContent.none);
                GUI.Label(new Rect(box.x + 12f, box.y + 8f, width - 24f, 20f),
                    _runner.Channelling.Label + "...", _label);
                Bar(new Rect(box.x + 12f, box.y + 30f, width - 24f, 12f), t,
                    new Color(0.8f, 0.7f, 0.2f), new Color(0.4f, 0.85f, 0.45f));
                return;
            }

            if (options.Count == 0) return;

            float panelHeight = 26f + options.Count * 20f;
            Rect panel = new Rect(Screen.width - 332f, Screen.height - panelHeight - 16f, 320f, panelHeight);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 4f, 300f, 18f), "Within reach", _small);

            for (int i = 0; i < options.Count && i < 9; i++)
            {
                InteractionOption option = options[i];

                GUI.color = option.IsSabotage ? new Color(1f, 0.65f, 0.4f) : Color.white;
                GUI.Label(new Rect(panel.x + 12f, panel.y + 22f + i * 20f, 300f, 18f),
                    "[" + (i + 1) + "] " + option.Label, _label);
                GUI.color = Color.white;
            }
        }

        private void DrawFeed(HudSnapshot hud)
        {
            int count = Mathf.Min(6, hud.Feed.Count);
            if (count == 0) return;

            float height = 12f + count * 17f;
            Rect panel = new Rect(12f, Screen.height - height - 16f, 430f, height);
            GUI.Box(panel, GUIContent.none);

            for (int i = 0; i < count; i++)
            {
                string line = hud.Feed[hud.Feed.Count - count + i];
                GUI.color = new Color(1f, 1f, 1f, 0.45f + 0.55f * ((i + 1f) / count));
                GUI.Label(new Rect(panel.x + 10f, panel.y + 6f + i * 17f, panel.width - 20f, 16f),
                    line, _small);
            }
            GUI.color = Color.white;
        }

        /// <summary>Names, moods and speech drawn over each NPC in screen space.</summary>
        private void DrawWorldLabels(Simulation sim)
        {
            Camera camera = _runner.Player != null ? _runner.Player.Camera : Camera.main;
            if (camera == null) return;

            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];

                Vector3 world = LevelView.ToUnity(npc.Position) + Vector3.up * 2.1f;
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;

                float x = screen.x - 100f;
                float y = Screen.height - screen.y;

                GUI.color = npc.IsTarget ? new Color(1f, 0.9f, 0.5f) : Color.white;
                GUI.Label(new Rect(x, y, 200f, 18f),
                    npc.Name + " - " + npc.MoodWord, Centered(_small));

                if (npc.Speech.Length > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.92f);
                    GUI.Label(new Rect(x - 40f, y - 18f, 280f, 18f), "\"" + npc.Speech + "\"",
                        Centered(_small));
                }

                GUI.color = Color.white;
            }
        }

        private GUIStyle Centered(GUIStyle source)
        {
            GUIStyle style = new GUIStyle(source);
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        private void DrawDebugPanel(Simulation sim)
        {
            float width = 430f;
            float height = 40f + sim.World.Npcs.Count * 52f;
            Rect panel = new Rect(Screen.width - width - 12f, 12f, width, height);
            GUI.Box(panel, GUIContent.none);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, width - 24f, 20f),
                "AI debug (Tab)   t=" + Mathf.RoundToInt(sim.Time) + "s   seed " + _runner.Seed, _small);

            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];
                float y = panel.y + 32f + i * 52f;

                GUI.Label(new Rect(panel.x + 12f, y, width - 24f, 18f),
                    string.Format("{0} ({1})  {2}", npc.Name, npc.Role,
                        npc.Activity + (npc.CurrentPlan != null ? ": " + npc.CurrentPlan.Describe() : "")),
                    _small);

                GUI.Label(new Rect(panel.x + 12f, y + 16f, width - 24f, 18f),
                    string.Format("anger {0:0.00}  tension {1:0.00}  suspects you {2:0.00}  fails {3}",
                        npc.Anger, npc.Tension, npc.SuspicionOf(PlayerAvatar.PlayerId), npc.FrustrationCount),
                    _small);

                GUI.Label(new Rect(panel.x + 12f, y + 32f, width - 24f, 18f),
                    "needs " + npc.Needs + "   wants: " + npc.Needs.MostUrgent(), _small);
            }
        }

        private void DrawOutcome(HudSnapshot hud)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string headline;
            string detail;

            switch (hud.Outcome)
            {
                case GameOutcome.Won:
                    headline = "CLEAN GETAWAY";
                    detail = hud.TargetName + " is beside himself and hasn't the faintest idea it was you.";
                    break;
                case GameOutcome.Caught:
                    headline = "RUMBLED";
                    detail = "Somebody worked out exactly what you were doing.";
                    break;
                default:
                    headline = "SERVICE OVER";
                    detail = "Everyone went home only mildly irritated.";
                    break;
            }

            GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 40f), headline, _centred);
            GUI.Label(new Rect(0f, Screen.height * 0.4f + 42f, Screen.width, 24f), detail, Centered(_label));
            GUI.Label(new Rect(0f, Screen.height * 0.4f + 76f, Screen.width, 24f),
                "R - new restaurant     T - same seed again", Centered(_small));
        }

        // ------------------------------------------------------------------

        private static void Bar(Rect rect, float value, Color low, Color high)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = Color.Lerp(low, high, Mathf.Clamp01(value));
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height),
                Texture2D.whiteTexture);

            GUI.color = Color.white;
        }

        private static void Marker(Rect rect, float value)
        {
            Marker(rect, value, new Color(1f, 1f, 1f, 0.8f));
        }

        private static void Marker(Rect rect, float value, Color colour)
        {
            if (value <= 0f) return;

            GUI.color = colour;
            GUI.DrawTexture(new Rect(rect.x + rect.width * Mathf.Clamp01(value) - 1f, rect.y, 2f, rect.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
