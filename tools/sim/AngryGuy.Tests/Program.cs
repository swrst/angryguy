using System;
using System.Collections.Generic;
using AngryGuy.Core;

namespace AngryGuy.Tests
{
    public static class Program
    {
        private static int _passed;
        private static int _failed;

        public static int Main(string[] args)
        {
            Console.WriteLine("AngryGuy simulation tests");
            Console.WriteLine("=========================");

            Test("Vec3 distance ignores height", Vec3FlatDistance);
            Test("RNG is deterministic per seed", RngDeterminism);
            Test("Need urgency is non-linear", NeedUrgency);

            Test("Utility AI chooses the most urgent need", UtilityPicksUrgentNeed);
            Test("Utility AI ignores sabotage affordances", UtilityIgnoresSabotage);
            Test("Hidden faults are still advertised, and fail on arrival", HiddenFaultsAdvertiseAnyway);
            Test("Visible state is respected when planning", VisibleStateRespectedWhenPlanning);
            Test("Utility AI backs off after a failure", UtilityAvoidsFailedObject);

            Test("Removing the pan makes the chef's plan fail", SabotageCausesPlanFailure);
            Test("A failed plan makes the NPC angrier", PlanFailureRaisesAnger);
            Test("Repeated failures escalate", FrustrationEscalates);

            Test("Blame lands on whoever was seen nearby", BlameUsesOpportunity);
            Test("No evidence means no suspect", BlameNeedsEvidence);
            Test("Paranoid NPCs blame on thinner evidence", ParanoiaLowersBar);
            Test("Disliked NPCs are easier to blame", MotiveAffectsBlame);

            Test("Gossip spreads suspicion between NPCs", GossipSpreadsSuspicion);
            Test("Memory fades over time", MemoryDecays);

            Test("Delayed effects fire on schedule", DelayedEffectsFire);
            Test("Being watched while sabotaging raises suspicion", SeenSabotageRaisesSuspicion);
            Test("Sabotaging unobserved raises no suspicion", UnseenSabotageIsClean);

            Test("Walls block line of sight", WallsBlockSight);
            Test("A shut door blocks line of sight", ShutDoorBlocksSight);
            Test("Hiding makes the player unseeable", HidingWorks);
            Test("Sneaking shortens the range you are spotted at", SneakingShortensSpotting);
            Test("Throwing lands the object away from you and makes noise", ThrowingMakesNoiseElsewhere);
            Test("Suspicion changes are reported to the player", SuspicionIsReported);
            Test("An NPC can path around the wall to the other room", PortalRoutingWorks);
            Test("An NPC that cannot reach its target gives up", StuckNpcGivesUp);
            Test("Anger at the ceiling reports nothing, not a phantom gain", ClampedAngerReportsNothing);

            Test("Wrong clothes in a staff area draw suspicion", TrespassingIsSuspicious);
            Test("The right uniform makes you belong", DisguiseRemovesSuspicion);
            Test("Killing the lights shortens how far people see", DarknessShortensSight);
            Test("The fire alarm empties the building", AlarmEvacuates);

            Test("Misfortune makes an NPC wary, then certain", WarinessBuilds);
            Test("People enjoy the misfortune of someone they dislike", Schadenfreude);
            Test("Public failure is worse than private failure", AudienceMakesItWorse);

            Test("A diligent NPC undoes sabotage he finds", TheFixerFixesThings);
            Test("The fixer notes a problem but finishes his task first", FixerFinishesFirst);
            Test("Subtle tampering survives being tidied up", SubtleTamperSurvivesRepair);
            Test("A clumsy NPC creates anomalies but no anger", ClumsinessIsHarmless);

            Test("Same seed produces the same run", RunDeterminism);

            Test("End to end: the chef can be driven furious", EndToEndAngerRises);
            Test("End to end: a clean run stays unsuspected", EndToEndStaysClean);

            Console.WriteLine();
            Console.WriteLine("-------------------------");
            Console.WriteLine("passed: " + _passed + "   failed: " + _failed);
            return _failed == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------
        // Primitives
        // ------------------------------------------------------------------

        private static void Vec3FlatDistance()
        {
            Vec3 a = new Vec3(0f, 0f, 0f);
            Vec3 b = new Vec3(3f, 99f, 4f);
            AssertNear(5f, Vec3.FlatDistance(a, b), 0.001f, "flat distance");
        }

        private static void RngDeterminism()
        {
            Rng a = new Rng(1234);
            Rng b = new Rng(1234);
            for (int i = 0; i < 50; i++)
            {
                AssertNear(a.NextFloat(), b.NextFloat(), 0.000001f, "same seed, same stream");
            }

            Rng c = new Rng(9999);
            Rng d = new Rng(1234);
            bool differs = false;
            for (int i = 0; i < 20 && !differs; i++)
            {
                if (Math.Abs(c.NextFloat() - d.NextFloat()) > 0.0001f) differs = true;
            }
            AssertTrue(differs, "different seeds diverge");
        }

        private static void NeedUrgency()
        {
            float nearlyFull = Mathx.Urgency(0.9f);
            float empty = Mathx.Urgency(0.1f);
            AssertTrue(empty > nearlyFull * 5f,
                "an empty need should dominate a nearly full one (got " + empty + " vs " + nearlyFull + ")");
        }

        // ------------------------------------------------------------------
        // Utility AI
        // ------------------------------------------------------------------

        private static void UtilityPicksUrgentNeed()
        {
            Simulation sim = KitchenLevel.Build(1);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);

            // Desperate for the toilet, everything else fine.
            for (int i = 0; i < Needs.Count; i++) terry.Needs.Set((NeedType)i, 0.95f);
            terry.Needs.Set(NeedType.Bladder, 0.02f);

            List<ScoredOption> options = UtilityAi.ScoreAll(terry, sim.World, sim.Time);
            AssertTrue(options.Count > 0, "should find something to do");
            AssertEqual(KitchenLevel.Ids.Toilet, options[0].Object.Id,
                "top option should be the toilet, was " + options[0]);
        }

