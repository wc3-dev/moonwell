using System;

namespace Moonwell.Testing
{
    /// <summary>A hero that drops a second item of a kind it holds, in its own OnItemPickedUp.</summary>
    public sealed class OneOfAKind : Hero
    {
        /// <summary>Logs the pick-up and drops the item.</summary>
        public override void OnItemPickedUp(Item pickedItem)
        {
            TestLog.Lines.Add("hero got");
            Drop(pickedItem);
        }
    }

    /// <summary>A unit component that counts the items its unit holds, by the pick-ups and drops it hears.</summary>
    public sealed class ItemTally : UnitComponent
    {
        /// <summary>Logs "tally +1".</summary>
        public override void OnItemPickedUp(Item pickedItem)
        {
            TestLog.Lines.Add("tally +1");
        }

        /// <summary>Logs "tally -1".</summary>
        public override void OnItemDropped(Item droppedItem)
        {
            TestLog.Lines.Add("tally -1");
        }
    }

    /// <summary>A tome that is read and removes itself as it is picked up.</summary>
    public sealed class Tome : Item
    {
        /// <summary>Logs "tome read" and removes the tome.</summary>
        public override void OnPickedUp(Unit carrierUnit)
        {
            TestLog.Lines.Add("tome read");
            Remove();
        }
    }

    /// <summary>A flag that logs its drops, removes itself when dropped while Leaves is set, and runs AtLeave as it leaves.</summary>
    public sealed class Flag : Item
    {
        /// <summary>Whether a drop removes the flag.</summary>
        public static bool Leaves;

        /// <summary>Runs as the flag leaves, standing for the game's drop event inside RemoveItem.</summary>
        public static Action? AtLeave;

        /// <summary>Logs "flag dropped", then removes the flag while Leaves is set.</summary>
        public override void OnDropped(Unit carrierUnit)
        {
            TestLog.Lines.Add("flag dropped");
            if (Leaves)
            {
                Remove();
            }
        }

        /// <summary>Logs "flag left" and runs AtLeave.</summary>
        protected override void OnLeave()
        {
            TestLog.Lines.Add("flag left");
            AtLeave?.Invoke();
        }
    }

    /// <summary>A unit that logs its death and the damage it takes.</summary>
    public sealed class Ward : Unit
    {
        /// <summary>Logs "ward died".</summary>
        public override void OnDeath(Unit? killerUnit)
        {
            TestLog.Lines.Add("ward died");
        }

        /// <summary>Logs "ward hurt".</summary>
        public override void OnDamaged(Damage damage)
        {
            TestLog.Lines.Add("ward hurt");
        }
    }

    /// <summary>A unit component whose removal kills or hurts its unit.</summary>
    public sealed class Tether : UnitComponent
    {
        /// <summary>Whether the removal deals damage instead of killing.</summary>
        public bool Hurts;

        /// <summary>Kills the unit, or deals it damage.</summary>
        protected override void OnRemoved()
        {
            if (Hurts)
            {
                Unit.Damage(Unit, 10.0f);
            }
            else
            {
                Unit.Kill();
            }
        }
    }

    /// <summary>A game component that logs the deaths and damage of the map.</summary>
    public sealed class MapDeathLog : GameComponent
    {
        /// <summary>Logs "map saw death".</summary>
        public override void OnUnitDied(Unit dyingUnit, Unit? killerUnit)
        {
            TestLog.Lines.Add("map saw death");
        }

        /// <summary>Logs "map saw damage".</summary>
        public override void OnUnitDamaged(Damage damage)
        {
            TestLog.Lines.Add("map saw damage");
        }
    }

    /// <summary>A game component that removes each unit it hears of.</summary>
    public sealed class Cleanup : GameComponent
    {
        /// <summary>Removes the unit.</summary>
        public override void OnUnitCreated(Unit createdUnit)
        {
            createdUnit.Remove();
        }
    }

    /// <summary>A player component that logs each unit it hears of.</summary>
    public sealed class ArmyList : PlayerComponent
    {
        /// <summary>Logs "army got a unit".</summary>
        public override void OnUnitCreated(Unit createdUnit)
        {
            TestLog.Lines.Add("army got a unit");
        }
    }

    /// <summary>A unit whose constructor throws, counting its runs.</summary>
    public sealed class Broken : Unit
    {
        /// <summary>How many times the constructor ran.</summary>
        public static int Made;

