namespace Moonwell.AI
{
    /// <summary>Runs its child; once it has run that many seconds, it halts the child and gives Failed, without running the child at that pass.</summary>
    internal sealed class TimeoutNode : Branch
    {
        /// <summary>The child it runs.</summary>
        private readonly Node child;

        /// <summary>The seconds the child may run.</summary>
        private readonly float seconds;

        /// <summary>The time it started, by Minds.Now.</summary>
        private float startedAt;

        /// <summary>A timeout of that many seconds on a child.</summary>
        internal TimeoutNode(float seconds, Node child)
            : base(new Node[] { child })
        {
            this.seconds = seconds;
            this.child = child;
        }

        /// <summary>Notes the time it started.</summary>
        protected override void Start()
        {
            startedAt = Minds.Now();
        }

        /// <summary>Runs the child until the time is up; then halts it and fails.</summary>
        protected override Status Run()
        {
            if (Passed(startedAt, seconds))
            {
                child.Halt();
                return Status.Failed;
            }

            return child.Tick();
        }
    }
}
