namespace Moonwell.AI
{
    /// <summary>Runs its child and gives Failed when it succeeds, Succeeded when it fails.</summary>
    internal sealed class InvertNode : Branch
    {
        /// <summary>The child it inverts.</summary>
        private readonly Node child;

        /// <summary>An invert of a child.</summary>
        internal InvertNode(Node child)
            : base(new Node[] { child })
        {
            this.child = child;
        }

        /// <summary>Runs the child and swaps how it ended.</summary>
        protected override Status Run()
        {
            var status = child.Tick();
            if (status == Status.Succeeded)
            {
                return Status.Failed;
            }

            return status == Status.Failed ? Status.Succeeded : Status.Running;
        }
    }
}
