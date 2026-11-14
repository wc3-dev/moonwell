using System;
using System.Collections.Generic;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>A unit brain that notes the loop's step at each think, then runs the action a case gives it.</summary>
    public sealed class StepBrain : UnitBrain
    {
        /// <summary>The step of each think, in order.</summary>
        public List<int> Thought = new List<int>();

        /// <summary>Run in Think after the step is noted; null for nothing.</summary>
        public Action? During;

        protected override void Think()
        {
            Thought.Add(Minds.Current().Step);
            During?.Invoke();
        }
    }

    /// <summary>A player brain that thinks every step, set in its OnAdded, and notes the step of each think.</summary>
    public sealed class StepPlayerBrain : PlayerBrain
    {
        /// <summary>The step of each think, in order.</summary>
        public List<int> Thought = new List<int>();

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            Thought.Add(Minds.Current().Step);
        }
    }

    /// <summary>A game brain that notes the step of each think, and its label in a shared log, then runs the action a case gives it.</summary>
    public sealed class StepGameBrain : GameBrain
    {
        /// <summary>The step of each think, in order.</summary>
        public List<int> Thought = new List<int>();

        /// <summary>The name it writes in Log.</summary>
        public string Label = "";

        /// <summary>A log several brains share; null for none.</summary>
        public List<string>? Log;

        /// <summary>Run in Think after the step is noted; null for nothing.</summary>
        public Action? During;

        protected override void Think()
        {
            Thought.Add(Minds.Current().Step);
            Log?.Add(Label);
            During?.Invoke();
        }
    }

    /// <summary>A game brain whose Think throws "boom" after counting.</summary>
    public sealed class ThrowingBrain : GameBrain
    {
        /// <summary>How many times it thought.</summary>
        public int Runs;

        protected override void Think()
        {
            Runs++;
            throw new InvalidOperationException("boom");
        }
    }
}
