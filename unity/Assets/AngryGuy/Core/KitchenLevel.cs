using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// The prototype level: a restaurant kitchen and dining room.
    ///
    /// Target: make Gordon (the chef) furious without him working out it was you.
    ///
    /// Nothing here is a scripted solution. Each object simply advertises what
    /// it offers and what state it is in. The "puzzle" is whatever the player
    /// works out from those parts.
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
            public const string Chair = "chair";
            public const string Coffee = "coffee";
            public const string Desk = "desk";
            public const string Door = "door";
            public const string Pantry = "pantry";
            public const string Booth = "booth";

            public const string Chef = "chef";
            public const string Waiter = "waiter";
            public const string Dishwasher = "dishwasher";
            public const string Manager = "manager";
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

            world.Zones.Add(new Zone { Id = "kitchen", Name = "Kitchen", Center = new Vec3(-5f, 0f, 1f), Radius = 4.5f });
            world.Zones.Add(new Zone { Id = "dining", Name = "Dining room", Center = new Vec3(5f, 0f, 1f), Radius = 4.5f });
            world.Zones.Add(new Zone { Id = "washup", Name = "Wash-up", Center = new Vec3(-7f, 0f, 6f), Radius = 2.5f });
            world.Zones.Add(new Zone { Id = "floor", Name = "Front of house", Center = new Vec3(3f, 0f, -4f), Radius = 4.5f });
            world.Zones.Add(new Zone { Id = "exit", Name = "Back door", Center = new Vec3(-8.5f, 0f, 8.5f), Radius = 1.6f });

            BuildObjects(sim, world);
            BuildNpcs(sim, world);

            sim.TargetNpcId = Ids.Chef;
            sim.ExitZoneId = "exit";
            sim.Player.Position = new Vec3(4f, 0f, -6f);

            sim.Log("Objective: make Gordon furious. Don't let anyone pin it on you. Then use the back door.");
            return sim;
        }

        // ------------------------------------------------------------------
        // Objects
        // ------------------------------------------------------------------

        private static void BuildObjects(Simulation sim, World world)
        {
            world.Add(BuildStove(world));
            world.Add(BuildPan());
            world.Add(BuildFridge());
            world.Add(BuildSalt());
            world.Add(BuildOil());
            world.Add(BuildBell());
            world.Add(BuildBin());
            world.Add(BuildSink());
            world.Add(BuildToilet());
            world.Add(BuildRadio());
            world.Add(BuildTable());
            world.Add(BuildDoor(world));
            world.Add(BuildHidingSpot(Ids.Pantry, "pantry", new Vec3(-8.6f, 0f, 1.6f)));
            world.Add(BuildHidingSpot(Ids.Booth, "corner booth", new Vec3(8.4f, 0f, 4.6f)));
            world.Add(BuildCoffeeMachine());
            world.Add(BuildDesk());
            world.Add(BuildChair("chair_a", new Vec3(4f, 0f, 2.4f)));
            world.Add(BuildChair("chair_b", new Vec3(6.2f, 0f, 2.4f)));

            // Kitchen staff need somewhere to take the weight off on their own side
            // of the wall, or they spend the whole service walking to the dining room.
            world.Add(BuildChair("stool", new Vec3(-3.4f, 0f, 5.2f)));
        }

        private static SmartObject BuildStove(World world)
        {
            SmartObject stove = At(Ids.Stove, "stove", new Vec3(-6f, 0f, 3f));
            stove.OwnerId = Ids.Chef;
            stove.Size = new Vec3(1.6f, 1.0f, 0.9f);
            stove.WithTag(Tags.Appliance).WithState(StateKeys.Broken, 0f).WithState(StateKeys.Heat, 0.5f);

            // --- NPC side ---------------------------------------------------
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

                    // The payoff lands later. If the salt was swapped an hour ago,
                    // this is the moment it detonates.
                    ctx.Sim.Schedule(14f, "dish finishes", delegate(Simulation s)
                    {
                        ctx.Object.SetState(StateKeys.Cooking, 0f);

                        SmartObject salt = s.World.GetObject(Ids.Salt);
                        bool saltRuined = salt != null && salt.GetState(StateKeys.Tampered) > 0f;
                        bool burned = ctx.Object.GetState(StateKeys.Heat) >= 0.95f;

                        if (!saltRuined && !burned) return;

                        Npc cook = s.World.GetNpc(cookId);

                        // Ground truth of who is responsible. NPCs still have to
                        // work it out for themselves - this only matters if someone
                        // happens to be watching the player when it lands.
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

            // --- Player side ------------------------------------------------
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
                    ctx.Sim.Log("You quietly turn the burner to maximum.");
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
            SmartObject pan = At(Ids.Pan, "good pan", new Vec3(-6f, 0f, 2.1f));
            pan.OwnerId = Ids.Chef;
            pan.Portable = true;
            pan.Size = new Vec3(0.45f, 0.18f, 0.45f);
            pan.WithTag(Tags.Tool);
            AddCarryAffordances(pan, 0.65f, 0.5f);
            return pan;
        }

        private static SmartObject BuildFridge()
        {
            SmartObject fridge = At(Ids.Fridge, "fridge", new Vec3(-8.4f, 0f, -2f));
            fridge.OwnerId = Ids.Chef;
            fridge.Size = new Vec3(1f, 2f, 0.9f);
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
                Effect = delegate(AffordanceContext ctx) { ctx.Object.AddState(StateKeys.Contents, -1f); }
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
                    ctx.Sim.Log("You leave the fridge door hanging open. Give it a minute.");

                    // Slow, deniable, and by the time it matters you are elsewhere.
                    ctx.Sim.Schedule(55f, "fridge spoils", delegate(Simulation s)
                    {
                        if (ctx.Object.GetState("doorOpen") <= 0f) return;
                        ctx.Object.SetState(StateKeys.Contents, 0f);
                        ctx.Object.SetState("doorOpen", 0f);
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
            SmartObject salt = At(Ids.Salt, "salt shaker", new Vec3(-4.6f, 0f, 1.4f));
            salt.OwnerId = Ids.Chef;
            salt.Portable = true;
            salt.Size = new Vec3(0.2f, 0.3f, 0.2f);
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
                    ctx.Sim.Log("Sugar in the salt shaker. Nobody will know until it's cooked.");
                }
            });

            AddCarryAffordances(salt, 0.4f, 0.35f);
            return salt;
        }

        private static SmartObject BuildOil()
        {
            SmartObject oil = At(Ids.Oil, "oil bottle", new Vec3(-4.2f, 0f, -2.6f));
            oil.Portable = true;
            oil.Size = new Vec3(0.22f, 0.45f, 0.22f);
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

                    Vec3 where = ctx.Sim.Player.Position;
                    SmartObject slick = At("slick_" + ctx.Sim.Events.Log.Count, "oil slick", where);
                    slick.Size = new Vec3(1.2f, 0.02f, 1.2f);
                    slick.WithTag(Tags.Mess).WithTag(Tags.Hazard);

                    slick.WithAffordance(new Affordance
                    {
                        Id = "mop",
                        Verb = "Mop up",
                        Actors = ActorKind.Npc,
                        Duration = 6f,
                        Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Order, 0.5f) },
                        Effect = delegate(AffordanceContext c)
                        {
                            c.Object.Tags.Remove(Tags.Hazard);
                            c.Object.Concealed = true;
                            c.Sim.Log(c.Npc.Name + " mops up the " + c.Object.Name + ".");
                        }
                    });

                    ctx.Sim.World.Add(slick);
                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.SpillCreated,
                        Position = where,
                        TrueActorId = ctx.ActorId,
                        ObjectId = slick.Id,
                        Loudness = 0.1f,
                        Severity = 0.4f,
                        LeavesEvidence = true,
                        Description = "there is oil all over the floor"
                    });
                }
            });

            AddCarryAffordances(oil, 0.3f, 0.25f);
            return oil;
        }

        private static SmartObject BuildBell()
        {
            SmartObject bell = At(Ids.Bell, "service bell", new Vec3(1.2f, 0f, 0.6f));
            bell.Size = new Vec3(0.25f, 0.25f, 0.25f);
            bell.WithTag(Tags.Noisy);

            // The lure. Almost no suspicion attached: ringing a bell is not a crime,
            // it just puts everyone somewhere else for twenty seconds.
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
                        TrueActorId = "",
                        ObjectId = ctx.Object.Id,
                        Loudness = 0.95f,
                        Severity = 0.1f,
                        Description = "the service bell rings"
                    });
                }
            });

            return bell;
        }

        private static SmartObject BuildBin()
        {
            SmartObject bin = At(Ids.Bin, "bin", new Vec3(-8.2f, 0f, 4.6f));
            bin.Size = new Vec3(0.7f, 1f, 0.7f);
            bin.WithTag(Tags.Container).WithState(StateKeys.Dirty, 1f);

            bin.WithAffordance(new Affordance
            {
                Id = "stash",
                Verb = "Stash what you're holding in the",
                Actors = ActorKind.Player,
                Duration = 1.5f,
                Incrimination = 0.45f,
                Precondition = delegate(SmartObject o, Npc npc) { return true; },
                Effect = delegate(AffordanceContext ctx)
                {
                    Simulation sim = ctx.Sim;
                    if (!sim.Player.IsCarrying)
                    {
                        sim.Log("Your hands are empty.");
                        return;
                    }

                    SmartObject held = sim.World.GetObject(sim.Player.CarryingObjectId);
                    if (held == null) return;

                    held.HeldBy = "";
                    held.Concealed = true;
                    held.Position = ctx.Object.Position;
                    held.SetState("inBin", 1f);
                    sim.Player.CarryingObjectId = "";
                    sim.Log("You drop the " + held.Name + " in the bin.");
                }
            });

            // A tidy NPC emptying the bin will undo the player's hiding place.
            // The world pushing back is what stops one trick solving every level.
            bin.WithAffordance(new Affordance
            {
                Id = "empty_bin",
                Verb = "Empty the",
                Actors = ActorKind.Npc,
                Duration = 7f,
                BaseAppeal = 0.9f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Order, 0.45f) },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Dirty) > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Dirty, 0f);
                    Simulation sim = ctx.Sim;

                    for (int i = 0; i < sim.World.Objects.Count; i++)
                    {
                        SmartObject o = sim.World.Objects[i];
                        if (o.GetState("inBin") <= 0f) continue;

                        o.SetState("inBin", 0f);
                        o.Concealed = false;
                        o.Position = ctx.Object.Position;

                        sim.Publish(new WorldEvent
                        {
                            Kind = EventKind.Discovery,
                            Position = ctx.Object.Position,
                            TrueActorId = "",
                            ObjectId = o.Id,
                            VictimId = o.OwnerId,
                            Loudness = 0.5f,
                            Severity = 0.6f,
                            LeavesEvidence = true,
                            Description = ctx.Npc.Name + " finds the " + o.Name + " in the bin"
                        });

                        ctx.Npc.Say("Why is the " + o.Name + " in the BIN?");
                    }

                    // Bins fill up again, so this stays available all level.
                    sim.Schedule(45f, "bin fills up", delegate(Simulation s)
                    {
                        ctx.Object.SetState(StateKeys.Dirty, 1f);
                    });
                }
            });

            return bin;
        }

        private static SmartObject BuildSink()
        {
            SmartObject sink = At(Ids.Sink, "sink", new Vec3(-7f, 0f, 6.4f));
            sink.OwnerId = Ids.Dishwasher;
            sink.Size = new Vec3(1.4f, 0.9f, 0.7f);
            sink.WithTag(Tags.Appliance).WithState(StateKeys.Broken, 0f);

            sink.WithAffordance(new Affordance
            {
                Id = "wash",
                Verb = "Wash up at",
                Actors = ActorKind.Npc,
                Duration = 10f,
                Noise = 0.2f,
                BaseAppeal = 1.25f,
                Satisfies = new List<NeedDelta>
                {
                    new NeedDelta(NeedType.Order, 0.6f),
                    new NeedDelta(NeedType.Comfort, 0.25f)
                },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc) { return !o.IsBroken; }
            });

            sink.WithAffordance(new Affordance
            {
                Id = "block_sink",
                Verb = "Jam a rag into",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 2.5f,
                Incrimination = 0.6f,
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
                        Loudness = 0.2f,
                        Severity = 0.6f,
                        LeavesEvidence = true,
                        Description = "the sink is blocked and overflowing"
                    });
                }
            });

            return sink;
        }

        private static SmartObject BuildToilet()
        {
            SmartObject toilet = At(Ids.Toilet, "staff toilet", new Vec3(-2f, 0f, -7.6f));
            toilet.Size = new Vec3(0.7f, 1f, 0.7f);
            toilet.WithTag(Tags.Toilet).WithState(StateKeys.Broken, 0f);

            toilet.WithAffordance(new Affordance
            {
                Id = "use_toilet",
                Verb = "Use the",
                Actors = ActorKind.Npc,
                Duration = 6f,
                BaseAppeal = 1.4f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Bladder, 0.95f) },
                ArrivalPrecondition = delegate(SmartObject o, Npc npc) { return !o.IsBroken; }
            });

            toilet.WithAffordance(new Affordance
            {
                Id = "block_toilet",
                Verb = "Block the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 3f,
                Incrimination = 0.4f,
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
                        Loudness = 0.15f,
                        Severity = 0.5f,
                        LeavesEvidence = true,
                        Description = "the staff toilet is blocked"
                    });
                }
            });

            return toilet;
        }

        private static SmartObject BuildRadio()
        {
            SmartObject radio = At(Ids.Radio, "radio", new Vec3(7.4f, 0f, -3f));
            radio.Size = new Vec3(0.5f, 0.3f, 0.3f);
            radio.WithTag(Tags.Social).WithState(StateKeys.Volume, 0.3f);

            radio.WithAffordance(new Affordance
            {
                Id = "listen",
                Verb = "Listen to the",
                Actors = ActorKind.Npc,
                Duration = 8f,
                Satisfies = new List<NeedDelta>
                {
                    new NeedDelta(NeedType.Comfort, 0.4f),
                    new NeedDelta(NeedType.Social, 0.2f)
                },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Volume) < 0.9f; }
            });

            radio.WithAffordance(new Affordance
            {
                Id = "crank_radio",
                Verb = "Crank up the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 1f,
                Incrimination = 0.3f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Volume) < 0.9f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Volume, 1f);
                    ScheduleRacket(ctx.Sim, ctx.Object, 6);
                }
            });

            radio.WithAffordance(new Affordance
            {
                Id = "turn_down",
                Verb = "Turn down the",
                Actors = ActorKind.Npc,
                Duration = 2f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Order, 0.35f) },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Volume) >= 0.9f; },
                Effect = delegate(AffordanceContext ctx) { ctx.Object.SetState(StateKeys.Volume, 0.3f); }
            });

            return radio;
        }

        private static void ScheduleRacket(Simulation sim, SmartObject radio, int repeats)
        {
            if (repeats <= 0) return;

            sim.Schedule(7f, "radio blares", delegate(Simulation s)
            {
                if (radio.GetState(StateKeys.Volume) < 0.9f) return;

                s.Publish(new WorldEvent
                {
                    Kind = EventKind.Noise,
                    Position = radio.Position,
                    TrueActorId = "",
                    ObjectId = radio.Id,
                    Loudness = 0.75f,
                    Severity = 0.15f,
                    Description = "the radio is blaring"
                });

                for (int i = 0; i < s.World.Npcs.Count; i++)
                {
                    Npc npc = s.World.Npcs[i];
                    if (Vec3.FlatDistance(npc.Position, radio.Position) > 9f) continue;
                    AngerModel.Add(npc, AngerModel.DisorderSeen(npc, 0.5f), s, "that bloody radio");
                }

                ScheduleRacket(s, radio, repeats - 1);
            });
        }

        private static SmartObject BuildTable()
        {
            SmartObject table = At(Ids.Table, "dining table", new Vec3(5.2f, 0f, 2.4f));
            table.Size = new Vec3(2.2f, 0.8f, 1.2f);
            table.WithState(StateKeys.Dirty, 1f);

            table.WithAffordance(new Affordance
            {
                Id = "wipe",
                Verb = "Wipe down the",
                Actors = ActorKind.Npc,
                Duration = 6f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Order, 0.4f) },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Dirty) > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Dirty, 0f);
                    ctx.Sim.Schedule(40f, "table gets dirty", delegate(Simulation s)
                    {
                        ctx.Object.SetState(StateKeys.Dirty, 1f);
                    });
                }
            });

            return table;
        }

        /// <summary>
        /// The swing door in the only gap between kitchen and dining room.
        /// Shutting it is the cheapest, least incriminating thing in the level:
        /// it costs nothing, looks like nothing, and blinds half the cast.
        /// </summary>
        private static SmartObject BuildDoor(World world)
        {
            SmartObject door = At(Ids.Door, "swing door", new Vec3(0f, 0f, 0f));
            door.Size = new Vec3(0.2f, 2.1f, 2.2f);
            door.WithTag(Tags.Door).WithState("open", 1f);

            door.WithAffordance(new Affordance
            {
                Id = "close_door",
                Verb = "Close the",
                Actors = ActorKind.Player,
                Duration = 0.8f,
                Noise = 0.25f,
                Incrimination = 0.05f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("open") > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState("open", 0f);
                    ctx.Sim.Announce(FeedbackKind.Info, "Door shut - the kitchen can't see the dining room now");
                }
            });

            door.WithAffordance(new Affordance
            {
                Id = "open_door",
                Verb = "Open the",
                Actors = ActorKind.Player,
                Duration = 0.8f,
                Noise = 0.25f,
                Incrimination = 0.05f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("open") <= 0f; },
                Effect = delegate(AffordanceContext ctx) { ctx.Object.SetState("open", 1f); }
            });

            world.Doors.Add(new DoorBlocker
            {
                Object = door,
                Segment = new Wall(0f, -1.2f, 0f, 1.2f)
            });

            return door;
        }

        /// <summary>
        /// Somewhere to wait out a search. Hiding costs the player all their
        /// agency while they do it, which is what stops it being a free win.
        /// </summary>
        private static SmartObject BuildHidingSpot(string id, string name, Vec3 position)
        {
            SmartObject spot = At(id, name, position);
            spot.Size = new Vec3(1.1f, 1.9f, 1.1f);
            spot.WithTag(Tags.Hiding);

            spot.WithAffordance(new Affordance
            {
                Id = "hide",
                Verb = "Hide in the",
                Actors = ActorKind.Player,
                Duration = 0.6f,
                Incrimination = 0.25f,
                Precondition = delegate(SmartObject o, Npc npc) { return true; },
                Effect = delegate(AffordanceContext ctx) { ctx.Sim.PlayerToggleHide(ctx.Object); }
            });

            return spot;
        }

        /// <summary>
        /// Front-of-house needs its own facilities. Without these, Marie and Eva
        /// spend the entire service walking into the kitchen for every need, which
        /// both looks wrong and leaves the dining room empty.
        /// </summary>
        private static SmartObject BuildCoffeeMachine()
        {
            SmartObject coffee = At(Ids.Coffee, "coffee machine", new Vec3(7.6f, 0f, 0.6f));
            coffee.Size = new Vec3(0.6f, 0.7f, 0.5f);
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
                Effect = delegate(AffordanceContext ctx) { ctx.Object.AddState(StateKeys.Contents, -1f); }
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
            SmartObject desk = At(Ids.Desk, "manager's desk", new Vec3(3.4f, 0f, -5.2f));
            desk.OwnerId = Ids.Manager;
            desk.Size = new Vec3(1.4f, 0.8f, 0.8f);
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

        private static SmartObject BuildChair(string id, Vec3 position)
        {
            SmartObject chair = At(id, "chair", position);
            chair.Size = new Vec3(0.45f, 0.9f, 0.45f);
            chair.WithTag(Tags.Seat);

            chair.WithAffordance(new Affordance
            {
                Id = "sit",
                Verb = "Sit on the",
                Actors = ActorKind.Npc,
                Duration = 9f,
                Satisfies = new List<NeedDelta>
                {
                    new NeedDelta(NeedType.Comfort, 0.5f),
                    new NeedDelta(NeedType.Energy, 0.35f)
                }
            });

            return chair;
        }

        // ------------------------------------------------------------------
        // NPCs
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

            world.Add(chef);
            world.Add(waiter);
            world.Add(dish);
            world.Add(manager);

            // Seeded relationships. Gordon already half-blames Terry for everything,
            // which is exactly the crack the player can widen.
            chef.AddRelationship(Ids.Dishwasher, -0.45f);
            chef.AddRelationship(Ids.Waiter, 0.15f);
            chef.AddRelationship(Ids.Manager, -0.2f);

            dish.AddRelationship(Ids.Chef, -0.3f);
            waiter.AddRelationship(Ids.Chef, 0.2f);
            waiter.AddRelationship(Ids.Dishwasher, 0.3f);
            manager.AddRelationship(Ids.Chef, 0.1f);
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
        // Helpers
        // ------------------------------------------------------------------

        private static SmartObject At(string id, string name, Vec3 position)
        {
            return new SmartObject
            {
                Id = id,
                Name = name,
                Position = position,
                HomePosition = position
            };
        }

        private static void AddCarryAffordances(SmartObject obj, float takeIncrimination, float dropIncrimination)
        {
            obj.WithAffordance(new Affordance
            {
                Id = "take",
                Verb = "Take the",
                Actors = ActorKind.Player,
                Duration = 0.8f,
                Incrimination = takeIncrimination,
                Precondition = delegate(SmartObject o, Npc npc) { return o.HeldBy.Length == 0 && !o.Concealed; },
                Effect = delegate(AffordanceContext ctx)
                {
                    Simulation sim = ctx.Sim;
                    if (sim.Player.IsCarrying)
                    {
                        sim.Log("You're already carrying something.");
                        return;
                    }

                    ctx.Object.HeldBy = sim.Player.Id;
                    sim.Player.CarryingObjectId = ctx.Object.Id;

                    sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.ObjectTaken,
                        Position = ctx.Object.HomePosition,
                        TrueActorId = ctx.ActorId,
                        ObjectId = ctx.Object.Id,
                        VictimId = ctx.Object.OwnerId,
                        Loudness = 0.1f,
                        Severity = ctx.Object.OwnerId.Length > 0 ? 0.55f : 0.3f,
                        LeavesEvidence = true,
                        Description = "the " + ctx.Object.Name + " is missing"
                    });
                }
            });

            obj.WithAffordance(new Affordance
            {
                Id = "throw",
                Verb = "Throw the",
                Actors = ActorKind.Player,
                Duration = 0.4f,
                Incrimination = 0.4f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.HeldBy == PlayerAvatar.PlayerId; },
                Effect = delegate(AffordanceContext ctx) { ctx.Sim.PlayerThrow(); }
            });

            obj.WithAffordance(new Affordance
            {
                Id = "drop",
                Verb = "Put down the",
                Actors = ActorKind.Player,
                Duration = 0.5f,
                Incrimination = dropIncrimination,
                Precondition = delegate(SmartObject o, Npc npc) { return o.HeldBy == PlayerAvatar.PlayerId; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.HeldBy = "";
                    ctx.Object.Position = ctx.Sim.Player.Position;
                    ctx.Sim.Player.CarryingObjectId = "";
                }
            });
        }
    }
}
