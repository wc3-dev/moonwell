using System;

namespace Moonwell
{
    internal static partial class Inbound
    {
        /// <summary>
        /// The game's equip event. A move by code fires a drop and a pick-up before it, which ran the reactions already; one by the
        /// player's hand in the 3.0 panel fires only this (probe 11-09), so Moonwell runs the drop and the pick-up a move by code
        /// would have, and with them OnEquipped (I54). Their reactions run after the move, so the item's Place already reads Equipped.
        /// </summary>
        internal static void EquipEvent(unit u, item i)
        {
            var known = KnownItems.Find(i);
            if (known != null && known.equippedOn != null && known.equippedOn == KnownUnits.Find(u))
            {
                return;
            }

            Dropped(u, i);
            PickedUp(u, i);
        }

        /// <summary>
        /// The game's unequip event, which names no item. A move by code fires a drop and a pick-up before it, and the drop ran
        /// OnUnequipped; one by hand fires only this (probe 11-09), and the item is in the bag, still marked as equipped on the unit:
        /// Moonwell runs the drop and the pick-up a move by code would have, and with them OnUnequipped (I54).
        /// </summary>
        internal static void UnequipEvent(unit u)
        {
            var by = KnownUnits.Find(u);
            if (by == null)
            {
                return;
            }

            var size = Runtime.World.BagSizeOf(u);
            for (var slot = 0; slot < size; slot++)
            {
                var i = Runtime.World.ItemInBagSlot(u, slot);
                var known = i != null ? KnownItems.Find(i) : null;
                if (known != null && known.equippedOn == by)
                {
                    Dropped(u, i!);
                    PickedUp(u, i!);
                }
            }
        }

        /// <summary>
        /// An item goes into or comes out of a unit's 3.0 equipment slots (decision 215): the item's OnEquipped or OnUnequipped and
        /// its components', then the unit's OnItemEquipped or OnItemUnequipped and its components'. Moonwell tells it from the
        /// pick-up and drop events the game fires around each (seen 11-08), on the bits of those reactions.
        /// </summary>
        private static void Equipment(Item item, Unit by, bool on)
        {
            var name = on ? ".OnEquipped" : ".OnUnequipped";
            var itemBit = on ? Hook.OnPickedUp : Hook.OnDropped;
            if (!item.HasEnded && (item.Mask & itemBit) != 0)
            {
                try
                {
                    if (on)
                    {
                        item.OnEquipped(by);
                    }
                    else
                    {
                        item.OnUnequipped(by);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(item) + name, error);
                }
            }

            var count = item.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = item.ComponentAt(i) as ItemComponent;
                if (component == null || component.HasEnded || (component.Mask & itemBit) == 0)
                {
                    continue;
                }

                try
                {
                    if (on)
                    {
                        component.OnEquipped(by);
                    }
                    else
                    {
                        component.OnUnequipped(by);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + name, error);
                }
            }

            item.EndDispatch();
            if (by.ending)
            {
                return;
            }

            var unitName = on ? ".OnItemEquipped" : ".OnItemUnequipped";
            var unitBit = on ? Hook.OnItemPickedUp : Hook.OnItemDropped;
            if (!by.HasEnded && (by.Mask & unitBit) != 0)
            {
                try
                {
                    if (on)
                    {
                        by.OnItemEquipped(item);
                    }
                    else
                    {
                        by.OnItemUnequipped(item);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(by) + unitName, error);
                }
            }

            count = by.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = by.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & unitBit) == 0)
                {
                    continue;
                }

                try
                {
                    if (on)
                    {
                        component.OnItemEquipped(item);
                    }
                    else
                    {
                        component.OnItemUnequipped(item);
                    }
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + unitName, error);
                }
            }

            by.EndDispatch();
        }
    }
}
