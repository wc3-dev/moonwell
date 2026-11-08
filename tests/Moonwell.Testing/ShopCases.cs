using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check shops (decision 214): the sale reactions on a shop and its components, and the stock put on sale by code.</summary>
    public static class ShopCases
    {
        private static readonly int Merchant = ObjectTypes.FourCC("n000");
        private static readonly int Potion = ObjectTypes.FourCC("pman");
        private static readonly int Paladin = ObjectTypes.FourCC("Hpal");

        /// <summary>A shop class that logs its sale reactions.</summary>
        private sealed class Goblin : Unit
        {
            public override void OnItemSold(Item item, Unit buyer)
            {
                TestLog.Lines.Add("sold " + Names.IdText(item.TypeId) + " to " + Names.IdText(buyer.TypeId));
            }

            public override void OnItemPawned(Item item, Unit seller)
            {
                TestLog.Lines.Add("pawned " + Names.IdText(item.TypeId) + " by " + Names.IdText(seller.TypeId));
            }

            public override void OnUnitSold(Unit soldUnit, Unit buyer)
            {
                TestLog.Lines.Add("sold unit " + Names.IdText(soldUnit.TypeId));
            }
        }

        /// <summary>A unit component that logs a sale.</summary>
        private sealed class Till : UnitComponent
        {
            public override void OnItemSold(Item item, Unit buyer)
            {
                TestLog.Lines.Add("till");
            }
        }

        /// <summary>An item class for the stock.</summary>
        private sealed class ManaPotion : Item
        {
        }

        /// <summary>The game's sale, pawn and unit sale run the shop's reactions, then its components'; the shop is the seller of an item and the buyer of a pawn. A shop class listens for sales and research, one bit.</summary>
        public static void SaleReactions(FakeWorld world)
        {
            TestLog.Lines.Clear();
            ObjectTypes.Unit(Merchant, () => new Goblin());
            Expect.True(world.Listening.Contains(GameEvent.Sale), "listens for sales");
            Expect.True(world.Listening.Contains(GameEvent.Research), "and research, on the same bit");
            var shop = Unit.Of(world.NewUnit(Merchant, slot: 27))!;
            shop.Add(new Till());
            var hero = world.NewUnit(Paladin, slot: 0);
            Selling.FromGame(Selling.ItemSold, shop.Handle, hero, world.NewItem(Potion), null);
            Selling.FromGame(Selling.ItemPawned, hero, shop.Handle, world.NewItem(Potion), null);
            Selling.FromGame(Selling.UnitSold, shop.Handle, hero, null, world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0));
            Expect.Equal("sold pman to Hpal,till,pawned pman by Hpal,sold unit hfoo", string.Join(",", TestLog.Lines), "the reactions");
        }

        /// <summary>A sale at a shop Moonwell does not know runs nothing and takes no item in.</summary>
        public static void UnknownShop(FakeWorld world)
        {
            TestLog.Lines.Clear();
            var shop = world.NewUnit(ObjectTypes.FourCC("ngme"), slot: 27);
            var potion = world.NewItem(Potion);
            Selling.FromGame(Selling.ItemSold, shop, world.NewUnit(Paladin, slot: 0), potion, null);
            Expect.True(KnownItems.Find(potion) == null, "the item is not taken in");
            Expect.Equal(0, TestLog.Lines.Count, "nothing ran");
        }

        /// <summary>AddStock puts an item class on sale with its count and most, RemoveStock takes it off; a shop without Sell Items is reported in a development build.</summary>
        public static void Stock(FakeWorld world)
        {
            ObjectTypes.Item(Potion, () => new ManaPotion());
            var market = Unit.Of(world.NewUnit(ObjectTypes.FourCC("nmrk"), slot: 27))!;
            world.SetAbilityLevel(market.Handle, ObjectTypes.FourCC("Asid"), 1);
            var before = world.Printed.Count;
            market.AddStock<ManaPotion>(1, 3);
            Expect.Equal("1 3", world.Stock[market.Handle][Potion][0] + " " + world.Stock[market.Handle][Potion][1], "the count and the most");
            Expect.Equal(before, world.Printed.Count, "a shop with Sell Items is not reported");
            market.RemoveStock<ManaPotion>();
            Expect.True(!world.Stock[market.Handle].ContainsKey(Potion), "taken off sale");
            var merchant = Unit.Of(world.NewUnit(ObjectTypes.FourCC("ngme"), slot: 27))!;
            merchant.AddStock<ManaPotion>(1, 1);
#if DEBUG
            Expect.True(world.Printed.Count > before, "a shop without Sell Items is reported in a development build");
#else
            Expect.Equal(before, world.Printed.Count, "a release build reports nothing");
#endif
        }
    }
}
