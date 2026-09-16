using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// Every prop that is not specific to one level, built once and reusable
    /// anywhere.
    ///
    /// A level should be a list of things and where they go, not a thousand lines
    /// of object definitions. Anything here can be dropped into a new level and
    /// every NPC immediately knows what to do with it, because behaviour lives in
    /// the affordances rather than in the AI.
    ///
    /// Level-specific interplay - the stove that needs *that* pan, the salt that
    /// ruins *that* dish - stays in the level file where it belongs.
    /// </summary>
    public static class ItemCatalogue
    {
        // ------------------------------------------------------------------
        // Furniture and fittings
        // ------------------------------------------------------------------

        public static SmartObject Chair(string id, Vec3 position, string name = "chair")
        {
            SmartObject chair = Make(id, name, position, new Vec3(0.45f, 0.9f, 0.45f));
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

        public static SmartObject Table(string id, Vec3 position, string name = "table")
        {
            SmartObject table = Make(id, name, position, new Vec3(2.2f, 0.8f, 1.2f));
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

        public static SmartObject Toilet(string id, Vec3 position, string name = "toilet")
        {
            SmartObject toilet = Make(id, name, position, new Vec3(0.7f, 1f, 0.7f));
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

            toilet.WithAffordance(Break("block_toilet", "Block the", 0.4f, 0.15f,
                "the toilet is blocked", 0.5f));

            return toilet;
        }

        public static SmartObject Sink(string id, Vec3 position, string ownerId, string name = "sink")
        {
            SmartObject sink = Make(id, name, position, new Vec3(1.4f, 0.9f, 0.7f));
            sink.OwnerId = ownerId;
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

            sink.WithAffordance(Break("block_sink", "Jam a rag into", 0.6f, 0.2f,
                "the sink is blocked and overflowing", 0.6f));

            return sink;
        }

        public static SmartObject Bin(string id, Vec3 position, string name = "bin")
        {
            SmartObject bin = Make(id, name, position, new Vec3(0.7f, 1f, 0.7f));
            bin.WithTag(Tags.Container).WithState(StateKeys.Dirty, 1f);

            bin.WithAffordance(new Affordance
            {
                Id = "stash",
                Verb = "Stash what you're holding in the",
                Actors = ActorKind.Player,
                Duration = 1.5f,
                Incrimination = 0.45f,
                Effect = delegate(AffordanceContext ctx)
                {
                    Simulation sim = ctx.Sim;
                    if (!sim.Player.IsCarrying)
                    {
                        sim.Announce(FeedbackKind.Info, "Your hands are empty.");
                        return;
                    }

                    SmartObject held = sim.World.GetObject(sim.Player.CarryingObjectId);
                    if (held == null) return;

                    held.HeldBy = "";
                    held.Concealed = true;
                    held.Position = ctx.Object.Position;
                    held.SetState("inBin", 1f);
                    sim.Player.CarryingObjectId = "";
                    sim.Announce(FeedbackKind.Info, "You drop the " + held.Name + " in the bin.");
                }
            });

            // A tidy NPC emptying the bin undoes the player's hiding place. The
            // world pushing back is what stops one trick solving every level.
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
                            ObjectId = o.Id,
                            VictimId = o.OwnerId,
                            Loudness = 0.5f,
                            Severity = 0.6f,
                            LeavesEvidence = true,
                            Description = ctx.Npc.Name + " finds the " + o.Name + " in the bin"
                        });

                        ctx.Npc.Say("Why is the " + o.Name + " in the BIN?");
                    }

                    sim.Schedule(45f, "bin fills up", delegate(Simulation s)
                    {
                        ctx.Object.SetState(StateKeys.Dirty, 1f);
                    });
                }
            });

            return bin;
        }

        public static SmartObject Radio(string id, Vec3 position, string name = "radio")
        {
            SmartObject radio = Make(id, name, position, new Vec3(0.5f, 0.3f, 0.3f));
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
                    Racket(ctx.Sim, ctx.Object, 6);
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

        private static void Racket(Simulation sim, SmartObject radio, int repeats)
        {
            if (repeats <= 0) return;

            sim.Schedule(7f, "radio blares", delegate(Simulation s)
            {
                if (radio.GetState(StateKeys.Volume) < 0.9f) return;

                s.Publish(new WorldEvent
                {
                    Kind = EventKind.Noise,
                    Position = radio.Position,
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

                Racket(s, radio, repeats - 1);
            });
        }

        // ------------------------------------------------------------------
        // Doors, hiding places, lockers
        // ------------------------------------------------------------------

        public static SmartObject Door(string id, Vec3 position, Wall segment, World world,
            string name = "swing door")
        {
            SmartObject door = Make(id, name, position, new Vec3(0.2f, 2.1f, 2.2f));
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
                    ctx.Sim.Announce(FeedbackKind.Info, "Door shut - that cuts the sight line");
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

            world.Doors.Add(new DoorBlocker { Object = door, Segment = segment });
            return door;
        }

        public static SmartObject HidingSpot(string id, string name, Vec3 position)
        {
            SmartObject spot = Make(id, name, position, new Vec3(1.1f, 1.9f, 1.1f));
            spot.WithTag(Tags.Hiding);

            spot.WithAffordance(new Affordance
            {
                Id = "hide",
                Verb = "Hide in the",
                Actors = ActorKind.Player,
                Duration = 0.6f,
                Incrimination = 0.25f,
                Effect = delegate(AffordanceContext ctx) { ctx.Sim.PlayerToggleHide(ctx.Object); }
            });

            return spot;
        }

        /// <summary>
        /// Where the uniforms live. Stealing one is the difference between
        /// sneaking through a kitchen and strolling through it.
        /// </summary>
        public static SmartObject Locker(string id, Vec3 position, string[] outfitIds,
            string name = "staff lockers")
        {
            SmartObject locker = Make(id, name, position, new Vec3(1.2f, 2f, 0.6f));
            locker.WithTag(Tags.Container);

            for (int i = 0; i < outfitIds.Length; i++)
            {
                string outfitId = outfitIds[i];
                Outfit sample = Outfit.ById(outfitId);

                locker.WithAffordance(new Affordance
                {
                    Id = "wear_" + outfitId,
                    Verb = "Put on the " + sample.Name + " from the",
                    Actors = ActorKind.Player,
                    Duration = 4f,
                    Incrimination = 0.7f,
                    Precondition = delegate(SmartObject o, Npc npc)
                    {
                        return true;
                    },
                    Effect = delegate(AffordanceContext ctx)
                    {
                        Outfit outfit = Outfit.ById(outfitId);
                        ctx.Sim.Player.Outfit = outfit;
                        ctx.Sim.Announce(FeedbackKind.Info,
                            "You're in " + outfit.Name + " now - you belong in the kitchen");
                    }
                });
            }

            locker.WithAffordance(new Affordance
            {
                Id = "wear_civilian",
                Verb = "Change back into your own clothes at the",
                Actors = ActorKind.Player,
                Duration = 4f,
                Incrimination = 0.5f,
                Precondition = delegate(SmartObject o, Npc npc) { return true; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Sim.Player.Outfit = Outfit.Civilian();
                    ctx.Sim.Announce(FeedbackKind.Info, "Back in your own clothes.");
                }
            });

            return locker;
        }

        // ------------------------------------------------------------------
        // Chaos tools
        // ------------------------------------------------------------------

        /// <summary>
        /// Kill the lights and everybody's eyes get much worse. Cheap, reversible
        /// by any tidy NPC, and it changes the whole level rather than one object.
        /// </summary>
        public static SmartObject LightSwitch(string id, Vec3 position, string name = "light switch")
        {
            SmartObject sw = Make(id, name, position, new Vec3(0.25f, 0.3f, 0.1f));
            sw.WithState("lightsOn", 1f);

            sw.WithAffordance(new Affordance
            {
                Id = "lights_off",
                Verb = "Kill the lights at the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 0.8f,
                Noise = 0.15f,
                Incrimination = 0.35f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("lightsOn") > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState("lightsOn", 0f);
                    ctx.Sim.World.LightLevel = 0.35f;
                    ctx.Sim.Announce(FeedbackKind.Info, "Lights out - nobody can see much now");

                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.Noise,
                        Position = ctx.Object.Position,
                        ObjectId = ctx.Object.Id,
                        Loudness = 0.2f,
                        Severity = 0.3f,
                        Description = "the lights go out"
                    });
                }
            });

            sw.WithAffordance(new Affordance
            {
                Id = "lights_on",
                Verb = "Put the lights back on at the",
                Actors = ActorKind.Both,
                Duration = 1.5f,
                BaseAppeal = 1.6f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Order, 0.5f) },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("lightsOn") <= 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState("lightsOn", 1f);
                    ctx.Sim.World.LightLevel = 1f;
                }
            });

            return sw;
        }

        /// <summary>
        /// The bluntest instrument in the game. Empties the building for half a
        /// minute, and gets you caught instantly if anyone sees you pull it.
        /// </summary>
        public static SmartObject FireAlarm(string id, Vec3 position, string name = "fire alarm")
        {
            SmartObject alarm = Make(id, name, position, new Vec3(0.3f, 0.4f, 0.15f));
            alarm.WithTag(Tags.Noisy);

            alarm.WithAffordance(new Affordance
            {
                Id = "pull_alarm",
                Verb = "Pull the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 1f,
                Noise = 1f,
                Incrimination = 0.95f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("pulled") <= 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState("pulled", 1f);
                    ctx.Sim.TriggerAlarm(35f);

                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.Noise,
                        Position = ctx.Object.Position,
                        TrueActorId = ctx.ActorId,
                        ObjectId = ctx.Object.Id,
                        Loudness = 1f,
                        Severity = 0.5f,
                        LeavesEvidence = true,
                        Description = "the fire alarm is going off"
                    });

                    // Alarms get reset, and then it can be pulled again.
                    ctx.Sim.Schedule(70f, "alarm reset", delegate(Simulation s)
                    {
                        ctx.Object.SetState("pulled", 0f);
                    });
                }
            });

            return alarm;
        }

        /// <summary>A portable source of floor hazard, so spills are not a one-off.</summary>
        public static SmartObject MopBucket(string id, Vec3 position, string name = "mop bucket")
        {
            SmartObject bucket = Make(id, name, position, new Vec3(0.45f, 0.5f, 0.45f));
            bucket.Portable = true;
            bucket.WithTag(Tags.Tool).WithState(StateKeys.Contents, 1f);

            bucket.WithAffordance(new Affordance
            {
                Id = "kick_over",
                Verb = "Kick over the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 1.2f,
                Noise = 0.5f,
                Incrimination = 0.6f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState(StateKeys.Contents) > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState(StateKeys.Contents, 0f);
                    Spill(ctx.Sim, ctx.Sim.Player.Position, ctx.ActorId, "dirty water");
                }
            });

            AddCarryVerbs(bucket, 0.3f, 0.25f);
            return bucket;
        }

        /// <summary>Decor that can be knocked over, and a place to lose small objects.</summary>
        public static SmartObject Plant(string id, Vec3 position, string name = "potted plant")
        {
            SmartObject plant = Make(id, name, position, new Vec3(0.6f, 1.3f, 0.6f));
            plant.WithState("knocked", 0f);

            plant.WithAffordance(new Affordance
            {
                Id = "knock_over",
                Verb = "Knock over the",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 0.8f,
                Noise = 0.65f,
                Incrimination = 0.55f,
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("knocked") <= 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState("knocked", 1f);
                    ctx.Object.Tags.Add(Tags.Mess);

                    ctx.Sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.ObjectBroken,
                        Position = ctx.Object.Position,
                        TrueActorId = ctx.ActorId,
                        ObjectId = ctx.Object.Id,
                        Loudness = 0.7f,
                        Severity = 0.4f,
                        LeavesEvidence = true,
                        Description = "soil all over the floor from the " + ctx.Object.Name
                    });
                }
            });

            plant.WithAffordance(new Affordance
            {
                Id = "tidy_plant",
                Verb = "Sweep up around the",
                Actors = ActorKind.Npc,
                Duration = 8f,
                Satisfies = new List<NeedDelta> { new NeedDelta(NeedType.Order, 0.5f) },
                Precondition = delegate(SmartObject o, Npc npc) { return o.GetState("knocked") > 0f; },
                Effect = delegate(AffordanceContext ctx)
                {
                    ctx.Object.SetState("knocked", 0f);
                    ctx.Object.Tags.Remove(Tags.Mess);
                }
            });

            return plant;
        }

        // ------------------------------------------------------------------
        // Shared helpers
        // ------------------------------------------------------------------

        /// <summary>Creates a slick at a point. Shared by anything that can spill.</summary>
        public static void Spill(Simulation sim, Vec3 where, string actorId, string what)
        {
            SmartObject slick = Make("slick_" + sim.Events.Log.Count, what + " on the floor", where,
                new Vec3(1.2f, 0.02f, 1.2f));
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

            sim.World.Add(slick);
            sim.Publish(new WorldEvent
            {
                Kind = EventKind.SpillCreated,
                Position = where,
                TrueActorId = actorId,
                ObjectId = slick.Id,
                Loudness = 0.1f,
                Severity = 0.4f,
                LeavesEvidence = true,
                Description = "there is " + what + " all over the floor"
            });
        }

        /// <summary>Take / throw / put down, for anything the player can pick up.</summary>
        public static void AddCarryVerbs(SmartObject obj, float takeIncrimination, float dropIncrimination)
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
                        sim.Announce(FeedbackKind.Info, "You're already carrying something.");
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

        /// <summary>
        /// Plant whatever you are carrying in somebody's patch.
        ///
        /// This is the verb the whole design was missing. Stealing the chef's pan
        /// makes him angry at nobody in particular; leaving it in the
        /// dishwasher's sink makes him angry at the dishwasher. The mechanism is
        /// not a special case - the object is genuinely sitting in Terry's
        /// workspace, Terry is genuinely always standing there, and the ordinary
        /// blame logic draws the obvious wrong conclusion on its own.
        ///
        /// Cheap if nobody sees you, ruinous if they do, which is exactly the
        /// risk curve the game wants.
        /// </summary>
        public static void AddPlantVerb(SmartObject station, string ownerName)
        {
            station.WithAffordance(new Affordance
            {
                Id = "plant",
                Verb = "Plant what you're holding on",
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 1.6f,
                Incrimination = 0.65f,
                Precondition = delegate(SmartObject o, Npc npc) { return npc == null; },
                PlayerPrecondition = delegate(SmartObject o, Simulation sim)
                {
                    return sim.Player.IsCarrying && sim.Player.CarryingObjectId != o.Id;
                },
                Effect = delegate(AffordanceContext ctx)
                {
                    Simulation sim = ctx.Sim;
                    SmartObject held = sim.World.GetObject(sim.Player.CarryingObjectId);
                    if (held == null) return;

                    held.HeldBy = "";
                    held.Concealed = false;
                    held.Position = new Vec3(
                        ctx.Object.Position.X + 0.6f,
                        ctx.Object.Position.Y,
                        ctx.Object.Position.Z + 0.4f);
                    sim.Player.CarryingObjectId = "";

                    sim.Announce(FeedbackKind.Info,
                        "The " + held.Name + " is now sitting in " + ownerName + "'s patch. "
                        + "Let somebody find it.");

                    sim.Publish(new WorldEvent
                    {
                        Kind = EventKind.ObjectTampered,
                        Position = held.Position,
                        TrueActorId = ctx.ActorId,
                        ObjectId = held.Id,
                        VictimId = held.OwnerId,
                        Loudness = 0.15f,
                        Severity = 0.5f,
                        LeavesEvidence = true,
                        Description = "the " + held.Name + " has turned up at the " + ctx.Object.Name
                    });
                }
            });
        }

        /// <summary>A generic "break it so it stops working" player verb.</summary>
        private static Affordance Break(string id, string verb, float incrimination, float noise,
            string description, float severity)
        {
            return new Affordance
            {
                Id = id,
                Verb = verb,
                Actors = ActorKind.Player,
                IsSabotage = true,
                Duration = 2.5f,
                Noise = noise,
                Incrimination = incrimination,
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
                        Loudness = noise,
                        Severity = severity,
                        LeavesEvidence = true,
                        Description = description
                    });
                }
            };
        }

        public static SmartObject Make(string id, string name, Vec3 position, Vec3 size)
        {
            return new SmartObject
            {
                Id = id,
                Name = name,
                Position = position,
                HomePosition = position,
                Size = size
            };
        }
    }
}