        /// <summary>Counts the run and throws.</summary>
        public Broken()
        {
            Made++;
            throw new InvalidOperationException("broken");
        }
    }

    /// <summary>An item whose constructor throws, counting its runs.</summary>
    public sealed class BrokenItem : Item
    {
        /// <summary>How many times the constructor ran.</summary>
        public static int Made;

        /// <summary>Counts the run and throws.</summary>
        public BrokenItem()
        {
            Made++;
            throw new InvalidOperationException("broken");
        }
    }

    /// <summary>A unit that removes itself as it dies, without reading its life.</summary>
    public sealed class Fleeting : Unit
    {
        /// <summary>Removes the unit.</summary>
        public override void OnDeath(Unit? killerUnit)
        {
            Remove();
        }
    }

    /// <summary>Cases for reactions that end, drop, remove or kill what the event they run in is about, and for objects the game or Moonwell has removed.</summary>
    public static class ReentryCases
    {
        /// <summary>Checks that the log is exactly the expected lines, in order.</summary>
        private static void ExpectLog(string[] expected, string what)
        {
            Expect.Equal(string.Join(", ", expected), string.Join(", ", TestLog.Lines), what);
        }

        /// <summary>A unit that drops the item in its own OnItemPickedUp: its components hear the drop, and no pick-up after it.</summary>
        public static void DropInsidePickUp(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H0ok"), () => new OneOfAKind());
            var hero = Unit.Of(world.NewUnit(ObjectTypes.FourCC("H0ok"), hero: true))!;
            hero.Add(new ItemTally());
            var item = Item.Create(ObjectTypes.FourCC("ratc"), 0.0f, 0.0f, Game.Instance);
            TestLog.Lines.Clear();
            hero.Give(item);
            ExpectLog(new string[] { "hero got", "tally -1" }, "the log");
            Expect.True(!item.IsCarried, "the item is on the ground");
        }

        /// <summary>A tome that removes itself as it is picked up: the unit hears no pick-up of an item that has left.</summary>
        public static void RemovedInsidePickUp(FakeWorld world)
        {
            ObjectTypes.Item(ObjectTypes.FourCC("I0tm"), () => new Tome());
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!;
            unit.Add(new ItemTally());
            var tome = Item.Create(ObjectTypes.FourCC("I0tm"), 0.0f, 0.0f, Game.Instance);
            TestLog.Lines.Clear();
            unit.Give(tome);
            ExpectLog(new string[] { "tome read" }, "the log");
            Expect.True(tome.HasEnded, "the tome has ended");
        }

        /// <summary>A drop the game fires while a carried item is being removed (inside RemoveItem) runs no drop reaction: the item has left.</summary>
        public static void DropWhileRemoved(FakeWorld world)
        {
            ObjectTypes.Item(ObjectTypes.FourCC("I0fl"), () => new Flag());
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!;
            unit.Add(new ItemTally());
            var flag = Item.Create(ObjectTypes.FourCC("I0fl"), 0.0f, 0.0f, Game.Instance);
            unit.Give(flag);
            Flag.Leaves = false;
            Flag.AtLeave = () => world.DropItem(unit.Handle, flag.Handle);
            TestLog.Lines.Clear();
            flag.Remove();
            Flag.AtLeave = null;
            ExpectLog(new string[] { "flag left" }, "the log");
        }

        /// <summary>A flag that removes itself from its own OnDropped runs OnDropped once, and its carrier hears the drop once.</summary>
        public static void RemovedInsideDrop(FakeWorld world)
        {
            ObjectTypes.Item(ObjectTypes.FourCC("I0fl"), () => new Flag());
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!;
            unit.Add(new ItemTally());
            var flag = Item.Create(ObjectTypes.FourCC("I0fl"), 0.0f, 0.0f, Game.Instance);
            unit.Give(flag);
            Flag.Leaves = true;
            Flag.AtLeave = () => world.DropItem(unit.Handle, flag.Handle);
            TestLog.Lines.Clear();
            unit.Drop(flag);
            Flag.Leaves = false;
            Flag.AtLeave = null;
            ExpectLog(new string[] { "flag dropped", "flag left", "tally -1" }, "the log");
        }