        private static void UtilityIgnoresSabotage()
        {
            Simulation sim = KitchenLevel.Build(2);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);

            List<ScoredOption> options = UtilityAi.ScoreAll(chef, sim.World, sim.Time);
            for (int i = 0; i < options.Count; i++)
            {
                AssertTrue(!options[i].Affordance.IsSabotage,
                    "NPCs must never consider sabotage verbs: " + options[i]);
            }
        }

        /// <summary>
        /// A blocked toilet looks perfectly normal from across the room. Terry
        /// must still choose it, walk over, and only then find out - otherwise
        /// sabotage silently does nothing.
        /// </summary>
        private static void HiddenFaultsAdvertiseAnyway()
        {
            Simulation sim = KitchenLevel.Build(3);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);
            for (int i = 0; i < Needs.Count; i++) terry.Needs.Set((NeedType)i, 0.95f);
            terry.Needs.Set(NeedType.Bladder, 0.02f);

            SmartObject toilet = sim.World.GetObject(KitchenLevel.Ids.Toilet);
            toilet.SetState(StateKeys.Broken, 1f);

            Affordance use = FindAffordance(toilet, "use_toilet");
            AssertTrue(use.AdvertisedTo(toilet, terry),
                "a blocked toilet should still look usable from a distance");
            AssertTrue(!use.AvailableFor(toilet, terry),
                "but it must fail the check once he is standing at it");

