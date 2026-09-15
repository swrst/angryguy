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

            _sim = KitchenLevel.Build(seed);
            Console.WriteLine("ANGRY GUY - kitchen prototype (seed " + seed + ")");
            Console.WriteLine();

            if (crimeMode) return Crime(watchSeconds);
            if (watchOnly) return Watch(watchSeconds);

            Console.WriteLine("Objective: make Gordon the chef furious, without anyone deciding it was you.");
            Console.WriteLine("Then leave through the back door.");
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

                switch (cmd)
                {
                    case "help": PrintHelp(); break;
                    case "map": PrintMap(); break;
                    case "look": PrintLook(); break;
                    case "who": PrintWho(); break;
                    case "log": PrintLog(); break;
                    case "go": Go(arg); break;
                    case "use": Use(arg); break;
                    case "wait": Wait(arg); break;
                    case "exit": Go("exit"); break;
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

            Zone exit = _sim.World.GetZone(_sim.ExitZoneId);
            if (exit != null) Plot(grid, exit.Center, 'E', width, height);

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
            Console.WriteLine("   @ you   E back door   # wall   UPPER = people   lower = objects");
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
            for (int i = 0; i < _lastLook.Count; i++)
            {
                InteractionOption o = _lastLook[i];
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
            if (!Resolve(name, out destination))
            {
                Console.WriteLine("  Can't find '" + name + "'.");
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

            Console.WriteLine(string.Format("  You walk over. ({0:0.0}s)", travelled));
            PrintLook();
        }

        private static bool Resolve(string name, out Vec3 position)
        {
            position = Vec3.Zero;
            string needle = name.ToLowerInvariant();

            Zone zone = _sim.World.GetZone(needle);
            if (zone != null)
            {
                position = zone.Center;
                return true;
            }

            for (int i = 0; i < _sim.World.Objects.Count; i++)
            {
                SmartObject o = _sim.World.Objects[i];
                if (o.Concealed) continue;
                if (o.Id.ToLowerInvariant().Contains(needle) || o.Name.ToLowerInvariant().Contains(needle))
                {
                    position = o.Position;
                    return true;
                }
            }

            for (int i = 0; i < _sim.World.Npcs.Count; i++)
            {
                Npc n = _sim.World.Npcs[i];
                if (n.Id.ToLowerInvariant().Contains(needle) || n.Name.ToLowerInvariant().Contains(needle))
                {
                    position = n.Position;
                    return true;
                }
            }

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

            if (_lastLook.Count == 0) _lastLook = _sim.GetPlayerInteractions();

            if (index < 1 || index > _lastLook.Count)
            {
                Console.WriteLine("  No such option. Run 'look' again.");
                return;
            }

            InteractionOption option = _lastLook[index - 1];
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
            for (int i = shown; i < _sim.Feed.Count; i++)
            {
                Console.WriteLine("   . " + _sim.Feed[i]);
            }
            return _sim.Feed.Count;
        }
    }
}
