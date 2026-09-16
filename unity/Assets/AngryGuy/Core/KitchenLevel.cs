using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// The prototype level: a restaurant kitchen and dining room.
    ///
    /// Target: make Gordon (the chef) furious without him working out it was you.
    ///
    /// Most of this file is composition - pulling generic props out of
    /// ItemCatalogue and placing them. Only the bespoke interplay lives here: the
    /// stove that needs *that* pan, and the salt that ruins *that* dish.
    /// Everything else is reusable in the next level.
    /// </summary>
    public static class KitchenLevel
    {
        public static class Ids
        {
            public const string Stove = "stove";
            public const string Pan = "pan";
            public const string Fridge = "fridge";
            public const string Salt = "salt";
            public const string Oil = "oil";
            public const string Bell = "bell";
            public const string Bin = "bin";
            public const string Sink = "sink";
            public const string Toilet = "toilet";
            public const string Radio = "radio";
            public const string Table = "table";
            public const string Coffee = "coffee";
            public const string Desk = "desk";
            public const string Door = "door";
            public const string Pantry = "pantry";
            public const string Booth = "booth";
            public const string Locker = "locker";
            public const string Lights = "lights";
            public const string Alarm = "alarm";
            public const string Bucket = "bucket";
            public const string Plant = "plant";

            public const string Chef = "chef";
            public const string Waiter = "waiter";
            public const string Dishwasher = "dishwasher";
            public const string Manager = "manager";
            public const string SousChef = "souschef";
            public const string Porter = "porter";
        }

        public static Simulation Build(int seed)
        {
            Simulation sim = new Simulation(seed);
            World world = sim.World;

            world.FloorMin = new Vec3(-10f, 0f, -10f);
            world.FloorMax = new Vec3(10f, 0f, 10f);

            // A dividing wall with a single doorway. Sight lines are the level design.
            world.Walls.Add(new Wall(0f, -10f, 0f, -1.2f));
            world.Walls.Add(new Wall(0f, 1.2f, 0f, 10f));

            // The one way between the two halves. Everything routes through here.
            world.Portals.Add(new Vec3(0f, 0f, 0f));

            // StaffOnly is what gives a stolen uniform a job to do: in your own
            // clothes, simply standing in the kitchen is slowly incriminating.
            world.Zones.Add(new Zone { Id = "kitchen", Name = "Kitchen", Center = new Vec3(-5f, 0f, 1f), Radius = 4.5f, StaffOnly = true });
            world.Zones.Add(new Zone { Id = "washup", Name = "Wash-up", Center = new Vec3(-7f, 0f, 6f), Radius = 2.5f, StaffOnly = true });
            world.Zones.Add(new Zone { Id = "dining", Name = "Dining room", Center = new Vec3(5f, 0f, 1f), Radius = 4.5f });
            world.Zones.Add(new Zone { Id = "floor", Name = "Front of house", Center = new Vec3(3f, 0f, -4f), Radius = 4.5f });
            world.Zones.Add(new Zone { Id = "exit", Name = "Back door", Center = new Vec3(-8.5f, 0f, 8.5f), Radius = 1.6f });
            world.Zones.Add(new Zone { Id = "assembly", Name = "Out front", Center = new Vec3(8.4f, 0f, -8.4f), Radius = 2f });

            BuildObjects(sim, world);
            BuildNpcs(sim, world);

            sim.TargetNpcId = Ids.Chef;
            sim.ExitZoneId = "exit";
            sim.AssemblyZoneId = "assembly";
            sim.Player.Position = new Vec3(4f, 0f, -6f);
            sim.Player.Outfit = Outfit.Civilian();

            sim.Log("Objective: make Gordon furious. Don't let anyone pin it on you. Then use the back door.");
            return sim;
        }

        // ------------------------------------------------------------------
        // Objects
        // ------------------------------------------------------------------

        private static void BuildObjects(Simulation sim, World world)
        {
            // --- bespoke to this level -------------------------------------
            world.Add(BuildStove(world));
            world.Add(BuildPan());
            world.Add(BuildFridge());
            world.Add(BuildSalt());
            world.Add(BuildOil());
            world.Add(BuildBell());
            world.Add(BuildCoffeeMachine());
            world.Add(BuildDesk());

            // --- straight out of the catalogue ------------------------------
            world.Add(ItemCatalogue.Bin(Ids.Bin, new Vec3(-8.2f, 0f, 4.6f)));
            world.Add(ItemCatalogue.Sink(Ids.Sink, new Vec3(-7f, 0f, 6.4f), Ids.Dishwasher));
            world.Add(ItemCatalogue.Toilet(Ids.Toilet, new Vec3(-2f, 0f, -7.6f), "staff toilet"));
            world.Add(ItemCatalogue.Radio(Ids.Radio, new Vec3(7.4f, 0f, -3f)));
            world.Add(ItemCatalogue.Table(Ids.Table, new Vec3(5.2f, 0f, 2.4f), "dining table"));
            world.Add(ItemCatalogue.Door(Ids.Door, new Vec3(0f, 0f, 0f), new Wall(0f, -1.2f, 0f, 1.2f), world));

            world.Add(ItemCatalogue.HidingSpot(Ids.Pantry, "pantry", new Vec3(-8.6f, 0f, 1.6f)));
            world.Add(ItemCatalogue.HidingSpot(Ids.Booth, "corner booth", new Vec3(8.4f, 0f, 4.6f)));

            world.Add(ItemCatalogue.Chair("chair_a", new Vec3(4f, 0f, 2.4f)));
            world.Add(ItemCatalogue.Chair("chair_b", new Vec3(6.2f, 0f, 2.4f)));

            // Kitchen staff need somewhere to take the weight off on their own
            // side of the wall, or they cross the level for every sit-down.
            world.Add(ItemCatalogue.Chair("stool", new Vec3(-3.4f, 0f, 5.2f), "stool"));

            // --- the newer toys ---------------------------------------------
            world.Add(ItemCatalogue.Locker(Ids.Locker, new Vec3(-8.2f, 0f, 3f),
                new[] { "whites", "apron", "overalls" }));
            world.Add(ItemCatalogue.LightSwitch(Ids.Lights, new Vec3(-0.9f, 0f, 1.9f)));
            world.Add(ItemCatalogue.FireAlarm(Ids.Alarm, new Vec3(5.5f, 0f, -6f)));
            world.Add(ItemCatalogue.MopBucket(Ids.Bucket, new Vec3(-6.2f, 0f, 7.2f)));
            world.Add(ItemCatalogue.Plant(Ids.Plant, new Vec3(7.5f, 0f, 3.5f)));
        }

        /// <summary>
        /// The heart of the level. Everything about the stove is built around
        /// Gordon believing it will work right up until he is standing at it.
        /// </summary>
        private static SmartObject BuildStove(World world)
        {
            SmartObject stove = ItemCatalogue.Make(Ids.Stove, "stove", new Vec3(-6f, 0f, 3f),
                new Vec3(1.6f, 1.0f, 0.9f));
            stove.OwnerId = Ids.Chef;
            stove.WithTag(Tags.Appliance).WithState(StateKeys.Broken, 0f).WithState(StateKeys.Heat, 0.5f);

            stove.WithAffordance(new Affordance
            {
                Id = "cook",
                Verb = "Cook on",
                Actors = ActorKind.Npc,
                Duration = 9f,
                Noise = 0.25f,
                BaseAppeal = 1.35f,
                Satisfies = new List<NeedDelta>
                {
                    new NeedDelta(NeedType.Comfort, 0.55f),
                    new NeedDelta(NeedType.Hunger, 0.15f)
                },
                // Only discoverable at the stove itself. Gordon sets off to cook
                // believing everything is where he left it.
                ArrivalPrecondition = delegate(SmartObject o, Npc npc)
                {
                    if (o.IsBroken) return false;
                    SmartObject pan = world.GetObject(Ids.Pan);
                    return pan != null && !pan.Concealed && pan.HeldBy.Length == 0 && !pan.IsAwayFromHome;
                },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Cooking, 1f);
                    string cookId = ctx.ActorId;

                    // The payoff lands later. If the salt was swapped minutes ago,
                    // this is the moment it detonates.
                    ctx.Sim.Schedule(14f, "dish finishes", delegate(Simulation s)
                    {
                        ctx.Object.SetState(StateKeys.Cooking, 0f);

                        SmartObject salt = s.World.GetObject(Ids.Salt);
                        bool saltRuined = salt != null && salt.GetState(StateKeys.Tampered) > 0f;
                        bool burned = ctx.Object.GetState(StateKeys.Heat) >= 0.95f;

                        if (!saltRuined && !burned) return;

                        Npc cook = s.World.GetNpc(cookId);

                        string culprit = "";
                        if (saltRuined && salt.GetState("tamperedBy") > 0.5f) culprit = PlayerAvatar.PlayerId;
                        else if (burned && ctx.Object.GetState("heatSetBy") > 0.5f) culprit = PlayerAvatar.PlayerId;

                        if (saltRuined) salt.SetState(StateKeys.Tampered, 0f);
                        if (burned) ctx.Object.SetState(StateKeys.Heat, 0.5f);

                        s.Publish(new WorldEvent
                        {
                            Kind = EventKind.FoodRuined,
                            Position = ctx.Object.Position,
                            TrueActorId = culprit,
                            ObjectId = ctx.Object.Id,
                            VictimId = cookId,
                            Loudness = 0.5f,
                            Severity = 0.85f,
                            LeavesEvidence = true,
                            Description = burned
                                ? "the dish is burnt to the pan"
                                : "the dish is completely inedible - it's all sugar"
                        });

                        s.Announce(FeedbackKind.Payoff, burned
                            ? "The dish is burnt - that heat you turned up has landed"
                            : "The sugar in the salt shaker has just been served up");

                        if (cook != null)
                        {
                            cook.Say(burned
                                ? "IT'S BURNT! WHO TOUCHED MY HEAT?!"
                                : "This is SUGAR! Somebody put SUGAR in my salt!");
                        }
                    });
                }
            });

            stove.WithAffordance(new Affordance
            {
                Id = "taste",
                Verb = "Taste the pot on",
                Actors = ActorKind.Npc,
                Duration = 2f,
                BaseAppeal = 1.1f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Hunger, 0.35f) },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc) { return !o.IsBroken; }
            });

            stove.WithAffordance(new Affordance
            {
                Id = "crank_heat",
                Verb = "Crank the heat on",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 1f,
                Incrimination = 0.35f,
                Precondition = delegate(SmartObject o, Npc npc)
                {
                    return !o.IsBroken && o.GetState(StateKeys.Heat) < 0.95f;
                },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Heat, 1f);
                    ctx.Object.SetState("heatSetBy", 1f);
                    ctx.Sim.Announce(FeedbackKind.Info,
                        "Burner on maximum - whatever he cooks next is ruined");
                }
            });

            stove.WithAffordance(new Affordance
            {
                Id = "break_stove",
                Verb = "Break the knobs off",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 2f,
                Noise = 0.7f,
                Incrimination = 0.9f,
                Precondition = delegate(SmartObject o, Npc npc) { return !o.IsBroken; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Broken, 1f);
                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.ObjectBroken,
                        Position = ctx.Object.Position,
                        TrueActorId = ctx.ActorId,
                        ObjectId = ctx.Object.Id,
                        VictimId = ctx.Object.OwnerId,
                        Loudness = 0.7f,
                        Severity = 0.8f,
                        LeavesEvidence = true,
                        Description = "the stove is broken"
                    });
                }
            });

            return stove;
        }

        private static SmartObject BuildPan()
        {
            SmartObject pan = ItemCatalogue.Make(Ids.Pan, "good pan", new Vec3(-6f, 0f, 2.1f),
                new Vec3(0.45f, 0.18f, 0.45f));
            pan.OwnerId = Ids.Chef;
            pan.Portable = true;
            pan.WithTag(Tags.Tool);
            ItemCatalogue.AddCarryVerbs(pan, 0.65f, 0.5f);
            return pan;
        }

        private static SmartObject BuildFridge()
        {
            SmartObject fridge = ItemCatalogue.Make(Ids.Fridge, "fridge", new Vec3(-8.4f, 0f, -2f),
                new Vec3(1f, 2f, 0.9f));
            fridge.OwnerId = Ids.Chef;
            fridge.WithTag(Tags.Container).WithTag(Tags.Food).WithState(StateKeys.Contents, 3f);

            fridge.WithAffordance(new Affordance
            {
                Id = "eat",
                Verb = "Get food from",
                Actors = ActorKind.Npc,
                Duration = 5f,
                BaseAppeal = 1.2f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Hunger, 0.7f) },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Contents) > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.AddState(StateKeys.Contents, -1f);
                    Restock(ctx.Sim, ctx.Object, 3f, 70f, "a delivery arrives");
                }
            });

            fridge.WithAffordance(new Affordance
            {
                Id = "empty_fridge",
                Verb = "Quietly empty",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 3f,
                Incrimination = 0.55f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Contents) > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Contents, 0f);
                    ctx.Object.SetState("drainedByPlayer", 1f);
                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.ObjectTampered,
                        Position = ctx.Object.Position,
                        TrueActorId = ctx.ActorId,
                        ObjectId = ctx.Object.Id,
                        VictimId = ctx.Object.OwnerId,
                        Loudness = 0.15f,
                        Severity = 0.55f,
                        LeavesEvidence = true,
                        Description = "the fridge has been cleared out"
                    });
                }
            });

            fridge.WithAffordance(new Affordance
            {
                Id = "leave_open",
                Verb = "Leave the door of",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 1f,
                Incrimination = 0.2f,
                Precondition = delegate(SmartObject o, Npc npc)
                {
                    return o.GetState("doorOpen") <= 0f && o.GetState(StateKeys.Contents) > 0f;
                },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState("doorOpen", 1f);
                    ctx.Sim.Announce(FeedbackKind.Info, "Fridge door left open. Give it a minute.");

                    ctx.Sim.Schedule(55f, "fridge spoils", delegate(Simulation s)
                    {
                        if (ctx.Object.GetState("doorOpen") <= 0f) return;
                        ctx.Object.SetState(StateKeys.Contents, 0f);
                        ctx.Object.SetState("drainedByPlayer", 1f);
                        ctx.Object.SetState("doorOpen", 0f);

                        s.Announce(FeedbackKind.Payoff, "Everything in the fridge has spoiled");
                        s.Publish(new WorldEvent
                        {
                            Kind = EventKind.ObjectTampered,
                            Position = ctx.Object.Position,
                            TrueActorId = ctx.ActorId,
                            ObjectId = ctx.Object.Id,
                            VictimId = ctx.Object.OwnerId,
                            Loudness = 0.1f,
                            Severity = 0.7f,
                            LeavesEvidence = true,
                            Description = "everything in the fridge has spoiled"
                        });
                    });
                }
            });

            return fridge;
        }

        private static SmartObject BuildSalt()
        {
            SmartObject salt = ItemCatalogue.Make(Ids.Salt, "salt shaker", new Vec3(-4.6f, 0f, 1.4f),
                new Vec3(0.2f, 0.3f, 0.2f));
            salt.OwnerId = Ids.Chef;
            salt.Portable = true;
            salt.WithTag(Tags.Ingredient);

            salt.WithAffordance(new Affordance
            {
                Id = "swap_salt",
                Verb = "Swap the salt in",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 3f,
                Incrimination = 0.5f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Tampered) <= 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Tampered, 1f);
                    ctx.Object.SetState(StateKeys.Subtle, 1f);
                    ctx.Object.SetState("tamperedBy", 1f);
                    ctx.Sim.Announce(FeedbackKind.Info,
                        "Sugar in the salt shaker. Nobody knows until it's cooked.");
                }
            });

            ItemCatalogue.AddCarryVerbs(salt, 0.4f, 0.35f);
            return salt;
        }

        private static SmartObject BuildOil()
        {
            SmartObject oil = ItemCatalogue.Make(Ids.Oil, "oil bottle", new Vec3(-4.2f, 0f, -2.6f),
                new Vec3(0.22f, 0.45f, 0.22f));
            oil.Portable = true;
            oil.WithTag(Tags.Tool).WithState(StateKeys.Contents, 1f);

            oil.WithAffordance(new Affordance
            {
                Id = "pour_oil",
                Verb = "Pour out the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 2f,
                Incrimination = 0.6f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Contents) > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Contents, 0f);
                    ItemCatalogue.Spill(ctx.Sim, ctx.Sim.Player.Position, ctx.ActorId, "oil");
                }
            });

            ItemCatalogue.AddCarryVerbs(oil, 0.3f, 0.25f);
            return oil;
        }

        private static SmartObject BuildBell()
        {
            SmartObject bell = ItemCatalogue.Make(Ids.Bell, "service bell", new Vec3(1.2f, 0f, 0.6f),
                new Vec3(0.25f, 0.25f, 0.25f));
            bell.WithTag(Tags.Noisy);

            // The lure. Almost no suspicion attached: ringing a bell is not a
            // crime, it just puts everyone somewhere else for twenty seconds.
            bell.WithAffordance(new Affordance
            {
                Id = "ring",
                Verb = "Ring the",
                Actors = ActorKind.Player,
                Duration = 0.5f,
                Incrimination = 0.1f,
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.Noise,
                        Position = ctx.Object.Position,
                        ObjectId = ctx.Object.Id,
                        Loudness = 0.95f,
                        Severity = 0.1f,
                        Description = "the service bell rings"
                    });
                }
            });

            return bell;
        }

        /// <summary>
        /// Front-of-house needs its own facilities, or Marie and Eva spend the
        /// whole service in the kitchen and the dining room stands empty.
        /// </summary>
        private static SmartObject BuildCoffeeMachine()
        {
            SmartObject coffee = ItemCatalogue.Make(Ids.Coffee, "coffee machine", new Vec3(7.6f, 0f, 0.6f),
                new Vec3(0.6f, 0.7f, 0.5f));
            coffee.WithTag(Tags.Appliance).WithTag(Tags.Food).WithState(StateKeys.Contents, 6f);

            coffee.WithAffordance(new Affordance
            {
                Id = "coffee",
                Verb = "Make a coffee at the",
                Actors = ActorKind.Npc,
                Duration = 6f,
                Noise = 0.2f,
                BaseAppeal = 1.15f,
                Satisfies = new List<NeedDelta>
                {
                    new NeedDelta(NeedType.Hunger, 0.35f),
                    new NeedDelta(NeedType.Comfort, 0.35f)
                },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc)
                {
                    return o.GetState(StateKeys.Contents) > 0f && !o.IsBroken;
                },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.AddState(StateKeys.Contents, -1f);
                    Restock(ctx.Sim, ctx.Object, 6f, 90f, "someone refills the water tank");
                }
            });

            coffee.WithAffordance(new Affordance
            {
                Id = "drain_coffee",
                Verb = "Drain the tank of the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 2.5f,
                Incrimination = 0.45f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Contents) > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Contents, 0f);
                    ctx.Object.SetState("drainedByPlayer", 1f);
                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.ObjectTampered,
                        Position = ctx.Object.Position,
                        TrueActorId = ctx.ActorId,
                        ObjectId = ctx.Object.Id,
                        Loudness = 0.15f,
                        Severity = 0.4f,
                        LeavesEvidence = true,
                        Description = "the coffee machine has been drained dry"
                    });
                }
            });

            return coffee;
        }

        private static SmartObject BuildDesk()
        {
            SmartObject desk = ItemCatalogue.Make(Ids.Desk, "manager's desk", new Vec3(3.4f, 0f, -5.2f),
                new Vec3(1.4f, 0.8f, 0.8f));
            desk.OwnerId = Ids.Manager;
            desk.WithState(StateKeys.Tampered, 0f);

            desk.WithAffordance(new Affordance
            {
                Id = "paperwork",
                Verb = "Do the paperwork at the",
                Actors = ActorKind.Npc,
                Duration = 14f,
                BaseAppeal = 1.1f,
                Satisfies = new List<NeedDelta>
                {
                    new NeedDelta(NeedType.Order, 0.5f),
                    new NeedDelta(NeedType.Comfort, 0.25f)
                },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc)
                {
                    return o.GetState(StateKeys.Tampered) <= 0f;
                }
            });

            desk.WithAffordance(new Affordance
            {
                Id = "shuffle_papers",
                Verb = "Shuffle the papers on the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 3f,
                Incrimination = 0.5f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Tampered) <= 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Tampered, 1f);
                    ctx.Object.SetState(StateKeys.Subtle, 1f);
                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.ObjectTampered,
                        Position = ctx.Object.Position,
                        TrueActorId = ctx.ActorId,
                        ObjectId = ctx.Object.Id,
                        VictimId = ctx.Object.OwnerId,
                        Loudness = 0.1f,
                        Severity = 0.5f,
                        LeavesEvidence = true,
                        Description = "the paperwork is completely out of order"
                    });
                }
            });

            return desk;
        }

        // ------------------------------------------------------------------
        // Cast
        // ------------------------------------------------------------------

        private static void BuildNpcs(Simulation sim, World world)
        {
            Npc chef = MakeNpc(Ids.Chef, "Gordon", "head chef", new Vec3(-6f, 0f, 2.4f),
                Personality.Chef(), "kitchen");
            chef.IsTarget = true;
            chef.Needs.SetDecay(NeedType.Comfort, 0.014f);

            Npc waiter = MakeNpc(Ids.Waiter, "Marie", "waiter", new Vec3(5f, 0f, 0f),
                Personality.Waiter(), "dining");

            Npc dish = MakeNpc(Ids.Dishwasher, "Terry", "dishwasher", new Vec3(-7f, 0f, 5.6f),
                Personality.Dishwasher(), "washup");

            Npc manager = MakeNpc(Ids.Manager, "Eva", "manager", new Vec3(3f, 0f, -4f),
                Personality.Manager(), "floor");
            manager.MoveSpeed = 2.5f;

            // Bruno is the level's clock. He cannot walk past a broken thing, so
            // every trap the player sets is now a race: will Gordon get to the
            // stove before Bruno gets to the knobs? Dealing with Bruno - keeping
            // him busy, keeping him out, or timing around his rounds - is the
            // first real tactical problem the player has.
            Npc sous = MakeNpc(Ids.SousChef, "Bruno", "sous chef", new Vec3(-4.4f, 0f, 4.6f),
                Personality.SousChef(), "kitchen");
            sous.MoveSpeed = 2.8f;

            // Pip is the level's alibi. He genuinely knocks things over, so the
            // building has a history of unexplained incidents that had nothing to
            // do with the player - and a credulous, low-status body for blame to
            // land on when it does.
            Npc porter = MakeNpc(Ids.Porter, "Pip", "kitchen porter", new Vec3(-8.2f, 0f, 3.2f),
                Personality.Porter(), "washup");
            porter.MoveSpeed = 3.0f;

            world.Add(chef);
            world.Add(waiter);
            world.Add(dish);
            world.Add(manager);
            world.Add(sous);
            world.Add(porter);

            // Habits: the signature behaviours that make each of them legible.
            // The player learns "Gordon is always back at that stove" and builds
            // a plan around it.
            chef.AddHabit(Ids.Stove, "cook", 2.2f);
            chef.AddHabit(Ids.Stove, "taste", 1.9f);
            chef.AddHabit(Ids.Fridge, "eat", 1.3f);

            waiter.AddHabit(Ids.Table, "wipe", 2.0f);
            waiter.AddHabit(Ids.Coffee, "coffee", 1.8f);
            waiter.AddHabit(Ids.Radio, "listen", 1.4f);

            dish.AddHabit(Ids.Sink, "wash", 2.4f);
            dish.AddHabit(Ids.Bin, "empty_bin", 1.6f);

            manager.AddHabit(Ids.Desk, "paperwork", 2.1f);
            manager.AddHabit(Ids.Table, "wipe", 1.5f);
            manager.AddHabit(Ids.Radio, "turn_down", 1.8f);
            manager.AddHabit(Ids.Lights, "lights_on", 2.5f);

            sous.AddHabit(Ids.Stove, "cook", 1.6f);
            sous.AddHabit(Ids.Sink, "wash", 1.5f);
            sous.AddHabit(Ids.Table, "wipe", 1.7f);
            sous.AddHabit(Ids.Plant, "tidy_plant", 1.9f);

            porter.AddHabit(Ids.Bin, "empty_bin", 2.0f);
            porter.AddHabit(Ids.Fridge, "eat", 2.2f);
            porter.AddHabit(Ids.Sink, "wash", 1.3f);
            porter.AddHabit(Ids.Coffee, "coffee", 1.5f);

            // Seeded relationships. Gordon already half-blames Terry for everything,
            // which is exactly the crack the player can widen.
            chef.AddRelationship(Ids.Dishwasher, -0.45f);
            chef.AddRelationship(Ids.Waiter, 0.15f);
            chef.AddRelationship(Ids.Manager, -0.2f);
            chef.AddRelationship(Ids.SousChef, 0.55f);
            chef.AddRelationship(Ids.Porter, -0.35f);

            dish.AddRelationship(Ids.Chef, -0.3f);
            waiter.AddRelationship(Ids.Chef, 0.2f);
            waiter.AddRelationship(Ids.Dishwasher, 0.3f);
            manager.AddRelationship(Ids.Chef, 0.1f);

            // Bruno is Gordon's man, and resents being the only one who tidies.
            // Both of those are levers: turn Bruno against Gordon and the target
            // loses his repair service and gains an enemy in the same move.
            sous.AddRelationship(Ids.Chef, 0.6f);
            sous.AddRelationship(Ids.Porter, -0.5f);
            sous.AddRelationship(Ids.Dishwasher, -0.15f);

            porter.AddRelationship(Ids.Chef, -0.2f);
            porter.AddRelationship(Ids.SousChef, -0.3f);
            porter.AddRelationship(Ids.Dishwasher, 0.45f);
            manager.AddRelationship(Ids.Porter, -0.25f);
        }

        private static Npc MakeNpc(string id, string name, string role, Vec3 position,
            Personality personality, string homeZone)
        {
            Npc npc = new Npc
            {
                Id = id,
                Name = name,
                Role = role,
                Position = position,
                Personality = personality,
                HomeZoneId = homeZone
            };
            npc.Perception = PerceptionModel.FromPersonality(personality);
            return npc;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Refill a consumable a while after it runs dry. Without this the kitchen
        /// slowly empties over a long level and NPCs get frustrated for reasons
        /// the player had nothing to do with, which quietly breaks the rule that
        /// any anger on screen is anger the player caused.
        /// </summary>
        private static void Restock(Simulation sim, SmartObject obj, float amount,
            float delay, string description)
        {
            if (obj.GetState(StateKeys.Contents) > 0f) return;
            if (obj.GetState("restocking") > 0f) return;

            obj.SetState("restocking", 1f);
            sim.Schedule(delay, "restock " + obj.Id, delegate(Simulation s)
            {
                obj.SetState("restocking", 0f);

                // A player who emptied it on purpose keeps their sabotage: only a
                // naturally drained container refills.
                if (obj.GetState("drainedByPlayer") > 0f) return;

                obj.SetState(StateKeys.Contents, amount);
                s.Log(description);
            });
        }
    }
}
