using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// LEVEL 1 — Marking Hour.
    ///
    /// One room. One man. Two verbs: take and drop.
    ///
    /// Everything about this level is smaller than we want it to be, on purpose.
    /// The prototype's failure was handing a player fifteen sabotage options and
    /// six people at minute one, which reads as noise rather than depth. Here
    /// there are nine objects and nobody to hide behind, so the rules arrive one
    /// at a time and the player can actually see each one land.
    ///
    /// Five things this level teaches, without a line of tutorial text:
    ///
    ///   1. He has a routine, and it repeats.
    ///   2. Taking a thing he needs makes his plan fail.
    ///   3. A failed plan angers him far more than a broken object does.
    ///   4. He only knows what he sees.
    ///   5. Where he is looking is the resource you are managing.
    ///
    /// If a playtester finishes this level unable to state all five, the level is
    /// wrong and gets rebuilt. We do not paper over it with a hint.
    /// </summary>
    public static class MarkingHour
    {
        public static class Ids
        {
            public const string Pemberton = "pemberton";

            public const string Desk = "desk";
            public const string Chair = "chair";
            public const string Marking = "marking";
            public const string RedPen = "redpen";
            public const string Mug = "mug";
            public const string Kettle = "kettle";
            public const string Biscuits = "biscuits";
            public const string Bin = "bin";
            public const string Window = "window";
            public const string Door = "door";
        }

        public static Simulation Build(int seed)
        {
            Simulation sim = new Simulation(seed);
            World world = sim.World;

            // A staffroom, not a kitchen. Small enough that the player can see
            // the whole thing from the doorway, which is what lets them plan.
            world.FloorMin = new Vec3(-5f, 0f, -4f);
            world.FloorMax = new Vec3(5f, 0f, 4f);

            world.Zones.Add(new Zone
            {
                Id = "staffroom", Name = "Staffroom",
                Center = new Vec3(0f, 0f, 0f), Radius = 6.5f
            });
            world.Zones.Add(new Zone
            {
                Id = "exit", Name = "Corridor",
                Center = new Vec3(0f, 0f, -3.4f), Radius = 1.3f
            });

            BuildObjects(sim, world);
            BuildPemberton(sim, world);

            sim.TargetNpcId = Ids.Pemberton;
            sim.ExitZoneId = "exit";
            sim.Player.Position = new Vec3(0f, 0f, -3.0f);
            sim.Player.Outfit = Outfit.Civilian();

            sim.Contract = BuildContract();
            sim.TimeLimit = sim.Contract.TimeLimit;

            sim.Log("4:02pm. Pemberton is marking. The bell goes at 4:30.");
            return sim;
        }

        // ------------------------------------------------------------------
        // The room — nine objects and a door
        // ------------------------------------------------------------------

        private static void BuildObjects(Simulation sim, World world)
        {
            world.Add(BuildDesk());
            world.Add(BuildMarkingPile());
            world.Add(BuildRedPen());
            world.Add(BuildMug());
            world.Add(BuildKettle());
            world.Add(BuildBiscuits());
            world.Add(BuildWindow());

            SmartObject chair = ItemCatalogue.Chair(Ids.Chair, new Vec3(-1.4f, 0f, 1.2f), "chair");
            ItemCatalogue.AddBusywork(chair, "Straighten the", 3f);
            world.Add(chair);

            SmartObject bin = ItemCatalogue.Bin(Ids.Bin, new Vec3(3.4f, 0f, 2.2f), "waste bin");
            ItemCatalogue.AddBusywork(bin, "Push down the rubbish in the", 3f);
            world.Add(bin);
        }

        private static SmartObject BuildDesk()
        {
            SmartObject desk = ItemCatalogue.Make(Ids.Desk, "marking table",
                new Vec3(-1.4f, 0f, 2.0f), new Vec3(2.0f, 0.75f, 1.0f));
            desk.OwnerId = Ids.Pemberton;
            ItemCatalogue.AddBusywork(desk, "Tidy the", 5f);
            return desk;
        }

        /// <summary>
        /// The stack of books. This is his job, so it is what he keeps coming
        /// back to, and it is the anchor the whole routine hangs off.
        /// </summary>
        private static SmartObject BuildMarkingPile()
        {
            SmartObject pile = ItemCatalogue.Make(Ids.Marking, "stack of books",
                new Vec3(-1.4f, 0.8f, 2.0f), new Vec3(0.35f, 0.2f, 0.28f));
            pile.OwnerId = Ids.Pemberton;
            pile.Portable = true;
            pile.WithState(StateKeys.Contents, 12f);

            pile.WithAffordance(new Affordance
            {
                Id = "mark",
                Verb = "Mark",
                Actors = ActorKind.Npc,
                Duration = 7f,
                BaseAppeal = 1.5f,
                Satisfies = new List<NeedDelta>
                {
                    new NeedDelta(NeedType.Order, 0.45f),
                    new NeedDelta(NeedType.Comfort, 0.1f)
                },

                // The pen is the dependency, and it is deliberately only
                // checkable on arrival. He sits down believing he can work.
                ArrivalPrecondition = delegate(SmartObject o, Npc npc)
                {
                    SmartObject pen = FindIn(npc, Ids.RedPen);
                    return pen != null && pen.HeldBy.Length == 0
                           && Vec3.FlatDistance(pen.Position, o.Position) < 1.8f;
                },

                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.AddState(StateKeys.Contents, -1f);
                    if (ctx.Npc != null) ctx.Npc.Attention.Glance(ctx.Object.Position, 1.5f);
                }
            });

            ItemCatalogue.AddBusywork(pile, "Square up the", 4f);
            ItemCatalogue.AddCarryVerbs(pile, 0.5f, 0.3f);
            return pile;
        }

        /// <summary>
        /// The most important object in the game so far. Take this and the whole
        /// level happens.
        /// </summary>
        private static SmartObject BuildRedPen()
        {
            SmartObject pen = ItemCatalogue.Make(Ids.RedPen, "red pen",
                new Vec3(-0.9f, 0.8f, 2.0f), new Vec3(0.04f, 0.04f, 0.16f));
            pen.OwnerId = Ids.Pemberton;
            pen.Portable = true;

            // Cheap to take and easy to miss, which is exactly right for the
            // first sabotage a player ever performs. It should feel like
            // nothing, and then cost him twenty minutes.
            ItemCatalogue.AddCarryVerbs(pen, 0.35f, 0.25f);
            return pen;
        }

        private static SmartObject BuildMug()
        {
            SmartObject mug = ItemCatalogue.Make(Ids.Mug, "his mug",
                new Vec3(-2.0f, 0.8f, 2.0f), new Vec3(0.12f, 0.12f, 0.12f));
            mug.OwnerId = Ids.Pemberton;
            mug.Portable = true;
            mug.WithTag(Tags.Fragile).WithState(StateKeys.Contents, 1f).WithState("hot", 1f);

            mug.WithAffordance(new Affordance
            {
                Id = "sip",
                Verb = "Drink from",
                Actors = ActorKind.Npc,
                Duration = 2.5f,
                BaseAppeal = 1.1f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Comfort, 0.3f) },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Contents) > 0f; },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc)
                {
                    return o.HeldBy.Length == 0;
                },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.AddState(StateKeys.Contents, -0.34f);

                    // Cold tea is a small, specific, entirely deserved misery.
                    if (ctx.Object.GetState("hot") <= 0f && ctx.Npc != null)
                    {
                        AngerModel.Add(ctx.Npc, 0.05f, ctx.Sim, "the tea's gone cold");
                        ctx.Npc.Say("...that's stone cold.", 3f);
                        ctx.Sim.NoteFlag("drank_cold_tea");
                    }
                }
            });

            ItemCatalogue.AddCarryVerbs(mug, 0.4f, 0.3f);
            return mug;
        }

        private static SmartObject BuildKettle()
        {
            SmartObject kettle = ItemCatalogue.Make(Ids.Kettle, "kettle",
                new Vec3(3.6f, 0f, -0.4f), new Vec3(0.24f, 0.3f, 0.24f));
            kettle.WithTag(Tags.Appliance).WithState(StateKeys.Broken, 0f);

            kettle.WithAffordance(new Affordance
            {
                Id = "brew",
                Verb = "Put the kettle on at the",
                Actors = ActorKind.Npc,
                Duration = 6f,
                Noise = 0.35f,
                BaseAppeal = 1.0f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Comfort, 0.35f) },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc) { return !o.IsBroken; },
                Effect = delegate(AffordanceContext ctx)
                {
                    SmartObject mug = ctx.Sim.World.GetObject(Ids.Mug);
                    if (mug == null) return;
                    mug.SetState(StateKeys.Contents, 1f);
                    mug.SetState("hot", 1f);

                    // Tea goes cold on its own. If the player has moved the mug
                    // somewhere he cannot reach, it is cold by the time he finds it.
                    ctx.Sim.Schedule(75f, "tea goes cold", delegate(Simulation s)
                    {
                        mug.SetState("hot", 0f);
                    });
                }
            });

            ItemCatalogue.AddBusywork(kettle, "Wipe down the counter by the", 4f);
            return kettle;
        }

        private static SmartObject BuildBiscuits()
        {
            SmartObject tin = ItemCatalogue.Make(Ids.Biscuits, "biscuit tin",
                new Vec3(3.2f, 0f, -1.2f), new Vec3(0.26f, 0.14f, 0.2f));
            tin.Portable = true;
            tin.WithTag(Tags.Food).WithState(StateKeys.Contents, 5f);

            tin.WithAffordance(new Affordance
            {
                Id = "biscuit",
                Verb = "Take a biscuit from the",
                Actors = ActorKind.Npc,
                Duration = 2f,
                BaseAppeal = 1.2f,
                Satisfies = new List<NeedDelta>
                {
                    new NeedDelta(NeedType.Hunger, 0.4f),
                    new NeedDelta(NeedType.Comfort, 0.15f)
                },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Contents) > 0f; },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc)
                {
                    return o.HeldBy.Length == 0 && o.GetState(StateKeys.Contents) > 0f;
                },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.AddState(StateKeys.Contents, -1f);
                }
            });

            ItemCatalogue.AddCarryVerbs(tin, 0.4f, 0.3f);
            return tin;
        }

        /// <summary>
        /// The slow solution. Closing it costs nothing, is completely
        /// unincriminating, and makes every other grievance land harder — which
        /// is the first hint that the player is managing a curve, not a checklist.
        /// </summary>
        private static SmartObject BuildWindow()
        {
            SmartObject window = ItemCatalogue.Make(Ids.Window, "window",
                new Vec3(0f, 1.2f, 3.9f), new Vec3(1.6f, 1.1f, 0.1f));
            window.WithState("open", 1f);

            window.WithAffordance(new Affordance
            {
                Id = "shut_window",
                ActId = Acts.Tamper,
                Verb = "Quietly shut the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 1.6f,
                Incrimination = 0.15f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("open") > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState("open", 0f);
                    ctx.Sim.Announce(FeedbackKind.Info,
                        "Window shut. The room will get stuffy, and he will get shorter with everything.");
                }
            });

            window.WithAffordance(new Affordance
            {
                Id = "open_window",
                Verb = "Open the",
                Actors = ActorKind.Both,
                Duration = 1.6f,
                Incrimination = 0.1f,
                BaseAppeal = 0.9f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Comfort, 0.4f) },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("open") <= 0f; },
                Effect = delegate(AffordanceContext ctx) { ctx.Object.SetState("open", 1f); }
            });

            ItemCatalogue.AddBusywork(window, "Stare out of the", 7f);
            return window;
        }

        // ------------------------------------------------------------------
        // The man
        // ------------------------------------------------------------------

        private static void BuildPemberton(Simulation sim, World world)
        {
            Npc p = new Npc
            {
                Id = Ids.Pemberton,
                Name = "Mr Pemberton",
                Role = "deputy head",
                Position = new Vec3(-1.4f, 0f, 1.2f),
                HomeZoneId = "staffroom",
                IsTarget = true,
                MoveSpeed = 2.2f,
                Personality = Pemberton()
            };

            p.Perception = PerceptionModel.FromPersonality(p.Personality);

            // The routine, expressed as habits. The weights are what make him
            // legible: he goes back to the same four things in the same rough
            // order, so a player who watches one cycle can predict the next.
            p.AddHabit(Ids.Marking, "mark", 3.0f);
            p.AddHabit(Ids.Mug, "sip", 2.2f);
            p.AddHabit(Ids.Biscuits, "biscuit", 1.8f);
            p.AddHabit(Ids.Kettle, "brew", 1.4f);
            p.AddHabit(Ids.Chair, "sit", 1.2f);

            // He is here to work, so marking is the thing he is always drawn
            // back toward. Comfort drains a little faster than usual, which is
            // what sends him off for tea and biscuits between books.
            // Marking is the spine of the routine: Order drains fast enough that
            // he is always being pulled back to the books, and everything else -
            // tea, biscuits, a stare out of the window - is a break from it. That
            // asymmetry is what makes the loop readable in a single watch.
            p.Needs.Set(NeedType.Order, 0.30f);
            p.Needs.SetDecay(NeedType.Order, 0.022f);
            p.Needs.SetDecay(NeedType.Comfort, 0.011f);
            p.Needs.SetDecay(NeedType.Hunger, 0.006f);
            p.Needs.SetDecay(NeedType.Bladder, 0.0015f);
            p.Needs.SetDecay(NeedType.Social, 0.0008f);

            world.Add(p);
        }

        /// <summary>
        /// Tired, fussy, and quietly certain everything is somebody else's fault.
        /// Alone in the room, so his paranoia has nowhere to go but the player —
        /// which is the honest version of the rules, before blame exists.
        /// </summary>
        private static Personality Pemberton()
        {
            return new Personality
            {
                Temper = 0.78f,
                Tidiness = 0.85f,
                Paranoia = 0.30f,
                Sociability = 0.25f,
                Gluttony = 0.60f,
                Territoriality = 0.90f,
                Observance = 0.45f,
                Gullibility = 0.55f,
                Grudge = 0.75f,
                Diligence = 0.55f,
                Clumsiness = 0.05f
            };
        }

        // ------------------------------------------------------------------
        // The brief
        // ------------------------------------------------------------------

        private static Contract BuildContract()
        {
            Contract c = new Contract
            {
                Id = "marking_hour",
                Title = "Marking Hour",
                Client = "A boy called Sam. Paid in coins.",
                Brief = "He shouts at everyone and then says he never shouts. "
                      + "I just want someone else to hear him do it.",
                TargetNpcId = Ids.Pemberton,
                TimeLimit = 28f * 60f
            };

            c.Add(new Job
            {
                Id = "contract",
                Text = "Make Mr Pemberton lose his temper before the bell.",
                Required = true,
                Reward = 5,
                IsSatisfied = delegate(Simulation s)
                {
                    Npc t = s.Target;
                    return t != null && t.PeakAnger >= Simulation.AngerWinThreshold;
                }
            });

            c.Add(new Job
            {
                Id = "under_table",
                Text = "Make Mr Pemberton look under the table.",
                Reward = 2,
                IsSatisfied = delegate(Simulation s) { return s.HasFlag("searched_floor"); }
            });

            c.Add(new Job
            {
                Id = "bad_word",
                Text = "Make Mr Pemberton say a word he would tell a child off for.",
                Reward = 2,
                IsSatisfied = delegate(Simulation s) { return s.HasFlag("swore"); }
            });

            c.Add(new Job
            {
                Id = "cold_tea",
                Text = "Make Mr Pemberton drink cold tea.",
                Reward = 2,
                IsSatisfied = delegate(Simulation s) { return s.HasFlag("drank_cold_tea"); }
            });

            c.Add(new Job
            {
                Id = "blame_kettle",
                Text = "Make Mr Pemberton blame the kettle.",
                Reward = 2,
                IsSatisfied = delegate(Simulation s) { return s.HasFlag("blamed_kettle"); }
            });

            // Hidden. The player sees only an asterisk until they manage it.
            c.Add(new Job
            {
                Id = "tin_out_window",
                Text = "Make Mr Pemberton throw the biscuit tin out of the window.",
                Hidden = true,
                Reward = 4,
                IsSatisfied = delegate(Simulation s) { return s.HasFlag("tin_defenestrated"); }
            });

            return c;
        }

        // ------------------------------------------------------------------

        /// <summary>Look up another object from inside a precondition.</summary>
        private static SmartObject FindIn(Npc npc, string id)
        {
            return npc != null && npc.World != null ? npc.World.GetObject(id) : null;
        }
    }
}
