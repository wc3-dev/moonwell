using System;

namespace Moonwell.AI
{
    /// <summary>What the three brain classes share, as C# lets a class inherit only one: the brain's tick, its wakes, its place in the loop and its blackboard.</summary>
    internal sealed class Mind
    {
        /// <summary>The brain, for the error reports.</summary>
        private readonly Component brain;

        /// <summary>The brain's Think.</summary>
        private readonly Action think;

        /// <summary>The brain's own blackboard.</summary>
        internal readonly Blackboard Board;

        /// <summary>ThinkEvery as set.</summary>
        internal float ThinkEvery;

        /// <summary>The loop the brain was started on; null until it starts.</summary>
        private Minds? loop;

        /// <summary>The steps of the tick; 0 for none.</summary>
        private int every;

        /// <summary>The step the next tick is due at, while there is a tick.</summary>
        private int tickAt;

        /// <summary>The step a wake is due at; 0 for none.</summary>
        private int wokenAt;

        /// <summary>The step the brain last thought at, so that it thinks once a step.</summary>
        private int thoughtAt;

        /// <summary>Whether a wake came before the brain started, to run once it starts.</summary>
        private bool wakeAsked;

        /// <summary>Whether the brain has ended.</summary>
        private bool ended;

        internal Mind(Component brain, Action think)
        {
            this.brain = brain;
            this.think = think;
            Board = new Blackboard(Blackboard.BrainKind, null);
        }

        /// <summary>Starts the brain on the loop, after its OnAdded: its first tick is spread over the tick, and a wake that came before runs now. A brain that ended in OnAdded does not start.</summary>
        internal void Start()
        {
            if (loop != null || ended || brain.ending || brain.HasEnded)
            {
                return;
            }

            loop = Minds.Current();
            loop.Started();
            Tick(Minds.StepsOf(ThinkEvery));
            if (wakeAsked)
            {
                Wake();
            }
        }

        /// <summary>Sets ThinkEvery; on a started brain a change of its steps places its next tick from now, spread as a first tick is.</summary>
        internal void SetEvery(float seconds)
        {
            ThinkEvery = seconds;
            if (loop == null || ended)
            {
                return;
            }

            var steps = Minds.StepsOf(seconds);
            if (steps != every)
            {
                Tick(steps);
            }
        }

        /// <summary>Takes the tick's steps and places its first one.</summary>
        private void Tick(int steps)
        {
            every = steps;
            if (every > 0)
            {
                tickAt = loop!.FirstTick(every);
                loop.Due(this, tickAt);
            }
        }

        /// <summary>Has the brain think at the next step, once however many wakes come.</summary>
        internal void Wake()
        {
            if (ended)
            {
                return;
            }

            if (loop == null)
            {
                wakeAsked = true;
                return;
            }

            var at = loop.Step + 1;
            if (wokenAt == at)
            {
                return;
            }

            wokenAt = at;
            loop.Due(this, at);
        }

        /// <summary>The loop meets the brain at a step: it thinks unless it has ended, has thought at this step already, or the entry is stale; a tick places the next one first, so a Think that changes ThinkEvery takes over from it.</summary>
        internal void ThinkAt(int step)
        {
            if (ended || thoughtAt == step)
            {
                return;
            }

            var ticked = every > 0 && tickAt == step;
            if (!ticked && wokenAt != step)
            {
                return;
            }

            thoughtAt = step;
            if (ticked)
            {
                tickAt = step + every;
                loop!.Due(this, tickAt);
            }

            try
            {
                think();
            }
            catch (Exception error)
            {
                Errors.Report(brain, ".Think", error);
            }
        }

        /// <summary>The brain ended: its blackboard ends, and the loop counts it out. Its entries are dropped as the loop meets them.</summary>
        internal void End()
        {
            if (ended)
            {
                return;
            }

            ended = true;
            Board.End();
            if (loop != null)
            {
                loop.Ended();
            }
        }
    }
}
