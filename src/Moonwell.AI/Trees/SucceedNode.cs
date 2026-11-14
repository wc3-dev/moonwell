namespace Moonwell.AI
{
    /// <summary>Runs its child and gives Succeeded however it ends.</summary>
    internal sealed class SucceedNode : Branch
    {
        /// <summary>The child it runs.</summary>
        private readonly Node child;

        /// <summary>A succeed of a child.</summary>
        internal SucceedNode(Node child)
            : base(new Node[] { child })
        {
            this.child = child;
        }

        /// <summary>Runs the child; Failed becomes Succeeded.</summary>
        protected override Status Run()
        {
            return child.Tick() == Status.Running ? Status.Running : Status.Succeeded;
        }
    }
}
