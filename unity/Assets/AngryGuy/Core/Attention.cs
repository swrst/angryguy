using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// What an NPC is currently paying attention to, and the short beat between
    /// noticing a thing and doing something about it.
    ///
    /// This is the fix for "the NPCs are really stupid". The simulation was
    /// already doing the thinking; none of it was externalised, so a player
    /// watching the screen saw a person stand still and correctly concluded
    /// there was nobody home.
    ///
    /// Two behaviours carry almost all of the perceived intelligence:
    ///
    ///   1. Nobody reacts instantly. Every reaction is Notice -> hold -> React.
    ///      The pause is what separates "it saw that" from "nothing happened".
    ///   2. Everybody visibly looks at whatever they are currently thinking
    ///      about. A head that tracks the thing you just moved is unnerving;
    ///      a head that never moves is furniture.
    ///
    /// Both are cheap. Neither makes the AI any cleverer. That is the point -
    /// F.E.A.R.'s squad AI was famously read as brilliant largely because it
    /// narrated decisions it had already made.
    /// </summary>
    public sealed class Attention
    {
        /// <summary>Where they are looking. Drives head aim in the renderer.</summary>
        public Vec3 LookAt;

        /// <summary>True while LookAt should override their facing direction.</summary>
        public bool HasLookTarget;

        /// <summary>Seconds left of the "hold on, what was that" beat.</summary>
        public float NoticeTimer;

        /// <summary>What they will do once the beat is over.</summary>
        public string PendingReason = "";
        public Vec3 PendingPoint;

        /// <summary>Set while they are mid-notice, so nothing else grabs them.</summary>
        public bool Noticing
        {
            get { return NoticeTimer > 0f; }
        }

        /// <summary>How long the look lingers after the reaction is done.</summary>
        public float LookHold;

        public void Tick(float dt)
        {
            if (NoticeTimer > 0f) NoticeTimer -= dt;

            if (LookHold > 0f)
            {
                LookHold -= dt;
                if (LookHold <= 0f) HasLookTarget = false;
            }
        }

        /// <summary>Look at something for a while without otherwise reacting.</summary>
        public void Glance(Vec3 at, float seconds)
        {
            LookAt = at;
            HasLookTarget = true;
            if (seconds > LookHold) LookHold = seconds;
        }

        public void Clear()
        {
            NoticeTimer = 0f;
            LookHold = 0f;
            HasLookTarget = false;
            PendingReason = "";
        }
    }

    /// <summary>
    /// Hands out permission for group reactions, so that six people do not all
    /// do the same thing to the same event.
    ///
    /// Without this the room reacts like a shoal of fish: a noise goes off and
    /// everybody turns, everybody walks over, everybody arrives at the same
    /// coordinate and jams. One NPC takes the ticket and investigates; the rest
    /// are told there is no ticket left and do something smaller instead -
    /// glance up, say something, carry on working. That difference in response
    /// is most of what "they behave like people" means.
    /// </summary>
    public sealed class ReactionTickets
    {
        private readonly Dictionary<string, TicketHolder> _held = new Dictionary<string, TicketHolder>();

        private struct TicketHolder
        {
            public string NpcId;
            public float ExpiresAt;
        }

        /// <summary>
        /// Claim the right to handle this kind of reaction at this place. Returns
        /// false when somebody else already has it and has not finished.
        /// </summary>
        public bool TryClaim(string kind, string npcId, float now, float holdSeconds)
        {
            string key = kind;

            TicketHolder holder;
            if (_held.TryGetValue(key, out holder))
            {
                if (holder.NpcId == npcId)
                {
                    _held[key] = new TicketHolder { NpcId = npcId, ExpiresAt = now + holdSeconds };
                    return true;
                }
                if (holder.ExpiresAt > now) return false;
            }

            _held[key] = new TicketHolder { NpcId = npcId, ExpiresAt = now + holdSeconds };
            return true;
        }

        public void Release(string kind, string npcId)
        {
            TicketHolder holder;
            if (_held.TryGetValue(kind, out holder) && holder.NpcId == npcId) _held.Remove(kind);
        }

        public bool IsHeld(string kind, float now)
        {
            TicketHolder holder;
            return _held.TryGetValue(kind, out holder) && holder.ExpiresAt > now;
        }

        public void Clear()
        {
            _held.Clear();
        }
    }
}
