using System;

namespace Moonwell
{
    internal static partial class Inbound
    {
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
