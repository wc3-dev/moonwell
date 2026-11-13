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

        /// <summary>The step being made: "Heights", "Tiles" or "Blocked"; the last one once the ground is made.</summary>
        [LocalSafe]
        public string Step => work.Step;

        /// <summary>Whether the ground is made.</summary>
        [LocalSafe]
        public bool IsDone { get; private set; }

        /// <summary>Runs once the ground is made, after the last step. Not if the scope ends first.</summary>
        public event Action? Done;

        private void Next()
        {
            if (!work.Run(budget))
            {
                return;
            }

            IsDone = true;
            timer.End();
            Done?.Invoke();
        }
    }
}
