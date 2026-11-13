using System;

namespace Moonwell.AI
{
    /// <summary>Runs its child while a condition holds, checked at every pass: false at its start, it gives Failed without starting the child; false while running, it halts the child and gives Failed. A Select watches it (see SelectNode).</summary>
    internal sealed class WhenNode : Branch
    {
        /// <summary>The condition, given the brain's blackboard.</summary>
        private readonly Func<Blackboard, bool> condition;

        /// <summary>The child it runs.</summary>
        private readonly Node child;

        /// <summary>A when of a condition and a child.</summary>
        internal WhenNode(Func<Blackboard, bool> condition, Node child)
            : base(new Node[] { child })
        {
            this.condition = condition;
            this.child = child;
        }

        /// <summary>Whether the condition holds now.</summary>
        internal bool Holds()
        {
            return condition(Blackboard);
        }

        /// <summary>Runs the child while the condition holds; otherwise halts it and fails.</summary>
        protected override Status Run()
        {
            if (!Holds())
            {
                child.Halt();
                return Status.Failed;
            }

            return child.Tick();
        }
    }
}
