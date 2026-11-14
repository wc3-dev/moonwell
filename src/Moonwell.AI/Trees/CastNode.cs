using System;

namespace Moonwell.AI
{
    /// <summary>Casts a spell by its order string, with no target, at a place or on a widget, through the unit's brain, and watches it as OrderWatch does. A unit already carrying that order string is not ordered again, only watched.</summary>
    internal sealed class CastNode : OrderWatch
    {
        /// <summary>The spell's order string.</summary>
        private readonly string order;

        /// <summary>Gives the place's x, read at the start; null for a cast with no place.</summary>
        private readonly Func<float>? x;

        /// <summary>Gives the place's y, read at the start; null for a cast with no place.</summary>
        private readonly Func<float>? y;

        /// <summary>Gives the target, read at the start; null for a cast with no target.</summary>
        private readonly Func<Widget?>? target;

        /// <summary>A cast with no target, at a place (x and y) or on a target.</summary>
        internal CastNode(string order, Func<float>? x, Func<float>? y, Func<Widget?>? target)
        {
            this.order = order;
            this.x = x;
            this.y = y;
            this.target = target;
        }

        /// <summary>Fails for a null, dead or gone target; otherwise orders the cast, unless the unit already carries it, with the brain's last order forgotten so that a second cast of the same spell is sent.</summary>
        protected override bool Give(Unit unit)
        {
            Widget? aim = null;
            if (target != null)
            {
                aim = target();
                if (aim == null || !Moonwell.Unit.Stands(aim))
                {
                    return false;
                }
            }

            if (unit.CurrentOrder == order)
            {
                return true;
            }

            tree!.unitBrain!.sentOrder = null;
            if (aim != null)
            {
                return OrderOn(order, aim);
            }

            if (x != null)
            {
                return OrderAt(order, x(), y!());
            }

            return Order(order);
        }
    }
}
