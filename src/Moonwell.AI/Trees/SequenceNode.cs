namespace Moonwell.AI
{
    /// <summary>Runs its children in order, going on at the next pass where it left off: Failed at the first that fails, Succeeded once all have succeeded.</summary>
    internal sealed class SequenceNode : Branch
    {
        /// <summary>The child it is at.</summary>
        private int at;

        /// <summary>A sequence of these children.</summary>
        internal SequenceNode(Node[] children)
            : base(children)
        {
        }

        /// <summary>Starts at the first child.</summary>
        protected override void Start()
        {
            at = 0;
        }

        /// <summary>Runs the child it is at, and the next ones in the same pass while they succeed.</summary>
        protected override Status Run()
        {
            while (at < children.Length)
            {
                var status = children[at].Tick();
                if (status != Status.Succeeded)
                {
                    return status;
                }

                at++;
            }

            return Status.Succeeded;
        }
    }
}
