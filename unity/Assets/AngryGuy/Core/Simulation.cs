using System;
using System.Collections.Generic;

namespace AngryGuy.Core
{
    public enum GameOutcome
    {
        InProgress,
        Won,
        Caught,
        TimeUp
    }

    /// <summary>
    /// The whole game, minus rendering and input.
    ///
    /// Everything here is deterministic given a seed, has no Unity dependency,
    /// and can be stepped at any rate. That is what makes the AI testable in
    /// plain C# and what keeps the Unity layer a thin view on top.
    /// </summary>
    public sealed class Simulation
    {
        public readonly World World = new World();
        public readonly EventBus Events = new EventBus();
        public readonly PlayerAvatar Player = new PlayerAvatar();
        public readonly Rng Rng;

        public float Time;
        public float TimeLimit = 420f;

        public string TargetNpcId = "";
        public string ExitZoneId = "exit";

        public GameOutcome Outcome = GameOutcome.InProgress;

        /// <summary>Target reached furious while the player stayed unsuspected.</summary>
        public bool ObjectiveMet;

        /// <summary>Suspicion at which an NPC is certain the player did it.</summary>
        public const float CaughtThreshold = 1.0f;

        /// <summary>Above this the target "knows it was you", which spoils the win.</summary>
        public const float BlownCoverThreshold = 0.5f;

        public const float AngerWinThreshold = 0.9f;

        private readonly List<PendingEffect> _pending = new List<PendingEffect>();
        private readonly List<string> _feed = new List<string>();
        private const int FeedCapacity = 64;

        public IReadOnlyList<string> Feed
        {
            get { return _feed; }
        }

        public IReadOnlyList<PendingEffect> Pending
        {
            get { return _pending; }
        }

        public Simulation(int seed)
        {
            Rng = new Rng(seed);
        }

        public Npc Target
        {
            get { return World.GetNpc(TargetNpcId); }
        }

        // ------------------------------------------------------------------
        // Main loop
        // ------------------------------------------------------------------

        public void Tick(float dt)
        {
            if (Outcome != GameOutcome.InProgress) return;

            Time += dt;

            TickPending();

            for (int i = 0; i < World.Npcs.Count; i++)
            {
                TickNpc(World.Npcs[i], dt);
            }

            TickSocialProximity(dt);
            CheckObjective();

            if (Time >= TimeLimit && Outcome == GameOutcome.InProgress)
            {
                Outcome = GameOutcome.TimeUp;
                Log("Time is up.");
            }
        }

