namespace Moonwell.Testing
{
    /// <summary>Cases that check the 3.0 bag and equipment slots (decision 215): Equip and Unequip, their reactions, Equipped, Bag and an item's Place.</summary>
    public static class InventoryCases
    {
        private static readonly int RingId = ObjectTypes.FourCC("ebtf");
        private static readonly int Paladin = ObjectTypes.FourCC("Hpal");

        /// <summary>An item class that logs its reactions.</summary>
        private sealed class Ring : Item
        {
            public override void OnPickedUp(Unit carrierUnit)
            {
                TestLog.Lines.Add("picked " + (int)Place);
            }

            public override void OnDropped(Unit carrierUnit)
            {
                TestLog.Lines.Add("dropped");
            }

            public override void OnEquipped(Unit carrierUnit)
            {
                TestLog.Lines.Add("equipped");
            }

            public override void OnUnequipped(Unit carrierUnit)
            {
                TestLog.Lines.Add("unequipped");
            }
        }

        /// <summary>A unit component that logs equipping.</summary>
        private sealed class Wardrobe : UnitComponent
        {
            public override void OnItemEquipped(Item equippedItem)
            {
                TestLog.Lines.Add("unit equipped");
            }

            public override void OnItemUnequipped(Item unequippedItem)
            {
                TestLog.Lines.Add("unit unequipped");
            }
        }

        /// <summary>A hero with the 3.0 slots and a component that logs, and a ring of a class that logs.</summary>
        private static (Unit Hero, Item Ring) Setup(FakeWorld world)
        {
            TestLog.Lines.Clear();
            ObjectTypes.Item(RingId, () => new Ring());
            var hero = Unit.Of(world.NewUnit(Paladin, slot: 0))!;
            world.BagSizes[hero.Handle] = 30;
            hero.Add(new Wardrobe());
            return (hero, Item.Of(world.NewItem(RingId))!);
        }

        /// <summary>Equip from the ground: the pick-up, then the item's and the unit's equip reactions; Unequip: the drop, the unequip reactions, then the pick-up into the bag. Equipped, Bag and Place follow.</summary>
        public static void EquipAndUnequip(FakeWorld world)
        {
            var (hero, ring) = Setup(world);
            Expect.Equal((int)ItemPlace.Ground, (int)ring.Place, "on the ground");
            Expect.True(hero.Equip(ring), "equipped");
            Expect.Equal("picked 3,equipped,unit equipped", string.Join(",", TestLog.Lines), "the equip reactions");
            Expect.Equal((int)ItemPlace.Equipped, (int)ring.Place, "its place");
            Expect.Same(ring, hero.Equipped[0], "in Equipped");
            TestLog.Lines.Clear();
            hero.Unequip(ring);
            Expect.Equal("dropped,unequipped,unit unequipped,picked 2", string.Join(",", TestLog.Lines), "the unequip reactions");
            Expect.Equal((int)ItemPlace.Bag, (int)ring.Place, "in the bag");
            Expect.Same(ring, hero.Bag[0], "in Bag");
            Expect.Equal(0, hero.Equipped.Count, "Equipped is empty");
            TestLog.Lines.Clear();
            hero.Unequip(ring);
            Expect.Equal(0, TestLog.Lines.Count, "unequipping an item not equipped does nothing");
        }

        /// <summary>Equip from the six slots: the drop, then the pick-up into the equipment slots and the equip reactions.</summary>
        public static void EquipFromSlots(FakeWorld world)
        {
            var (hero, ring) = Setup(world);
            hero.Give(ring);
            Expect.Equal((int)ItemPlace.Inventory, (int)ring.Place, "in the six slots");
            TestLog.Lines.Clear();
            Expect.True(hero.Equip(ring), "equipped");
            Expect.Equal("dropped,picked 3,equipped,unit equipped", string.Join(",", TestLog.Lines), "the reactions");
            Expect.True(hero.ItemIn(0) == null, "out of the slot");
        }

        /// <summary>A player's equip and unequip by hand in the 3.0 panel fire only the game's equip and unequip events (probe 11-09): the same reactions run as for Equip and Unequip from the bag, once each.</summary>
        public static void ByHand(FakeWorld world)
        {
            var (hero, ring) = Setup(world);
            world.PutInBag(hero.Handle, ring.Handle);
            TestLog.Lines.Clear();
            world.EquipByHand(hero.Handle, ring.Handle);
            Expect.Equal("dropped,picked 3,equipped,unit equipped", string.Join(",", TestLog.Lines), "the equip reactions");
            Expect.Same(ring, hero.Equipped[0], "in Equipped");
            TestLog.Lines.Clear();
            world.UnequipByHand(hero.Handle, ring.Handle);
            Expect.Equal("dropped,unequipped,unit unequipped,picked 2", string.Join(",", TestLog.Lines), "the unequip reactions");
            Expect.Same(ring, hero.Bag[0], "in Bag");
            TestLog.Lines.Clear();
            Expect.True(hero.Equip(ring), "equipped by code from the bag");
            Expect.Equal("dropped,picked 3,equipped,unit equipped", string.Join(",", TestLog.Lines), "by code, the game's equip event runs nothing more");
        }

        /// <summary>A unit without the 3.0 slots equips nothing and has an empty bag.</summary>
        public static void NoSlots(FakeWorld world)
        {
            var (hero, ring) = Setup(world);
            var footman = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
            Expect.True(!footman.Equip(ring), "nothing equipped");
            Expect.Equal((int)ItemPlace.Ground, (int)ring.Place, "still on the ground");
            Expect.Equal(0, footman.Bag.Count, "no bag");
            Expect.Equal(0, TestLog.Lines.Count, "no reaction");
        }
    }
}
