using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that hold the fake world to what the game does (docs/game-facts.md), so a case on it cannot pass on a kindness the game does not have.</summary>
    public static class FakeGameCases
    {
        /// <summary>RemoveDestructable fires the death event of a watched destructable inside the call, for one that was alive too (seen 10-23).</summary>
        public static void RemovedDestructableDies(FakeWorld world)
        {
            ObjectTypes.Destructable(ObjectTypes.FourCC("B001"), () => new TestGate());
            var handle = world.NewDestructable(ObjectTypes.FourCC("B001"));
            Destructable.Of(handle);
            TestLog.Lines.Clear();
            world.DeleteDestructable(handle);
            Expect.True(TestLog.Lines.Contains("gate died"), "the death came inside the call");
        }

        /// <summary>Damage lowers the life, by the amount the reactions left, after the damage event and before the call returns (seen 10-20).</summary>
        public static void DamageLowersLife(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("h003"), () => new LifeReader());
            var source = Unit.Of(world.NewUnit(ObjectTypes.FourCC("h003")))!;
            var handle = world.NewUnit(ObjectTypes.FourCC("h003"));
            var target = (LifeReader)Unit.Of(handle)!;
            world.SetLife(handle, 100.0f);
            source.Damage(target, 30.0f);
            Expect.Near(100.0f, target.LifeSeen, "the life in the event");
            Expect.Near(85.0f, world.LifeOf(handle), "the life after the call: 15, half of the 30, was dealt");
        }

        /// <summary>Damage that leaves 0.405 life or less kills inside the call, and the killer is the source.</summary>
        public static void DamageKills(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H002"), () => new Striker());
            var source = Unit.Of(world.NewUnit(ObjectTypes.FourCC("H002")))!;
            var handle = world.NewUnit(ObjectTypes.FourCC("H002"));
            var target = Unit.Of(handle)!;
            world.SetLife(handle, 20.0f);
            TestLog.Lines.Clear();
            source.Damage(target, 30.0f);
            Expect.True(!target.IsAlive, "the target is dead");
            Expect.Near(0.0f, world.LifeOf(handle), "with no life");
            Expect.True(TestLog.Lines.Contains("striker killed"), "the source got the kill");
        }

        /// <summary>Damage to a dead unit answers true, fires no damage event and takes no life (seen 10-31).</summary>
        public static void DamageToTheDead(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("h003"), () => new LifeReader());
            var source = Unit.Of(world.NewUnit(ObjectTypes.FourCC("h003")))!;
            var handle = world.NewUnit(ObjectTypes.FourCC("h003"));
            var target = (LifeReader)Unit.Of(handle)!;
            world.SetLife(handle, 20.0f);
            world.SlayUnit(handle);
            Expect.True(world.DealDamage(source.Handle, handle, 30.0f, false, false, 0, 14, 0), "the game answers true");
            Expect.Equal(0, target.Hits, "no damage event");
            Expect.Near(0.0f, world.LifeOf(handle), "still no life");
        }

        /// <summary>After RemoveUnit the unit reads as it was in the same frame; from the next, its owner is nil and every number 0 (seen 10-17, 10-21).</summary>
        public static void RemovedUnitReadsNothing(FakeWorld world)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 3);
            world.SetLife(handle, 100.0f);
            world.MoveUnit(handle, 50.0f, 60.0f);
            var unit = Unit.Of(handle)!;
            unit.Remove();
            Expect.Near(100.0f, world.LifeOf(handle), "the life in the same frame");
            Expect.Same(world.PlayerAt(3), world.OwnerOf(handle), "the owner in the same frame");
            world.Advance(0.05f);
            Expect.Near(0.0f, world.LifeOf(handle), "the life from the next frame");
            Expect.Near(0.0f, world.UnitX(handle), "the x from the next frame");
            Expect.True(world.OwnerOf(handle) == null, "no owner from the next frame");
        }

        /// <summary>ReviveHero gives full life, but not full mana (seen 10-21).</summary>
        public static void ReviveGivesFullLife(FakeWorld world)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true);
            var hero = (Hero)Unit.Of(handle)!;
            world.SetMaxLife(handle, 300.0f);
            world.SetMaxMana(handle, 200.0f);
            world.SetLife(handle, 120.0f);
            world.SetMana(handle, 50.0f);
            hero.Kill();
            hero.Revive(10.0f, 20.0f);
            Expect.Near(300.0f, world.LifeOf(handle), "the life after Revive");
            Expect.Near(50.0f, world.ManaOf(handle), "the mana after Revive");
        }

        /// <summary>An item given to another unit leaves its old holder, with a drop while the old holder still has it and then a pick-up, and reads the old holder's place (seen 10-31). A full unit answers false, with no event, and the old holder keeps it.</summary>
        public static void GivenItemLeavesItsHolder(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H002"), () => new TestHero());
            var first = world.NewUnit(ObjectTypes.FourCC("H002"), hero: true);
            var second = world.NewUnit(ObjectTypes.FourCC("H002"), hero: true);
            var item = world.NewItem(ObjectTypes.FourCC("I001"));
            var carrier = Unit.Of(first)!;
            var taker = Unit.Of(second)!;
            var fullHandle = world.NewUnit(ObjectTypes.FourCC("H002"), hero: true);
            var full = Unit.Of(fullHandle)!;
            world.SetInventorySize(fullHandle, 1);
            world.PutInSlot(fullHandle, 0, world.NewItem(ObjectTypes.FourCC("I001")));
            world.MoveUnit(first, 100.0f, 0.0f);
            world.MoveUnit(second, 300.0f, 0.0f);
            carrier.Give(Item.Of(item)!);
            TestLog.Lines.Clear();
            Expect.True(!full.Give(Item.Of(item)!), "a full unit does not take it");
            Expect.True(world.CarriesItem(first, item), "and the old holder keeps it");
            Expect.Equal(0, TestLog.Lines.Count, "with no event");
            taker.Give(Item.Of(item)!);
            Expect.True(!world.CarriesItem(first, item), "the old holder has it no more");
            Expect.True(world.CarriesItem(second, item), "the new holder has it");
            Expect.Equal("hero dropped orb|tally dropped orb|hero got orb|tally got orb", string.Join("|", TestLog.Lines), "a drop, then a pick-up");
            Expect.Near(100.0f, world.ItemX(item), "it reads the old holder's place");
        }
    }

    /// <summary>A unit that notes the life it has in its OnDamaged, and takes half the damage.</summary>
    public class LifeReader : Unit
    {
        /// <summary>The life in the last OnDamaged.</summary>
        public float LifeSeen;

        /// <summary>How many times OnDamaged ran.</summary>
        public int Hits;

        /// <summary>Notes the life and halves the damage.</summary>
        public override void OnDamaged(Damage damage)
        {
            Hits++;
            LifeSeen = Life;
            damage.Amount = damage.Amount / 2.0f;
        }
    }
}
