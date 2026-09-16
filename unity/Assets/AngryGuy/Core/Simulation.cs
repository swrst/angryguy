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

        /// <summary>
        /// Player-facing feedback, drained by whatever is presenting the game.
        /// Everything the player needs to understand goes through here.
        /// </summary>
        public readonly FeedbackQueue Feedback = new FeedbackQueue();

        private readonly List<PlayerAction> _playerActions = new List<PlayerAction>();

        /// <summary>
        /// How unsettled the whole building is. Rises with every unexplained
        /// thing that gets discovered and makes everyone warier.
        ///
        /// This is the difficulty curve, told from inside the fiction: a player
        /// who breaks everything at once ends up working in a room full of people
        /// who are actively looking for a culprit.
        /// </summary>
        public float Unease;

        /// <summary>While this is in the future, everyone abandons work and files out.</summary>
        public float AlarmUntil = -1f;

        public bool AlarmRinging
        {
            get { return Time < AlarmUntil; }
        }

        /// <summary>Assembly point during an evacuation. Defaults to the exit.</summary>
        public string AssemblyZoneId = "";

        public IReadOnlyList<PlayerAction> PlayerActions
        {
            get { return _playerActions; }
        }

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

            // The building calms down if nothing else happens.
            Unease = Mathx.Clamp01(Unease - 0.006f * dt);

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
            npc.Mind.Tick(dt, npc.Personality);
            AngerModel.Decay(npc, dt);

            TickRealisation(npc);

            float starvation = AngerModel.Starvation(npc, dt);
            if (starvation > 0f) AngerModel.Add(npc, starvation, this, "unmet needs");

            npc.GossipCooldown -= dt;
            npc.ConfrontCooldown -= dt;
            npc.OutburstCooldown -= dt;
            npc.DecisionCooldown -= dt;
            npc.FumbleCooldown -= dt;
            npc.SpeechTimer -= dt;
            if (npc.SpeechTimer <= 0f) npc.Speech = "";

            PerceptionSample(npc, dt);
            TickBelonging(npc, dt);
            CheckHazards(npc);
            TickClumsiness(npc, dt);

            // Explode when things get WORSE, not merely while they are still bad.
            // Without the rising check a pegged-out NPC shouts every 26 seconds
            // forever, which stops meaning anything.
            if (npc.Anger >= AngerModel.BoilingPoint
                && npc.OutburstCooldown <= 0f
                && npc.Anger > npc.AngerAtLastOutburst + 0.02f)
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

                case NpcActivity.Repairing:
                    TickRepairing(npc, dt);
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

        /// <summary>
        /// The moment an NPC stops believing in coincidence. It happens once, it
        /// is said out loud, and from then on they are a much harder audience.
        /// </summary>
        private void TickRealisation(Npc npc)
        {
            if (!npc.Mind.SuspectsSabotage || npc.Mind.VoicedSuspicionOfSabotage) return;

            npc.Mind.VoicedSuspicionOfSabotage = true;
            npc.Say(Lines.RealisesSabotage(npc), 5f);

            Announce(FeedbackKind.Alert,
                npc.Name + " has decided this isn't bad luck - they're looking for someone now",
                npc.Id);

            Publish(new WorldEvent
            {
                Kind = EventKind.Realisation,
                Position = npc.Position,
                TrueActorId = npc.Id,
                Loudness = 0.55f,
                Severity = 0.3f,
                Description = npc.Name + " thinks someone is doing this deliberately"
            });

            Unease = Mathx.Clamp01(Unease + 0.2f);
        }

        /// <summary>
        /// Something has gone wrong for one person, in front of whoever happened
        /// to be looking. This is where most of the character comes from: the
        /// same mishap humiliates the victim, delights people who dislike them,
        /// and frightens the timid.
        /// </summary>
        private void ReactToMisfortune(Npc victim, float severity, Vec3 where, string what)
        {
            victim.Mind.Misfortune(severity, victim.Personality);

            List<Npc> witnesses = new List<Npc>();
            for (int i = 0; i < World.Npcs.Count; i++)
            {
                Npc other = World.Npcs[i];
                if (other == victim) continue;
                if (other.Perception.CanSee(other.Position, other.Facing, where, World)) witnesses.Add(other);
            }

            if (witnesses.Count == 0)
            {
                victim.Say(Lines.Misfortune(victim, what));
                return;
            }

            // Failing in private is annoying. Failing in front of the staff is
            // humiliating, and humiliation is worth far more anger.
            float audience = Mathx.Clamp(witnesses.Count / 2f, 0.5f, 1.6f);
            float humiliation = 0.09f * severity * audience
                                * Mathx.Lerp(0.5f, 1.8f, victim.Personality.Temper);

            victim.Mind.Embarrassment = Mathx.Clamp01(victim.Mind.Embarrassment + 0.3f * audience);
            AngerModel.Add(victim, humiliation, this, "and everyone saw it");
            victim.Say(Lines.Embarrassed(victim, witnesses.Count));

            for (int i = 0; i < witnesses.Count; i++)
            {
                Npc witness = witnesses[i];
                float relationship = witness.RelationshipWith(victim.Id);

                bool findsItFunny = severity > 0.4f &&
                                    (relationship < -0.1f || witness.Personality.Sociability > 0.65f);

                if (findsItFunny)
                {
                    Laugh(witness, victim);
                }
                else if (severity > 0.5f && witness.Personality.Temper < 0.55f)
                {
                    // The timid find other people's accidents unsettling.
                    witness.Mind.Fear = Mathx.Clamp01(witness.Mind.Fear + 0.22f);
                    witness.Say(Lines.Afraid(witness), 2.5f);
                }
            }
        }

        /// <summary>
        /// Being laughed at is its own injury, and it damages the relationship
        /// with whoever laughed - which is who they will blame next time.
        /// </summary>
        private void Laugh(Npc laugher, Npc victim)
        {
            laugher.Mind.Amusement = Mathx.Clamp01(laugher.Mind.Amusement + 0.45f);
            laugher.Mind.Boredom = 0f;
            laugher.Say(Lines.Amused(laugher, victim.Name), 3f);

            Publish(new WorldEvent
            {
                Kind = EventKind.Laughter,
                Position = laugher.Position,
                TrueActorId = laugher.Id,
                VictimId = victim.Id,
                Loudness = 0.5f,
                Severity = 0.25f,
                Description = laugher.Name + " laughs at " + victim.Name
            });

            if (victim.Perception.CanHear(victim.Position, laugher.Position, 0.5f, World))
            {
                AngerModel.Add(victim, 0.05f * Mathx.Lerp(0.5f, 1.9f, victim.Personality.Temper),
                    this, "being laughed at by " + laugher.Name);
                victim.AddRelationship(laugher.Id, -0.2f);
            }
        }

        /// <summary>
        /// Sets everyone evacuating for a while. Enormously effective and
        /// enormously incriminating - the classic "worth it?" tool.
        /// </summary>
        public void TriggerAlarm(float seconds)
        {
            AlarmUntil = Time + seconds;
            Announce(FeedbackKind.Alert, "FIRE ALARM - everyone is heading outside");

            for (int i = 0; i < World.Npcs.Count; i++)
            {
                Npc npc = World.Npcs[i];
                npc.AbandonPlan();
                npc.Mind.Fear = Mathx.Clamp01(npc.Mind.Fear + 0.3f);
                npc.Say("Is that the alarm? Out, everyone out.", 4f);
            }
        }

        private void Decide(Npc npc)
        {
            if (npc.DecisionCooldown > 0f) return;
            npc.DecisionCooldown = 0.4f;

            // An alarm overrides everything. Nobody stops to finish the washing up.
            if (AlarmRinging)
            {
                Zone assembly = World.GetZone(AssemblyZoneId.Length > 0 ? AssemblyZoneId : ExitZoneId);
                if (assembly != null)
                {
                    npc.CurrentPlan = null;
                    npc.MoveTarget = assembly.Center;
                    npc.Activity = NpcActivity.Walking;
                    npc.DecisionCooldown = 2f;
                    npc.ClosestApproach = float.MaxValue;
                    npc.StuckTimer = 0f;
                    return;
                }
            }

            // Free at last - go and deal with the thing they noticed earlier.
            if (npc.PendingRepairId.Length > 0)
            {
                SmartObject pending = World.GetObject(npc.PendingRepairId);
                npc.PendingRepairId = "";
                if (TryStartRepair(npc, pending)) return;
            }

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
            npc.ClosestApproach = float.MaxValue;
            npc.StuckTimer = 0f;
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
                npc.ClosestApproach = float.MaxValue;
                npc.StuckTimer = 0f;
                return;
            }

            // Nobody likes being bored, and nobody likes being alone in a room
            // where things keep going wrong. Both send them looking for company,
            // which quietly reshapes where everyone is standing.
            if (npc.Mind.Boredom > 0.7f || npc.Mind.Fear > 0.55f)
            {
                Npc company = NearestOtherNpc(npc);
                if (company != null && Vec3.FlatDistance(company.Position, npc.Position) > 2.5f)
                {
                    npc.MoveTarget = company.Position;
                    npc.Activity = NpcActivity.Walking;
                    npc.DecisionCooldown = 3f;
                    npc.Mind.Boredom = Mathx.Clamp01(npc.Mind.Boredom - 0.25f);
                    if (Rng.Chance(0.3f)) npc.Say(Lines.Bored(npc), 2.5f);
                    return;
                }
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

        private Npc NearestOtherNpc(Npc npc)
        {
            Npc best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < World.Npcs.Count; i++)
            {
                Npc other = World.Npcs[i];
                if (other == npc) continue;

                float distance = Vec3.FlatDistance(other.Position, npc.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                }
            }

            return best;
        }

        private void TickWalking(Npc npc, float dt)
        {
            // Without this, a shut door turns into a permanent roadblock and the
            // NPC stands against it for the rest of the level looking broken.
            SmartObject door = World.BlockingDoor(npc.Position, npc.MoveTarget);
            if (door != null && Vec3.FlatDistance(npc.Position, door.Position) < 1.6f)
            {
                door.SetState("open", 1f);
                Publish(new WorldEvent
                {
                    Kind = EventKind.Noise,
                    Position = door.Position,
                    TrueActorId = npc.Id,
                    ObjectId = door.Id,
                    Loudness = 0.3f,
                    Severity = 0.05f,
                    Description = npc.Name + " pushes the " + door.Name + " open"
                });
            }

            // Steer via a doorway when the target is behind a wall, otherwise the
            // slide-along-walls fallback dead-ends in a corner and never recovers.
            Vec3 goal = npc.MoveTarget;
            if (!World.HasLineOfSight(npc.Position, goal))
            {
                Vec3 portal;
                if (World.TryFindPortal(npc.Position, goal, out portal)) goal = portal;
            }

            MoveActor(ref npc.Position, ref npc.Facing, goal, npc.MoveSpeed * dt);

            float remaining = Vec3.FlatDistance(npc.Position, npc.MoveTarget);

            // Safety net for any geometry the portals do not cover: if they have
            // stopped getting closer, give up rather than freezing for the level.
            if (remaining < npc.ClosestApproach - 0.15f)
            {
                npc.ClosestApproach = remaining;
                npc.StuckTimer = 0f;
            }
            else
            {
                npc.StuckTimer += dt;
                if (npc.StuckTimer > 6f)
                {
                    GiveUpOnPlan(npc);
                    return;
                }
            }

            if (remaining > 1.1f) return;

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

        /// <summary>
        /// Couldn't get there at all. Not a sabotage failure - no anger, no
        /// grievance, just a quiet change of mind, because the alternative is an
        /// NPC standing against a wall forever.
        /// </summary>
        private void GiveUpOnPlan(Npc npc)
        {
            if (npc.CurrentPlan != null)
            {
                npc.AvoidUntil[npc.CurrentPlan.Target.Id] = Time + 12f;
            }

            npc.AbandonPlan();
            npc.StuckTimer = 0f;
            npc.ClosestApproach = float.MaxValue;
            npc.DecisionCooldown = 1f;
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
            npc.Mind.Satisfaction(npc.Personality);

            if (npc.Mind.Pride > 0.5f && Rng.Chance(0.25f)) npc.Say(Lines.Proud(npc), 2.5f);

            npc.CurrentPlan = null;
            npc.Activity = NpcActivity.Idle;
            npc.DecisionCooldown = 0.7f;
        }

        private void FailPlan(Npc npc, Plan plan)
        {
            float amount = AngerModel.PlanFailure(npc, plan.Motive);
            AngerModel.Add(npc, amount, this, "couldn't " + plan.Describe());

            npc.AvoidUntil[plan.Target.Id] = Time + 32f;
            ReactToMisfortune(npc, 0.45f, plan.Target.Position, plan.Target.Name);

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

        // ------------------------------------------------------------------
        // Putting things right
        // ------------------------------------------------------------------

        /// <summary>
        /// Anything a conscientious person would feel compelled to deal with:
        /// broken, filthy, spilled, or simply not where it lives.
        /// </summary>
        public static bool NeedsPuttingRight(SmartObject obj)
        {
            if (obj == null || obj.Concealed) return false;
            if (obj.HeldBy.Length > 0) return false;
            if (obj.IsBroken) return true;
            if (obj.HasTag(Tags.Mess) && obj.HasTag(Tags.Hazard)) return true;
            if (obj.GetState(StateKeys.Dirty) > 0.5f) return true;
            if (obj.Portable && obj.IsAwayFromHome) return true;
            return false;
        }

        /// <summary>
        /// A diligent NPC who has just clocked a problem goes and fixes it.
        ///
        /// This is the single most important pressure on the player: sabotage
        /// now has a shelf life. Break the stove and Bruno may well have it
        /// working again before Gordon ever walks over to it. The counter-play
        /// is to keep him busy, keep him out of the room, or set the trap so
        /// close to the target's arrival that nobody has time to undo it.
        /// </summary>
        public bool TryStartRepair(Npc npc, SmartObject obj)
        {
            if (npc.Personality.Diligence < 0.55f) return false;
            if (!NeedsPuttingRight(obj)) return false;
            if (npc.Activity == NpcActivity.Repairing) return false;
            if (npc.Anger >= AngerModel.BoilingPoint) return false;

            float until;
            if (npc.RepairCooldown.TryGetValue(obj.Id, out until) && until > Time) return false;

            // Mid-task, he notes it and carries on. Sabotage lands in the gap
            // between him seeing the problem and him being free to deal with it,
            // so keeping him occupied is a genuine tactic rather than a nuisance.
            if (npc.Activity == NpcActivity.Using || npc.Activity == NpcActivity.Confronting)
            {
                npc.PendingRepairId = obj.Id;
                npc.Say(Lines.NotesForLater(npc, obj), 3f);
                return false;
            }

            npc.PendingRepairId = "";
            npc.AbandonPlan();
            npc.RepairTargetId = obj.Id;
            npc.RepairTimer = 0f;
            npc.MoveTarget = obj.Position;
            npc.ClosestApproach = float.MaxValue;
            npc.StuckTimer = 0f;
            npc.Activity = NpcActivity.Repairing;
            npc.Say(Lines.StartsFixing(npc, obj), 3f);
            return true;
        }

        private void TickRepairing(Npc npc, float dt)
        {
            SmartObject obj = World.GetObject(npc.RepairTargetId);
            if (obj == null || !NeedsPuttingRight(obj))
            {
                // Somebody beat them to it, or the player picked it up again.
                npc.Activity = NpcActivity.Idle;
                npc.RepairTargetId = "";
                npc.DecisionCooldown = 0.8f;
                return;
            }

            if (npc.RepairTimer > 0f)
            {
                npc.RepairTimer -= dt;
                if (npc.RepairTimer <= 0f) CompleteRepair(npc, obj);
                return;
            }

            npc.MoveTarget = obj.Position;
            Vec3 goal = npc.MoveTarget;
            if (!World.HasLineOfSight(npc.Position, goal))
            {
                Vec3 portal;
                if (World.TryFindPortal(npc.Position, goal, out portal)) goal = portal;
            }

            MoveActor(ref npc.Position, ref npc.Facing, goal, npc.MoveSpeed * dt);

            float remaining = Vec3.FlatDistance(npc.Position, npc.MoveTarget);
            if (remaining <= 1.3f)
            {
                // Putting a shaker back on a shelf is a moment. Getting the knobs
                // back on a stove is a job, and a job is a window: the player can
                // watch him kneel down and go and cause trouble somewhere else
                // while he is committed to it.
                npc.RepairTimer = obj.IsBroken ? 9f + Rng.Range(0f, 3f) : 3.5f + Rng.Range(0f, 2f);
                npc.Say(Lines.WorkingOnIt(npc, obj), npc.RepairTimer);
                return;
            }

            if (remaining < npc.ClosestApproach - 0.15f)
            {
                npc.ClosestApproach = remaining;
                npc.StuckTimer = 0f;
            }
            else
            {
                npc.StuckTimer += dt;
                if (npc.StuckTimer > 6f)
                {
                    npc.RepairCooldown[npc.RepairTargetId] = Time + 45f;
                    npc.RepairTargetId = "";
                    npc.Activity = NpcActivity.Idle;
                    npc.DecisionCooldown = 1f;
                }
            }
        }

        private void CompleteRepair(Npc npc, SmartObject obj)
        {
            string what = obj.Name;

            if (obj.HasTag(Tags.Mess) && obj.HasTag(Tags.Hazard))
            {
                obj.Tags.Remove(Tags.Hazard);
                obj.Concealed = true;
            }

            obj.SetState(StateKeys.Broken, 0f);
            obj.SetState(StateKeys.Dirty, 0f);

            // Subtle tampering survives: he can put the shaker back on the shelf,
            // but he has no way of knowing the salt inside it is sugar.
            if (obj.GetState(StateKeys.Subtle) <= 0f) obj.SetState(StateKeys.Tampered, 0f);

            if (obj.Portable && obj.IsAwayFromHome) obj.Position = obj.HomePosition;

            npc.RepairCooldown[obj.Id] = Time + 25f;
            npc.RepairTargetId = "";
            npc.Activity = NpcActivity.Idle;
            npc.DecisionCooldown = 0.8f;

            npc.Needs.Add(NeedType.Order, 0.35f);
            npc.Mind.Satisfaction(npc.Personality);
            npc.Say(Lines.FinishedFixing(npc), 3f);

            Log(npc.Name + " puts " + what + " back in order");

            Publish(new WorldEvent
            {
                Kind = EventKind.Noise,
                Position = obj.Position,
                TrueActorId = npc.Id,
                ObjectId = obj.Id,
                Loudness = 0.25f,
                Severity = 0f,
                LeavesEvidence = false,
                Description = npc.Name + " sorts out " + what
            });

            Announce(FeedbackKind.Alert, npc.Name + " has undone your work on " + what, npc.Id);
        }

        // ------------------------------------------------------------------
        // Butterfingers
        // ------------------------------------------------------------------

        /// <summary>
        /// Clumsy NPCs generate real, unexplained anomalies of their own.
        ///
        /// This exists for the player's benefit, not for flavour. In a building
        /// where things go wrong on their own, "someone did this deliberately"
        /// is a much harder case to make - and there is a walking, credulous
        /// alternative suspect standing right there.
        ///
        /// Deliberately harmless: noise and nudged objects, never damage. The
        /// invariant that all anger on screen is player-caused has to hold.
        /// </summary>
        private void TickClumsiness(Npc npc, float dt)
        {
            if (npc.Personality.Clumsiness < 0.5f) return;
            if (npc.FumbleCooldown > 0f) return;
            if (npc.Activity != NpcActivity.Walking) return;

            // Roughly one fumble a minute for a very clumsy NPC on the move.
            if (!Rng.Chance(npc.Personality.Clumsiness * 0.02f * dt * 60f)) return;

            npc.FumbleCooldown = 25f;

            SmartObject victim = null;
            float best = 2.0f;
            for (int i = 0; i < World.Objects.Count; i++)
            {
                SmartObject candidate = World.Objects[i];
                if (!candidate.Portable || candidate.HeldBy.Length > 0) continue;
                if (candidate.OwnerId.Length > 0) continue;   // never their prized things
                float d = Vec3.FlatDistance(candidate.Position, npc.Position);
                if (d < best)
                {
                    best = d;
                    victim = candidate;
                }
            }

            if (victim == null)
            {
                npc.Say(Lines.Fumbles(npc), 2.5f);
                Publish(new WorldEvent
                {
                    Kind = EventKind.Noise,
                    Position = npc.Position,
                    TrueActorId = npc.Id,
                    ObjectId = "",
                    Loudness = 0.5f,
                    Severity = 0f,
                    LeavesEvidence = false,
                    Description = npc.Name + " walks into something"
                });
                return;
            }

            // Knock it somewhere near, and leave evidence with his name on it.
            // A history of Pip-shaped incidents is exactly what the player wants
            // in the room's memory when their own sabotage is discovered.
            victim.Position = new Vec3(
                victim.Position.X + Rng.Range(-1.1f, 1.1f),
                victim.Position.Y,
                victim.Position.Z + Rng.Range(-1.1f, 1.1f));

            npc.Say(Lines.Fumbles(npc), 2.5f);

            Publish(new WorldEvent
            {
                Kind = EventKind.ObjectTaken,
                Position = victim.Position,
                TrueActorId = npc.Id,
                ObjectId = victim.Id,
                Loudness = 0.45f,
                Severity = 0.05f,
                LeavesEvidence = true,
                Description = npc.Name + " knocks " + victim.Name + " over"
            });
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

            // Being repeatedly fingered for things wears on a person, and the
            // scapegoat eventually starts fighting back.
            accused.Mind.FeelsBlamed = Mathx.Clamp01(accused.Mind.FeelsBlamed + 0.35f);
            accused.Mind.DayQuality = Mathx.Clamp01(accused.Mind.DayQuality - 0.12f);
            if (accused.Mind.FeelsBlamed > 0.6f) accused.Say(Lines.FeelsBlamed(accused), 4f);
            AngerModel.Add(accuser, 0.03f, this, "arguing with " + accused.Name);

            accused.AddRelationship(accuser.Id, -0.35f);
            accuser.AddRelationship(accused.Id, -0.25f);
            RaiseSuspicion(accused, accuser.Id, 0.08f, "resents being accused");

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
                RaiseSuspicion(accuser, deflectTo, best * 0.35f * accuser.Personality.Gullibility,
                    "was told it was " + DisplayName(deflectTo));

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
            npc.AngerAtLastOutburst = npc.Anger;
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
            if (CanSeePlayer(npc))
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
                !CanSeePlayer(npc) &&
                npc.Perception.CanHear(npc.Position, Player.Position, Player.MovementNoise, World) &&
                npc.Activity == NpcActivity.Idle &&
                Rng.Chance(dt * 0.25f * npc.Personality.Observance))
            {
                StartInvestigation(npc, Player.Position, "heard something");
            }

            // Spotting that an object is wrong. Only a chance per sample, so
            // observant NPCs are meaningfully more dangerous - and a wary NPC in
            // an unsettled building is much more dangerous again.
            if (!Rng.Chance(dt * 0.6f)) return;

            float alertness = npc.Mind.Alertness * (1f + Unease * 0.6f);

            List<SmartObject> near = World.ObjectsNear(npc.Position, npc.Perception.SightRange);
            for (int i = 0; i < near.Count; i++)
            {
                SmartObject obj = near[i];
                if (!npc.Perception.CanSee(npc.Position, npc.Facing, obj.Position, World)) continue;
                if (!LooksWrong(obj)) continue;
                if (!Rng.Chance(Mathx.Clamp01(npc.Perception.NoticeChance * alertness))) continue;

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
                npc.AbandonPlan();
                npc.DecisionCooldown = 2.5f;

                // Going over in front of the whole kitchen is the single funniest
                // thing in the game, and by far the most humiliating.
                ReactToMisfortune(npc, 0.85f, npc.Position, obj.Name);

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

            // Every unexplained thing that comes to light makes the building
            // warier as a whole, not just the person who found it.
            Unease = Mathx.Clamp01(Unease + 0.05f * cause.Severity);

            if (mine || cause.VictimId == npc.Id)
            {
                AngerModel.Add(npc, AngerModel.PropertyViolation(npc, cause.Severity), this,
                    "someone messed with " + (obj != null ? obj.Name : "their things"));
                npc.Mind.Misfortune(cause.Severity, npc.Personality);
            }
            else
            {
                AngerModel.Add(npc, AngerModel.DisorderSeen(npc, cause.Severity), this, "state of the place");
            }

            BlameResult blame = BlameResolver.Resolve(npc, cause, this);
            if (!blame.HasSuspect)
            {
                npc.Say("How did that even happen?", 2.5f);
                TryStartRepair(npc, obj);
                return;
            }

            ApplyBlame(npc, blame, cause);

            // Whoever they have decided to blame, the thing still needs sorting
            // out - and the sort of person who sorts things out is the sort of
            // person who ruins the player's afternoon.
            TryStartRepair(npc, obj);
        }

        private void ApplyBlame(Npc npc, BlameResult blame, WorldEvent cause)
        {
            float paranoiaScale = Mathx.Lerp(0.6f, 1.4f, npc.Personality.Paranoia);
            float gain = blame.Confidence * 0.45f * paranoiaScale;

            RaiseSuspicion(npc, blame.SuspectId, gain, blame.Reason);
            npc.AddRelationship(blame.SuspectId, -0.12f);

            string suspectName = DisplayName(blame.SuspectId);
            npc.Say(suspectName + "... " + blame.Reason + ".", 3f);
            Log(npc.Name + " suspects " + suspectName + " (" + blame.Reason + ")");

            if (blame.SuspectId == Player.Id) return;

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
                    // Bad, but survivable. Being seen once should put the player
                    // on the back foot, not end the level outright - they need the
                    // chance to back off and let it cool.
                    float shock = 0.25f + e.Severity * 0.35f;
                    RaiseSuspicion(npc, e.TrueActorId, shock, "saw you do it");
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
                    // A loud thing with nobody obviously behind it is unnerving,
                    // and the more of them there have been, the worse it gets.
                    if (e.TrueActorId.Length == 0 && e.Loudness > 0.5f)
                    {
                        npc.Mind.Fear = Mathx.Clamp01(
                            npc.Mind.Fear + 0.10f * (1f + Unease) *
                            Mathx.Lerp(1.4f, 0.5f, npc.Personality.Temper));
                    }

                    if (npc.Activity == NpcActivity.Idle &&
                        Rng.Chance((0.25f + npc.Personality.Observance * 0.45f) * npc.Mind.Alertness))
                    {
                        StartInvestigation(npc, e.Position, "what was that");
                    }
                    break;

                case EventKind.Realisation:
                    // Suspicion is contagious. One person saying it out loud puts
                    // the idea in everyone else's head.
                    npc.Mind.Wariness = Mathx.Clamp01(
                        npc.Mind.Wariness + 0.18f * npc.Personality.Gullibility);
                    break;

                case EventKind.Laughter:
                    // Laughter spreads, but only among people already enjoying themselves.
                    if (npc.Mind.Amusement > 0.2f)
                    {
                        npc.Mind.Amusement = Mathx.Clamp01(npc.Mind.Amusement + 0.15f);
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

            bool witnessed = AnyoneWatchingPlayer();

            if (option.Affordance.Effect != null) option.Affordance.Effect(ctx);

            _playerActions.Add(new PlayerAction
            {
                Time = Time,
                Label = option.Label,
                Sabotage = option.Affordance.IsSabotage,
                Witnessed = witnessed
            });

            // Anyone watching right now has seen you do this.
            float incrimination = option.Affordance.Incrimination;
            if (incrimination > 0f)
            {
                for (int i = 0; i < World.Npcs.Count; i++)
                {
                    Npc npc = World.Npcs[i];
                    if (!CanSeePlayer(npc)) continue;

                    RaiseSuspicion(npc, Player.Id, incrimination * 0.55f, "watched you do it");
                    npc.Memory.RecordSighting(Player.Id, Player.Position, Time, incrimination);
                    if (incrimination > 0.5f)
                    {
                        npc.Say("What are you doing over there?");
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
                Announce(FeedbackKind.Objective,
                    target.Name + " has completely lost it - now get out the back door", target.Id);
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

        /// <summary>
        /// The only place player visibility is decided, so hiding and sneaking
        /// cannot be silently bypassed by one caller that forgot about them.
        /// </summary>
        public bool CanSeePlayer(Npc npc)
        {
            if (Player.IsHidden) return false;

            if (!npc.Perception.CanSee(npc.Position, npc.Facing, Player.Position, World)) return false;

            // Moving slowly and low makes you much harder to pick out at range,
            // though anyone close still sees you perfectly well.
            if (Player.Sneaking)
            {
                float distance = Vec3.FlatDistance(npc.Position, Player.Position);
                if (distance > npc.Perception.SightRange * 0.45f) return false;
            }

            return true;
        }

        /// <summary>Is any NPC looking at the player right now?</summary>
        public bool AnyoneWatchingPlayer()
        {
            for (int i = 0; i < World.Npcs.Count; i++)
            {
                if (CanSeePlayer(World.Npcs[i])) return true;
            }
            return false;
        }

        /// <summary>Every NPC who can currently see the player, for the HUD warning.</summary>
        public List<Npc> WatchersOfPlayer()
        {
            List<Npc> watchers = new List<Npc>();
            for (int i = 0; i < World.Npcs.Count; i++)
            {
                if (CanSeePlayer(World.Npcs[i])) watchers.Add(World.Npcs[i]);
            }
            return watchers;
        }

        // ------------------------------------------------------------------
        // Throwing
        // ------------------------------------------------------------------

        /// <summary>
        /// Lob whatever you are holding. The noise happens where it lands, not
        /// where you are - the cleanest distraction tool in the game, and the
        /// one that best teaches "separate yourself from the evidence".
        /// </summary>
        public bool PlayerThrow()
        {
            if (!Player.IsCarrying || Outcome != GameOutcome.InProgress) return false;

            SmartObject held = World.GetObject(Player.CarryingObjectId);
            if (held == null) return false;

            Vec3 direction = Player.Facing.Normalized;
            if (direction.SqrMagnitude < 0.001f) direction = new Vec3(0f, 0f, 1f);

            // Walk the throw forward until something stops it.
            Vec3 landing = Player.Position;
            for (float step = 0.5f; step <= Player.ThrowRange; step += 0.5f)
            {
                Vec3 candidate = World.Clamp(Player.Position + direction * step);
                if (!World.HasLineOfSight(Player.Position, candidate)) break;
                landing = candidate;
            }

            held.HeldBy = "";
            held.Concealed = false;
            held.Position = landing;
            Player.CarryingObjectId = "";

            bool seen = AnyoneWatchingPlayer();

            Publish(new WorldEvent
            {
                Kind = EventKind.Noise,
                Position = landing,
                // Nobody sees where a thrown object came from unless they were
                // already watching the player.
                TrueActorId = seen ? Player.Id : "",
                ObjectId = held.Id,
                Loudness = 0.85f,
                Severity = 0.2f,
                Description = "something clatters across the floor"
            });

            _playerActions.Add(new PlayerAction
            {
                Time = Time,
                Label = "Threw the " + held.Name,
                Sabotage = false,
                Witnessed = seen
            });

            Announce(FeedbackKind.Info, "You throw the " + held.Name + " - that'll draw someone over");
            return true;
        }

        /// <summary>Tuck into a hiding spot, or step back out of one.</summary>
        public bool PlayerToggleHide(SmartObject spot)
        {
            if (Player.IsHidden)
            {
                Player.HidingInObjectId = "";
                Announce(FeedbackKind.Info, "You step back out");
                return true;
            }

            if (spot == null || !spot.HasTag(Tags.Hiding)) return false;
            if (Vec3.FlatDistance(spot.Position, Player.Position) > 2.2f) return false;

            Player.HidingInObjectId = spot.Id;
            Player.Position = spot.Position;
            Announce(FeedbackKind.Info, "Hidden. Nobody can see you here.");
            return true;
        }

        /// <summary>The zone a point sits in, innermost first.</summary>
        public Zone ZoneAt(Vec3 position)
        {
            Zone best = null;
            float bestRadius = float.MaxValue;

            for (int i = 0; i < World.Zones.Count; i++)
            {
                Zone zone = World.Zones[i];
                if (!zone.Contains(position)) continue;
                if (zone.Radius < bestRadius)
                {
                    bestRadius = zone.Radius;
                    best = zone;
                }
            }

            return best;
        }

        /// <summary>
        /// Being somewhere you have no business being, in front of someone who
        /// works there. Slow, constant pressure rather than a single spike - the
        /// player can cross the kitchen, they just cannot loiter in it.
        /// </summary>
        private void TickBelonging(Npc npc, float dt)
        {
            if (Player.IsHidden) return;
            if (!CanSeePlayer(npc)) return;

            Zone zone = ZoneAt(Player.Position);
            if (zone == null || !zone.StaffOnly) return;

            if (Player.Outfit.BelongsInZone(zone.Id))
            {
                // The uniform works. But it hides your role, not your face, and
                // someone who knows every member of staff will get there in the end.
                if (npc.Personality.Observance < 0.8f) return;

                float doubt = 0.004f * dt * (1f - Player.Outfit.Quality) * npc.Mind.Alertness;
                if (doubt <= 0f) return;

                RaiseSuspicion(npc, Player.Id, doubt, "doesn't recognise you", true);
                if (Rng.Chance(dt * 0.05f)) npc.Say("Hang on - do you work here?", 3f);
                return;
            }

            float rate = 0.045f * dt * npc.Mind.Alertness
                         * Mathx.Lerp(0.6f, 1.5f, npc.Personality.Paranoia);

            RaiseSuspicion(npc, Player.Id, rate,
                "you shouldn't be in the " + zone.Name.ToLowerInvariant(), true);
        }

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
            // Below this it is background grumbling, not a beat the player caused.
            if (amount < 0.02f) return;

            Feedback.Push(new FeedbackEvent
            {
                Kind = FeedbackKind.Anger,
                ActorId = npc.Id,
                Amount = ToDisplay(amount),
                Text = reason,
                Time = Time
            });

            if (amount >= 0.05f)
            {
                Log(string.Format("{0}: +{1} anger - {2}", npc.Name, ToDisplay(amount), reason));
            }
        }

        /// <summary>
        /// The one place suspicion is raised, so every increase is both clamped
        /// and reported. Direct calls to Npc.AddSuspicion bypass the player's
        /// only warning that they are being noticed.
        /// </summary>
        /// <summary>
        /// The one place suspicion is raised, so every increase is both clamped
        /// and reported.
        ///
        /// Set <paramref name="continuous"/> for per-tick pressure such as
        /// loitering somewhere you do not belong. The de-duplication below exists
        /// to stop a single discrete action stacking three separate penalties;
        /// applied to a trickle it quarters every tick and decay eats the rest,
        /// so a continuous source would silently do nothing at all.
        /// </summary>
        public void RaiseSuspicion(Npc npc, string actorId, float amount, string reason,
            bool continuous = false)
        {
            if (amount <= 0f || actorId.Length == 0) return;

            if (actorId == Player.Id && !continuous)
            {
                if (Time - npc.LastPlayerSuspicionTime < 1.5f) amount *= 0.25f;
                npc.LastPlayerSuspicionTime = Time;
            }

            float before = npc.SuspicionOf(actorId);
            npc.AddSuspicion(actorId, amount);
            float after = npc.SuspicionOf(actorId);
            float applied = after - before;
            if (applied <= 0f) return;

            if (actorId == Player.Id)
            {
                // Continuous pressure - standing somewhere you shouldn't - arrives
                // a sliver at a time. Reporting every sliver buries the one-off
                // events that actually need reacting to under a wall of "+1".
                // Bank it instead and report a meaningful amount.
                bool report = true;
                if (continuous)
                {
                    npc.PendingSuspicionReport += applied;
                    if (npc.PendingSuspicionReport < 0.05f) report = false;
                    else
                    {
                        applied = npc.PendingSuspicionReport;
                        npc.PendingSuspicionReport = 0f;
                    }
                }

                if (report)
                {
                    Feedback.Push(new FeedbackEvent
                    {
                        Kind = FeedbackKind.Suspicion,
                        ActorId = npc.Id,
                        Amount = ToDisplay(applied),
                        Text = reason,
                        Time = Time
                    });
                }

                SuspicionTier wasTier = Suspicion.TierFor(before);
                SuspicionTier isTier = Suspicion.TierFor(after);
                if (isTier > wasTier)
                {
                    Feedback.Push(new FeedbackEvent
                    {
                        Kind = FeedbackKind.Alert,
                        ActorId = npc.Id,
                        Text = npc.Name + " is " + Suspicion.Describe(isTier),
                        Time = Time
                    });
                    Log(npc.Name + " is " + Suspicion.Describe(isTier));
                }

                if (after >= CaughtThreshold)
                {
                    Outcome = GameOutcome.Caught;
                    Log(npc.Name + " is certain it was you.");
                }
            }
        }

        /// <summary>Internal 0..1 to the 0-100 scale players actually read.</summary>
        public static int ToDisplay(float normalised)
        {
            int value = (int)System.Math.Round(normalised * 100f);
            if (value == 0 && normalised > 0f) value = 1;
            return value;
        }

        public void Announce(FeedbackKind kind, string text, string actorId = "")
        {
            Feedback.Push(new FeedbackEvent
            {
                Kind = kind,
                ActorId = actorId,
                Text = text,
                Time = Time
            });
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
