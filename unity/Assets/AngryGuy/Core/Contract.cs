using System;
using System.Collections.Generic;

namespace AngryGuy.Core
{
    /// <summary>
    /// One thing the player is trying to cause.
    ///
    /// The wording rule is load-bearing, not style: every job names an OUTCOME
    /// and never a method. "Make Pemberton look under the table" states a goal
    /// while concealing every route to it. "Put the pen in the bin" is a
    /// solution, and writing a solution down deletes the puzzle.
    ///
    /// Untitled Goose Game's whole list works on that grammar. Every job in this
    /// game begins with Make, Get or Have, and names a person and an observable
    /// act.
    /// </summary>
    public sealed class Job
    {
        public string Id = "";

        /// <summary>Shown to the player, verbatim. Starts with Make / Get / Have.</summary>
        public string Text = "";

        /// <summary>
        /// Hidden jobs appear in the list only as an asterisk. The player can see
        /// how many secrets a level holds without being told what they are, which
        /// advertises depth for the price of one string.
        /// </summary>
        public bool Hidden;

        /// <summary>The contract itself: the level is not finished without it.</summary>
        public bool Required;

        /// <summary>What completing it pays toward the next tool.</summary>
        public int Reward = 1;

        public bool Done;

        /// <summary>When it completed, for the end-of-level summary ordering.</summary>
        public float DoneAt = -1f;

        /// <summary>Checked every tick. Returns true once, and then it is done.</summary>
        public Func<Simulation, bool> IsSatisfied;

        /// <summary>What the player sees in the list right now.</summary>
        public string Display
        {
            get
            {
                if (Done) return Text;
                return Hidden ? "✱ ✱ ✱" : Text;
            }
        }
    }

    /// <summary>
    /// A level's brief: who the client is, who the target is, and what counts
    /// as done. One contract, a handful of optional jobs, and one or two hidden
    /// ones.
    /// </summary>
    public sealed class Contract
    {
        public string Id = "";
        public string Title = "";

        /// <summary>Who called it in. Answering-machine flavour, one or two lines.</summary>
        public string Client = "";

        /// <summary>The brief, in the client's own words.</summary>
        public string Brief = "";

        public string TargetNpcId = "";

        /// <summary>Seconds on the clock. Zero means no deadline.</summary>
        public float TimeLimit;

        public readonly List<Job> Jobs = new List<Job>();

        public Contract Add(Job job)
        {
            Jobs.Add(job);
            return this;
        }

        public Job Find(string id)
        {
            for (int i = 0; i < Jobs.Count; i++)
            {
                if (Jobs[i].Id == id) return Jobs[i];
            }
            return null;
        }

        public Job Required
        {
            get
            {
                for (int i = 0; i < Jobs.Count; i++)
                {
                    if (Jobs[i].Required) return Jobs[i];
                }
                return null;
            }
        }

        public bool ContractDone
        {
            get
            {
                Job required = Required;
                return required != null && required.Done;
            }
        }

        public int DoneCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Jobs.Count; i++)
                {
                    if (Jobs[i].Done) n++;
                }
                return n;
            }
        }

        public int HiddenCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Jobs.Count; i++)
                {
                    if (Jobs[i].Hidden) n++;
                }
                return n;
            }
        }
    }

    /// <summary>
    /// What the player carries between attempts at the same level.
    ///
    /// Being caught restarts the level, and that restart has to cost almost
    /// nothing or the failure state becomes a punishment rather than a rewind.
    /// So knowledge survives: every job you have already worked out stays worked
    /// out, every hidden job you uncovered stays uncovered, and money you banked
    /// stays banked. What you lose is the run, not the progress.
    /// </summary>
    public sealed class LevelProgress
    {
        public readonly HashSet<string> CompletedJobs = new HashSet<string>();
        public readonly HashSet<string> RevealedHidden = new HashSet<string>();
        public int Banked;
        public int Attempts;

        public void Remember(Job job)
        {
            CompletedJobs.Add(job.Id);
            if (job.Hidden) RevealedHidden.Add(job.Id);
        }

        /// <summary>Re-apply what the player already knows to a freshly built level.</summary>
        public void Restore(Contract contract)
        {
            if (contract == null) return;

            for (int i = 0; i < contract.Jobs.Count; i++)
            {
                Job job = contract.Jobs[i];

                // The contract itself always has to be earned again - it is the
                // level. Everything else the player has already proved they can do.
                if (job.Required) continue;

                if (CompletedJobs.Contains(job.Id))
                {
                    job.Done = true;
                    job.DoneAt = 0f;
                }
            }
        }

        /// <summary>A hidden job the player has seen before shows its text even before doing it again.</summary>
        public bool KnowsAbout(Job job)
        {
            return RevealedHidden.Contains(job.Id);
        }
    }
}
