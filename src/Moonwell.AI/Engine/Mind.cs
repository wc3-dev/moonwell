using System;

namespace Moonwell.AI
{
    /// <summary>What the three brain classes share, as C# lets a class inherit only one: the brain's tick, its wakes, its place in the loop and its blackboard.</summary>
    internal sealed class Mind
    {
        /// <summary>The brain, for the error reports and the AI tools.</summary>
        internal readonly Component brain;

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

        /// <summary>The step the next tick is due at, while there is a tick; internal for the cases.</summary>
        internal int TickAt;

        /// <summary>The step a wake is due at; 0 for none.</summary>
        private int wokenAt;

        /// <summary>The step the brain last thought at, so that it thinks once a step.</summary>
        private int thoughtAt;

        /// <summary>Whether a wake came before the brain started, to run once it starts.</summary>
        private bool wakeAsked;

        /// <summary>Whether the brain has ended.</summary>
        private bool ended;
#if DEBUG

        /// <summary>The run of the AI tools' log the brain's trees and choices were last noted for; noted anew at its first logged think in another.</summary>
        internal int logRun;

        /// <summary>The brain's class name in lower case, for -brain and -brainlog; null until asked.</summary>
        private string? className;
#endif

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
#if DEBUG
            loop.started.Add(this);
#endif
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

        /// <summary>Takes the tick's steps and places its first one, taking out the entry of the tick it replaces unless a wake is due at that step too.</summary>
        private void Tick(int steps)
        {
            if (every > 0 && wokenAt != TickAt)
            {
                loop!.Undue(this, TickAt);
            }

            every = steps;
            if (every > 0)
            {
                TickAt = loop!.FirstTick(every);
                loop.Due(this, TickAt);
            }
        }

        /// <summary>Has the brain think at the next step, once however many wakes come; one during a step in which the brain still waits to think from a wake is that think.</summary>
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
            if (wokenAt == at || (wokenAt == loop.Step && thoughtAt != loop.Step))
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

            var ticked = every > 0 && TickAt == step;
            if (!ticked && wokenAt != step)
            {
                return;
            }

            thoughtAt = step;
            if (ticked)
            {
                TickAt = step + every;
                loop!.Due(this, TickAt);
            }

#if DEBUG
            var logged = BrainLog.On && BrainLog.Follows(this);
            if (logged)
            {
                BrainLog.Before(this);
            }
#endif
            try
            {
                think();
            }
            catch (Exception error)
            {
                Errors.Report(brain, ".Think", error);
            }
#if DEBUG
            if (logged)
            {
                BrainLog.After(this);
            }
#endif
        }

        /// <summary>The brain ended: its blackboard ends, its tick's entry leaves its step, and the loop counts it out. A wake's entry, a step away at most, is dropped as the loop meets it.</summary>
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
                if (every > 0)
                {
                    loop.Undue(this, TickAt);
                }

#if DEBUG
                loop.started.Remove(this);
#endif
                loop.Ended();
            }
        }
#if DEBUG

        /// <summary>The brain's class name in lower case, worked out once.</summary>
        internal string ClassName()
        {
            if (className == null)
            {
                className = Names.NameOf(brain).ToLower();
            }

            return className;
        }
#endif
    }
}
