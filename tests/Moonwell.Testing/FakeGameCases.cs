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

        private static readonly int DummyBolt = ObjectTypes.FourCC("A010");
        private static readonly int DummyStorm = ObjectTypes.FourCC("A011");

        /// <summary>A unit of slot 0 with a fake spell at level 1.</summary>
        private static unit SpellCaster(FakeWorld world, int abilityId)
        {
            var caster = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0);
            world.SetAbilityLevel(caster, abilityId, 1);
            return caster;
        }

        /// <summary>A dummy's spell takes effect inside the order call, after the order's own event, and an instant spell's end-cast comes in the same call (seen 11-03).</summary>
        public static void DummyEffectInOrder(FakeWorld world)
        {
            var watch = new FakeCastWatch();
            Game.Instance.Add(watch);
            world.Dummies.Spell(DummyBolt, "thunderbolt", SpellKind.Target, (caster, target, x, y) => TestLog.Lines.Add("effect"));
            var bolter = SpellCaster(world, DummyBolt);
            var target = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            TestLog.Lines.Clear();
            Expect.True(world.OrderUnitOn(bolter, "thunderbolt", target), "the order is taken");
            Expect.Equal("cast,effect", string.Join(",", TestLog.Lines), "the cast's reactions, then the spell's effect, inside the call");
            Expect.True(watch.Last != null && watch.Last.HasEnded, "an instant spell's cast ended inside the call");
        }

        /// <summary>The game refuses a dummy's order of the wrong kind, and a target a case refuses (seen 11-03: a target the spell does not allow).</summary>
        public static void DummyWrongKind(FakeWorld world)
        {
            var watch = new FakeCastWatch();
            Game.Instance.Add(watch);
            world.Dummies.Spell(DummyStorm, "thunderclap", SpellKind.None, (caster, target, x, y) => { });
            world.Dummies.Spell(DummyBolt, "thunderbolt", SpellKind.Target, (caster, target, x, y) => { });
            var both = SpellCaster(world, DummyStorm);
            world.SetAbilityLevel(both, DummyBolt, 1);
            var target = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            Expect.True(!world.OrderUnitAt(both, "thunderclap", 100.0f, 0.0f), "a spell with no target, ordered at a place");
            Expect.True(!world.OrderUnit(both, "thunderbolt"), "a spell at a unit, ordered with no target");
            world.Dummies.Refuse(DummyBolt);
            Expect.True(!world.OrderUnitOn(both, "thunderbolt", target), "a refusal the case sets");
            Expect.True(world.OrderUnit(both, "thunderclap"), "the spell with no target, ordered as it is");
            Expect.Equal(1, watch.Casts, "only the last took effect");
        }

        /// <summary>A target its caster's player does not see is refused until a vision patch covers it, and an invisible one always (seen 11-03).</summary>
        public static void DummyHiddenTarget(FakeWorld world)
        {
            world.Dummies.Spell(DummyBolt, "thunderbolt", SpellKind.Target, (caster, target, x, y) => { });
            var bolter = SpellCaster(world, DummyBolt);
            var target = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            world.PlaceUnit(target, 500.0f, 0.0f);
            var owner = world.PlayerAt(0);
            world.Dummies.Hide(owner, target);
            Expect.True(!world.OrderUnitOn(bolter, "thunderbolt", target), "a target in fog");
            var patch = world.Dummies.Patch(owner, 500.0f, 0.0f);
            Expect.True(world.OrderUnitOn(bolter, "thunderbolt", target), "the patch shows it");
            world.Dummies.Unpatch(patch);
            Expect.True(!world.OrderUnitOn(bolter, "thunderbolt", target), "hidden again once the patch is gone");
            world.Dummies.Invisible(target);
            var again = world.Dummies.Patch(owner, 500.0f, 0.0f);
            Expect.True(!world.OrderUnitOn(bolter, "thunderbolt", target), "an invisible target, patch or not");
            world.Dummies.Unpatch(again);
            Expect.Equal("made,stopped,made,stopped", string.Join(",", world.Dummies.Patches), "each patch made and stopped, in order");
        }

        /// <summary>A channel's end-cast comes at its end or at a stop order, not inside the order (seen 11-03).</summary>
        public static void DummyChannelStop(FakeWorld world)
        {
            var watch = new FakeCastWatch();
            Game.Instance.Add(watch);
            world.Dummies.Spell(DummyStorm, "blizzard", SpellKind.Point, (caster, target, x, y) => { }, channel: true);
            var stopped = SpellCaster(world, DummyStorm);
            var ended = SpellCaster(world, DummyStorm);
            Expect.True(world.OrderUnitAt(stopped, "blizzard", 300.0f, 0.0f), "the channel starts");
            var first = watch.Last!;
            Expect.True(!first.HasEnded, "a channel's cast goes on after the call");
            world.OrderUnit(stopped, "stop");
            Expect.True(first.HasEnded, "a stop ends it");
            world.OrderUnitAt(ended, "blizzard", 300.0f, 0.0f);
            var second = watch.Last!;
            Expect.True(!second.HasEnded, "the second channel goes on");
            world.Dummies.EndChannel(ended);
            Expect.True(second.HasEnded, "its end ends it");
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

        /// <summary>An item a drop reaction gives to a third unit, while UnitAddItem takes it from its holder, stays in one inventory: that give answers false.</summary>
        public static void ItemGivenAwayInItsDrop(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H005"), () => new DropRegiver());
            var first = world.NewUnit(ObjectTypes.FourCC("H005"), hero: true);
            var second = world.NewUnit(ObjectTypes.FourCC("H005"), hero: true);
            var third = world.NewUnit(ObjectTypes.FourCC("H005"), hero: true);
            Unit.Of(first);
            Unit.Of(second);
            Unit.Of(third);
            var item = world.NewItem(ObjectTypes.FourCC("I001"));
            Item.Of(item);
            world.GiveItem(first, item);
            DropRegiver.World = world;
            DropRegiver.To = third;
            Expect.True(!world.GiveItem(second, item), "the give answers false");
            Expect.True(!world.CarriesItem(first, item) && !world.CarriesItem(second, item) && world.CarriesItem(third, item), "only the third unit has it");
        }

        /// <summary>Game time never goes back: a wake asked for in the past is due now, as NativeWorld starts the game's timer at 0 for it, and a wake that comes late keeps its time past the end of the Advance.</summary>
        public static void TimeNeverGoesBack(FakeWorld world)
        {
            world.Advance(5.0f);
            world.WakeIn(-0.5f);
            Expect.Near(5.0f, world.WakeAt!.Value, "a wake asked for 0.5 s ago is due now");
            world.Advance(1.0f);
            Expect.Near(6.0f, world.Time, "the time after");
            world.WakeLate = 0.0005f;
            Game.Instance.After(1.0f, () => { });
            world.Advance(1.0f);
            Expect.True(world.Time > 7.0002f, "the late wake's time is kept: " + world.Time);
        }

        /// <summary>A unit removed in one wake reads type id 0 at the next, though one Advance runs both: its frame ends before the game's time moves on (seen 10-21: 0 by 0.05 s).</summary>
        public static void RemovalEndsBetweenWakes(FakeWorld world)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            var unit = Unit.Of(handle)!;
            var typeLater = -1;
            Game.Instance.After(1.0f, () => unit.Remove());
            Game.Instance.After(2.0f, () => typeLater = world.UnitTypeId(handle));
            world.Advance(3.0f);
            Expect.Equal(0, typeLater, "the type id a second after the removal");
        }

        /// <summary>A call on a unit the runtime removed counts as a call on a gone unit from the call that removed it on, as one on a unit the game removed does.</summary>
        public static void RemovedUnitCallsCounted(FakeWorld world)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            Unit.Of(handle)!.Remove();
            world.GoneCalls = 0;
            world.LifeOf(handle);
            world.SetLife(handle, 50.0f);
            Expect.Equal(2, world.GoneCalls, "both calls counted in the frame of the removal");
        }

        /// <summary>The units in range come in the order they were made, in Lua too, where a dictionary's order is not that.</summary>
        public static void UnitsInRangeInTheOrderMade(FakeWorld world)
        {
            var made = new List<unit>();
            for (var i = 0; i < 12; i++)
            {
                var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
                world.MoveUnit(handle, i * 10.0f, 0.0f);
                made.Add(handle);
            }

            var found = world.UnitsInRange(0.0f, 0.0f, 500.0f);
            Expect.Equal(made.Count, found.Count, "every unit found");
            for (var i = 0; i < made.Count; i++)
            {
                Expect.True(found[i] == made[i], "unit " + i + " in its place");
            }
        }

        /// <summary>An aura whose holder was removed stops: a unit near the origin, where a removed unit reads, never gets its buff, a unit it gave it to loses it 3 s later, and it makes no call on the removed holder.</summary>
        public static void AuraOfARemovedHolderStops(FakeWorld world)
        {
            var buff = ObjectTypes.FourCC("B020");
            var holder = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.MoveUnit(holder, 1000.0f, 1000.0f);
            var near = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.MoveUnit(near, 1000.0f, 1050.0f);
            var bystander = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.MoveUnit(bystander, 50.0f, 0.0f);
            world.GameBuffs.Aura(holder, ObjectTypes.FourCC("A020"), buff, 300.0f);
            Expect.Equal(1, world.AbilityLevel(near, buff), "the near unit has the buff");
            world.VanishUnit(holder);
            world.GoneCalls = 0;
            world.Advance(4.0f);
            Expect.Equal(0, world.GoneCalls, "no call on the removed holder");
            Expect.Equal(0, world.AbilityLevel(bystander, buff), "the unit near the origin never got it");
            world.Advance(4.0f);
            Expect.Equal(0, world.AbilityLevel(near, buff), "the near unit lost it 3 s after");
        }

        /// <summary>A unit in reach of two auras that give the same buff keeps it while one of them still reaches it, when the other's 3 s run out at the last step of an Advance.</summary>
        public static void BuffKeptWhileAnAuraReaches(FakeWorld world)
        {
            var buff = ObjectTypes.FourCC("B021");
            var weak = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            var strong = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            var reached = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.MoveUnit(reached, 100.0f, 0.0f);
            world.GameBuffs.Aura(weak, ObjectTypes.FourCC("A021"), buff, 300.0f);
            world.GameBuffs.Aura(strong, ObjectTypes.FourCC("A022"), buff, 300.0f);
            world.MoveUnit(strong, 5000.0f, 0.0f);
            world.Advance(0.1f);
            world.Advance(3.0f);
            Expect.Equal(1, world.AbilityLevel(reached, buff), "the weak aura still gives it");
        }

        /// <summary>A new item belongs to neutral passive (seen 10-21).</summary>
        public static void NewItemIsNeutralPassives(FakeWorld world)
        {
            var item = Item.Create(ObjectTypes.FourCC("ofoo"), 0.0f, 0.0f, Game.Instance);
            Expect.Same(Player.NeutralPassive, item.Owner, "its owner");
        }

        /// <summary>After RemoveItem the item's type id and every number of it read 0 in the same frame (seen 10-21).</summary>
        public static void RemovedItemReadsNothing(FakeWorld world)
        {
            var item = world.NewItem(ObjectTypes.FourCC("I001"));
            world.SetCharges(item, 3);
            world.MoveItem(item, 50.0f, 60.0f);
            world.DeleteItem(item);
            Expect.Equal(0, world.ItemTypeId(item), "the type id");
            Expect.Equal(0, world.ChargesOf(item), "the charges");
            Expect.Near(0.0f, world.ItemX(item), "the x");
            Expect.Near(0.0f, world.ItemY(item), "the y");
        }

        /// <summary>A unit made by CreateUnit faces as asked, and is a hero when its type's id starts with a capital letter, as the id decides (seen 10-28).</summary>
        public static void SpawnedUnitFacesAndIsAHero(FakeWorld world)
        {
            var footman = world.SpawnUnit(world.PlayerAt(0), ObjectTypes.FourCC("hfoo"), 0.0f, 0.0f, 90.0f);
            var paladin = world.SpawnUnit(world.PlayerAt(0), ObjectTypes.FourCC("Hpal"), 0.0f, 0.0f, 0.0f);
            Expect.Near(90.0f, world.FacingOf(footman), "the facing");
            Expect.True(!world.IsHero(footman), "a footman is no hero");
            Expect.True(world.IsHero(paladin), "a paladin is");
        }

        /// <summary>A mouse button past the game's three reads not held, as NativeInputWorld answers.</summary>
        public static void MouseButtonsPastThree(FakeWorld world)
        {
            var fake = new FakeInputWorld();
            fake.Held[1] = true;
            Expect.True(fake.IsMouseButtonPressed(1), "the left button held");
            Expect.True(!fake.IsMouseButtonPressed(0) && !fake.IsMouseButtonPressed(4) && !fake.IsMouseButtonPressed(-1), "no button 0, 4 or -1");
        }

        /// <summary>A stop given inside a channel's effect ends the channel, with its end-cast inside the stop, as a stop does (seen 11-03).</summary>
        public static void DummyStopInsideEffect(FakeWorld world)
        {
            var watch = new FakeCastWatch();
            Game.Instance.Add(watch);
            world.Dummies.Spell(DummyStorm, "blizzard", SpellKind.Point, (caster, target, x, y) => world.OrderUnit(caster, "stop"), channel: true);
            var dummy = SpellCaster(world, DummyStorm);
            Expect.True(world.OrderUnitAt(dummy, "blizzard", 300.0f, 0.0f), "the channel starts");
            Expect.True(watch.Last != null && watch.Last.HasEnded, "the stop inside the effect ended it");
        }

        /// <summary>An order whose reaction takes the spell away casts nothing.</summary>
        public static void DummySpellTakenInItsOrder(FakeWorld world)
        {
            world.Dummies.Spell(DummyBolt, "thunderbolt", SpellKind.Target, (caster, target, x, y) => TestLog.Lines.Add("effect"));
            var bolter = SpellCaster(world, DummyBolt);
            var target = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1);
            Game.Instance.Add(new SpellTaker(world, DummyBolt));
            TestLog.Lines.Clear();
            world.OrderUnitOn(bolter, "thunderbolt", target);
            Expect.True(!TestLog.Lines.Contains("effect"), "no effect");
        }

        /// <summary>The fake damage world cuts a normal hit by armor as the game does, below 0 too (100 at armor -20 took 171, seen 10-20), and leaves a magic and a sonic hit whole (10 magic took 10 off a footman, seen 10-16; Storm Bolt's sonic 100 was not cut, seen 10-01).</summary>
        public static void DamageWorldArmor(FakeWorld world)
        {
            var fake = new FakeDamageWorld(world);
            var source = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            var target = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            fake.Body(target).Life = 1000.0f;
            fake.Body(target).Armor = -20.0f;
            fake.Hit(source, target, 100.0f, damageType: 4);
            Expect.Near(829.0106f, fake.Life(target), "a normal hit at armor -20 takes 171");
            fake.Body(target).Armor = 2.0f;
            var before = fake.Life(target);
            fake.Hit(source, target, 10.0f, damageType: 14);
            fake.Hit(source, target, 100.0f, damageType: 15);
            Expect.Near(before - 110.0f, fake.Life(target), "a magic and a sonic hit at armor 2 take all of 10 and 100");
        }

        /// <summary>The fake damage world tells core's fake of a hit that kills, whose death comes inside the call with the source as the killer (seen 10-20), and plays no hit on a dead unit, which fires neither event and takes no life (seen 10-31).</summary>
        public static void DamageWorldDeaths(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H002"), () => new Striker());
            var fake = new FakeDamageWorld(world);
            var source = world.NewUnit(ObjectTypes.FourCC("H002"));
            Unit.Of(source);
            var target = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            var victim = Unit.Of(target)!;
            var events = 0;
            fake.Listen(() => events++, () => events++);
            TestLog.Lines.Clear();
            fake.Hit(source, target, 200.0f, damageType: 4);
            Expect.True(!world.IsAlive(target) && !victim.IsAlive, "the target is dead in core's fake too");
            Expect.True(TestLog.Lines.Contains("striker killed"), "the death came, with the source as the killer");
            events = 0;
            fake.Hit(source, target, 10.0f, damageType: 4);
            Expect.Equal(0, events, "no event for the dead");
            Expect.Near(0.0f, fake.Life(target), "and no life taken");
        }

