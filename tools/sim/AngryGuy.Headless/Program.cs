using System;
using System.Collections.Generic;
using System.Globalization;
using AngryGuy.Core;

namespace AngryGuy.Headless
{
    /// <summary>
    /// Terminal front-end for the simulation.
    ///
    ///   dotnet run                  play it
    ///   dotnet run -- watch 300     watch the NPCs with no player at all
    ///   dotnet run -- seed 42       pick a seed
    /// </summary>
    public static class Program
    {
        private const float Dt = 0.1f;

        private static Simulation _sim;
        private static List<InteractionOption> _lastLook = new List<InteractionOption>();

        /// <summary>Exactly what the player last saw numbered on screen.</summary>
        private static readonly List<string> _shownLabels = new List<string>();

        public static int Main(string[] args)
        {
            int seed = Environment.TickCount;
            bool watchOnly = false;
            float watchSeconds = 240f;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant().TrimStart('-');
                if (a == "seed" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out seed);
                    i++;
                }
                else if (a == "watch")
                {
                    watchOnly = true;
                    if (i + 1 < args.Length &&
                        float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
                    {
                        watchSeconds = s;
                        i++;
                    }
                }
            }

            bool crimeMode = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLowerInvariant().TrimStart('-') == "crime") crimeMode = true;
            }

            // The kitchen is the old systems sandbox and still useful for
            // stress-testing six NPCs at once. Marking Hour is the actual game.
            bool kitchen = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLowerInvariant().TrimStart('-') == "kitchen") kitchen = true;
            }

            if (kitchen)
            {
                _sim = KitchenLevel.Build(seed);
                Console.WriteLine("ANGRY GUY - kitchen sandbox (seed " + seed + ")");
            }
            else
            {
                _sim = MarkingHour.Build(seed);
                PrintBriefing();
            }
            Console.WriteLine();

            if (crimeMode && !kitchen)
            {
                Console.WriteLine("  (crime mode is the kitchen sandbox - add --kitchen)");
                return 1;
            }
            if (crimeMode) return Crime(watchSeconds);
            if (watchOnly) return Watch(watchSeconds);

            Console.WriteLine("Type 'help' for commands.");
            Console.WriteLine();

            PrintMap();
            return Play();
        }

        // ------------------------------------------------------------------
        // Observation mode
        // ------------------------------------------------------------------

        private static int Watch(float seconds)
        {
            Console.WriteLine("Watching for " + seconds + "s with no player input.");
            Console.WriteLine("This is the ambient simulation: routines, needs, chatter, tidying up.");
            Console.WriteLine();

            // Park the player outside everyone's world so nothing is attributed to them.
            _sim.Player.Position = new Vec3(9.5f, 0f, -9.5f);

            // Time-use tally. The fastest way to spot an NPC that has stopped
            // behaving like its job title.
            Dictionary<string, float> timeUse = new Dictionary<string, float>();

            int printed = 0;
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                _sim.Tick(Dt);

                for (int n = 0; n < _sim.World.Npcs.Count; n++)
                {
                    Npc npc = _sim.World.Npcs[n];
                    string key = npc.Name + " | " +
                                 (npc.Activity == NpcActivity.Using && npc.CurrentPlan != null
                                     ? npc.CurrentPlan.Describe()
                                     : npc.Activity.ToString());

                    float existing;
                    timeUse.TryGetValue(key, out existing);
                    timeUse[key] = existing + Dt;
                }

                FlushFeedback();
                while (printed < _sim.Feed.Count)
                {
                    Console.WriteLine("  " + _sim.Feed[printed]);
                    printed++;
                }
            }

            Console.WriteLine();
            PrintWho();

            Console.WriteLine();
            Console.WriteLine("  Where the time went:");
            List<KeyValuePair<string, float>> rows = new List<KeyValuePair<string, float>>(timeUse);
            rows.Sort(delegate(KeyValuePair<string, float> a, KeyValuePair<string, float> b)
            {
                return b.Value.CompareTo(a.Value);
            });
            for (int i = 0; i < rows.Count && i < 22; i++)
            {
                Console.WriteLine(string.Format("   {0,6:0}s  {1}", rows[i].Value, rows[i].Key));
            }

            return 0;
        }

        // ------------------------------------------------------------------
        // Scripted-run mode: designer replay for tuning the anger curve
        // ------------------------------------------------------------------

        private static int Crime(float seconds)
        {
            Console.WriteLine("Scripted sabotage run against Gordon, then " + seconds + "s of consequences.");
            Console.WriteLine();

            Npc chef = _sim.World.GetNpc(KitchenLevel.Ids.Chef);
            chef.Needs.Set(NeedType.Comfort, 0.05f);

            for (int i = 0; i < _sim.World.Npcs.Count; i++)
            {
                _sim.World.Npcs[i].Position = new Vec3(6f + i * 0.4f, 0f, 6f);
                _sim.World.Npcs[i].Facing = new Vec3(1f, 0f, 0f);
            }

            Do(KitchenLevel.Ids.Salt, "swap_salt");
            Do(KitchenLevel.Ids.Stove, "crank_heat");
            Do(KitchenLevel.Ids.Pan, "take");
            Do(KitchenLevel.Ids.Bin, "stash");
            Do(KitchenLevel.Ids.Oil, "pour_oil");
            Do(KitchenLevel.Ids.Fridge, "empty_fridge");

            _sim.Player.Position = new Vec3(6f, 0f, -6f);

            int printed = _sim.Feed.Count;
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                _sim.Tick(Dt);

                FlushFeedback();
                while (printed < _sim.Feed.Count)
                {
                    Console.WriteLine("  " + _sim.Feed[printed]);
                    printed++;
                }

                if (i % 300 == 0)
                {
                    Console.WriteLine(string.Format(
                        "  --- t={0,3:0}s  Gordon anger {1:0.00} peak {2:0.00} tension {3:0.00} frustrations {4} | {5}",
                        _sim.Time, chef.Anger, chef.PeakAnger, chef.Tension, chef.FrustrationCount,
                        chef.Activity + (chef.CurrentPlan != null ? ": " + chef.CurrentPlan.Describe() : "")));
                }
            }

            Console.WriteLine();
            PrintWho();
            Console.WriteLine();
            Console.WriteLine(string.Format("  Gordon peak anger: {0:0.00}   outcome: {1}",
                chef.PeakAnger, _sim.Outcome));
            return 0;
        }

        private static void Do(string objectId, string affordanceId)
        {
            SmartObject obj = _sim.World.GetObject(objectId);
            if (obj == null)
            {
                Console.WriteLine("  !! no object " + objectId);
                return;
            }

            _sim.Player.Position = obj.Position;
            bool ok = _sim.PlayerInteract(objectId, affordanceId);
            Console.WriteLine("  " + (ok ? "did" : "FAILED") + ": " + affordanceId + " on " + obj.Name);
        }

        // ------------------------------------------------------------------
        // Interactive mode
        // ------------------------------------------------------------------

        private static int Play()
        {
            int feedShown = _sim.Feed.Count;

            while (_sim.Outcome == GameOutcome.InProgress)
            {
                Console.Write(Prompt());
                string line = Console.ReadLine();
                if (line == null) break;

                line = line.Trim();
                if (line.Length == 0) continue;

                string[] parts = line.Split(' ');
                string cmd = parts[0].ToLowerInvariant();
                string arg = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : "";

                // Verbs people actually type. Rejecting "take 1" when option 1
                // is literally "Take the good pan" is the front-end being
                // pedantic at the player, which costs a run and teaches nothing.
                switch (cmd)
                {
                    case "take": case "do": case "u": case "grab": case "pick":
                        cmd = "use"; break;
                    case "l": case "examine": case "inspect":
                        cmd = "look"; break;
                    case "g": case "walk": case "goto":
                        cmd = "go"; break;
                    case "w": case "wait": case "stand":
                        cmd = "wait"; break;
                    case "t": cmd = "throw"; break;
                    case "h": cmd = "hide"; break;
                    case "q": cmd = "quit"; break;
                }

                // Typing "1" is what everyone tries first. Accept it.
                int bareNumber;
                if (int.TryParse(cmd, out bareNumber))
                {
                    arg = cmd;
                    cmd = "use";
                }

                switch (cmd)
                {
                    case "help": PrintHelp(); break;
                    case "map": PrintMap(); break;
                    case "look": PrintLook(); break;
                    case "who": PrintWho(); break;
                    case "log": PrintLog(); break;
                    case "traps": case "setup": case "plans": PrintTraps(); break;
                    case "jobs": case "todo": case "list": PrintJobs(); break;
                    case "brief": PrintBriefing(); break;
                    case "go": Go(arg); break;
                    case "use": Use(arg); break;
                    case "wait": Wait(arg); break;
                    case "exit": Go("exit"); break;
                    case "throw": Throw(); break;
                    case "hide": Hide(); break;
                    case "quit": return 0;
                    default:
                        Console.WriteLine("  ? try 'help'");
                        break;
                }

                feedShown = FlushFeed(feedShown);
            }

            Console.WriteLine();
            switch (_sim.Outcome)
            {
                case GameOutcome.Won:
                    Console.WriteLine("  *** CLEAN GETAWAY ***");
                    Console.WriteLine("  Gordon is beside himself and hasn't the faintest idea it was you.");
                    break;
                case GameOutcome.Caught:
                    Console.WriteLine("  *** RUMBLED ***");
                    Console.WriteLine("  Somebody worked out exactly what you were doing.");
                    break;
                case GameOutcome.TimeUp:
                    Console.WriteLine("  *** SERVICE ENDS ***");
                    Console.WriteLine("  Everyone goes home only mildly irritated. You have failed.");
                    break;
            }

            PrintWho();
            return 0;
        }

        private static string Prompt()
        {
            HudSnapshot hud = _sim.BuildHud();
            return string.Format(
                "[{0,3:0}s] {1} anger {2:0.00}{3} | most suspicious: {4} {5:0.00} > ",
                hud.Time,
                hud.TargetName,
                hud.TargetAnger,
                hud.ObjectiveMet ? " (DONE - get out)" : "",
                hud.HighestSuspicionBy.Length > 0 ? hud.HighestSuspicionBy : "-",
                hud.HighestSuspicion);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("  map            top-down view");
            Console.WriteLine("  look           what is within reach, and what you can do to it");
            Console.WriteLine("  use <n>        do interaction n from the last 'look'");
            Console.WriteLine("  go <name>      walk to an object, npc or zone (e.g. 'go salt', 'go exit')");
            Console.WriteLine("  wait <sec>     stand still and let things happen");
            Console.WriteLine("  throw          lob whatever you are holding - noise lands over there");
            Console.WriteLine("  hide           duck into a hiding spot you are standing next to");
            Console.WriteLine("  jobs           the to-do list");
            Console.WriteLine("  traps          what you've set up, and whether it's still armed");
            Console.WriteLine("  who            what every NPC is feeling and doing");
            Console.WriteLine("  log            recent events");
            Console.WriteLine("  quit");
            Console.WriteLine();
            Console.WriteLine("  Tip: sabotage has a delay. Set something up, then be seen somewhere else.");
        }

        private static void PrintMap()
        {
            const int width = 42;
            const int height = 21;
            char[,] grid = new char[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) grid[y, x] = '.';
            }

            // Walls.
            for (int i = 0; i < _sim.World.Walls.Count; i++)
            {
                Wall w = _sim.World.Walls[i];
                int steps = 60;
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vec3 p = new Vec3(
                        w.A.X + (w.B.X - w.A.X) * t, 0f,
                        w.A.Z + (w.B.Z - w.A.Z) * t);
                    Plot(grid, p, '#', width, height);
                }
            }

            // '+' not 'E': NPCs are drawn by their initial, and Eva was rendering
            // as a second back door.
            Zone exit = _sim.World.GetZone(_sim.ExitZoneId);
            if (exit != null) Plot(grid, exit.Center, '+', width, height);

            for (int i = 0; i < _sim.World.Objects.Count; i++)
            {
                SmartObject o = _sim.World.Objects[i];
                if (o.Concealed || o.HeldBy.Length > 0) continue;
                Plot(grid, o.Position, char.ToLowerInvariant(o.Name[0]), width, height);
            }

            for (int i = 0; i < _sim.World.Npcs.Count; i++)
            {
                Npc n = _sim.World.Npcs[i];
                Plot(grid, n.Position, char.ToUpperInvariant(n.Name[0]), width, height);
            }

            Plot(grid, _sim.Player.Position, '@', width, height);

            Console.WriteLine("  +" + new string('-', width) + "+");
            for (int y = 0; y < height; y++)
            {
                char[] row = new char[width];
                for (int x = 0; x < width; x++) row[x] = grid[y, x];
                Console.WriteLine("  |" + new string(row) + "|");
            }
            Console.WriteLine("  +" + new string('-', width) + "+");
            Console.WriteLine("   @ you   + back door   # wall   UPPER = people   lower = objects");
        }

        private static void Plot(char[,] grid, Vec3 p, char c, int width, int height)
        {
            float u = (p.X - _sim.World.FloorMin.X) / (_sim.World.FloorMax.X - _sim.World.FloorMin.X);
            float v = (p.Z - _sim.World.FloorMin.Z) / (_sim.World.FloorMax.Z - _sim.World.FloorMin.Z);

            int x = (int)(u * (width - 1));
            int y = (int)((1f - v) * (height - 1));

            if (x < 0 || x >= width || y < 0 || y >= height) return;
            grid[y, x] = c;
        }

        private static void PrintLook()
        {
            _lastLook = _sim.GetPlayerInteractions();
            if (_lastLook.Count == 0)
            {
                Console.WriteLine("  Nothing within reach. Try 'go <something>' first, or 'map'.");
                return;
            }

            Console.WriteLine("  Within reach:");
            _shownLabels.Clear();
            for (int i = 0; i < _lastLook.Count; i++)
            {
                InteractionOption o = _lastLook[i];
                _shownLabels.Add(o.Label);
                Console.WriteLine(string.Format("   {0,2}. {1}{2}",
                    i + 1,
                    o.Label,
                    o.IsSabotage ? "   [sabotage]" : ""));
            }

            List<string> watchers = Watchers();
            Console.WriteLine(watchers.Count == 0
                ? "  Nobody can see you right now."
                : "  Being watched by: " + string.Join(", ", watchers.ToArray()));
        }

        /// <summary>
        /// What the player has set up. A sandbox about laying traps has to tell
        /// you which of yours are still live - otherwise you spend the level
        /// waiting on sabotage that was mopped up four minutes ago.
        /// </summary>
        /// <summary>The answering machine. Forty seconds of story, no cutscene.</summary>
        private static void PrintBriefing()
        {
            Contract c = _sim.Contract;
            if (c == null) return;

            Console.WriteLine();
            Console.WriteLine("  LAST STRAW — " + c.Title.ToUpperInvariant());
            Console.WriteLine("  " + new string('-', 52));
            Console.WriteLine("  Client: " + c.Client);
            Console.WriteLine();
            foreach (string line in Wrap(c.Brief, 52)) Console.WriteLine("    \"" + line + "\"");
            Console.WriteLine();
            PrintJobs();
        }

        private static IEnumerable<string> Wrap(string text, int width)
        {
            string[] words = text.Split(' ');
            string line = "";
            foreach (string w in words)
            {
                if (line.Length + w.Length + 1 > width)
                {
                    yield return line;
                    line = w;
                }
                else line = line.Length == 0 ? w : line + " " + w;
            }
            if (line.Length > 0) yield return line;
        }

        /// <summary>
        /// The to-do list. Every line names an outcome and never a method, which
        /// is the whole reason the structure works.
        /// </summary>
        private static void PrintJobs()
        {
            Contract c = _sim.Contract;
            if (c == null)
            {
                Console.WriteLine("  No contract on this level.");
                return;
            }

            Console.WriteLine("  TO DO:");
            for (int i = 0; i < c.Jobs.Count; i++)
            {
                Job job = c.Jobs[i];
                string box = job.Done ? "[x]" : "[ ]";
                string star = job.Required ? " *CONTRACT*" : "";
                Console.WriteLine("   " + box + " " + job.Display + star);
            }
        }

        private static void PrintTraps()
        {
            IReadOnlyList<Trap> traps = _sim.Traps.All;
            if (traps.Count == 0)
            {
                Console.WriteLine("  You haven't set anything up yet.");
                return;
            }

            Console.WriteLine("  What you've set up:");
            for (int i = 0; i < traps.Count; i++)
            {
                Trap t = traps[i];
                string waiting = t.WaitingForName.Length > 0 && !t.Sprung && !t.Defused
                    ? "  waiting for " + t.WaitingForName
                    : "";

                Console.WriteLine(string.Format("   [{0,-7}] {1,-44} set {2,3:0}s ago{3}",
                    t.StatusWord, t.Label, _sim.Time - t.SetAt, waiting));
            }

            Console.WriteLine("   " + _sim.Traps.ArmedCount + " still armed.");
        }

        private static List<string> Watchers()
        {
            List<string> watchers = new List<string>();
            for (int i = 0; i < _sim.World.Npcs.Count; i++)
            {
                Npc n = _sim.World.Npcs[i];
                if (n.Perception.CanSee(n.Position, n.Facing, _sim.Player.Position, _sim.World))
                {
                    watchers.Add(n.Name);
                }
            }
            return watchers;
        }

        private static void PrintWho()
        {
            Console.WriteLine("  Who's who:");
            for (int i = 0; i < _sim.World.Npcs.Count; i++)
            {
                Npc n = _sim.World.Npcs[i];
                Console.WriteLine(string.Format(
                    "   {0,-7} {1,-12} anger {2:0.00} ({3,-9}) suspects you {4:0.00} | {5} | {6}",
                    n.Name,
                    n.Role,
                    n.Anger,
                    n.MoodWord,
                    n.SuspicionOf(PlayerAvatar.PlayerId),
                    n.Activity + (n.CurrentPlan != null ? ": " + n.CurrentPlan.Describe() : ""),
                    n.Personality.Describe()));
            }
        }

        private static void PrintLog()
        {
            List<WorldEvent> recent = _sim.Events.Recent(12);
            for (int i = 0; i < recent.Count; i++) Console.WriteLine("   " + recent[i]);
        }

        private static void Go(string name)
        {
            if (name.Length == 0)
            {
                Console.WriteLine("  Go where?");
                return;
            }

            Vec3 destination;
            string what;
            List<string> ambiguous;

            if (!Resolve(name, out destination, out what, out ambiguous))
            {
                if (ambiguous.Count > 1)
                {
                    // One letter used to silently pick whichever object happened
                    // to be first in the list, which turned navigation into
                    // typing random letters and seeing where you ended up.
                    Console.WriteLine("  Which one? " + string.Join(", ", ambiguous.ToArray()));
                }
                else
                {
                    Console.WriteLine("  Can't find '" + name + "'. Try 'map' for what's about.");
                }
                return;
            }

            if (Vec3.FlatDistance(destination, _sim.Player.Position) < 1.1f)
            {
                Console.WriteLine("  You're already at the " + what + ".");
                PrintLook();
                return;
            }

            // Walk there, ticking the world as we go. Time spent walking is time
            // the NPCs use to live their lives.
            float travelled = 0f;
            while (_sim.Outcome == GameOutcome.InProgress && travelled < 60f)
            {
                Vec3 delta = destination - _sim.Player.Position;
                if (delta.Magnitude < 1.1f) break;

                _sim.MovePlayer(delta, false, Dt);
                _sim.Tick(Dt);
                travelled += Dt;
            }

            Console.WriteLine(string.Format("  You walk over to the {0}. ({1:0.0}s)", what, travelled));
            PrintLook();
        }

        /// <summary>
        /// Turn what the player typed into somewhere to walk.
        ///
        /// Matching is layered: exact name, then whole-word prefix, then
        /// substring. A vague needle that hits several things asks rather than
        /// guessing, because silently picking the first match is how "go d"
        /// ends up somewhere the player never intended and they lose the thread
        /// of where they are.
        /// </summary>
        private static bool Resolve(string name, out Vec3 position, out string what,
            out List<string> candidates)
        {
            position = Vec3.Zero;
            what = "";
            candidates = new List<string>();

            string needle = name.Trim().ToLowerInvariant();
            if (needle.Length == 0) return false;

            List<string> names = new List<string>();
            List<Vec3> spots = new List<Vec3>();

            for (int i = 0; i < _sim.World.Zones.Count; i++)
            {
                Zone z = _sim.World.Zones[i];
                names.Add(z.Name.Length > 0 ? z.Name : z.Id);
                spots.Add(z.Center);
            }

            for (int i = 0; i < _sim.World.Objects.Count; i++)
            {
                SmartObject o = _sim.World.Objects[i];
                if (o.Concealed) continue;
                names.Add(o.Name);
                spots.Add(o.Position);
            }

            for (int i = 0; i < _sim.World.Npcs.Count; i++)
            {
                names.Add(_sim.World.Npcs[i].Name);
                spots.Add(_sim.World.Npcs[i].Position);
            }

            // "exit" and "out" always mean the way out, whatever it is called.
            if (needle == "exit" || needle == "out" || needle == "door out" || needle == "back door")
            {
                Zone exit = _sim.World.GetZone(_sim.ExitZoneId);
                if (exit != null)
                {
                    position = exit.Center;
                    what = "back door";
                    return true;
                }
            }

            int exact = -1;
            List<int> prefix = new List<int>();
            List<int> loose = new List<int>();

            for (int i = 0; i < names.Count; i++)
            {
                string hay = names[i].ToLowerInvariant();
                if (hay == needle) { exact = i; break; }

                bool wordPrefix = hay.StartsWith(needle);
                if (!wordPrefix)
                {
                    string[] words = hay.Split(' ');
                    for (int w = 0; w < words.Length; w++)
                    {
                        if (words[w].StartsWith(needle)) { wordPrefix = true; break; }
                    }
                }

                if (wordPrefix) prefix.Add(i);
                else if (hay.Contains(needle)) loose.Add(i);
            }

            if (exact >= 0)
            {
                position = spots[exact];
                what = names[exact];
                return true;
            }

            List<int> hits = prefix.Count > 0 ? prefix : loose;

            // Several matches that are all the same place is not really ambiguous.
            if (hits.Count > 1)
            {
                bool sameSpot = true;
                for (int i = 1; i < hits.Count; i++)
                {
                    if (Vec3.FlatDistance(spots[hits[0]], spots[hits[i]]) > 1.4f) { sameSpot = false; break; }
                }
                if (sameSpot) hits = new List<int> { hits[0] };
            }

            if (hits.Count == 1)
            {
                position = spots[hits[0]];
                what = names[hits[0]];
                return true;
            }

            for (int i = 0; i < hits.Count && i < 8; i++) candidates.Add(names[hits[i]]);
            return false;
        }

        private static void Use(string arg)
        {
            int index;
            if (!int.TryParse(arg.Trim(), out index))
            {
                Console.WriteLine("  use <number>, from 'look'.");
                return;
            }

            if (_shownLabels.Count == 0) PrintLook();

            if (index < 1 || index > _shownLabels.Count)
            {
                Console.WriteLine("  No such option. Run 'look' again.");
                return;
            }

            // The number the player typed refers to the list they were SHOWN,
            // not to whatever the live list happens to be now. Acting on a
            // shifted index is how "use 2" ends up putting down the pan you
            // just picked up, which looks like the game lying to you.
            string wanted = _shownLabels[index - 1];
            _lastLook = _sim.GetPlayerInteractions();

            InteractionOption option = null;
            for (int i = 0; i < _lastLook.Count; i++)
            {
                if (_lastLook[i].Label != wanted) continue;
                option = _lastLook[i];
                break;
            }

            if (option == null)
            {
                Console.WriteLine("  '" + wanted + "' isn't on offer any more - things have moved on.");
                PrintLook();
                return;
            }

            float duration = option.Affordance.Duration;

            if (!_sim.PlayerInteract(option))
            {
                Console.WriteLine("  That isn't possible right now.");
                return;
            }

            Console.WriteLine("  You " + option.Label.ToLowerInvariant() + ".");

            int steps = (int)(duration / Dt);
            for (int i = 0; i < steps && _sim.Outcome == GameOutcome.InProgress; i++)
            {
                _sim.MovePlayer(Vec3.Zero, true, Dt);
                _sim.Tick(Dt);
            }

            _lastLook = _sim.GetPlayerInteractions();
            _shownLabels.Clear();
        }

        private static void Throw()
        {
            if (!_sim.PlayerThrow()) Console.WriteLine("  Your hands are empty.");
            Step(0.6f);
        }

        private static void Hide()
        {
            if (_sim.Player.IsHidden)
            {
                _sim.PlayerToggleHide(null);
                Step(0.5f);
                return;
            }

            SmartObject nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < _sim.World.Objects.Count; i++)
            {
                SmartObject o = _sim.World.Objects[i];
                if (!o.HasTag(Tags.Hiding)) continue;
                float d = Vec3.FlatDistance(o.Position, _sim.Player.Position);
                if (d < best) { best = d; nearest = o; }
            }

            if (nearest == null || best > 2.2f)
            {
                Console.WriteLine("  Nothing to hide in here.");
                return;
            }

            _sim.PlayerToggleHide(nearest);
            Step(0.5f);
        }

        private static void Step(float seconds)
        {
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps && _sim.Outcome == GameOutcome.InProgress; i++)
            {
                _sim.MovePlayer(Vec3.Zero, false, Dt);
                _sim.Tick(Dt);
            }
        }

        private static void Wait(string arg)
        {
            float seconds;
            if (!float.TryParse(arg.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            {
                seconds = 10f;
            }
            seconds = Math.Min(seconds, 120f);

            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps && _sim.Outcome == GameOutcome.InProgress; i++)
            {
                _sim.MovePlayer(Vec3.Zero, false, Dt);
                _sim.Tick(Dt);
            }

            Console.WriteLine(string.Format("  You wait {0:0}s.", seconds));
        }

        private static int FlushFeed(int shown)
        {
            FlushFeedback();

            for (int i = shown; i < _sim.Feed.Count; i++)
            {
                Console.WriteLine("   . " + _sim.Feed[i]);
            }
            return _sim.Feed.Count;
        }

        /// <summary>
        /// Prints the same feedback stream the Unity HUD turns into floating
        /// numbers and toasts. Keeping both front-ends on one queue is how the
        /// terminal build stays useful for judging whether the game reads well.
        /// </summary>
        private static void FlushFeedback()
        {
            List<FeedbackEvent> events = _sim.Feedback.Drain();
            for (int i = 0; i < events.Count; i++)
            {
                FeedbackEvent e = events[i];
                string who = e.ActorId.Length > 0 ? _sim.DisplayName(e.ActorId) : "";

                switch (e.Kind)
                {
                    case FeedbackKind.Anger:
                        Console.WriteLine("   >> " + who + "  +" + e.Amount + " ANGER  (" + e.Text + ")");
                        break;
                    case FeedbackKind.Suspicion:
                        Console.WriteLine("   !! " + who + "  +" + e.Amount + " SUSPICION  (" + e.Text + ")");
                        break;
                    case FeedbackKind.Alert:
                        Console.WriteLine("   ** " + e.Text);
                        break;
                    case FeedbackKind.Objective:
                        Console.WriteLine("   ## " + e.Text);
                        break;
                    default:
                        Console.WriteLine("   -- " + e.Text);
                        break;
                }
            }
        }
    }
}
