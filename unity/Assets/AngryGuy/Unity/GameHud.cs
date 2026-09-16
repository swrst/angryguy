using System.Collections.Generic;
using AngryGuy.Core;
using UnityEngine;

namespace AngryGuy.UnityLayer
{
    /// <summary>
    /// Everything the player reads.
    ///
    /// The prototype's blocking problem was not the simulation, it was that none
    /// of it surfaced: no numbers, no names, no cause and effect. This draws the
    /// two meters that matter, labels every NPC with what they are doing and how
    /// suspicious they are, floats the exact anger and suspicion each action
    /// caused over the person it happened to, and ends the level by showing the
    /// player what they actually did.
    ///
    /// Still IMGUI: no canvas, no prefabs, no font assets, no scene setup.
    /// Replace with uGUI once the design stops moving.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private SimRunner _runner;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _tiny;
        private GUIStyle _title;
        private GUIStyle _huge;
        private bool _showDebug;
        private bool _showControls = true;

        private static readonly Color AngerLow = new Color(0.35f, 0.72f, 0.42f);
        private static readonly Color AngerHigh = new Color(0.95f, 0.22f, 0.16f);
        private static readonly Color SuspicionLow = new Color(0.35f, 0.55f, 0.85f);
        private static readonly Color SuspicionHigh = new Color(1f, 0.5f, 0.1f);

        private void Awake()
        {
            _runner = GetComponent<SimRunner>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) _showDebug = !_showDebug;
            if (Input.GetKeyDown(KeyCode.H)) _showControls = !_showControls;

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
            _label.wordWrap = true;

            _small = new GUIStyle(_label);
            _small.fontSize = 12;

            _tiny = new GUIStyle(_label);
            _tiny.fontSize = 10;

            _title = new GUIStyle(_label);
            _title.fontSize = 18;
            _title.fontStyle = FontStyle.Bold;

