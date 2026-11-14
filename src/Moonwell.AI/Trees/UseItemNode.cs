using System;

namespace Moonwell.AI
{
    /// <summary>
    /// Has the unit use an item, with no target, at a place or on a widget, through the unit's own UseItem calls, and watches
    /// it as OrderWatch does. A potion's use reads no order right after it (probe 11-14), so the node succeeds at once. A unit
    /// whose current order id is the use of that item's slot, 852008 for the first slot (seen) and one more for each slot
    /// after (unmeasured), is not ordered again, only watched.
    /// </summary>
    internal sealed class UseItemNode : OrderWatch
    {
        /// <summary>The order id of a use of the item in the first slot; each slot after is one more.</summary>
        private const int FirstSlotUse = 852008;

        /// <summary>The slots an inventory has at most.</summary>
        private const int Slots = 6;

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

        /// <summary>Fails for a null item and a null or gone target; otherwise uses the item, unless the unit carries out a use of it already, the unit's call giving false for an item gone.</summary>
        protected override bool Give(Unit unit)
        {
            var used = item();
            if (used == null)
            {
                return false;
            }

            Widget? aim = null;
            if (target != null)
            {
                aim = target();
                if (aim == null || aim.IsGone)
                {
                    return false;
                }
            }

            if (Using(unit, used))
            {
                return true;
            }

            if (aim != null)
            {
                return unit.UseItemOn(used, aim);
            }

            if (x != null)
            {
                return unit.UseItemAt(used, x(), y!());
            }

            return unit.UseItem(used);
        }

        /// <summary>Whether the unit's current order id is the use of the item's slot; false for an item it does not carry.</summary>
        private static bool Using(Unit unit, Item used)
        {
            var current = Runtime.World.CurrentOrderOf(unit.Handle);
            for (var i = 0; i < Slots; i++)
            {
                if (unit.ItemIn(i) == used)
                {
                    return current == FirstSlotUse + i;
                }
            }

            return false;
        }
    }
}
