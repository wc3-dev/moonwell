using System;

namespace Moonwell.AI
{
    /// <summary>Casts a spell by its order string, with no target, at a place or on a widget, through the unit's brain, and watches it as OrderWatch does. A unit still carrying out the cast as the brain last gave it, on the same target or at a place within 32, is not ordered again, only watched.</summary>
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

        /// <summary>Fails for a null, dead or gone target; otherwise orders the cast, unless the unit still carries it out as the brain last gave it, with the brain's last order forgotten so that a second cast of the same spell is sent.</summary>
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

            var toX = x != null ? x() : 0f;
            var toY = y != null ? y() : 0f;
            var kind = aim != null ? UnitBrain.OnWidget : x != null ? UnitBrain.AtPlace : UnitBrain.Plain;
            var brain = tree!.unitBrain!;
            var current = unit.CurrentOrder;
            if (current != "" && current == brain.sentCurrent && brain.Repeats(order, kind, toX, toY, aim))
            {
                return true;
            }

            brain.sentOrder = null;
            if (aim != null)
            {
                return OrderOn(order, aim);
            }

            if (x != null)
            {
                return OrderAt(order, toX, toY);
            }

            return Order(order);
        }
    }
}
