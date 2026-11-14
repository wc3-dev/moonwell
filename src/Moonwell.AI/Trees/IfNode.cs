using System;

namespace Moonwell.AI
{
    /// <summary>Checks a condition once, when a pass reaches it: Succeeded when it holds, Failed when not. Never watched.</summary>
    internal sealed class IfNode : Node
    {
        /// <summary>The condition, given the brain's blackboard.</summary>
        private readonly Func<Blackboard, bool> condition;

        /// <summary>An if of a condition.</summary>
        internal IfNode(Func<Blackboard, bool> condition)
        {
            this.condition = condition;
        }

        /// <summary>Checks the condition.</summary>
        protected override Status Run()
        {
            var holds = condition(Blackboard);
#if DEBUG
            lastHeld = holds;
#endif
            return holds ? Status.Succeeded : Status.Failed;
        }
    }
}