            List<ScoredOption> options = UtilityAi.ScoreAll(terry, sim.World, sim.Time);
            AssertEqual(KitchenLevel.Ids.Toilet, options[0].Object.Id,
                "he should still head for the toilet, was " + options[0]);
        }

        /// <summary>
        /// The opposite case: state anyone can see from anywhere must be
        /// respected when planning, or NPCs generate nonsense failures.
        /// </summary>
        private static void VisibleStateRespectedWhenPlanning()
        {
            Simulation sim = KitchenLevel.Build(3);
            Npc eva = sim.World.GetNpc(KitchenLevel.Ids.Manager);
            SmartObject bin = sim.World.GetObject(KitchenLevel.Ids.Bin);

            Affordance empty = FindAffordance(bin, "empty_bin");
            AssertTrue(empty.AdvertisedTo(bin, eva), "a full bin advertises emptying");

            bin.SetState(StateKeys.Dirty, 0f);
            AssertTrue(!empty.AdvertisedTo(bin, eva),
                "an already-empty bin must not be advertised - that is not sabotage, just state");
        }

        private static void UtilityAvoidsFailedObject()
        {
            Simulation sim = KitchenLevel.Build(4);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);
            for (int i = 0; i < Needs.Count; i++) terry.Needs.Set((NeedType)i, 0.95f);
            terry.Needs.Set(NeedType.Bladder, 0.02f);

            List<ScoredOption> before = UtilityAi.ScoreAll(terry, sim.World, sim.Time);
            float scoreBefore = TopScoreFor(before, KitchenLevel.Ids.Toilet);

            terry.AvoidUntil[KitchenLevel.Ids.Toilet] = sim.Time + 20f;

            List<ScoredOption> after = UtilityAi.ScoreAll(terry, sim.World, sim.Time);
            float scoreAfter = TopScoreFor(after, KitchenLevel.Ids.Toilet);

            AssertTrue(scoreAfter < scoreBefore,
                "recently failed objects should score lower (" + scoreBefore + " -> " + scoreAfter + ")");
        }

        private static float TopScoreFor(List<ScoredOption> options, string objectId)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Object.Id == objectId) return options[i].Score;
            }
            return 0f;
        }

        // ------------------------------------------------------------------
        // Sabotage -> frustration -> anger
        // ------------------------------------------------------------------

        private static void SabotageCausesPlanFailure()
        {
            Simulation sim = KitchenLevel.Build(5);
            SmartObject stove = sim.World.GetObject(KitchenLevel.Ids.Stove);
            SmartObject pan = sim.World.GetObject(KitchenLevel.Ids.Pan);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);

            Affordance cook = FindAffordance(stove, "cook");
            AssertTrue(cook.AvailableFor(stove, chef), "cooking should be possible to start with");

            pan.Concealed = true;
            AssertTrue(!cook.AvailableFor(stove, chef), "hiding the pan should make cooking impossible");
        }

        private static void PlanFailureRaisesAnger()
        {
            Simulation sim = KitchenLevel.Build(6);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);
            SmartObject pan = sim.World.GetObject(KitchenLevel.Ids.Pan);

            chef.Needs.Set(NeedType.Comfort, 0.02f);

            // The pan vanishes before he gets there.
            pan.Concealed = true;
            pan.HeldBy = PlayerAvatar.PlayerId;

            RunFor(sim, 40f);

            // Peak, not current: anger decays, and a single frustration should not
            // still be burning forty seconds later.
            AssertTrue(chef.PeakAnger > 0.02f,
                "chef should have spiked when his plan collapsed (peak " + chef.PeakAnger + ")");
            AssertTrue(HasEvent(sim, EventKind.PlanFailed), "a PlanFailed event should have been published");
        }

        private static void FrustrationEscalates()
        {
            Npc npc = new Npc { Id = "n", Name = "N", Personality = Personality.Chef() };
            npc.Needs.Set(NeedType.Hunger, 0.1f);

            float first = AngerModel.PlanFailure(npc, NeedType.Hunger);
            float second = AngerModel.PlanFailure(npc, NeedType.Hunger);
            float third = AngerModel.PlanFailure(npc, NeedType.Hunger);

            AssertTrue(second > first && third > second,
                "each successive failure should hurt more (" + first + ", " + second + ", " + third + ")");
        }

        // ------------------------------------------------------------------
        // Blame
        // ------------------------------------------------------------------

        private static void BlameUsesOpportunity()
        {
            Simulation sim = KitchenLevel.Build(7);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);
            sim.Time = 100f;

            Vec3 scene = new Vec3(-6f, 0f, 3f);
            chef.Memory.RecordSighting(KitchenLevel.Ids.Waiter, scene, 95f, 0f);

            WorldEvent anomaly = new WorldEvent
            {
                Kind = EventKind.ObjectBroken,
                Position = scene,
                Time = 96f,
                ObjectId = KitchenLevel.Ids.Stove,
                VictimId = chef.Id,
                Severity = 0.8f
            };

            BlameResult blame = BlameResolver.Resolve(chef, anomaly, sim);
            AssertTrue(blame.HasSuspect, "someone was standing right there; he should have a suspect");
            AssertEqual(KitchenLevel.Ids.Waiter, blame.SuspectId, "should blame the person he saw");
        }

        private static void BlameNeedsEvidence()
        {
            Simulation sim = KitchenLevel.Build(8);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);
            terry.Relationships.Clear();
            sim.Time = 100f;

            WorldEvent anomaly = new WorldEvent
            {
                Kind = EventKind.ObjectBroken,
                Position = new Vec3(-6f, 0f, 3f),
                Time = 96f,
                ObjectId = KitchenLevel.Ids.Stove,
                Severity = 0.5f
            };

            BlameResult blame = BlameResolver.Resolve(terry, anomaly, sim);
            AssertTrue(!blame.HasSuspect,
                "a trusting NPC with no sightings should assume an accident, blamed " + blame.SuspectId);
        }

        private static void ParanoiaLowersBar()
        {
            Simulation sim = KitchenLevel.Build(9);
            Npc eva = sim.World.GetNpc(KitchenLevel.Ids.Manager);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);
            sim.Time = 100f;

            Vec3 scene = new Vec3(0f, 0f, 0f);
            WorldEvent anomaly = new WorldEvent
            {
                Kind = EventKind.ObjectBroken,
                Position = scene,
                Time = 96f,
                ObjectId = KitchenLevel.Ids.Stove,
                Severity = 0.5f
            };

            // Both saw the same weak thing: someone was vaguely in the area a while ago.
            eva.Memory.RecordSighting(PlayerAvatar.PlayerId, new Vec3(4f, 0f, 3f), 80f, 0f);
            terry.Memory.RecordSighting(PlayerAvatar.PlayerId, new Vec3(4f, 0f, 3f), 80f, 0f);

            BlameResult evaBlame = BlameResolver.Resolve(eva, anomaly, sim);
            BlameResult terryBlame = BlameResolver.Resolve(terry, anomaly, sim);

            AssertTrue(!terryBlame.HasSuspect || evaBlame.HasSuspect,
                "the paranoid manager should never be harder to convince than the trusting dishwasher");
        }

        private static void MotiveAffectsBlame()
        {
            Simulation sim = KitchenLevel.Build(10);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);
            sim.Time = 100f;

            Vec3 scene = new Vec3(-6f, 0f, 3f);
            chef.Memory.RecordSighting(KitchenLevel.Ids.Waiter, scene, 95f, 0f);
            chef.Memory.RecordSighting(KitchenLevel.Ids.Dishwasher, scene, 95f, 0f);

            WorldEvent anomaly = new WorldEvent
            {
                Kind = EventKind.ObjectBroken,
                Position = scene,
                Time = 96f,
                ObjectId = KitchenLevel.Ids.Stove,
                VictimId = chef.Id,
                Severity = 0.8f
            };

            // Gordon dislikes Terry out of the box, so with equal opportunity
            // Terry should carry more of the blame more often.
            int terryBlamed = 0;
            for (int i = 0; i < 40; i++)
            {
                BlameResult blame = BlameResolver.Resolve(chef, anomaly, sim);
                if (blame.SuspectId == KitchenLevel.Ids.Dishwasher) terryBlamed++;
            }

            AssertTrue(terryBlamed > 20,
                "the disliked dishwasher should usually take the blame (" + terryBlamed + "/40)");
        }

        // ------------------------------------------------------------------
        // Social
        // ------------------------------------------------------------------

        private static void GossipSpreadsSuspicion()
        {
            Simulation sim = KitchenLevel.Build(11);
            Npc marie = sim.World.GetNpc(KitchenLevel.Ids.Waiter);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);

            marie.AddSuspicion(PlayerAvatar.PlayerId, 0.8f);
            float before = terry.SuspicionOf(PlayerAvatar.PlayerId);

            Gossip.Exchange(marie, terry, sim);

            AssertTrue(terry.SuspicionOf(PlayerAvatar.PlayerId) > before,
                "Terry should pick up Marie's suspicion (" + terry.SuspicionOf(PlayerAvatar.PlayerId) + ")");
        }

        private static void MemoryDecays()
        {
            NpcMemory memory = new NpcMemory();
            memory.Remember(new MemoryEntry
            {
                Kind = EventKind.ObjectBroken,
                ObjectId = "stove",
                When = 0f,
                Confidence = 1f
            });

            for (int i = 0; i < 400; i++) memory.Tick(0.5f, 0.0f, i * 0.5f);

            AssertTrue(memory.Entries.Count == 0, "a low-grudge NPC should eventually forget");
        }

        // ------------------------------------------------------------------
        // Player
        // ------------------------------------------------------------------

        private static void DelayedEffectsFire()
        {
            Simulation sim = KitchenLevel.Build(12);
            bool fired = false;
            sim.Schedule(5f, "test", delegate(Simulation s) { fired = true; });

            RunFor(sim, 4f);
            AssertTrue(!fired, "should not fire early");

            RunFor(sim, 2f);
            AssertTrue(fired, "should fire after the delay");
        }

        private static void SeenSabotageRaisesSuspicion()
        {
            Simulation sim = KitchenLevel.Build(13);
            Npc eva = sim.World.GetNpc(KitchenLevel.Ids.Manager);
            SmartObject salt = sim.World.GetObject(KitchenLevel.Ids.Salt);

            // Put the manager right next to the player, staring at them.
            sim.Player.Position = salt.Position;
            eva.Position = new Vec3(salt.Position.X + 1.5f, 0f, salt.Position.Z);
            eva.Facing = (sim.Player.Position - eva.Position).Normalized;

            AssertTrue(sim.PlayerInteract(KitchenLevel.Ids.Salt, "swap_salt"), "swap should succeed");
            AssertTrue(eva.SuspicionOf(PlayerAvatar.PlayerId) > 0.1f,
                "doing it in full view should be noticed (" + eva.SuspicionOf(PlayerAvatar.PlayerId) + ")");
        }

        private static void UnseenSabotageIsClean()
        {
            Simulation sim = KitchenLevel.Build(14);
            SmartObject salt = sim.World.GetObject(KitchenLevel.Ids.Salt);

            // Move everyone far away and face them at a wall.
            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                sim.World.Npcs[i].Position = new Vec3(8f, 0f, 8f);
                sim.World.Npcs[i].Facing = new Vec3(1f, 0f, 0f);
            }

            sim.Player.Position = salt.Position;
            AssertTrue(sim.PlayerInteract(KitchenLevel.Ids.Salt, "swap_salt"), "swap should succeed");

            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                AssertNear(0f, sim.World.Npcs[i].SuspicionOf(PlayerAvatar.PlayerId), 0.001f,
                    sim.World.Npcs[i].Name + " saw nothing and should suspect nothing");
            }
        }

        private static void WallsBlockSight()
        {
            Simulation sim = KitchenLevel.Build(15);

            // Straight across the dividing wall, away from the doorway.
            Vec3 kitchenSide = new Vec3(-3f, 0f, 6f);
            Vec3 diningSide = new Vec3(3f, 0f, 6f);
            AssertTrue(!sim.World.HasLineOfSight(kitchenSide, diningSide), "the wall should block sight");

            // Through the doorway gap.
            AssertTrue(sim.World.HasLineOfSight(new Vec3(-3f, 0f, 0f), new Vec3(3f, 0f, 0f)),
                "the doorway should be see-through");
        }

        private static void ShutDoorBlocksSight()
        {
            Simulation sim = KitchenLevel.Build(200);
            SmartObject door = sim.World.GetObject(KitchenLevel.Ids.Door);

            Vec3 kitchenSide = new Vec3(-3f, 0f, 0f);
            Vec3 diningSide = new Vec3(3f, 0f, 0f);

            AssertTrue(sim.World.HasLineOfSight(kitchenSide, diningSide),
                "the doorway is open to begin with");

            door.SetState("open", 0f);
            AssertTrue(!sim.World.HasLineOfSight(kitchenSide, diningSide),
                "shutting the door should cut the sight line");
        }

        private static void HidingWorks()
        {
            Simulation sim = KitchenLevel.Build(201);
            Npc eva = sim.World.GetNpc(KitchenLevel.Ids.Manager);
            SmartObject pantry = sim.World.GetObject(KitchenLevel.Ids.Pantry);

            sim.Player.Position = pantry.Position;
            eva.Position = new Vec3(pantry.Position.X + 2f, 0f, pantry.Position.Z);
            eva.Facing = (sim.Player.Position - eva.Position).Normalized;

            AssertTrue(sim.CanSeePlayer(eva), "standing in the open, she should see you");

            AssertTrue(sim.PlayerToggleHide(pantry), "hiding should succeed next to the pantry");
            AssertTrue(!sim.CanSeePlayer(eva), "hidden, she should not see you at all");

            AssertTrue(sim.PlayerToggleHide(null), "toggling again should come back out");
            AssertTrue(sim.CanSeePlayer(eva), "out in the open again, she sees you");
        }

        private static void SneakingShortensSpotting()
        {
            Simulation sim = KitchenLevel.Build(202);
            Npc eva = sim.World.GetNpc(KitchenLevel.Ids.Manager);

            eva.Position = new Vec3(0f, 0f, -5f);
            eva.Facing = new Vec3(1f, 0f, 0f);

            // Well inside her sight range, but beyond the sneaking cut-off.
            sim.Player.Position = new Vec3(eva.Position.X + eva.Perception.SightRange * 0.8f, 0f, eva.Position.Z);

            sim.Player.Sneaking = false;
            AssertTrue(sim.CanSeePlayer(eva), "walking upright at that range, she sees you");

            sim.Player.Sneaking = true;
            AssertTrue(!sim.CanSeePlayer(eva), "sneaking at that range, she should not");

            // But sneaking is no help at all up close.
            sim.Player.Position = new Vec3(eva.Position.X + 1.5f, 0f, eva.Position.Z);
            AssertTrue(sim.CanSeePlayer(eva), "sneaking right in front of her is still visible");
        }

        private static void ThrowingMakesNoiseElsewhere()
        {
            Simulation sim = KitchenLevel.Build(203);
            SmartObject salt = sim.World.GetObject(KitchenLevel.Ids.Salt);

            ParkEveryoneInDining(sim);
            sim.Player.Position = new Vec3(-5f, 0f, 0f);
            sim.Player.Facing = new Vec3(0f, 0f, -1f);

            salt.HeldBy = PlayerAvatar.PlayerId;
            sim.Player.CarryingObjectId = salt.Id;

            AssertTrue(sim.PlayerThrow(), "throwing should succeed while carrying");
            AssertTrue(!sim.Player.IsCarrying, "hands should be empty afterwards");

            float distance = Vec3.FlatDistance(salt.Position, sim.Player.Position);
            AssertTrue(distance > 2f, "it should land away from you, landed " + distance + "m away");

            bool noiseAtLanding = false;
            IReadOnlyList<WorldEvent> log = sim.Events.Log;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].Kind != EventKind.Noise) continue;
                if (Vec3.FlatDistance(log[i].Position, salt.Position) < 0.5f) noiseAtLanding = true;
            }
            AssertTrue(noiseAtLanding, "the noise must happen where it lands, not where you are");
        }

        private static void SuspicionIsReported()
        {
            Simulation sim = KitchenLevel.Build(204);
            Npc eva = sim.World.GetNpc(KitchenLevel.Ids.Manager);

            sim.Feedback.Drain();
            sim.RaiseSuspicion(eva, PlayerAvatar.PlayerId, 0.3f, "saw something");

            List<FeedbackEvent> events = sim.Feedback.Drain();
            bool reported = false;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == FeedbackKind.Suspicion && events[i].Amount > 0) reported = true;
            }

            AssertTrue(reported,
                "a suspicion increase the player caused must surface as feedback with a number");
        }

        private static void PortalRoutingWorks()
        {
            Simulation sim = KitchenLevel.Build(300);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);

            // Far corner of the dining room, target deep in the kitchen: the
            // straight line is blocked and sliding alone dead-ends in a corner.
            terry.Position = new Vec3(7f, 0f, 7f);
            terry.MoveTarget = new Vec3(-7f, 0f, 6.4f);

            for (int i = 0; i < Needs.Count; i++) terry.Needs.Set((NeedType)i, 0.95f);
            terry.Needs.Set(NeedType.Order, 0.02f);

            RunFor(sim, 40f);

            float distance = Vec3.FlatDistance(terry.Position, new Vec3(-7f, 0f, 6.4f));
            AssertTrue(terry.Position.X < 0f,
                "he should have got through the doorway into the kitchen, ended at " + terry.Position);
        }

        private static void StuckNpcGivesUp()
        {
            Simulation sim = KitchenLevel.Build(301);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);

            // A target outside the world entirely: unreachable by any route.
            terry.Activity = NpcActivity.Walking;
            terry.CurrentPlan = null;
            terry.MoveTarget = new Vec3(-9.9f, 0f, -9.9f);
            terry.ClosestApproach = 0f;

            RunFor(sim, 10f);

            AssertTrue(terry.Activity != NpcActivity.Walking || terry.StuckTimer < 6f,
                "an NPC that cannot make progress must give up rather than freeze");
        }

        private static void ClampedAngerReportsNothing()
        {
            Simulation sim = KitchenLevel.Build(302);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);

            chef.Anger = 1f;
            sim.Feedback.Drain();

            AngerModel.Add(chef, 0.5f, sim, "test");

            List<FeedbackEvent> events = sim.Feedback.Drain();
            for (int i = 0; i < events.Count; i++)
            {
                AssertTrue(events[i].Kind != FeedbackKind.Anger,
                    "anger already at the ceiling must not report a gain that did not happen");
            }
        }

        // ------------------------------------------------------------------
        // Disguises, light, alarm
        // ------------------------------------------------------------------

        private static void TrespassingIsSuspicious()
        {
            Simulation sim = KitchenLevel.Build(310);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);

            // Standing in the middle of the kitchen in your own clothes, watched.
            sim.Player.Position = new Vec3(-5f, 0f, 1f);
            sim.Player.Outfit = Outfit.Civilian();
            chef.Position = new Vec3(-3.5f, 0f, 1f);
            chef.Facing = (sim.Player.Position - chef.Position).Normalized;

            float before = chef.SuspicionOf(PlayerAvatar.PlayerId);
            for (int i = 0; i < 100; i++)
            {
                chef.Facing = (sim.Player.Position - chef.Position).Normalized;
                sim.Tick(0.1f);
            }

            AssertTrue(chef.SuspicionOf(PlayerAvatar.PlayerId) > before + 0.05f,
                "loitering in the kitchen in civvies should be noticed (" +
                chef.SuspicionOf(PlayerAvatar.PlayerId) + ")");
        }

        private static void DisguiseRemovesSuspicion()
        {
            Simulation sim = KitchenLevel.Build(310);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);

            sim.Player.Position = new Vec3(-5f, 0f, 1f);
            sim.Player.Outfit = Outfit.ChefWhites();
            chef.Position = new Vec3(-3.5f, 0f, 1f);

            for (int i = 0; i < 100; i++)
            {
                chef.Facing = (sim.Player.Position - chef.Position).Normalized;
                sim.Tick(0.1f);
            }

            // Gordon is not especially observant, so the whites hold up for him.
            AssertTrue(chef.SuspicionOf(PlayerAvatar.PlayerId) < 0.05f,
                "in chef's whites the kitchen should be safe ground (" +
                chef.SuspicionOf(PlayerAvatar.PlayerId) + ")");
        }

        private static void DarknessShortensSight()
        {
            Simulation sim = KitchenLevel.Build(311);
            Npc eva = sim.World.GetNpc(KitchenLevel.Ids.Manager);

            eva.Position = new Vec3(0f, 0f, -5f);
            eva.Facing = new Vec3(1f, 0f, 0f);
            sim.Player.Position = new Vec3(eva.Position.X + eva.Perception.SightRange * 0.8f, 0f, eva.Position.Z);

            AssertTrue(sim.CanSeePlayer(eva), "with the lights on she can see that far");

            sim.World.LightLevel = 0.35f;
            AssertTrue(!sim.CanSeePlayer(eva), "in the dark that range should be well out of reach");
        }

        private static void AlarmEvacuates()
        {
            Simulation sim = KitchenLevel.Build(312);
            Zone assembly = sim.World.GetZone("assembly");
            AssertTrue(assembly != null, "the level needs an assembly point");

            sim.TriggerAlarm(30f);
            RunFor(sim, 25f);

            int outside = 0;
            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                if (Vec3.FlatDistance(sim.World.Npcs[i].Position, assembly.Center) < 5f) outside++;
            }

            AssertTrue(outside >= 3,
                "most of the staff should have headed for the assembly point (" + outside + "/4)");
        }

        // ------------------------------------------------------------------
        // The mind
        // ------------------------------------------------------------------

        private static void WarinessBuilds()
        {
            Npc npc = new Npc { Id = "n", Name = "N", Personality = Personality.Manager() };

            AssertTrue(!npc.Mind.SuspectsSabotage, "one quiet shift, nothing to suspect");

            for (int i = 0; i < 6; i++) npc.Mind.Misfortune(0.6f, npc.Personality);

            AssertTrue(npc.Mind.SuspectsSabotage,
                "after six things go wrong a paranoid manager should stop believing in luck (" +
                npc.Mind.Wariness + ")");
            AssertTrue(npc.EffectiveParanoia > npc.Personality.Paranoia - 0.001f,
                "wariness should make them harder to fool");
        }

        // ------------------------------------------------------------------
        // The fixer and the wildcard
        // ------------------------------------------------------------------

        private static void TheFixerFixesThings()
        {
            Simulation sim = KitchenLevel.Build(404);
            Npc bruno = sim.World.GetNpc(KitchenLevel.Ids.SousChef);
            SmartObject stove = sim.World.GetObject(KitchenLevel.Ids.Stove);

            // Get everyone else out of the way so this is unambiguously Bruno.
            ParkEveryoneExcept(sim, bruno);

            stove.SetState(StateKeys.Broken, 1f);
            bruno.Position = new Vec3(stove.Position.X + 1.5f, 0f, stove.Position.Z);

            AssertTrue(sim.TryStartRepair(bruno, stove), "an idle fixer should take the job");

            RunFor(sim, 25f);

            AssertTrue(stove.GetState(StateKeys.Broken) <= 0f,
                "Bruno should have the stove working again");
        }

        private static void FixerFinishesFirst()
        {
            Simulation sim = KitchenLevel.Build(405);
            Npc bruno = sim.World.GetNpc(KitchenLevel.Ids.SousChef);
            SmartObject stove = sim.World.GetObject(KitchenLevel.Ids.Stove);

            stove.SetState(StateKeys.Broken, 1f);
            bruno.Activity = NpcActivity.Using;
            bruno.ActivityTimer = 20f;

            AssertTrue(!sim.TryStartRepair(bruno, stove),
                "he should not down tools the instant he sees a problem");
            AssertTrue(bruno.PendingRepairId == stove.Id,
                "but he should remember it for when he is free");
        }

        private static void SubtleTamperSurvivesRepair()
        {
            Simulation sim = KitchenLevel.Build(406);
            Npc bruno = sim.World.GetNpc(KitchenLevel.Ids.SousChef);
            SmartObject salt = sim.World.GetObject(KitchenLevel.Ids.Salt);

            ParkEveryoneExcept(sim, bruno);

            // Swapped contents, and knocked off its shelf so he has a reason to
            // come over. Tidying it away must not undo the swap: the whole point
            // of subtle sabotage is that looking at it tells you nothing.
            salt.SetState(StateKeys.Tampered, 1f);
            salt.SetState(StateKeys.Subtle, 1f);
            salt.Position = new Vec3(salt.HomePosition.X + 3f, 0f, salt.HomePosition.Z + 1f);
            bruno.Position = new Vec3(salt.Position.X + 1f, 0f, salt.Position.Z);

            AssertTrue(sim.TryStartRepair(bruno, salt), "an out-of-place shaker is a job");

            RunFor(sim, 25f);

            AssertTrue(salt.GetState(StateKeys.Subtle) > 0f && salt.GetState(StateKeys.Tampered) > 0f,
                "the salt is still sugar however neatly it is shelved");
        }

        private static void ClumsinessIsHarmless()
        {
            Simulation sim = KitchenLevel.Build(407);
            Npc pip = sim.World.GetNpc(KitchenLevel.Ids.Porter);

            AssertTrue(pip.Personality.Clumsiness > 0.5f, "Pip is the clumsy one");

            RunFor(sim, 300f);

            // The point of Pip is deniability, and deniability is worthless if he
            // also drives the target up the wall on his own. Every scrap of anger
            // in this game has to be traceable to the player.
            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];
                AssertTrue(npc.Anger < 0.05f,
                    npc.Name + " should be calm in an empty kitchen (" + npc.Anger + ")");
            }
        }

        /// <summary>Move everyone but one NPC far away, so a test observes one actor.</summary>
        private static void ParkEveryoneExcept(Simulation sim, Npc keep)
        {
            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];
                if (npc == keep) continue;
                npc.Position = new Vec3(60f + i * 3f, 0f, 60f);
            }
        }

        private static void Schadenfreude()
        {
            Simulation sim = KitchenLevel.Build(320);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);

            // Gordon dislikes Terry out of the box and is standing right there.
            terry.Position = new Vec3(-5f, 0f, 3f);
            chef.Position = new Vec3(-3.5f, 0f, 3f);
            chef.Facing = (terry.Position - chef.Position).Normalized;

            // Spill directly at Terry's feet: this test is about the reaction,
            // not about whether the player could reach the bottle.
            ItemCatalogue.Spill(sim, terry.Position, PlayerAvatar.PlayerId, "oil");

            RunFor(sim, 6f);

            AssertTrue(chef.Mind.Amusement > 0.1f || terry.Mind.Embarrassment > 0.1f,
                "someone going over in front of a colleague should land emotionally " +
                "(amusement " + chef.Mind.Amusement + ", embarrassment " + terry.Mind.Embarrassment + ")");
        }

        private static void AudienceMakesItWorse()
        {
            float alone = RunSlipTest(false);
            float watched = RunSlipTest(true);

            AssertTrue(watched > alone,
                "slipping over in front of people should hurt more than slipping alone (" +
                alone + " vs " + watched + ")");
        }

        private static float RunSlipTest(bool withAudience)
        {
            Simulation sim = KitchenLevel.Build(321);
            Npc terry = sim.World.GetNpc(KitchenLevel.Ids.Dishwasher);

            terry.Position = new Vec3(-5f, 0f, 3f);

            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc other = sim.World.Npcs[i];
                if (other == terry) continue;

                if (withAudience)
                {
                    other.Position = new Vec3(-3.5f, 0f, 3f);
                    other.Facing = (terry.Position - other.Position).Normalized;
                }
                else
                {
                    other.Position = new Vec3(8f, 0f, 8f);
                    other.Facing = new Vec3(1f, 0f, 0f);
                }
            }

            sim.Player.Position = new Vec3(9f, 0f, -9f);
            ItemCatalogue.Spill(sim, terry.Position, PlayerAvatar.PlayerId, "oil");
            RunFor(sim, 5f);

            return terry.PeakAnger;
        }

        private static void RunDeterminism()
        {
            Simulation a = KitchenLevel.Build(4242);
            Simulation b = KitchenLevel.Build(4242);

            RunFor(a, 90f);
            RunFor(b, 90f);

            AssertEqual(a.Events.Log.Count.ToString(), b.Events.Log.Count.ToString(),
                "same seed should produce the same number of events");

            Npc chefA = a.World.GetNpc(KitchenLevel.Ids.Chef);
            Npc chefB = b.World.GetNpc(KitchenLevel.Ids.Chef);
            AssertNear(chefA.Anger, chefB.Anger, 0.0001f, "same seed should produce the same anger");
        }

        // ------------------------------------------------------------------
        // End to end
        // ------------------------------------------------------------------

        private static void EndToEndAngerRises()
        {
            Simulation sim = KitchenLevel.Build(77);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);

            PerfectCrime(sim);
            RunFor(sim, 240f);

            AssertTrue(chef.PeakAnger > 0.85f,
                "a full sabotage run should drive the chef to fury (peak " + chef.PeakAnger + ")");
        }

        private static void EndToEndStaysClean()
        {
            Simulation sim = KitchenLevel.Build(77);
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);

            PerfectCrime(sim);
            RunFor(sim, 240f);

            AssertTrue(sim.Outcome != GameOutcome.Caught,
                "sabotage done out of sight should not get the player caught");
            AssertTrue(chef.SuspicionOf(PlayerAvatar.PlayerId) < Simulation.BlownCoverThreshold,
                "the chef should not be sure it was the player (" +
                chef.SuspicionOf(PlayerAvatar.PlayerId) + ")");
        }

        /// <summary>
        /// A scripted "everyone is looking the other way" run. Positions NPCs
        /// away from each sabotage so the test exercises the consequence chain
        /// rather than the player's stealth routing.
        /// </summary>
        private static void PerfectCrime(Simulation sim)
        {
            Npc chef = sim.World.GetNpc(KitchenLevel.Ids.Chef);
            chef.Needs.Set(NeedType.Comfort, 0.05f);

            ParkEveryoneInDining(sim);

            // Everything here is aimed squarely at Gordon. Spraying sabotage
            // around the level annoys four people slightly; the objective needs
            // one person pushed over the edge.
            Teleport(sim, sim.World.GetObject(KitchenLevel.Ids.Salt).Position);
            sim.PlayerInteract(KitchenLevel.Ids.Salt, "swap_salt");

            Teleport(sim, sim.World.GetObject(KitchenLevel.Ids.Stove).Position);
            sim.PlayerInteract(KitchenLevel.Ids.Stove, "crank_heat");

            // Take his pan and bury it in the bin: every attempt to cook now fails.
            Teleport(sim, sim.World.GetObject(KitchenLevel.Ids.Pan).Position);
            sim.PlayerInteract(KitchenLevel.Ids.Pan, "take");
            Teleport(sim, sim.World.GetObject(KitchenLevel.Ids.Bin).Position);
            sim.PlayerInteract(KitchenLevel.Ids.Bin, "stash");

            Teleport(sim, sim.World.GetObject(KitchenLevel.Ids.Oil).Position);
            sim.PlayerInteract(KitchenLevel.Ids.Oil, "pour_oil");

            Teleport(sim, sim.World.GetObject(KitchenLevel.Ids.Fridge).Position);
            sim.PlayerInteract(KitchenLevel.Ids.Fridge, "empty_fridge");

            // Retreat to the dining room and behave like a normal customer.
            Teleport(sim, new Vec3(6f, 0f, -6f));
        }

        private static void ParkEveryoneInDining(Simulation sim)
        {
            for (int i = 0; i < sim.World.Npcs.Count; i++)
            {
                Npc npc = sim.World.Npcs[i];
                npc.Position = new Vec3(6f + i * 0.4f, 0f, 6f);
                npc.Facing = new Vec3(1f, 0f, 0f);
            }
        }

        private static void Teleport(Simulation sim, Vec3 position)
        {
            sim.Player.Position = position;
            sim.Player.MovementNoise = 0f;
        }

        // ------------------------------------------------------------------
        // Harness
        // ------------------------------------------------------------------

        private static void RunFor(Simulation sim, float seconds, float dt = 0.1f)
        {
            int steps = (int)(seconds / dt);
            for (int i = 0; i < steps; i++) sim.Tick(dt);
        }

        private static bool HasEvent(Simulation sim, EventKind kind)
        {
            IReadOnlyList<WorldEvent> log = sim.Events.Log;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].Kind == kind) return true;
            }
            return false;
        }

        private static Affordance FindAffordance(SmartObject obj, string id)
        {
            for (int i = 0; i < obj.Affordances.Count; i++)
            {
                if (obj.Affordances[i].Id == id) return obj.Affordances[i];
            }
            throw new Exception("no affordance '" + id + "' on " + obj.Name);
        }

        private static void Test(string name, Action body)
        {
            try
            {
                body();
                _passed++;
                Console.WriteLine("  PASS  " + name);
            }
            catch (Exception e)
            {
                _failed++;
                Console.WriteLine("  FAIL  " + name);
                Console.WriteLine("        " + e.Message);
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void AssertEqual(string expected, string actual, string message)
        {
            if (expected != actual)
            {
                throw new Exception(message + " (expected '" + expected + "', got '" + actual + "')");
            }
        }

        private static void AssertNear(float expected, float actual, float tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                throw new Exception(message + " (expected " + expected + ", got " + actual + ")");
            }
        }
    }
}
