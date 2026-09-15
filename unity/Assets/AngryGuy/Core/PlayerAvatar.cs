using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// The player's presence inside the simulation. Deliberately has no special
    /// powers: NPCs perceive the player through exactly the same sight and
    /// hearing code they use on each other.
    /// </summary>
    public sealed class PlayerAvatar
    {
        public const string PlayerId = "player";

        public string Id = PlayerId;
        public string Name = "You";

        public Vec3 Position;
        public Vec3 Facing = new Vec3(0f, 0f, 1f);

        public float WalkSpeed = 3.4f;
        public float SneakSpeed = 1.5f;

        public bool Sneaking;

        /// <summary>Object currently carried, empty if hands are free.</summary>
        public string CarryingObjectId = "";

        /// <summary>Noise generated this moment by moving. Sneaking is near-silent.</summary>
        public float MovementNoise;

        public bool IsCarrying
        {
            get { return CarryingObjectId.Length > 0; }
        }

        /// <summary>
        /// How guilty the player currently looks to anyone who glances over.
        /// Carrying someone's property in the open is the classic tell.
        /// </summary>
        public float VisibleGuilt(Simulation sim)
        {
            float guilt = 0f;
            if (IsCarrying)
            {
                SmartObject carried = sim.World.GetObject(CarryingObjectId);
                if (carried != null)
                {
                    guilt += 0.35f;
                    if (carried.OwnerId.Length > 0) guilt += 0.2f;
                }
            }
            if (Sneaking) guilt += 0.15f;
            return Mathx.Clamp01(guilt);
        }
    }

    /// <summary>One row in the player's interaction prompt.</summary>
    public sealed class InteractionOption
    {
        public SmartObject Object;
        public Affordance Affordance;
        public float Distance;

        public string Label
        {
            get { return Affordance.Verb + " " + Object.Name; }
        }

        public bool IsSabotage
        {
            get { return Affordance.IsSabotage; }
        }
    }

    /// <summary>Snapshot the UI layer renders. Keeps Unity code free of sim logic.</summary>
    public sealed class HudSnapshot
    {
        public float Time;
        public float TimeRemaining;
        public string TargetName = "";
        public float TargetAnger;
        public float TargetPeakAnger;
        public float HighestSuspicion;
        public string HighestSuspicionBy = "";
        public bool ObjectiveMet;
        public GameOutcome Outcome;
        public List<string> Feed = new List<string>();
        public List<InteractionOption> Interactions = new List<InteractionOption>();
    }
}
