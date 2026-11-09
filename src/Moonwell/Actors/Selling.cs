using System;

namespace Moonwell
{
    /// <summary>Shops (decision 214): the game's sales of items and units and its pawns, run on the shop and its components.</summary>
    internal static class Selling
    {
        /// <summary>The kinds of the game's sale events.</summary>
        internal const int ItemSold = 0, ItemPawned = 1, UnitSold = 2;

        /// <summary>
        /// A sale. For an item sold the shop is the selling unit, for a pawn the buying unit (seen 11-08). Runs only for a shop
        /// Moonwell knows (of a registered class, or holding components); the sold item or unit is taken in then.
        /// </summary>
        internal static void FromGame(int kind, unit selling, unit buying, item? soldItem, unit? soldUnit)
        {
            var shop = KnownUnits.Find(kind == ItemPawned ? buying : selling);
            if (shop == null || shop.HasEnded)
            {
                return;
            }

            var other = Unit.Of(kind == ItemPawned ? selling : buying);
            var item = kind == UnitSold ? null : Item.Of(soldItem);
            var unit = kind == UnitSold ? Unit.Of(soldUnit) : null;
            if (other == null || (item == null && unit == null))
            {
                return;
            }

            if ((shop.Mask2 & Hook2.OnResearchOrSale) != 0)
            {
                try
                {
                    Run(shop, null, kind, item, unit, other);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(shop) + "." + NameOf(kind), error);
                }
            }

            var count = shop.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = shop.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnResearchOrSale) == 0)
                {
                    continue;
                }

                try
                {
                    Run(null, component, kind, item, unit, other);
                }
                catch (Exception error)
                {
                    Errors.Report(Names.NameOf(component) + "." + NameOf(kind), error);
                }
            }

            shop.EndDispatch();
        }

        /// <summary>Runs one sale reaction on the shop or on one of its components.</summary>
        private static void Run(Unit? shop, UnitComponent? component, int kind, Item? item, Unit? unit, Unit other)
        {
            if (kind == ItemSold)
            {
                shop?.OnItemSold(item!, other);
                component?.OnItemSold(item!, other);
            }
            else if (kind == ItemPawned)
            {
                shop?.OnItemPawned(item!, other);
                component?.OnItemPawned(item!, other);
            }
            else
            {
                shop?.OnUnitSold(unit!, other);
                component?.OnUnitSold(unit!, other);
            }
        }

        private static string NameOf(int kind)
        {
            return kind == ItemSold ? "OnItemSold" : kind == ItemPawned ? "OnItemPawned" : "OnUnitSold";
        }
    }
}