            _huge = new GUIStyle(_title);
            _huge.fontSize = 30;
            _huge.alignment = TextAnchor.MiddleCenter;
        }

        private void OnGUI()
        {
            if (_runner == null || _runner.Sim == null) return;
            EnsureStyles();

            Simulation sim = _runner.Sim;

            if (_runner.InMenu)
            {
                DrawMenu();
                return;
            }

            DrawObjectivePanel(sim);
            DrawTrapPanel(sim);
            DrawWatchers(sim);
            DrawWorldLabels(sim);
            DrawPopups();
            DrawPrompt(sim);
            DrawToasts();
            if (_showControls) DrawControls();
            if (_showDebug) DrawDebugPanel(sim);
            if (sim.Outcome != GameOutcome.InProgress) DrawOutcome(sim);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Start screen. The level is already built and frozen behind it, so
        /// Play is instantaneous and the menu doubles as a look at the room.
        /// </summary>
        private void DrawMenu()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = 480f;
            float height = 372f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f,
                width, height);
            Panel(panel);

            GUI.color = new Color(1f, 0.85f, 0.45f);
            GUI.Label(new Rect(panel.x, panel.y + 22f, width, 44f), "ANGRY GUY", _huge);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x, panel.y + 66f, width, 22f),
                "Make one man furious. Don't let anyone work out it was you.", Centred(_small));

            float y = panel.y + 108f;

            if (GUI.Button(new Rect(panel.x + 120f, y, 240f, 40f), "PLAY  -  The Restaurant"))
            {
                _runner.BeginPlay();
            }

            y += 50f;
            GUI.Label(new Rect(panel.x, y, width, 20f), "seed " + _runner.Seed, Centred(_small));

            y += 24f;
            if (GUI.Button(new Rect(panel.x + 120f, y, 115f, 28f), "New seed"))
            {
                _runner.StartLevel(Random.Range(1, int.MaxValue));
            }

            if (GUI.Button(new Rect(panel.x + 245f, y, 115f, 28f), "Quit"))
            {
                Application.Quit();
            }

            y += 46f;
            string[] help =
            {
                "WASD move     SHIFT sneak     E interact     1-9 pick option",
                "T throw held item     F hide     Q cancel     V camera",
                "TAB debug overlay     H toggle controls",
                "",
                "Orange options are sabotage. Most of it pays off on a delay -",
                "set it up, then make sure you're somewhere else when it lands."
            };

            for (int i = 0; i < help.Length; i++)
            {
                GUI.Label(new Rect(panel.x + 20f, y + i * 17f, width - 40f, 16f),
                    help[i], Centred(_tiny));
            }
        }

        private void DrawObjectivePanel(Simulation sim)
        {
            Npc target = sim.Target;
            if (target == null) return;

            Rect panel = new Rect(14f, 14f, 330f, 166f);
            Panel(panel);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, 310f, 24f),
                "MAKE " + target.Name.ToUpperInvariant() + " FURIOUS", _title);

            // Anger, with the current value, the best reached, and the bar to clear.
            GUI.Label(new Rect(panel.x + 12f, panel.y + 36f, 200f, 18f),
                "Anger  " + Simulation.ToDisplay(target.Anger) + " / 100", _small);

            Rect angerBar = new Rect(panel.x + 12f, panel.y + 56f, 306f, 16f);
            Bar(angerBar, target.Anger, AngerLow, AngerHigh);
            Marker(angerBar, target.PeakAnger, new Color(1f, 1f, 1f, 0.75f));
            Marker(angerBar, Simulation.AngerWinThreshold, new Color(0.4f, 1f, 0.55f, 0.95f));

            float highest = _runner.HighestSuspicion();
            string who = MostSuspicious(sim);
            SuspicionTier tier = Suspicion.TierFor(highest);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 78f, 310f, 18f),
                who.Length > 0
                    ? "Suspicion  " + Simulation.ToDisplay(highest) + "  (" + who + ": " + Suspicion.Label(tier) + ")"
                    : "Suspicion  0  (nobody has noticed you)",
                _small);

            Rect suspicionBar = new Rect(panel.x + 12f, panel.y + 98f, 306f, 16f);
            Bar(suspicionBar, highest, SuspicionLow, SuspicionHigh);
            Marker(suspicionBar, Simulation.BlownCoverThreshold, new Color(1f, 0.85f, 0.2f, 0.9f));

            string status = sim.ObjectiveMet
                ? "DONE - now get out the back door"
                : Mathf.RoundToInt(Mathf.Max(0f, sim.TimeLimit - sim.Time)) + "s left";

            GUI.color = sim.ObjectiveMet ? new Color(0.45f, 1f, 0.6f) : Color.white;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 120f, 200f, 20f), status, _small);

            // What you are wearing decides where you can stand without it
            // slowly costing you, so it belongs on screen.
            GUI.color = sim.Player.Outfit.Id == "civilian"
                ? new Color(0.8f, 0.8f, 0.85f)
                : new Color(0.55f, 0.9f, 1f);
            GUI.Label(new Rect(panel.x + 150f, panel.y + 120f, 170f, 20f),
                sim.Player.Outfit.Name, _small);
            GUI.color = Color.white;

            // The whole building getting jumpy is the difficulty curve. Only show
            // it once it actually matters.
            if (sim.Unease > 0.15f)
            {
                GUI.color = Color.Lerp(new Color(0.8f, 0.8f, 0.5f), new Color(1f, 0.4f, 0.3f), sim.Unease);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 140f, 310f, 20f),
                    sim.Unease > 0.6f
                        ? "The staff know something is going on"
                        : "People are starting to get twitchy",
                    _small);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Everything the player has set in motion that has not landed yet.
        ///
        /// Without this the player sets six traps and loses track of which ones
        /// somebody has already quietly undone, so half the level is spent
        /// waiting on sabotage that stopped existing minutes ago. Knowing what
        /// is still armed is what makes the waiting a decision rather than a
        /// guess.
        /// </summary>
        private void DrawTrapPanel(Simulation sim)
        {
            IReadOnlyList<Trap> traps = sim.Traps.All;
            if (traps.Count == 0) return;

            int shown = 0;
            for (int i = traps.Count - 1; i >= 0 && shown < 6; i--)
            {
                if (traps[i].Sprung) continue;
                shown++;
            }
            if (shown == 0) return;

            float height = 30f + shown * 19f;
            Rect panel = new Rect(14f, 192f, 330f, height);
            Panel(panel);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, 310f, 20f),
                "SET UP  (" + sim.Traps.ArmedCount + " armed)", _title);

            float y = panel.y + 28f;
            int drawn = 0;

            for (int i = traps.Count - 1; i >= 0 && drawn < 6; i--)
            {
                Trap trap = traps[i];
                if (trap.Sprung) continue;

                GUI.color = trap.Defused
                    ? new Color(0.55f, 0.55f, 0.58f)
                    : new Color(0.95f, 0.82f, 0.45f);

                string line = trap.Defused
                    ? trap.Label + " - undone"
                    : trap.Label + (trap.WaitingForName.Length > 0 ? "  (" + trap.WaitingForName + ")" : "");

                GUI.Label(new Rect(panel.x + 12f, y, 310f, 18f), line, _small);
                y += 19f;
                drawn++;
            }

            GUI.color = Color.white;
        }

        private string MostSuspicious(Simulation sim)
        {
            string who = "";
            float highest = 0f;
            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                float s = sim.World.Npcs[i].SuspicionOf(PlayerAvatar.PlayerId);
                if (s > highest)
                {
                    highest = s;
                    who = sim.World.Npcs[i].Name;
                }
            }
            return who;
        }

        /// <summary>
        /// The single most important readout in a stealth game: am I being
        /// looked at right now?
        /// </summary>
        private void DrawWatchers(Simulation sim)
        {
            if (sim.Player.IsHidden)
            {
                Banner("HIDDEN - press F to come out", new Color(0.45f, 0.85f, 1f));
                return;
            }

            List<Npc> watchers = sim.WatchersOfPlayer();
            if (watchers.Count == 0) return;

            string names = "";
            for (int i = 0; i < watchers.Count; i++)
            {
                if (i > 0) names += ", ";
                names += watchers[i].Name;
            }

            Banner(names + " can see you", new Color(1f, 0.72f, 0.25f));
        }

        private void Banner(string text, Color colour)
        {
            float width = 420f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, 18f, width, 26f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = colour;
            GUIStyle centred = new GUIStyle(_label);
            centred.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, text, centred);
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------

        private void DrawWorldLabels(Simulation sim)
        {
            Camera camera = _runner.Player != null ? _runner.Player.Camera : Camera.main;
            if (camera == null) return;

            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];

                Vector3 world = LevelView.ToUnity(npc.Position) + Vector3.up * 2.15f;
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;

                float x = screen.x - 120f;
                float y = Screen.height - screen.y;

                GUIStyle centred = new GUIStyle(_small);
                centred.alignment = TextAnchor.MiddleCenter;

                // Glyph: the at-a-glance "this one has noticed something".
                if (npc.StatusGlyph.Length > 0)
                {
                    GUIStyle glyph = new GUIStyle(_title);
                    glyph.alignment = TextAnchor.MiddleCenter;
                    GUI.color = npc.Anger >= 0.85f ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.85f, 0.3f);
                    GUI.Label(new Rect(x, y - 40f, 240f, 22f), npc.StatusGlyph, glyph);
                }

                GUI.color = npc.IsTarget ? new Color(1f, 0.88f, 0.45f) : new Color(0.92f, 0.92f, 0.95f);
                GUI.Label(new Rect(x, y - 18f, 240f, 18f),
                    npc.Name + (npc.IsTarget ? "  [TARGET]" : ""), centred);

                GUI.color = AngerTint(npc.Anger);
                GUI.Label(new Rect(x, y, 240f, 16f),
                    npc.MoodWord + " - " + npc.StatusLabel, Centred(_tiny));

                float suspicion = npc.SuspicionOf(PlayerAvatar.PlayerId);
                if (suspicion > 0.05f)
                {
                    GUI.color = Color.Lerp(SuspicionLow, SuspicionHigh, suspicion);
                    GUI.Label(new Rect(x, y + 14f, 240f, 16f),
                        "suspects you " + Simulation.ToDisplay(suspicion), Centred(_tiny));
                }

                if (npc.Speech.Length > 0)
                {
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x - 40f, y - 60f, 320f, 20f),
                        "\"" + npc.Speech + "\"", Centred(_small));
                }

                GUI.color = Color.white;
            }
        }

        private void DrawPopups()
        {
            Camera camera = _runner.Player != null ? _runner.Player.Camera : Camera.main;
            if (camera == null) return;

            GUIStyle style = new GUIStyle(_label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;

            for (int i = 0; i < _runner.Popups.Count; i++)
            {
                Popup popup = _runner.Popups[i];

                Vector3 screen = camera.WorldToScreenPoint(popup.World);
                if (screen.z <= 0f) continue;

                float fade = 1f - Mathf.Clamp01(popup.Age / popup.Life);
                GUI.color = new Color(popup.Colour.r, popup.Colour.g, popup.Colour.b, fade);
                GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 30f, 200f, 22f),
                    popup.Text, style);
            }

            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------

        private void DrawPrompt(Simulation sim)
        {
            if (_runner.Channelling != null)
            {
                float duration = Mathf.Max(0.35f, _runner.Channelling.Affordance.Duration);
                float t = Mathf.Clamp01(_runner.ChannelProgress / duration);

                float width = 340f;
                Rect box = new Rect((Screen.width - width) * 0.5f, Screen.height - 150f, width, 56f);
                Panel(box);

                GUI.Label(new Rect(box.x + 12f, box.y + 8f, width - 24f, 20f),
                    _runner.Channelling.Label + "...", Centred(_label));
                Bar(new Rect(box.x + 12f, box.y + 32f, width - 24f, 12f), t,
                    new Color(0.85f, 0.7f, 0.2f), new Color(0.4f, 0.9f, 0.5f));
                return;
            }

            List<InteractionOption> options = _runner.FocusedOptions();
            if (options.Count == 0) return;

            float panelWidth = 400f;
            float height = 30f + options.Count * 20f;
            Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, Screen.height - height - 118f,
                panelWidth, height);
            Panel(panel);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panelWidth - 24f, 18f),
                options[0].Object.Name, _small);

            for (int i = 0; i < options.Count && i < 9; i++)
            {
                InteractionOption option = options[i];
                GUI.color = option.IsSabotage ? new Color(1f, 0.62f, 0.35f) : Color.white;

                string key = i == 0 ? "[E]" : "[" + (i + 1) + "]";
                GUI.Label(new Rect(panel.x + 12f, panel.y + 26f + i * 20f, panelWidth - 24f, 18f),
                    key + "  " + option.Label + (option.IsSabotage ? "   (sabotage)" : ""), _label);
            }

            GUI.color = Color.white;
        }

        private void DrawToasts()
        {
            int count = _runner.Toasts.Count;
            if (count == 0) return;

            float width = 560f;
            for (int i = 0; i < count; i++)
            {
                Toast toast = _runner.Toasts[i];
                float fade = Mathf.Clamp01(Mathf.Min(toast.Age * 3f, (toast.Life - toast.Age) * 2f));

                Rect rect = new Rect((Screen.width - width) * 0.5f,
                    Screen.height - 92f + (i - count + 1) * 22f, width, 20f);

                GUI.color = new Color(toast.Colour.r, toast.Colour.g, toast.Colour.b, fade);
                GUI.Label(rect, toast.Text, Centred(_small));
            }

            GUI.color = Color.white;
        }

        private void DrawControls()
        {
            string[] lines =
            {
                "WASD move     SHIFT sneak     V camera",
                "E / 1-9 interact     Q cancel",
                "T throw held item     F hide",
                "TAB debug     H hide this"
            };

            float width = 250f;
            Rect panel = new Rect(Screen.width - width - 14f, Screen.height - 96f, width, 82f);
            Panel(panel);

            for (int i = 0; i < lines.Length; i++)
            {
                GUI.Label(new Rect(panel.x + 10f, panel.y + 6f + i * 18f, width - 20f, 16f),
                    lines[i], _tiny);
            }
        }

        private void DrawDebugPanel(Simulation sim)
        {
            float width = 440f;
            float height = 44f + sim.World.Npcs.Count * 54f;
            Rect panel = new Rect(Screen.width - width - 14f, 14f, width, height);
            Panel(panel);

            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, width - 24f, 20f),
                "AI debug   t=" + Mathf.RoundToInt(sim.Time) + "s   seed " + _runner.Seed
                + "   pending traps: " + sim.Pending.Count, _small);

            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];
                float y = panel.y + 34f + i * 54f;

                GUI.Label(new Rect(panel.x + 12f, y, width - 24f, 18f),
                    npc.Name + " (" + npc.Role + ")  " + npc.Activity + " - " + npc.StatusLabel, _tiny);

                GUI.Label(new Rect(panel.x + 12f, y + 16f, width - 24f, 18f),
                    string.Format("anger {0}  tension {1}  suspects you {2}  failed plans {3}",
                        Simulation.ToDisplay(npc.Anger), Simulation.ToDisplay(npc.Tension),
                        Simulation.ToDisplay(npc.SuspicionOf(PlayerAvatar.PlayerId)), npc.FrustrationCount),
                    _tiny);

                GUI.Label(new Rect(panel.x + 12f, y + 32f, width - 24f, 18f),
                    "needs " + npc.Needs + "   wants " + npc.Needs.MostUrgent(), _tiny);
            }
        }

        // ------------------------------------------------------------------

        private void DrawOutcome(Simulation sim)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string headline;
            string detail;
            Color tint;

            switch (sim.Outcome)
            {
                case GameOutcome.Won:
                    headline = "CLEAN GETAWAY";
                    detail = (sim.Target != null ? sim.Target.Name : "The target")
                             + " is beside himself and hasn't the faintest idea it was you.";
                    tint = new Color(0.5f, 1f, 0.6f);
                    break;
                case GameOutcome.Caught:
                    headline = "RUMBLED";
                    detail = "Somebody worked out exactly what you were doing.";
                    tint = new Color(1f, 0.45f, 0.35f);
                    break;
                default:
                    headline = "SERVICE OVER";
                    detail = "Everyone went home only mildly irritated.";
                    tint = new Color(0.8f, 0.8f, 0.85f);
                    break;
            }

            float top = Screen.height * 0.16f;

            GUI.color = tint;
            GUI.Label(new Rect(0f, top, Screen.width, 44f), headline, _huge);
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, top + 46f, Screen.width, 24f), detail, Centred(_label));

            // "How did you do that" - the recap that teaches players what worked.
            float width = 620f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, top + 86f, width,
                70f + Mathf.Min(sim.PlayerActions.Count, 12) * 20f);
            Panel(panel);

            GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, width - 32f, 20f), "What you did", _title);

            int shown = Mathf.Min(sim.PlayerActions.Count, 12);
            int start = sim.PlayerActions.Count - shown;

            for (int i = 0; i < shown; i++)
            {
                PlayerAction action = sim.PlayerActions[start + i];
                GUI.color = action.Sabotage ? new Color(1f, 0.68f, 0.4f) : new Color(0.85f, 0.87f, 0.92f);
                GUI.Label(new Rect(panel.x + 16f, panel.y + 36f + i * 20f, width - 32f, 18f),
                    string.Format("{0,4:0}s   {1}{2}",
                        action.Time, action.Label, action.Witnessed ? "   (someone saw you)" : ""),
                    _small);
            }

            GUI.color = Color.white;

            Npc target = sim.Target;
            string summary = string.Format(
                "Peak anger {0}    highest suspicion {1}    {2} actions in {3}s",
                target != null ? Simulation.ToDisplay(target.PeakAnger) : 0,
                Simulation.ToDisplay(_runner.HighestSuspicion()),
                sim.PlayerActions.Count,
                Mathf.RoundToInt(sim.Time));

            GUI.Label(new Rect(panel.x, panel.y + panel.height - 26f, width, 20f),
                summary, Centred(_small));

            GUI.Label(new Rect(0f, panel.y + panel.height + 14f, Screen.width, 22f),
                "R - new restaurant     T - same seed again", Centred(_small));
        }

        // ------------------------------------------------------------------

        private GUIStyle Centred(GUIStyle source)
        {
            GUIStyle style = new GUIStyle(source);
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        private static void Panel(Rect rect)
        {
            GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.82f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static Color AngerTint(float anger)
        {
            return anger < 0.5f
                ? Color.Lerp(new Color(0.75f, 0.85f, 0.78f), new Color(1f, 0.85f, 0.35f), anger / 0.5f)
                : Color.Lerp(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.35f, 0.3f), (anger - 0.5f) / 0.5f);
        }

        private static void Bar(Rect rect, float value, Color low, Color high)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = Color.Lerp(low, high, Mathf.Clamp01(value));
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height),
                Texture2D.whiteTexture);

            GUI.color = Color.white;
        }

        private static void Marker(Rect rect, float value, Color colour)
        {
            if (value <= 0f) return;

            GUI.color = colour;
            GUI.DrawTexture(new Rect(rect.x + rect.width * Mathf.Clamp01(value) - 1f, rect.y - 2f, 2f,
                rect.height + 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
