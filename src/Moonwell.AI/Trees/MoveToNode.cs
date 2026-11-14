using System;

namespace Moonwell.AI
{
    /// <summary>Moves the unit to a place read at every pass, through its brain: Succeeded once within 64 of it, Failed for a dead or removed unit, a refused order, or a unit idle short of the place just after the order.</summary>
    internal sealed class MoveToNode : Node
    {
        /// <summary>Gives the place's x, read at every pass.</summary>
        private readonly Func<float> x;

        /// <summary>Gives the place's y, read at every pass.</summary>
        private readonly Func<float> y;

        /// <summary>A move to the place two functions give.</summary>
        internal MoveToNode(Func<float> x, Func<float> y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>Succeeded when there; otherwise orders the move, which the brain drops while the unit carries it.</summary>
        protected override Status Run()
        {
            var unit = Unit;
            if (unit == null || !unit.IsAlive)
            {
                return Status.Failed;
            }

            var toX = x();
            var toY = y();
            var dx = toX - unit.X;
            var dy = toY - unit.Y;
            if (dx * dx + dy * dy <= UnitBrain.Reached)
            {
                return Status.Succeeded;
            }

            // Idle short of the place just after the call: the game took nothing, or the brain dropped a jammed unit's repeat.
            if (!OrderAt(Orders.Move, toX, toY) || unit.CurrentOrder == "")
            {
                return Status.Failed;
            }

            return Status.Running;
        }
    }
}