        private void TickPending()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingEffect p = _pending[i];
                if (p.Cancelled)
                {
                    _pending.RemoveAt(i);
                    continue;
                }
                if (Time >= p.FireAtTime)
                {
                    _pending.RemoveAt(i);
                    if (p.Apply != null) p.Apply(this);
                }
            }
        }

        /// <summary>
        /// Schedule a consequence for later. This is the mechanism that lets the
        /// player be demonstrably elsewhere when their sabotage lands.
        /// </summary>
        public void Schedule(float delaySeconds, string label, Action<Simulation> apply)
        {
            _pending.Add(new PendingEffect
            {
                FireAtTime = Time + delaySeconds,
                Label = label,
                Apply = apply
            });
        }

        // ------------------------------------------------------------------
        // NPC update
        // ------------------------------------------------------------------

        private void TickNpc(Npc npc, float dt)
        {
            npc.Needs.Tick(dt);
            npc.Memory.Tick(dt, npc.Personality.Grudge, Time);
            AngerModel.Decay(npc, dt);

            float starvation = AngerModel.Starvation(npc, dt);
            if (starvation > 0f) AngerModel.Add(npc, starvation, this, "unmet needs");

            npc.GossipCooldown -= dt;
            npc.ConfrontCooldown -= dt;
            npc.OutburstCooldown -= dt;
            npc.DecisionCooldown -= dt;
            npc.SpeechTimer -= dt;
            if (npc.SpeechTimer <= 0f) npc.Speech = "";

            PerceptionSample(npc, dt);
            CheckHazards(npc);

            if (npc.Anger >= AngerModel.BoilingPoint && npc.OutburstCooldown <= 0f)
            {
                Outburst(npc);
            }

            switch (npc.Activity)
            {
                case NpcActivity.Idle:
                    Decide(npc);
                    break;

                case NpcActivity.Walking:
                    TickWalking(npc, dt);
                    break;

                case NpcActivity.Using:
                    TickUsing(npc, dt);
                    break;

                case NpcActivity.Investigating:
                    TickInvestigating(npc, dt);
                    break;

                case NpcActivity.Confronting:
                    TickConfronting(npc, dt);
                    break;

                case NpcActivity.Chatting:
                case NpcActivity.Watching:
                    npc.ActivityTimer -= dt;
                    if (npc.ActivityTimer <= 0f) npc.Activity = NpcActivity.Idle;
                    break;
            }
        }

        private void Decide(Npc npc)
        {
            if (npc.DecisionCooldown > 0f) return;
            npc.DecisionCooldown = 0.4f;

            ScoredOption option = UtilityAi.Choose(npc, World, Rng, Time);
            if (option == null)
            {
                Wander(npc);
                return;
            }

            npc.CurrentPlan = new Plan
            {
                Target = option.Object,
                Affordance = option.Affordance,
                Motive = option.Motive
            };
            npc.MoveTarget = option.Object.Position;
            npc.Activity = NpcActivity.Walking;
        }

        /// <summary>
        /// What an NPC does when nothing is worth doing. Getting this wrong is
        /// very visible: NPCs that wander every time they have a spare moment
        /// spend the whole level in transit, never settle into a routine, and
        /// give the player nothing to read or exploit.
        /// </summary>
        private void Wander(Npc npc)
        {
            npc.CurrentPlan = null;

            Zone home = World.GetZone(npc.HomeZoneId);

            // Drifted out of their patch? Head back. This is what keeps the chef
            // in the kitchen and the waiter out front without scripting routines.
            if (home != null && Vec3.FlatDistance(npc.Position, home.Center) > home.Radius)
            {
                npc.MoveTarget = home.Center;
                npc.Activity = NpcActivity.Walking;
                npc.DecisionCooldown = 2f;
                return;
            }

            // Mostly just stand about and look around.
            if (Rng.Chance(0.7f))
            {
                npc.Activity = NpcActivity.Watching;
                npc.ActivityTimer = Rng.Range(4f, 9f);
                npc.DecisionCooldown = 1f;

                Vec3 glance = new Vec3(Rng.Range(-1f, 1f), 0f, Rng.Range(-1f, 1f));
                if (glance.SqrMagnitude > 0.01f) npc.Facing = glance.Normalized;
                return;
            }

            Vec3 center = home != null ? home.Center : npc.Position;
            float radius = home != null ? home.Radius : 3f;

            npc.MoveTarget = World.Clamp(new Vec3(
                center.X + Rng.Range(-radius, radius),
                0f,
                center.Z + Rng.Range(-radius, radius)));

            npc.Activity = NpcActivity.Walking;
            npc.DecisionCooldown = 2.5f;
        }

        private void TickWalking(Npc npc, float dt)
        {
            MoveActor(ref npc.Position, ref npc.Facing, npc.MoveTarget, npc.MoveSpeed * dt);

            if (Vec3.FlatDistance(npc.Position, npc.MoveTarget) > 1.1f) return;

            if (npc.CurrentPlan == null)
            {
                npc.Activity = NpcActivity.Idle;
                return;
            }

            Plan plan = npc.CurrentPlan;

            // Re-validate on arrival. Everything the player does to sabotage a
            // level ultimately shows up as this check failing.
            if (!plan.Affordance.AvailableFor(plan.Target, npc))
            {
                FailPlan(npc, plan);
                return;
            }

            plan.InProgress = true;
            plan.Elapsed = 0f;
            npc.Activity = NpcActivity.Using;
        }

        private void TickUsing(Npc npc, float dt)
        {
            Plan plan = npc.CurrentPlan;
            if (plan == null)
            {
                npc.Activity = NpcActivity.Idle;
                return;
            }

            plan.Elapsed += dt;

            if (plan.Affordance.Noise > 0f && Rng.Chance(dt * 0.5f))
            {
                Publish(new WorldEvent
                {
                    Kind = EventKind.Noise,
                    Position = plan.Target.Position,
                    TrueActorId = npc.Id,
                    ObjectId = plan.Target.Id,
                    Loudness = plan.Affordance.Noise,
                    Severity = 0.05f,
                    Description = npc.Name + " is busy with " + plan.Target.Name
                });
            }

            if (plan.Elapsed < plan.Affordance.Duration) return;

            for (int i = 0; i < plan.Affordance.Satisfies.Count; i++)
            {
                NeedDelta d = plan.Affordance.Satisfies[i];
                npc.Needs.Add(d.Need, d.Amount);
            }

            if (plan.Affordance.Effect != null)
            {
                plan.Affordance.Effect(new AffordanceContext
                {
                    Sim = this,
                    Object = plan.Target,
                    Npc = npc,
                    ActorId = npc.Id
                });
            }

            // A satisfied NPC calms down slightly. Success is the counterweight
            // the player is working against.
            npc.Anger = Mathx.Clamp01(npc.Anger - 0.012f);

            npc.CurrentPlan = null;
            npc.Activity = NpcActivity.Idle;
            npc.DecisionCooldown = 0.7f;
        }

        private void FailPlan(Npc npc, Plan plan)
        {
            float amount = AngerModel.PlanFailure(npc, plan.Motive);
            AngerModel.Add(npc, amount, this, "couldn't " + plan.Describe());

            npc.AvoidUntil[plan.Target.Id] = Time + 22f;
            npc.Say(FrustrationLine(npc, plan.Target));

            Publish(new WorldEvent
            {
                Kind = EventKind.PlanFailed,
                Position = plan.Target.Position,
                TrueActorId = "",
                ObjectId = plan.Target.Id,
                VictimId = npc.Id,
                Loudness = 0.25f + npc.Personality.Temper * 0.25f,
                Severity = 0.4f,
                LeavesEvidence = false,
                Description = npc.Name + " can't " + plan.Describe()
            });

            // Standing at the scene of the problem is when they work out that
            // something was done to it.
            InvestigateObject(npc, plan.Target);

            npc.AbandonPlan();
            npc.DecisionCooldown = 1.2f;
        }

        private static string FrustrationLine(Npc npc, SmartObject obj)
        {
            if (npc.Anger > 0.7f) return "WHO TOUCHED MY " + obj.Name.ToUpperInvariant() + "?!";
            if (npc.Anger > 0.4f) return "Oh, come ON. The " + obj.Name + " as well?";
            return "Hm? The " + obj.Name + " isn't right...";
        }

        private void TickInvestigating(Npc npc, float dt)
        {
            MoveActor(ref npc.Position, ref npc.Facing, npc.InvestigationPoint, npc.MoveSpeed * 1.15f * dt);

            if (Vec3.FlatDistance(npc.Position, npc.InvestigationPoint) > 1.2f) return;

            List<WorldEvent> evidence = Events.EvidenceNear(npc.InvestigationPoint, 3.5f, 90f, Time);
            bool foundSomething = false;

            for (int i = 0; i < evidence.Count; i++)
            {
                WorldEvent e = evidence[i];
                if (npc.Memory.KnowsAbout(e.Kind, e.ObjectId)) continue;
                if (!Rng.Chance(npc.Perception.NoticeChance)) continue;

                RegisterDiscovery(npc, e);
                foundSomething = true;
                break;
            }

            if (!foundSomething) npc.Say("...must have been nothing.", 2f);

            npc.Activity = NpcActivity.Idle;
            npc.DecisionCooldown = 1f;
        }

        private void TickConfronting(Npc npc, float dt)
        {
            Npc other = World.GetNpc(npc.ConfrontTargetId);
            if (other == null)
            {
                npc.Activity = NpcActivity.Idle;
                return;
            }

            MoveActor(ref npc.Position, ref npc.Facing, other.Position, npc.MoveSpeed * 1.2f * dt);

            if (Vec3.FlatDistance(npc.Position, other.Position) > 1.6f) return;

            Accuse(npc, other);
            npc.Activity = NpcActivity.Idle;
            npc.ConfrontCooldown = 30f;
            npc.DecisionCooldown = 1.5f;
        }

        private void Accuse(Npc accuser, Npc accused)
        {
            float confidence = accuser.SuspicionOf(accused.Id);

            accuser.Say("It was you, wasn't it, " + accused.Name + "!");

            Publish(new WorldEvent
            {
                Kind = EventKind.Accusation,
                Position = accused.Position,
                TrueActorId = accuser.Id,
                VictimId = accused.Id,
                Loudness = 0.55f,
                Severity = 0.5f,
                Description = accuser.Name + " accuses " + accused.Name
            });

            AngerModel.Add(accused, AngerModel.Accused(accused, confidence), this,
                "accused by " + accuser.Name);
            AngerModel.Add(accuser, 0.03f, this, "arguing with " + accused.Name);

            accused.AddRelationship(accuser.Id, -0.35f);
            accuser.AddRelationship(accused.Id, -0.25f);
            accused.AddSuspicion(accuser.Id, 0.08f);

            // Deflection: the accused points at whoever they already suspect.
            // This is where blame chains stop being a two-person problem.
            string deflectTo = "";
            float best = 0f;
            foreach (KeyValuePair<string, float> kv in accused.Suspicion)
            {
                if (kv.Key == accuser.Id || kv.Key == accused.Id) continue;
                if (kv.Value > best)
                {
                    best = kv.Value;
                    deflectTo = kv.Key;
                }
            }

            if (deflectTo.Length > 0 && best > 0.2f)
            {
                accused.Say("Me? It was " + DisplayName(deflectTo) + ", everyone knows it!");
                accuser.AddSuspicion(deflectTo, best * 0.35f * accuser.Personality.Gullibility);

                Publish(new WorldEvent
                {
                    Kind = EventKind.Argument,
                    Position = accused.Position,
                    TrueActorId = accused.Id,
                    VictimId = deflectTo,
                    Loudness = 0.6f,
                    Severity = 0.4f,
                    Description = accused.Name + " blames " + DisplayName(deflectTo) + " instead"
                });
            }
            else
            {
                accused.Say("I didn't touch anything!");
            }
        }

        private void Outburst(Npc npc)
        {
            npc.OutburstCooldown = 26f;
            npc.Say("THAT'S IT! I'VE HAD ENOUGH OF THIS PLACE!", 5f);

            Publish(new WorldEvent
            {
                Kind = EventKind.Outburst,
                Position = npc.Position,
                TrueActorId = npc.Id,
                VictimId = npc.Id,
                Loudness = 1f,
                Severity = 0.6f,
                Description = npc.Name + " explodes with rage"
            });
        }

        // ------------------------------------------------------------------
        // Perception
        // ------------------------------------------------------------------

        private void PerceptionSample(Npc npc, float dt)
        {
            // Sight of the player.
            if (npc.Perception.CanSee(npc.Position, npc.Facing, Player.Position, World))
            {
                npc.Memory.RecordSighting(Player.Id, Player.Position, Time, Player.VisibleGuilt(this));
            }

            // Sight of other NPCs.
            for (int i = 0; i < World.Npcs.Count; i++)
            {
                Npc other = World.Npcs[i];
                if (other == npc) continue;
                if (npc.Perception.CanSee(npc.Position, npc.Facing, other.Position, World))
                {
                    npc.Memory.RecordSighting(other.Id, other.Position, Time, 0f);
                }
            }

            // Hearing the player move around out of sight is unsettling but anonymous.
            if (Player.MovementNoise > 0.2f &&
                !npc.Perception.CanSee(npc.Position, npc.Facing, Player.Position, World) &&
                npc.Perception.CanHear(npc.Position, Player.Position, Player.MovementNoise, World) &&
                npc.Activity == NpcActivity.Idle &&
                Rng.Chance(dt * 0.25f * npc.Personality.Observance))
            {
                StartInvestigation(npc, Player.Position, "heard something");
            }

            // Spotting that an object is wrong. Only a chance per sample, so
            // observant NPCs are meaningfully more dangerous.
            if (!Rng.Chance(dt * 0.6f)) return;

            List<SmartObject> near = World.ObjectsNear(npc.Position, npc.Perception.SightRange);
            for (int i = 0; i < near.Count; i++)
            {
                SmartObject obj = near[i];
                if (!npc.Perception.CanSee(npc.Position, npc.Facing, obj.Position, World)) continue;
                if (!LooksWrong(obj)) continue;
                if (!Rng.Chance(npc.Perception.NoticeChance)) continue;

                InvestigateObject(npc, obj);
                break;
            }
        }

        /// <summary>Event kinds that imply somebody did something they shouldn't have.</summary>
        private static bool IsIncriminating(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.ObjectBroken:
                case EventKind.ObjectTaken:
                case EventKind.ObjectTampered:
                case EventKind.ObjectMoved:
                case EventKind.SpillCreated:
                case EventKind.FoodRuined:
                    return true;
                default:
                    return false;
            }
        }

        private static bool LooksWrong(SmartObject obj)
        {
            // "subtle" tampering (sugar in the salt shaker) is invisible until
            // someone actually uses the object. That delay is the player's cover.
            bool obviousTamper = obj.GetState(StateKeys.Tampered) > 0f
                                 && obj.GetState(StateKeys.Subtle) <= 0f;

            return obj.IsBroken
                   || obviousTamper
                   || obj.HasTag(Tags.Mess)
                   || obj.IsAwayFromHome;
        }

        /// <summary>
        /// Environmental hazards the player left lying around. Checked per NPC
        /// per tick so a spill is a real trap rather than a decoration.
        /// </summary>
        private void CheckHazards(Npc npc)
        {
            for (int i = 0; i < World.Objects.Count; i++)
            {
                SmartObject obj = World.Objects[i];
                if (!obj.HasTag(Tags.Hazard)) continue;
                if (obj.GetState("spent") > 0f) continue;
                if (Vec3.FlatDistance(obj.Position, npc.Position) > 0.75f) continue;

                obj.SetState("spent", 1f);
                obj.Tags.Remove(Tags.Hazard);

                AngerModel.Add(npc, AngerModel.PropertyViolation(npc, 0.7f), this,
                    "went flying over " + obj.Name);
                npc.Say("WHOA— who left that there?!");
                npc.AbandonPlan();
                npc.DecisionCooldown = 2.5f;

                Publish(new WorldEvent
                {
                    Kind = EventKind.Slipped,
                    Position = npc.Position,
                    TrueActorId = "",
                    ObjectId = obj.Id,
                    VictimId = npc.Id,
                    Loudness = 0.85f,
                    Severity = 0.7f,
                    LeavesEvidence = true,
                    Description = npc.Name + " slips on " + obj.Name
                });

                InvestigateObject(npc, obj);
                return;
            }
        }

        /// <summary>
        /// An NPC is looking straight at something that is wrong. Find the event
        /// that caused it, remember it, and work out who to blame.
        /// </summary>
        public void InvestigateObject(Npc npc, SmartObject obj)
        {
            WorldEvent cause = null;
            IReadOnlyList<WorldEvent> log = Events.Log;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                WorldEvent e = log[i];
                if (!e.LeavesEvidence) continue;
                if (e.ObjectId != obj.Id) continue;
                if (Time - e.Time > 120f) break;
                cause = e;
                break;
            }

            if (cause == null) return;
            if (npc.Memory.KnowsAbout(cause.Kind, cause.ObjectId)) return;

            RegisterDiscovery(npc, cause);
        }

        private void RegisterDiscovery(Npc npc, WorldEvent cause)
        {
            npc.Memory.Remember(new MemoryEntry
            {
                Kind = cause.Kind,
                ObjectId = cause.ObjectId,
                BelievedActorId = "",
                Where = cause.Position,
                When = cause.Time,
                Confidence = 1f,
                FirstHand = true,
                Description = cause.Description
            });

            Publish(new WorldEvent
            {
                Kind = EventKind.Discovery,
                Position = cause.Position,
                TrueActorId = "",
                ObjectId = cause.ObjectId,
                VictimId = cause.VictimId,
                Loudness = 0.2f,
                Severity = 0.2f,
                Description = npc.Name + " discovers: " + cause.Description
            });

            SmartObject obj = World.GetObject(cause.ObjectId);
            bool mine = obj != null && obj.OwnerId == npc.Id;

            if (mine || cause.VictimId == npc.Id)
            {
                AngerModel.Add(npc, AngerModel.PropertyViolation(npc, cause.Severity), this,
                    "someone messed with " + (obj != null ? obj.Name : "their things"));
            }
            else
            {
                AngerModel.Add(npc, AngerModel.DisorderSeen(npc, cause.Severity), this, "state of the place");
            }

            BlameResult blame = BlameResolver.Resolve(npc, cause, this);
            if (!blame.HasSuspect)
            {
                npc.Say("How did that even happen?", 2.5f);
                return;
            }

            ApplyBlame(npc, blame, cause);
        }

        private void ApplyBlame(Npc npc, BlameResult blame, WorldEvent cause)
        {
            float paranoiaScale = Mathx.Lerp(0.6f, 1.4f, npc.Personality.Paranoia);
            float gain = blame.Confidence * 0.45f * paranoiaScale;

            npc.AddSuspicion(blame.SuspectId, gain);
            npc.AddRelationship(blame.SuspectId, -0.12f);

            string suspectName = DisplayName(blame.SuspectId);
            npc.Say(suspectName + "... " + blame.Reason + ".", 3f);
            Log(npc.Name + " suspects " + suspectName + " (" + blame.Reason + ")");

            if (blame.SuspectId == Player.Id)
            {
                if (npc.SuspicionOf(Player.Id) >= CaughtThreshold)
                {
                    Outcome = GameOutcome.Caught;
                    Log(npc.Name + " is certain it was you. Caught.");
                }
                return;
            }

            Npc suspect = World.GetNpc(blame.SuspectId);
            if (suspect == null) return;

            if (npc.SuspicionOf(blame.SuspectId) > 0.45f && npc.ConfrontCooldown <= 0f)
            {
                npc.ConfrontTargetId = suspect.Id;
                npc.Activity = NpcActivity.Confronting;
                npc.AbandonPlan();
                npc.Activity = NpcActivity.Confronting;
            }
        }

        private void StartInvestigation(Npc npc, Vec3 point, string why)
        {
            npc.InvestigationPoint = point;
            npc.Activity = NpcActivity.Investigating;
            npc.CurrentPlan = null;
            npc.Say(why + "...", 2f);
        }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        public WorldEvent Publish(WorldEvent e)
        {
            e.Time = Time;
            Events.Publish(e);

            for (int i = 0; i < World.Npcs.Count; i++)
            {
                PerceiveEvent(World.Npcs[i], e);
            }

            if (e.Severity >= 0.3f || e.Kind == EventKind.Outburst || e.Kind == EventKind.Accusation)
            {
                Log(e.Description);
            }

            return e;
        }

        private void PerceiveEvent(Npc npc, WorldEvent e)
        {
            if (e.TrueActorId == npc.Id) return;

            bool saw = npc.Perception.CanSee(npc.Position, npc.Facing, e.Position, World);
            bool heard = npc.Perception.CanHear(npc.Position, e.Position, e.Loudness, World);
            if (!saw && !heard) return;

            // Caught in the act: the culprit is visible at the scene right now.
            // Only for events that are actually incriminating - losing your temper
            // in public is not a crime you can be "caught" committing.
            if (saw && e.TrueActorId.Length > 0 && IsIncriminating(e.Kind))
            {
                Vec3 actorPos = ActorPosition(e.TrueActorId);
                bool actorVisible = npc.Perception.CanSee(npc.Position, npc.Facing, actorPos, World)
                                    && Vec3.FlatDistance(actorPos, e.Position) < 3.5f;

                if (actorVisible && e.Severity >= 0.25f)
                {
                    float shock = 0.45f + e.Severity * 0.5f;
                    npc.AddSuspicion(e.TrueActorId, shock);
                    npc.Memory.RecordSighting(e.TrueActorId, e.Position, Time, 1f);
                    npc.Memory.Remember(new MemoryEntry
                    {
                        Kind = e.Kind,
                        ObjectId = e.ObjectId,
                        BelievedActorId = e.TrueActorId,
                        Where = e.Position,
                        When = Time,
                        Confidence = 1f,
                        FirstHand = true,
                        Description = "saw " + DisplayName(e.TrueActorId) + ": " + e.Description
                    });

                    npc.Say("I SAW that, " + DisplayName(e.TrueActorId) + "!");
                    Log(npc.Name + " catches " + DisplayName(e.TrueActorId) + " in the act");

                    if (e.TrueActorId == Player.Id && npc.SuspicionOf(Player.Id) >= CaughtThreshold)
                    {
                        Outcome = GameOutcome.Caught;
                        Log(npc.Name + " saw you do it. Caught.");
                    }
                    return;
                }
            }

            switch (e.Kind)
            {
                case EventKind.Outburst:
                case EventKind.Argument:
                case EventKind.Accusation:
                    AngerModel.Add(npc, AngerModel.WitnessedConflict(npc), this, "shouting nearby");
                    if (npc.Activity == NpcActivity.Idle && Rng.Chance(0.35f))
                    {
                        StartInvestigation(npc, e.Position, "what's going on over there");
                    }
                    break;

                case EventKind.Noise:
                case EventKind.Slipped:
                    if (npc.Activity == NpcActivity.Idle &&
                        Rng.Chance(0.25f + npc.Personality.Observance * 0.45f))
                    {
                        StartInvestigation(npc, e.Position, "what was that");
                    }
                    break;

                default:
                    if (saw && e.LeavesEvidence)
                    {
                        SmartObject obj = World.GetObject(e.ObjectId);
                        if (obj != null) InvestigateObject(npc, obj);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Social proximity
        // ------------------------------------------------------------------

        private void TickSocialProximity(float dt)
        {
            for (int i = 0; i < World.Npcs.Count; i++)
            {
                Npc a = World.Npcs[i];
                if (a.GossipCooldown > 0f) continue;
                if (a.Activity != NpcActivity.Idle && a.Activity != NpcActivity.Walking) continue;

                for (int j = i + 1; j < World.Npcs.Count; j++)
                {
                    Npc b = World.Npcs[j];
                    if (b.GossipCooldown > 0f) continue;
                    if (b.Activity != NpcActivity.Idle && b.Activity != NpcActivity.Walking) continue;
                    if (Vec3.FlatDistance(a.Position, b.Position) > 2.6f) continue;

                    float chattiness = (a.Personality.Sociability + b.Personality.Sociability) * 0.5f;
                    if (!Rng.Chance(dt * (0.15f + chattiness * 0.5f))) continue;

                    Gossip.Exchange(a, b, this);
                    a.Activity = NpcActivity.Chatting;
                    b.Activity = NpcActivity.Chatting;
                    a.ActivityTimer = 4f;
                    b.ActivityTimer = 4f;
                    break;
                }
            }
        }

        // ------------------------------------------------------------------
        // Player API
        // ------------------------------------------------------------------

        /// <summary>Headless / simple movement. Unity uses SyncPlayer instead.</summary>
        public void MovePlayer(Vec3 direction, bool sneaking, float dt)
        {
            Player.Sneaking = sneaking;

            float speed = sneaking ? Player.SneakSpeed : Player.WalkSpeed;
            Vec3 dir = direction.Normalized;

            if (dir.SqrMagnitude > 0.0001f)
            {
                Vec3 target = Player.Position + dir * (speed * dt);
                MoveActor(ref Player.Position, ref Player.Facing, target, speed * dt);
                Player.MovementNoise = sneaking ? 0.05f : 0.45f;
            }
            else
            {
                Player.MovementNoise = 0f;
            }

            CarryFollow();
        }

        /// <summary>Unity drives the transform; push the result back into the sim.</summary>
        public void SyncPlayer(Vec3 position, Vec3 facing, bool sneaking, bool moving)
        {
            Player.Position = position;
            Player.Facing = facing;
            Player.Sneaking = sneaking;
            Player.MovementNoise = !moving ? 0f : (sneaking ? 0.05f : 0.45f);
            CarryFollow();
        }

        private void CarryFollow()
        {
            if (!Player.IsCarrying) return;
            SmartObject held = World.GetObject(Player.CarryingObjectId);
            if (held != null) held.Position = Player.Position;
        }

        public List<InteractionOption> GetPlayerInteractions(float reach = 2.2f)
        {
            List<InteractionOption> options = new List<InteractionOption>();

            for (int i = 0; i < World.Objects.Count; i++)
            {
                SmartObject obj = World.Objects[i];
                if (obj.HeldBy.Length > 0 && obj.HeldBy != Player.Id) continue;

                float distance = Vec3.FlatDistance(obj.Position, Player.Position);
                if (distance > reach) continue;

                for (int a = 0; a < obj.Affordances.Count; a++)
                {
                    Affordance aff = obj.Affordances[a];
                    if (!aff.AvailableFor(obj, null)) continue;
                    options.Add(new InteractionOption
                    {
                        Object = obj,
                        Affordance = aff,
                        Distance = distance
                    });
                }
            }

            options.Sort((x, y) => x.Distance.CompareTo(y.Distance));
            return options;
        }

        public bool PlayerInteract(InteractionOption option)
        {
            if (option == null || Outcome != GameOutcome.InProgress) return false;
            if (!option.Affordance.AvailableFor(option.Object, null)) return false;

            AffordanceContext ctx = new AffordanceContext
            {
                Sim = this,
                Object = option.Object,
                Npc = null,
                ActorId = Player.Id
            };

            if (option.Affordance.Effect != null) option.Affordance.Effect(ctx);

            // Anyone watching right now has seen you do this.
            float incrimination = option.Affordance.Incrimination;
            if (incrimination > 0f)
            {
                for (int i = 0; i < World.Npcs.Count; i++)
                {
                    Npc npc = World.Npcs[i];
                    if (!npc.Perception.CanSee(npc.Position, npc.Facing, Player.Position, World)) continue;

                    npc.AddSuspicion(Player.Id, incrimination * 0.55f);
                    npc.Memory.RecordSighting(Player.Id, Player.Position, Time, incrimination);
                    if (incrimination > 0.5f)
                    {
                        npc.Say("What are you doing over there?");
                        Log(npc.Name + " watches you closely.");
                    }

                    if (npc.SuspicionOf(Player.Id) >= CaughtThreshold)
                    {
                        Outcome = GameOutcome.Caught;
                        Log(npc.Name + " caught you red-handed.");
                    }
                }
            }

            return true;
        }

        public bool PlayerInteract(string objectId, string affordanceId)
        {
            List<InteractionOption> options = GetPlayerInteractions();
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Object.Id == objectId && options[i].Affordance.Id == affordanceId)
                {
                    return PlayerInteract(options[i]);
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Objective
        // ------------------------------------------------------------------

        private void CheckObjective()
        {
            Npc target = Target;
            if (target == null) return;

            if (!ObjectiveMet && target.PeakAnger >= AngerWinThreshold)
            {
                ObjectiveMet = true;
                Log(target.Name + " has completely lost it. Now get out without being pinned for it.");
            }

            if (!ObjectiveMet) return;

            Zone exit = World.GetZone(ExitZoneId);
            if (exit == null || !exit.Contains(Player.Position)) return;

            if (target.SuspicionOf(Player.Id) >= BlownCoverThreshold)
            {
                Outcome = GameOutcome.Caught;
                Log(target.Name + " knows exactly who did this. Not a clean escape.");
                return;
            }

            Outcome = GameOutcome.Won;
            Log("You slip out. Nobody has any idea it was you.");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        public Vec3 ActorPosition(string actorId)
        {
            if (actorId == Player.Id) return Player.Position;
            Npc npc = World.GetNpc(actorId);
            return npc != null ? npc.Position : Vec3.Zero;
        }

        public string DisplayName(string actorId)
        {
            if (actorId == Player.Id) return Player.Name;
            Npc npc = World.GetNpc(actorId);
            return npc != null ? npc.Name : actorId;
        }

        public void NoteAngerChange(Npc npc, float amount, string reason)
        {
            if (amount < 0.05f) return;
            Log(string.Format("{0} is angrier ({1:0.00}) - {2}", npc.Name, npc.Anger, reason));
        }

        public void Log(string line)
        {
            _feed.Add(string.Format("[{0:0}s] {1}", Time, line));
            if (_feed.Count > FeedCapacity) _feed.RemoveAt(0);
        }

        /// <summary>Walk toward a target, sliding along walls rather than through them.</summary>
        private void MoveActor(ref Vec3 position, ref Vec3 facing, Vec3 target, float step)
        {
            Vec3 desired = Vec3.MoveTowards(position, target, step);
            desired = World.Clamp(desired);

            if (World.HasLineOfSight(position, desired))
            {
                Vec3 delta = desired - position;
                if (delta.SqrMagnitude > 0.000001f) facing = delta.Normalized;
                position = desired;
                return;
            }

            Vec3 slideX = World.Clamp(new Vec3(desired.X, position.Y, position.Z));
            if (World.HasLineOfSight(position, slideX))
            {
                position = slideX;
                return;
            }

            Vec3 slideZ = World.Clamp(new Vec3(position.X, position.Y, desired.Z));
            if (World.HasLineOfSight(position, slideZ)) position = slideZ;
        }

        public HudSnapshot BuildHud()
        {
            HudSnapshot hud = new HudSnapshot();
            hud.Time = Time;
            hud.TimeRemaining = Math.Max(0f, TimeLimit - Time);
            hud.ObjectiveMet = ObjectiveMet;
            hud.Outcome = Outcome;

            Npc target = Target;
            if (target != null)
            {
                hud.TargetName = target.Name;
                hud.TargetAnger = target.Anger;
                hud.TargetPeakAnger = target.PeakAnger;
            }

            for (int i = 0; i < World.Npcs.Count; i++)
            {
                float s = World.Npcs[i].SuspicionOf(Player.Id);
                if (s > hud.HighestSuspicion)
                {
                    hud.HighestSuspicion = s;
                    hud.HighestSuspicionBy = World.Npcs[i].Name;
                }
            }

            int from = Math.Max(0, _feed.Count - 8);
            for (int i = from; i < _feed.Count; i++) hud.Feed.Add(_feed[i]);

            hud.Interactions = GetPlayerInteractions();
            return hud;
        }
    }
}
