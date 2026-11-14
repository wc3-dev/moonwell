using System;

namespace Moonwell.AI
{
    /// <summary>Attacks a target read at every pass, through the unit's brain, following it when it changes: Succeeded once it is dead or gone after the first pass; Failed for a null target, one dead or gone at the first pass, a dead or removed unit, a refused order, or a unit found idle with the same target alive, as one that lost sight of it or can't reach it.</summary>
    internal sealed class AttackNode : Node
    {
        /// <summary>The target, given the brain's blackboard.</summary>
        private readonly Func<Blackboard, Unit?> target;

        /// <summary>The target ordered at the last pass; null at the node's first pass.</summary>
        private Unit? attacked;

        /// <summary>An attack on the target a function gives.</summary>
        internal AttackNode(Func<Blackboard, Unit?> target)
        {
            this.target = target;
        }

        /// <summary>Starts with no target ordered.</summary>
        protected override void Start()
        {
            attacked = null;
        }

        /// <summary>Checks the target and the unit, then orders the attack, which the brain drops while the unit carries it.</summary>
        protected override Status Run()
        {
            var unit = Unit;
            if (unit == null || !unit.IsAlive)
            {
                return Status.Failed;
            }

            var aim = target(Blackboard);
            if (aim == null)
            {
                return Status.Failed;
            }

            if (!aim.IsAlive)
            {
                return attacked == null ? Status.Failed : Status.Succeeded;
            }

            if (aim == attacked && unit.CurrentOrder == "")
            {
                return Status.Failed;
            }

            if (!OrderOn(Orders.Attack, aim))
            {
                return Status.Failed;
            }

            attacked = aim;
            return Status.Running;
        }
    }
}
