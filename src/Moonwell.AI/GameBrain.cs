namespace Moonwell.AI
{
    /// <summary>
    /// The game's brain: a component whose <c>Think</c> runs every <c>ThinkEvery</c> seconds, when woken, or both, on the
    /// package's loop of 0.02 s steps. It is added like any component (<c>Game.Instance.Add(new Director())</c>) and hears
    /// the reactions of the map as a whole. Its blackboard's chain goes on to the game's.
    /// </summary>
    public abstract class GameBrain : GameComponent
    {
        /// <summary>The brain's tick, wakes and blackboard. Internal, not private, for the reason Scope gives.</summary>
        internal readonly Mind mind;

        /// <summary>Makes the brain; it starts thinking once added.</summary>
        protected GameBrain()
        {
            mind = new Mind(this, Think);
        }

        /// <summary>The seconds between thinks, rounded to whole milliseconds, then to whole steps of 0.02 s, a half step up (a quarter second is 13 steps, 0.03 s is 2), and at least one step; 0, the default, and it thinks only when woken. Set it at any time, in OnAdded too: a change thinks within the new tick from now, then at every tick, and brains started together spread their first thinks over their tick.</summary>
        [LocalSafe]
        public float ThinkEvery
        {
            get { return mind.ThinkEvery; }
            set { mind.SetEvery(value); }
        }

        /// <summary>The brain's own blackboard, the first of its chain: then the game's. It ends with the brain.</summary>
        [LocalSafe]
        public Blackboard Blackboard => mind.Board;

        /// <summary>Has the brain think at the next step, within 0.02 s: once in that step however many wakes come, a tick due then included. A wake from inside a step goes to the step after, unless the brain still waits to think from a wake in this one.</summary>
        public void Wake()
        {
            mind.Wake();
        }

        /// <summary>The value of a fact, read from the brain's blackboard up its chain, as <c>Blackboard.Read</c>.</summary>
        public T Read<T>(Fact<T> fact)
        {
            return mind.Board.Read(fact);
        }

        /// <summary>The seconds since the fact a read would find was written or worked out, as <c>Blackboard.Age</c>; <c>float.MaxValue</c> when there is none.</summary>
        public float Age<T>(Fact<T> fact)
        {
            return mind.Board.Age(fact);
        }

        /// <summary>The brain's work, run at its tick and when woken. One that throws is reported, and the other brains still think.</summary>
        protected abstract void Think();

        /// <summary>Runs OnAdded, then starts the brain, so OnAdded can set ThinkEvery first.</summary>
        internal override void CallAdded()
        {
            try
            {
                base.CallAdded();
            }
            finally
            {
                mind.Start();
            }
        }

        /// <summary>The brain has ended: so has its blackboard, and the loop counts it out.</summary>
        internal override void Finish()
        {
            base.Finish();
            mind.End();
        }
    }
}