#if DEBUG
        /// <summary>The fake debug world runs an After asked for from a game wake at its own time, before one asked earlier and due later, and with none waiting too.</summary>
        public static void DebugAfterFromAWake(FakeWorld world)
        {
            var debug = new FakeDebugWorld(world);
            var names = new List<string>();
            var times = new List<float>();
            Game.Instance.After(0.25f, () => debug.After(0.25f, () =>
            {
                names.Add("B");
                times.Add(world.Time);
            }));
            debug.After(0.75f, () =>
            {
                names.Add("A");
                times.Add(world.Time);
            });
            debug.Advance(1.0f);
            Expect.Equal("B,A", string.Join(",", names), "in due order");
            Expect.Near(0.5f, times[0], "B at its time");
            Expect.Near(0.75f, times[1], "A at its time");
            var ranAt = -1.0f;
            Game.Instance.After(0.25f, () => debug.After(0.25f, () => ranAt = world.Time));
            debug.Advance(1.0f);
            Expect.Near(1.5f, ranAt, "one asked with none waiting");
        }
#endif
    }

    /// <summary>A unit that gives an item it drops to another unit, once, inside the drop event.</summary>
    public sealed class DropRegiver : Unit
    {
        /// <summary>The fake world to give through.</summary>
        public static FakeWorld? World;

        /// <summary>The unit to give the next dropped item to; null once given.</summary>
        public static unit? To;

        /// <summary>Gives the dropped item to To.</summary>
        public override void OnItemDropped(Item droppedItem)
        {
            if (To != null)
            {
                var to = To;
                To = null;
                World!.GiveItem(to, droppedItem.Handle);
            }
        }
    }

    /// <summary>A game component that takes a spell from a unit when the unit is ordered.</summary>
    public sealed class SpellTaker : GameComponent
    {
        private readonly FakeWorld world;
        private readonly int abilityId;

        /// <summary>Takes the spell on each order.</summary>
        public SpellTaker(FakeWorld world, int abilityId)
        {
            this.world = world;
            this.abilityId = abilityId;
        }

        /// <summary>Takes the spell from the ordered unit.</summary>
        public override void OnUnitOrdered(Order order)
        {
            world.RemoveAbility(order.Unit.Handle, abilityId);
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

    /// <summary>A game component that logs "cast" for each cast and keeps the last.</summary>
    public sealed class FakeCastWatch : GameComponent
    {
        /// <summary>How many casts it saw.</summary>
        public int Casts;

        /// <summary>The last cast it saw; null before the first.</summary>
        public Cast? Last;

        /// <summary>Logs "cast" and keeps it.</summary>
        public override void OnUnitCast(Cast cast)
        {
            Casts++;
            Last = cast;
            TestLog.Lines.Add("cast");
        }
    }
}
