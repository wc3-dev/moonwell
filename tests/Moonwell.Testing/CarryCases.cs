using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A unit component an item gives its carrier, which logs its adding and removal.</summary>
    public sealed class Haste : UnitComponent
    {
        /// <summary>Logs "haste added".</summary>
        protected override void OnAdded()
        {
            TestLog.Lines.Add("haste added");
        }

        /// <summary>Logs "haste removed".</summary>
        protected override void OnRemoved()
        {
            TestLog.Lines.Add("haste removed");
        }
    }

    /// <summary>An item component that gives its carrier a Haste held by the item's carry, and logs whether the carry is live at the drop.</summary>
    public sealed class Swift : ItemComponent
    {
        /// <summary>The carry read at the last pick-up.</summary>
        public Scope? Seen;

        /// <summary>Gives the carrier a Haste held by the carry.</summary>
        public override void OnPickedUp(Unit carrierUnit)
        {
            Seen = Item.Carry;
            carrierUnit.Add(new Haste(), heldBy: Seen);
        }

        /// <summary>Logs "swift dropped" with whether the carry is still live.</summary>
        public override void OnDropped(Unit carrierUnit)
        {
            TestLog.Lines.Add("swift dropped, carry " + (Item.Carry.HasEnded ? "ended" : "live"));
        }
    }

    /// <summary>An item component that gives its item to another unit inside the drop's reactions, once.</summary>
    public sealed class PassOn : ItemComponent
    {
        /// <summary>The unit to give the item to at the next drop; null once given.</summary>
        public Unit? To;

        /// <summary>Gives the item to To, once.</summary>
        public override void OnDropped(Unit carrierUnit)
        {
            var to = To;
            To = null;
            to?.Give(Item);
        }
    }

    /// <summary>Cases for an item's carry: what an item gives its carrier with heldBy: Item.Carry ends at the drop (decision 166).</summary>
    public static class CarryCases
    {
        /// <summary>A pair of boots with a Swift on it, on the ground.</summary>
        private static Item NewBoots()
        {
            if (ObjectTypes.ItemMaker(ObjectTypes.FourCC("I001")) == null)
            {
                ObjectTypes.Item(ObjectTypes.FourCC("I001"), () => new DropLog("boots"));
            }

            var boots = Item.Create(ObjectTypes.FourCC("I001"), 0.0f, 0.0f, Game.Instance);
            boots.Add(new Swift());
            return boots;
        }

        /// <summary>A footman, whose inventory drops its items at death.</summary>
        private static Unit NewUnit(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
        }

        /// <summary>The index of the first log line that equals a line, or -1.</summary>
        private static int IndexOf(string line)
        {
            return TestLog.Lines.IndexOf(line);
        }

        /// <summary>Boots picked up give their Haste; dropped, the Haste ends after the drop's reactions, which still see the carry live.</summary>
        public static void Gift(FakeWorld world)
        {
            var unit = NewUnit(world);
            var boots = NewBoots();
            unit.Give(boots);
            Expect.True(unit.Has<Haste>(), "the carrier has the Haste");
            TestLog.Lines.Clear();
            unit.Drop(boots);
            Expect.True(!unit.Has<Haste>(), "the Haste is gone after the drop");
            Expect.True(IndexOf("swift dropped, carry live") >= 0, "OnDropped saw the carry live");
            Expect.True(IndexOf("haste removed") > IndexOf("swift dropped, carry live"), "the Haste ended after the drop's reactions");
        }

        /// <summary>Two boots of one kind give two Haste components; dropping one leaves the other's.</summary>
        public static void TwoBoots(FakeWorld world)
        {
            var unit = NewUnit(world);
            var first = NewBoots();
            var second = NewBoots();
            unit.Give(first);
            unit.Give(second);
            Expect.Equal(2, unit.GetAll<Haste>().Count, "two Haste components");
            unit.Drop(first);
            Expect.Equal(1, unit.GetAll<Haste>().Count, "one left after a drop");
        }

        /// <summary>Boots put in the 3.0 bag give nothing; equipped from there, they give their Haste again.</summary>
        public static void BagThenEquip(FakeWorld world)
        {
            var unit = NewUnit(world);
            var boots = NewBoots();
            unit.Give(boots);
            world.MoveToBag(unit.Handle, boots.Handle);
            Expect.True(!unit.Has<Haste>(), "no Haste while the boots are in the bag");
            world.Equip(unit.Handle, boots.Handle);
            Expect.True(unit.Has<Haste>(), "the Haste is back once equipped");
        }

        /// <summary>A footman that dies drops its boots before its death, so the Haste has ended before its OnDeath.</summary>
        public static void Death(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("U001"), () => new Dummy());
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("U001")))!;
            unit.Give(NewBoots());
            TestLog.Lines.Clear();
            unit.Kill();
            Expect.True(IndexOf("haste removed") >= 0, "the Haste ended");
            Expect.True(IndexOf("haste removed") < IndexOf("dummy died"), "before the unit's OnDeath");
        }

        /// <summary>Boots removed from the game while carried end their carry, and the Haste with it; the carrier stays.</summary>
        public static void ItemGone(FakeWorld world)
        {
            var unit = NewUnit(world);
            var boots = NewBoots();
            unit.Give(boots);
            boots.Remove();
            Expect.True(!unit.Has<Haste>(), "the Haste ended with the boots");
            Expect.True(!unit.HasEnded, "the carrier stays");
        }

        /// <summary>A carrier removed from the game ends the carry.</summary>
        public static void CarrierGone(FakeWorld world)
        {
            var unit = NewUnit(world);
            var boots = NewBoots();
            unit.Give(boots);
            var swift = boots.Get<Swift>()!;
            unit.Remove();
            Expect.True(swift.Seen!.HasEnded, "the carry ended with its carrier");
        }

        /// <summary>The game fires a drop when the player moves an item between slots (seen 10-15): the item is still carried, so what its carry holds stays, after the moment too, and goes at the real drop.</summary>
        public static void SlotMove(FakeWorld world)
        {
            var unit = NewUnit(world);
            var boots = NewBoots();
            unit.Give(boots);
            var swift = boots.Get<Swift>()!;
            TestLog.Lines.Clear();
            world.MoveItemSlot(unit.Handle, boots.Handle, 3);
            Expect.True(IndexOf("swift dropped, carry live") >= 0, "the game fired a drop for the move");
            world.Advance(0.1f);
            Expect.True(unit.Has<Haste>(), "the Haste stays after a slot move");
            Expect.True(!swift.Seen!.HasEnded, "the carry stays");
            unit.Drop(boots);
            Expect.True(!unit.Has<Haste>(), "the Haste is gone at the real drop");
        }

        /// <summary>A drop the game makes itself, as a player's, comes while the unit still has the item; its carry ends a moment later, once the game has taken the item out.</summary>
        public static void GameDrop(FakeWorld world)
        {
            var unit = NewUnit(world);
            var boots = NewBoots();
            unit.Give(boots);
            TestLog.Lines.Clear();
            world.DropItem(unit.Handle, boots.Handle);
            Expect.True(IndexOf("swift dropped, carry live") >= 0, "OnDropped saw the carry live");
            world.Advance(0.1f);
            Expect.True(!unit.Has<Haste>(), "the Haste is gone a moment after the drop");
        }

        /// <summary>Boots given to another unit inside the drop's reactions: the first unit's Haste ends, and the new holder's stays.</summary>
        public static void GivenOnInDrop(FakeWorld world)
        {
            var first = NewUnit(world);
            var second = NewUnit(world);
            var boots = NewBoots();
            var pass = new PassOn();
            boots.Add(pass);
            first.Give(boots);
            pass.To = second;
            first.Drop(boots);
            world.Advance(0.1f);
            Expect.Same(second, boots.Carrier, "the second unit carries the boots");
            Expect.True(!first.Has<Haste>(), "the first unit's Haste ended");
            Expect.True(second.Has<Haste>(), "the second unit keeps its Haste");
            Expect.True(!boots.Get<Swift>()!.Seen!.HasEnded, "the second unit's carry is live");
        }

        /// <summary>A carry read in the bag gives a scope that has ended, and what it would hold is not added, with no report.</summary>
        public static void BagIsQuiet(FakeWorld world)
        {
            var unit = NewUnit(world);
            var boots = NewBoots();
            var before = world.Printed.Count;
            world.PutInBag(unit.Handle, boots.Handle);
            Expect.True(!unit.Has<Haste>(), "no Haste from the bag");
            Expect.True(boots.Carry.HasEnded, "the carry read in the bag has ended");
            Expect.Equal(before, world.Printed.Count, "nothing reported");
        }

#if DEBUG
        /// <summary>A carry read on an item lying on the ground is reported in a development build, and gives a scope that has ended.</summary>
        public static void NotCarriedReported(FakeWorld world)
        {
            var boots = NewBoots();
            var before = world.Printed.Count;
            var carry = boots.Carry;
            Expect.True(carry.HasEnded, "the scope given has ended");
            Expect.Equal(before + 1, world.Printed.Count, "the lines printed");
            Expect.True(world.Printed[before].Contains("not carried"), "the report says why: " + world.Printed[before]);
        }
#endif
    }
}
