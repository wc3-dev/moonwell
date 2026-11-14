namespace Moonwell.AI
{
    /// <summary>Runs its child; once the child has ended, by itself or cut off, it gives Failed at once, without starting the child, until that many seconds have passed.</summary>
    internal sealed class CooldownNode : Branch
    {
        /// <summary>The child it runs.</summary>
        private readonly Node child;

        /// <summary>The seconds it fails for after the child ended.</summary>
        private readonly float seconds;

        /// <summary>Whether the child has ended and the cooldown may still run.</summary>
        private bool cooling;

        /// <summary>The time the child ended, by Minds.Now.</summary>
        private float endedAt;

        /// <summary>A cooldown of that many seconds on a child.</summary>
        internal CooldownNode(float seconds, Node child)
            : base(new Node[] { child })
        {
            this.seconds = seconds;
            this.child = child;
        }

        /// <summary>Fails while cooling; otherwise runs the child, cooling from when it ends.</summary>
        protected override Status Run()
        {
            if (cooling)
            {
                if (!Passed(endedAt, seconds))
                {
                    return Status.Failed;
                }

                cooling = false;
            }

            var status = child.Tick();
            if (status != Status.Running)
            {
                Cool();
            }

            return status;
        }

        /// <summary>Cut off: the child is too, and the cooldown starts.</summary>
        protected override void Stop()
        {
            child.Halt();
            Cool();
        }

        /// <summary>Starts the cooldown now.</summary>
        private void Cool()
        {
            cooling = true;
            endedAt = Minds.Now();
        }
    }
}
