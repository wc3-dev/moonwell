using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for what an outside review found in the stats (J150 to J158): a read that makes nothing, a whole number rounded exactly, a cap
    /// that is replaced, a change announced from the total last heard, and the attributes and other whole numbers read as the game has them.
    /// </summary>
    public static class StatReviewCases
    {
        /// <summary>A game component that holds modifiers, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        /// <summary>Hears every unit's and player's stat change and notes it by holder.</summary>
        private sealed class Tally : GameComponent
        {
            public Unit? First;

            public Unit? Second;

            public readonly List<string> OfFirst = new List<string>();

            public readonly List<string> OfSecond = new List<string>();

            public readonly List<string> OfPlayers = new List<string>();

            public override void OnUnitStatChanged(Unit unit, Stat stat, float before)
            {
                var line = stat.Name + " " + (int)(before * 10.0f) + " to " + (int)(unit.Get(stat) * 10.0f);
                if (unit == First)
                {
                    OfFirst.Add(line);
                }
                else if (unit == Second)
                {
                    OfSecond.Add(line);
                }
            }

            public override void OnPlayerStatChanged(Player player, Stat stat, float before)
            {
                OfPlayers.Add(player.Slot + " " + stat.Name + " " + (int)(before * 10.0f) + " to " + (int)(player.Get(stat) * 10.0f));
            }
        }

        /// <summary>Counts what a listener that adds up the total less the one before would count for a stat, and sets another stat's base inside a reaction to the first.</summary>
        private sealed class Counter : GameComponent
        {
            public Stat? Source;

            public Stat? Derived;

            public float Gained;

            public int Heard;

            public override void OnUnitStatChanged(Unit unit, Stat stat, float before)
            {
                if (stat == Derived)
                {
                    Gained += unit.Get(stat) - before;
                    Heard++;
                }
                else if (stat == Source)
                {
                    unit.SetBase(Derived!, 5.0f);
                }
            }
        }

        private static int Id(string code) => ObjectTypes.FourCC(code);

        private static Unit Footman(FakeWorld world) => Unit.Of(world.NewUnit(Id("hfoo")))!;

        /// <summary>A whole stat keeps a whole value from 2^23 up and rounds the halves half up, as before, and a value just under a half is not pushed up by the addition.</summary>
        public static void WholeRoundsExactly(FakeWorld world)
        {
            var unit = Footman(world);
            var score = new Stat("Review score", 8388609.0f, whole: true);
            Expect.True(unit.Get(score) == 8388609.0f, "a whole value above 2^23 stays: 8388609");
            Expect.True(Reading.Of(unit, score) == 8388609.0f, "and a unit with no sheet reads it the same");
            Expect.True(unit.stats == null, "with no sheet made");
            unit.SetBase(score, 8388611.0f);
            Expect.True(unit.Get(score) == 8388611.0f, "8388611 stays too");
            var half = new Stat("Review half", 0.0f, whole: true);
            unit.SetBase(half, 2.5f);
            Expect.True(unit.Get(half) == 3.0f, "2.5 rounds up");
            unit.SetBase(half, -0.5f);
            Expect.True(unit.Get(half) == 0.0f, "-0.5 rounds up to 0");
            unit.SetBase(half, -1.5f);
            Expect.True(unit.Get(half) == -1.0f, "-1.5 rounds up to -1");
            unit.SetBase(half, 0.49999997f);
            Expect.True(unit.Get(half) == 0.0f, "just under a half is 0");
            unit.SetBase(half, 7.0f);
            Expect.True(unit.Get(half) == 7.0f, "a whole value stays");
        }

        /// <summary>A read makes nothing: no sheet for a unit or a player with none, for an own stat, one with a formula, a capped one, a game stat, the bases and the modifiers and a save.</summary>
        public static void ReadsMakeNothing(FakeWorld world)
        {
            var own = new Stat("Review read own", 3.0f);
            var formula = new Stat("Review read formula", 1.0f).From(own, 2.0f);
            var capped = new Stat("Review read capped", 50.0f).CappedBy(own);
            var set = new StatSet(own, formula, capped, Stat.Strength, Stat.Damage, Stat.MaxLife);
            var hero = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            var unit = Footman(world);
            var player = Player.Two;
            Expect.Near(3.0f, unit.Get(own), "an own stat");
            Expect.Near(7.0f, unit.Get(formula), "one with a formula: 1 and 2 x 3");
            Expect.Near(3.0f, unit.Get(capped), "a capped one");
            Expect.Near(7.0f, unit.GetBase(formula), "its base");
            Expect.Near(0.0f, unit.GetBonus(formula), "its bonus");
            Expect.Equal(0, unit.Modifiers(formula).Count, "no modifiers");
            unit.Get(Stat.Damage);
            unit.Get(Stat.MaxLife);
            unit.Get(Stat.AttackSpeed);
            hero.Get(Stat.Strength);
            hero.GetBase(Stat.Armor);
            set.Save(unit);
            set.Save(hero);
            Expect.True(unit.stats == null, "the unit has no sheet after the reads");
            Expect.True(hero.stats == null, "nor the hero");
            Expect.Near(3.0f, player.Get(own), "a player's own stat");
            Expect.Near(7.0f, player.Get(formula), "one with a formula");
            Expect.Near(3.0f, player.Get(capped), "a capped one");
            Expect.Near(7.0f, player.GetBase(formula), "its base");
            Expect.Near(0.0f, player.GetBonus(formula), "its bonus");
            Expect.Equal(0, player.Modifiers(formula).Count, "no modifiers");
            player.Get(Stat.Damage);
            set.Save(player);
            Expect.True(player.stats == null, "the player has no sheet after the reads");
            unit.SetBase(own, 4.0f);
            Expect.True(unit.stats != null, "a write still makes the sheet");
            Expect.Near(9.0f, unit.Get(formula), "and counts: 1 and 2 x 4");
        }

        /// <summary>Two holders, one read first and one not, give the same totals and the same announcements after the same changes.</summary>
        public static void ReadFirstOrNot(FakeWorld world)
        {
            var tally = Game.Instance.Add(new Tally());
            var holder = Game.Instance.Add(new Holder());
            var power = new Stat("Review order power", 4.0f);
            var doubled = new Stat("Review order doubled", 0.0f).From(power, 2.0f);
            var read = Footman(world);
            var fresh = Footman(world);
            tally.First = read;
            tally.Second = fresh;
            Expect.Near(8.0f, read.Get(doubled), "read first: 2 x 4");
            read.Get(Stat.MaxLife);
            Player.One.Get(doubled);
            Modifiers(read, power, doubled, holder);
            Modifiers(fresh, power, doubled, holder);
            Player.One.Modify(power, 3.0f, heldBy: holder);
            Player.Two.Modify(power, 3.0f, heldBy: holder);
            Expect.Near(read.Get(power), fresh.Get(power), "the same power");
            Expect.Near(read.Get(doubled), fresh.Get(doubled), "the same doubled");
            Expect.Near(Player.One.Get(doubled), Player.Two.Get(doubled), "the same for the players");
            Expect.True(tally.OfFirst.Count > 0, "something was announced");
            Expect.Equal(tally.OfFirst.Count, tally.OfSecond.Count, "as many announcements");
            for (var i = 0; i < tally.OfFirst.Count && i < tally.OfSecond.Count; i++)
            {
                Expect.Equal(tally.OfFirst[i], tally.OfSecond[i], "the same announcement " + i);
            }

            Expect.Equal(4, tally.OfPlayers.Count, "two players, two stats each");
            Expect.Equal(tally.OfPlayers[0].Substring(1), tally.OfPlayers[2].Substring(1), "the same for the players");
        }

        private static void Modifiers(Unit unit, Stat power, Stat doubled, Scope holder)
        {
            unit.Modify(power, 6.0f, heldBy: holder);
            unit.SetBase(power, 1.0f);
            unit.Modify(doubled, percent: 50.0f, heldBy: holder);
        }

        /// <summary>A second CappedBy takes the stat off the old cap's readers, unless a From or Formula still reads it: the report's three lines no longer loop, and the old cap's changes no longer touch the stat.</summary>
        public static void CappedByReplaced(FakeWorld world)
        {
            var unit = Footman(world);
            var a = new Stat("Review cap a", 50.0f);
            var b = new Stat("Review cap b", 100.0f);
            var c = new Stat("Review cap c", 30.0f);
            a.CappedBy(b);
            a.CappedBy(c);
            b.From(a, 1.0f);
            Expect.True(b.Readers == null || !b.Readers.Contains(a), "the old cap no longer lists the stat as a reader");
            if (b.Readers != null && b.Readers.Contains(a))
            {
                return;
            }

            Expect.True(c.Readers != null && c.Readers.Contains(a), "the new cap does");
            unit.SetBase(c, 20.0f);
            Expect.Near(20.0f, unit.Get(a), "a change of the new cap works through, with no loop");
            Expect.Near(120.0f, unit.Get(b), "to what reads the stat");
            var tally = Game.Instance.Add(new Tally());
            tally.First = unit;
            unit.SetBase(b, 7.0f);
            Expect.Equal(1, tally.OfFirst.Count, "a change of the old cap touches nothing but itself");
            Expect.Near(20.0f, unit.Get(a), "the stat is where it was");

            var x = new Stat("Review cap x", 5.0f);
            var y = new Stat("Review cap y", 9.0f);
            var z = new Stat("Review cap z", 9.0f);
            x.From(y, 1.0f);
            x.CappedBy(y);
            x.CappedBy(z);
            Expect.True(y.Readers != null && y.Readers.Contains(x), "a stat that still reads the old cap through a From stays among its readers");
            Expect.Same(z, x.Cap, "the cap is the new one");
            x.CappedBy(z);
            Expect.True(z.Readers != null && z.Readers.Contains(x), "the same cap again changes nothing");
        }

        /// <summary>A change inside a reaction to the stat it reads is announced from the total last announced: the listener adding up the total less before counts 15 for a gain of 15, not 20.</summary>
        public static void AnnouncedFromTheLastTotal(FakeWorld world)
        {
            var unit = Footman(world);
            var a = new Stat("Review announce a", 0.0f);
            var b = new Stat("Review announce b", 0.0f).From(a, 1.0f);
            var counter = Game.Instance.Add(new Counter());
            counter.Source = a;
            counter.Derived = b;
            unit.SetBase(a, 10.0f);
            Expect.Near(15.0f, unit.Get(b), "10 from a and the base 5");
            Expect.Near(15.0f, counter.Gained, "a listener adding the total less before counts the gain once");
            Expect.Equal(1, counter.Heard, "heard once, from 0");
            unit.SetBase(a, 12.0f);
            Expect.Near(17.0f, counter.Gained, "the next change is announced from the 15 it reached: 2 more");
        }

        /// <summary>The three attributes are whole wherever they are read: Get gives what the hero has, set, modified or read by a formula.</summary>
        public static void AttributesAreWhole(FakeWorld world)
        {
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetStats(handle, 22, 13, 17);
            hero.SetBase(Stat.Strength, 10.5f);
            Expect.True(hero.Get(Stat.Strength) == 11.0f, "10.5 reads 11");
            Expect.Equal(11, world.StrengthOf(handle, false), "and the hero has 11");
            hero.SetBase(Stat.Agility, 9.4f);
            Expect.True(hero.Get(Stat.Agility) == 9.0f, "9.4 reads 9");
            Expect.Equal(9, world.AgilityOf(handle, false), "and the hero has 9");
            hero.SetBase(Stat.Intelligence, 12.5f);
            Expect.True(hero.Get(Stat.Intelligence) == 13.0f, "12.5 reads 13");
            Expect.Equal(13, world.IntelligenceOf(handle, false), "and the hero has 13");
            var power = new Stat("Review attribute power", 0.0f).From(Stat.Strength, 2.0f);
            Expect.Near(22.0f, hero.Get(power), "a formula reads the whole 11");
            var other = world.NewUnit(Id("Hpal"), hero: true);
            var unowned = Unit.Of(other)!;
            world.SetStats(other, 22, 13, 17);
            unowned.Modify(Stat.Agility, unowned, percent: 10.0f);
            Expect.Equal(14, world.AgilityOf(other, false), "13 and 10 percent is 14.3: the hero has 14");
            Expect.True(unowned.Get(Stat.Agility) == 14.0f, "and Get says 14");
        }

        /// <summary>Most life and most mana, damage and armor read as the game has them: the most is whole, damage and armor are the white number and a whole green bonus.</summary>
        public static void GameNumbersAsTheGameHasThem(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetMaxLife(handle, 650.0f);
            world.SetMaxMana(handle, 255.0f);
            hero.SetBase(Stat.MaxLife, 650.5f);
            Expect.Near(651.0f, world.MaxLifeOf(handle), "the game has 651 most life");
            Expect.True(hero.Get(Stat.MaxLife) == 651.0f, "and Get says 651");
            hero.SetBase(Stat.MaxMana, 100.4f);
            Expect.Near(100.0f, world.MaxManaOf(handle), "the game has 100 most mana");
            Expect.True(hero.Get(Stat.MaxMana) == 100.0f, "and Get says 100");
            hero.Modify(Stat.MaxLife, -0.2f, heldBy: holder);
            Expect.True(hero.Get(Stat.MaxLife) == world.MaxLifeOf(handle), "a modifier keeps them the same: 650.3");

            var footman = world.NewUnit(Id("hfoo"));
            var unit = Unit.Of(footman)!;
            world.SetAverageDamage(footman, 20.0f);
            unit.SetBase(Stat.Damage, 10.0f);
            unit.Modify(Stat.Damage, 2.5f, heldBy: holder);
            var damage = world.AverageDamageOf(footman) + world.BonusOf(footman, Id("AMWd"));
            Expect.Near(13.0f, damage, "the game has the white 10 and a green 3 (2.5 rounds up)");
            Expect.Near(damage, unit.Get(Stat.Damage), "and Get says the same");
            world.SetArmor(footman, 2.0f);
            unit.SetBase(Stat.Armor, 4.0f);
            unit.Modify(Stat.Armor, 2.5f, heldBy: holder);
            Expect.Near(7.0f, world.ArmorOf(footman), "the game has the base 4 and a green 3");
            Expect.Near(7.0f, unit.Get(Stat.Armor), "and Get says 7");
            Expect.Near(3.0f, unit.GetBonus(Stat.Armor), "the bonus is the green 3");

            var tally = Game.Instance.Add(new Tally());
            tally.First = hero;
            world.SetAverageDamage(handle, 20.0f);
            hero.SetBase(Stat.Damage, 10.0f);
            hero.Modify(Stat.Damage, 2.5f, heldBy: holder);
            Expect.Near(13.0f, hero.Get(Stat.Damage), "a hero's damage the same");
            tally.OfFirst.Clear();
            world.LevelHero(handle, 2, false);
            var spurious = 0;
            for (var i = 0; i < tally.OfFirst.Count; i++)
            {
                spurious += tally.OfFirst[i].StartsWith("Damage") ? 1 : 0;
            }

            Expect.Equal(0, spurious, "a level-up that changes nothing in damage announces nothing for it");
        }

        private static int bareRuns;

        /// <summary>One holder's reads of each kind of stat, as Get, GetBase and GetBonus say them, in a fixed order.</summary>
        private static List<float> ReadAll(Unit? unit, Player? player, Stat[] stats)
        {
            var read = new List<float>();
            for (var i = 0; i < stats.Length; i++)
            {
                read.Add(unit != null ? unit.Get(stats[i]) : player!.Get(stats[i]));
                read.Add(unit != null ? unit.GetBase(stats[i]) : player!.GetBase(stats[i]));
                read.Add(unit != null ? unit.GetBonus(stats[i]) : player!.GetBonus(stats[i]));
            }

            return read;
        }

        /// <summary>A holder with no sheet reads every kind of stat as the same holder does once a sheet exists (made by a modifier added and ended): an own stat, a type's base, a From, a Formula, a cap, limits and whole, a game stat the game owns, and one Moonwell owns through its type; nothing is made for any read.</summary>
        public static void BareReadsMatchASheet(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var own = new Stat("Review bare own", 3.5f, least: 1.0f, most: 9.0f);
            var typed = new Stat("Review bare typed", 2.0f);
            var from = new Stat("Review bare from", 1.0f).From(own, 2.0f);
            var formula = new Stat("Review bare formula", 0.0f).Formula(read => read(typed) * 3.0f + read(from), typed, from);
            var capped = new Stat("Review bare capped", 50.0f, whole: true).CappedBy(from);
            var whole = new Stat("Review bare whole", 2.5f, null, 100.0f, true);
            var other = new Stat("Review bare other");
            // The ready-made stats are static and keep a type's base for the rest of the run, so each run has types of its own.
            bareRuns++;
            var suffix = "" + (char)('a' + bareRuns / 26 % 26) + (char)('a' + bareRuns % 26);
            var type = Id("h1" + suffix);
            var ownedType = Id("h2" + suffix);
            typed.SetBase(type, 7.25f);
            var unit = Unit.Of(world.NewUnit(type))!;
            var hero = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            world.SetStats(hero.Handle, 22, 13, 17);
            world.SetMaxLife(hero.Handle, 650.0f);
            world.SetAverageDamage(hero.Handle, 30.0f);
            world.SetArmor(hero.Handle, 4.0f);
            var owner = Unit.Of(world.NewUnit(ownedType))!;
            world.SetMaxLife(owner.Handle, 400.0f);
            world.SetAverageDamage(owner.Handle, 12.0f);
            world.SetArmor(owner.Handle, 3.0f);
            Stat.MaxLife.SetBase(ownedType, 450.5f);
            Stat.Damage.SetBase(ownedType, 20.0f);
            Stat.Armor.SetBase(ownedType, 5.25f);
            var all = new Stat[] { own, typed, from, formula, capped, whole, Stat.Strength, Stat.MaxLife, Stat.Damage, Stat.Armor, Stat.AttackSpeed };
            var bareUnit = ReadAll(unit, null, all);
            var bareHero = ReadAll(hero, null, all);
            var bareOwner = ReadAll(owner, null, all);
            var barePlayer = ReadAll(null, Player.Two, all);
            Expect.True(unit.stats == null && hero.stats == null && owner.stats == null && Player.Two.stats == null, "no sheet was made by any read");
            Expect.Equal(0, unit.Modifiers(own).Count, "no modifiers for a unit");
            Expect.Equal(0, Player.Two.Modifiers(own).Count, "or a player");
            Expect.Near(7.25f, unit.Get(typed), "the type's base");
            Expect.Near(450.5f + 0.5f, owner.Get(Stat.MaxLife), "a most life Moonwell owns through the type, whole");
            unit.Modify(other, 1.0f, heldBy: holder).End();
            hero.Modify(other, 1.0f, heldBy: holder).End();
            owner.Modify(other, 1.0f, heldBy: holder).End();
            Player.Two.Modify(other, 1.0f, heldBy: holder).End();
            Expect.True(unit.stats != null && hero.stats != null && owner.stats != null && Player.Two.stats != null, "a modifier made a sheet for each");
            var sheetUnit = ReadAll(unit, null, all);
            var sheetHero = ReadAll(hero, null, all);
            var sheetOwner = ReadAll(owner, null, all);
            var sheetPlayer = ReadAll(null, Player.Two, all);
            for (var i = 0; i < bareUnit.Count; i++)
            {
                Expect.True(bareUnit[i] == sheetUnit[i], "a unit's read " + i + " is the same with a sheet");
                Expect.True(bareHero[i] == sheetHero[i], "a hero's read " + i + " is the same with a sheet");
                Expect.True(bareOwner[i] == sheetOwner[i], "an owning unit's read " + i + " is the same with a sheet");
                Expect.True(barePlayer[i] == sheetPlayer[i], "a player's read " + i + " is the same with a sheet");
            }
        }
    }
}
