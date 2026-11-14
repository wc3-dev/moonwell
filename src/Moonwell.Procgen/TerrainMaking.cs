using System;

namespace Moonwell.Procgen
{
    /// <summary>
    /// Ground being made in steps over frames, from <c>TerrainGenerator.Make</c>. Read <c>Progress</c> and <c>Step</c> to draw a
    /// bar; <c>Done</c> runs once the ground is made. Every machine does the same work at each step, so the ground is the same
    /// everywhere at every moment. Ending the scope it was made in stops it, and takes back the ground made so far.
    /// </summary>
    public sealed class TerrainMaking
    {
        /// <summary>The seconds between steps: about a step a frame.</summary>
        private const float Interval = 0.02f;

        private readonly Work work;

        /// <summary>The work a step does, in microseconds as estimated.</summary>
        private readonly float budget;

        private readonly Timer timer;

        internal TerrainMaking(Work work, float budget, Scope scope)
        {
            this.work = work;
            this.budget = budget;
            timer = scope.Every(Interval, Next);
        }

        /// <summary>The share made, from 0 to 1: it never goes back, and is 1 only once the ground is made.</summary>
        [LocalSafe]
        public float Progress => work.Progress;

        /// <summary>The step being made: "Roads", "Heights", "Tiles", "Blocked", "Trees" or "Places", in that order, each only when there is work for it; the last one once the ground is made.</summary>
        [LocalSafe]
        public string Step => work.Step;

        /// <summary>Whether the ground is made.</summary>
        [LocalSafe]
        public bool IsDone { get; private set; }

        /// <summary>Runs once the ground is made, after the last step. Not if the scope ends first.</summary>
        public event Action? Done;

        private void Next()
        {
#if DEBUG
            var step = work.Step;
            var started = DebugSession.Clock();
            var finished = work.Run(budget);
            Slow(step, DebugSession.Clock() - started);
            if (!finished)
            {
                return;
            }
#else
            if (!work.Run(budget))
            {
                return;
            }
#endif

            IsDone = true;
            timer.End();
            Done?.Invoke();
        }

#if DEBUG
        /// <summary>A step longer than this, in real seconds, is reported once a making (decision 344).</summary>
        private const float SlowStep = 1f;

        private bool reportedSlow;

        /// <summary>Reports, in a development build, the first step that took longer than a second of real time: a making that seems to have stopped.</summary>
        private void Slow(string step, float seconds)
        {
            if (reportedSlow || !(seconds > SlowStep))
            {
                return;
            }

            reportedSlow = true;
            var craters = work.Craters != null ? work.Craters() : 0;
            Dev.Report("TerrainGenerator.Make", "a step of \"" + step + "\" took " + DebugPanel.Tenths(seconds) + " s of real time, with " + DebugPanel.Count(craters) + " craters made; the game makes each crater slower the more it holds, so make less at once, or grow the world (Grow)");
        }
#endif
    }
}
