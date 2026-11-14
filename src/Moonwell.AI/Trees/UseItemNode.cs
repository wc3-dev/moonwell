using System;

namespace Moonwell.AI
{
    /// <summary>Has the unit use an item, with no target, at a place or on a widget, through the unit's own UseItem calls, and watches it as OrderWatch does.</summary>
    internal sealed class UseItemNode : OrderWatch
    {
        /// <summary>Gives the item, read at the start.</summary>
        private readonly Func<Item?> item;

        /// <summary>Gives the place's x, read at the start; null for a use with no place.</summary>
        private readonly Func<float>? x;

        /// <summary>Gives the place's y, read at the start; null for a use with no place.</summary>
        private readonly Func<float>? y;

        /// <summary>Gives the target, read at the start; null for a use with no target.</summary>
        private readonly Func<Widget?>? target;

        /// <summary>A use with no target, at a place (x and y) or on a target.</summary>
        internal UseItemNode(Func<Item?> item, Func<float>? x, Func<float>? y, Func<Widget?>? target)
        {
            this.item = item;
            this.x = x;
            this.y = y;
            this.target = target;
        }

        /// <summary>Fails for a null item or target; otherwise uses the item, the unit's call giving false for one gone.</summary>
        protected override bool Give(Unit unit)
        {
            var used = item();
            if (used == null)
            {
                return false;
            }

            if (target != null)
            {
                var aim = target();
                return aim != null && unit.UseItemOn(used, aim);
            }

            if (x != null)
            {
                return unit.UseItemAt(used, x(), y!());
            }

            return unit.UseItem(used);
        }
    }
}