        /// <summary>A unit killed from its component's OnRemoved, while the unit is being removed, runs no death reactions: it leaves, it does not die.</summary>
        public static void KilledWhileRemoved(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("o0wd"), () => new Ward());
            Game.Instance.Add(new MapDeathLog());
            var ward = Unit.Of(world.NewUnit(ObjectTypes.FourCC("o0wd")))!;
            ward.Add(new Tether());
            TestLog.Lines.Clear();
            ward.Remove();
            ExpectLog(new string[0], "the log");
        }

        /// <summary>A unit hurt from its component's OnRemoved, while the unit is being removed, runs no damage reactions.</summary>
        public static void HurtWhileRemoved(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("o0wd"), () => new Ward());
            Game.Instance.Add(new MapDeathLog());
            var ward = Unit.Of(world.NewUnit(ObjectTypes.FourCC("o0wd")))!;
            ward.Add(new Tether { Hurts = true });
            TestLog.Lines.Clear();
            ward.Remove();
            ExpectLog(new string[0], "the log");
        }

        /// <summary>A unit a game component removes in OnUnitCreated is not given to the player components after it.</summary>
        public static void RemovedBeforePlayerHears(FakeWorld world)
        {
            Game.Instance.Add(new Cleanup());
            Player.Get(0).Add(new ArmyList());
            TestLog.Lines.Clear();
            var unit = Unit.Create(ObjectTypes.FourCC("hfoo"), Player.Get(0), 0.0f, 0.0f, Game.Instance);
            Expect.True(unit.HasEnded, "the unit was removed");
            ExpectLog(new string[0], "the log");
        }

        /// <summary>Unit.Create of a class whose constructor throws: the error reaches the caller, the game's unit is removed, and the constructor does not run again for it.</summary>
        public static void ThrowingUnitConstructor(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("h0br"), () => new Broken());
            Broken.Made = 0;
            var removed = world.RemovedUnits.Count;
            var threw = false;
            try
            {
                Unit.Create(ObjectTypes.FourCC("h0br"), Player.Get(0), 0.0f, 0.0f, Game.Instance);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Expect.True(threw, "the constructor's error reached the caller");
            Expect.Equal(removed + 1, world.RemovedUnits.Count, "the game's unit removed");
            var placed = world.NewUnit(ObjectTypes.FourCC("h0br"));
            Broken.Made = 0;
            try
            {
                Unit.Of(placed);
            }
            catch (InvalidOperationException)
            {
            }

            Expect.True(Unit.Of(placed) == null, "a unit whose constructor threw is not taken in");
            Expect.Equal(1, Broken.Made, "the constructor ran once for the placed unit");
        }

        /// <summary>Item.Create of a class whose constructor throws: the error reaches the caller, the game's item is removed, and the constructor does not run again for an item whose constructor threw.</summary>
        public static void ThrowingItemConstructor(FakeWorld world)
        {
            ObjectTypes.Item(ObjectTypes.FourCC("I0br"), () => new BrokenItem());
            BrokenItem.Made = 0;
            var removed = world.RemovedItems.Count;
            var threw = false;
            try
            {
                Item.Create(ObjectTypes.FourCC("I0br"), 0.0f, 0.0f, Game.Instance);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Expect.True(threw, "the constructor's error reached the caller");
            Expect.Equal(removed + 1, world.RemovedItems.Count, "the game's item removed");
            var placed = world.NewItem(ObjectTypes.FourCC("I0br"));
            BrokenItem.Made = 0;
            try
            {
                Item.Of(placed);
            }
            catch (InvalidOperationException)
            {
            }

            Expect.True(Item.Of(placed) == null, "an item whose constructor threw is not taken in");
            Expect.Equal(1, BrokenItem.Made, "the constructor ran once for the placed item");
        }

        /// <summary>A class whose constructor throws in the register step is reported, and the classes registered after it still listen for their events.</summary>
        public static void ThrowingClassInRegisterStep(FakeWorld world)
        {
            var before = world.Printed.Count;
            ObjectTypes.Registering(() =>
            {
                ObjectTypes.Unit(ObjectTypes.FourCC("h0br"), () => new Broken());
                ObjectTypes.Unit(ObjectTypes.FourCC("o0wd"), () => new Ward());
            });
            Expect.True(Events.Wants(Hook.OnDeath), "the class after the throwing one listens");
            Expect.Equal(before + 1, world.Printed.Count, "the error reported");
        }

        /// <summary>Setters on an item the game has removed, before the sweep finds it, keep the last values and make no call to the game.</summary>
        public static void ItemSettersAfterGameRemoval(FakeWorld world)
        {
            var item = Item.Create(ObjectTypes.FourCC("ratc"), 0.0f, 0.0f, Game.Instance);
            item.Charges = 4;
            item.Owner = Player.Get(3);
            world.VanishItem(item.Handle);
            world.GoneCalls = 0;
            item.Charges = 7;
            item.Owner = Player.Get(5);
            Expect.Equal(0, world.GoneCalls, "calls on the removed item");
            Expect.Equal(4, item.Charges, "the charges before the sweep");
            Expect.Same(Player.Get(3), item.Owner, "the owner before the sweep");
            world.Advance(0.3f);
            Expect.Equal(4, item.Charges, "the charges after the sweep");
            Expect.Same(Player.Get(3), item.Owner, "the owner after the sweep");
        }

        /// <summary>Setters on a unit the game has removed, before the sweep finds it, keep the last values and make no call to the game.</summary>
        public static void UnitSettersAfterGameRemoval(FakeWorld world)
        {
            var unit = Unit.Create(ObjectTypes.FourCC("hfoo"), Player.Get(3), 0.0f, 0.0f, Game.Instance);
            var life = unit.Life;
            unit.Facing = 90.0f;
            world.VanishUnit(unit.Handle);
            world.GoneCalls = 0;
            unit.Life = 1.0f;
            unit.Owner = Player.Get(5);
            unit.Facing = 180.0f;
            unit.IsPaused = true;
            unit.Scale = 2.0f;
            unit.FlyHeight = 50.0f;
            unit.Name = "Gone";
            unit.Tint(1, 2, 3);
            unit.MoveTo(5.0f, 5.0f);
            unit.PlaceAt(5.0f, 5.0f);
            unit.FaceNow(10.0f);
            unit.Kill();
            Expect.Equal(0, world.GoneCalls, "calls on the removed unit");
            world.Advance(0.3f);
            Expect.Near(life, unit.Life, "the life after the sweep");
            Expect.Same(Player.Get(3), unit.Owner, "the owner after the sweep");
            Expect.Near(90.0f, unit.Facing, "the facing after the sweep");
            Expect.True(!unit.IsPaused, "not paused after the sweep");
        }

        /// <summary>A unit killed whose OnDeath removes it reads the life of 0 the game left, not the life it had.</summary>
        public static void KilledThenRemovedReadsNoLife(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("h0fl"), () => new Fleeting());
            var unit = Unit.Create(ObjectTypes.FourCC("h0fl"), Player.Get(0), 0.0f, 0.0f, Game.Instance);
            unit.Life = 300.0f;
            unit.Kill();
            Expect.True(unit.HasEnded, "the unit was removed in OnDeath");
            Expect.Near(0.0f, unit.Life, "the life");
        }

        /// <summary>Tint clamps each value to 0-255, as its doc says.</summary>
        public static void TintClamps(FakeWorld world)
        {
            var unit = Unit.Create(ObjectTypes.FourCC("hfoo"), Player.Get(0), 0.0f, 0.0f, Game.Instance);
            unit.Tint(300, -5, 128, 999);
            var tint = world.Tints[unit.Handle];
            Expect.Equal("255 0 128 255", tint[0] + " " + tint[1] + " " + tint[2] + " " + tint[3], "the tint given to the game");
        }

        /// <summary>Unit.Of gives null for a unit the game has removed, before the sweep finds it, as its doc says.</summary>
        public static void OfGameRemovedUnit(FakeWorld world)
        {
            var unit = Unit.Create(ObjectTypes.FourCC("hfoo"), Player.Get(0), 0.0f, 0.0f, Game.Instance);
            var handle = unit.Handle;
            world.VanishUnit(handle);
            Expect.True(Unit.Of(handle) == null, "Of a unit the game removed");
        }

        /// <summary>A hero the game has removed whose level was never read reads a LevelProgress of 0, and no error.</summary>
        public static void ProgressOfRemovedHero(FakeWorld world)
        {
            var hero = (Hero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!;
            world.VanishUnit(hero.Handle);
            Expect.Near(0.0f, hero.LevelProgress, "the progress");
        }
    }
}
