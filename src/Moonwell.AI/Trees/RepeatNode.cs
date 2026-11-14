namespace Moonwell.AI
{
    /// <summary>Runs its child again each time it succeeds, starting it anew at the next pass, never twice in one: Succeeded after that many times, Failed when the child fails.</summary>
    internal sealed class RepeatNode : Branch
    {
        /// <summary>The child it repeats.</summary>
        private readonly Node child;

        /// <summary>The times the child is to succeed; 0 or less for forever.</summary>
        private readonly int times;

        /// <summary>The times the child has succeeded since it started.</summary>
        private int count;

        /// <summary>A repeat of a child, that many times; 0 or less for forever.</summary>
        internal RepeatNode(Node child, int times)
            : base(new Node[] { child })
        {
            this.child = child;
            this.times = times;
        }

        /// <summary>The child has not succeeded yet.</summary>
        protected override void Start()
        {
            count = 0;
        }

        /// <summary>Runs the child; a success counts, and gives Running until the last.</summary>
        protected override Status Run()
        {
            var status = child.Tick();
            if (status != Status.Succeeded)
            {
                return status;
            }

            count++;
            return times > 0 && count >= times ? Status.Succeeded : Status.Running;
        }
    }
}
