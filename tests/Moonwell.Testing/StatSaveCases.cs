using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>A save class that keeps a unit's and a player's stats as lists, as a map's would.</summary>
    public sealed class StatSave : SaveGame
    {
        /// <summary>A unit's stats, from StatSet.Save.</summary>
        [Saved(1)] public List<float> Hero = new List<float>();

        /// <summary>A player's stats, from StatSet.Save.</summary>
        [Saved(2)] public List<float> Account = new List<float>();
    }

    /// <summary>Cases for saving a unit's and a player's stats (StatSet), a type's base by text, and the data recipe.</summary>
    public static class StatSaveCases
    {
        /// <summary>A buff that gives its unit a flat modifier of a stat, held by the buff.</summary>
        private sealed class Aid : Buff
        {
            private readonly Stat stat;

            private readonly float amount;

            public Aid(Stat stat, float amount)
            {
                this.stat = stat;
                this.amount = amount;
            }

            public override void OnApply()
            {
                Unit.Modify(stat, amount, heldBy: this);
            }
        }

        /// <summary>A row of the guide's stats file, as the build writes it.</summary>
        private sealed class StatRow
        {
            public readonly string Name;

            public readonly float Base;

            public readonly float Least;

            public readonly float Most;

            public readonly int Whole;

            public StatRow(string name, float baseValue, float least, float most, int whole)
            {
                Name = name;
                Base = baseValue;
                Least = least;
                Most = most;
                Whole = whole;
            }
        }

        /// <summary>A row of the guide's unit bases file, as the build writes it.</summary>
        private sealed class BaseRow
        {
            public readonly float Value;

            public readonly string Stat;

            public readonly string Unit;

            public BaseRow(float value, string stat, string unit)
            {
                Value = value;
                Stat = stat;
                Unit = unit;
            }
        }

        private static int round;

        private static int Id(string code) => ObjectTypes.FourCC(code);

        private static Unit Footman(FakeWorld world) => Unit.Of(world.NewUnit(Id("hfoo")))!;

        /// <summary>A stat of this run's own: the cases run more than once in a process, and a name made twice is reported.</summary>
        private static Stat Own(string name, float baseValue) => new Stat(name + " " + (++round), baseValue);

        /// <summary>A pair of boots on the ground, of an item class of this file's own.</summary>
        private static Item Boots()
        {
            if (ObjectTypes.ItemMaker(Id("I0SV")) == null)
            {
                ObjectTypes.Item(Id("I0SV"), () => new DropLog("sets"));
            }

            return Item.Create(Id("I0SV"), 0.0f, 0.0f, Game.Instance);
        }

        private static void Register(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            SaveGame.Key = "the stat case key";
            SaveGame.Register<StatSave>("stats", () => new StatSave(), (save, writer) =>
            {
                writer.FloatList(1, save.Hero);
                writer.FloatList(2, save.Account);
            }, (save, reader) =>
            {
                while (reader.Next())
                {
                    switch (reader.Number)
                    {
                        case 1: save.Hero = reader.FloatList() ?? save.Hero; break;
                        case 2: save.Account = reader.FloatList() ?? save.Account; break;
                        default: reader.Skip(); break;
                    }
                }
            });
        }

        /// <summary>Gives a unit a base of its own and four stages of modifiers it holds itself, two of each so that the totals are sums.</summary>
        private static void Dress(Unit unit, Stat stat, float own)
        {
            unit.SetBase(stat, own);
            unit.Modify(stat, 4.0f, heldBy: unit);
            unit.Modify(stat, 6.0f, heldBy: unit);
            unit.Modify(stat, heldBy: unit, percent: 20.0f);
            unit.Modify(stat, heldBy: unit, percent: 10.0f);
            unit.Modify(stat, heldBy: unit, times: 1.5f);
            unit.Modify(stat, heldBy: unit, times: 2.0f);
            unit.Modify(stat, heldBy: unit, late: 3.0f);
            unit.Modify(stat, heldBy: unit, late: 2.0f);
        }

        /// <summary>A unit's own bases and the totals of its own modifiers, each stage, come back on another unit, for an own stat and a game stat; what an item or a buff holds is left out, and so is a base the unit's type gives.</summary>
        public static void RoundTripUnit(FakeWorld world)
        {
            var rage = Own("Save rage", 10.0f);
            var skill = Own("Save skill", 2.0f);
            skill.SetBase(Id("hfoo"), 7.0f);
            var set = new StatSet(rage, skill, Stat.MaxLife);
            var hero = Footman(world);
            world.SetMaxLife(hero.Handle, 500.0f);
            Dress(hero, rage, 30.0f);
            hero.SetBase(Stat.MaxLife, 800.0f);
            hero.Modify(Stat.MaxLife, 20.0f, heldBy: hero);
            hero.Modify(Stat.MaxLife, heldBy: hero, percent: 10.0f);
            var boots = Boots();
            hero.Give(boots);
            hero.Modify(rage, 100.0f, heldBy: boots.Carry);
            hero.Apply(new Aid(rage, 50.0f));
            Expect.Near(746.0f, hero.Get(rage), "the whole total: (30 + 10 + 100 + 50) x 1.3 x 3 + 5");

            var saved = set.Save(hero);
            Expect.Equal(18, saved.Count, "six numbers for each stat");
            var expected = new float[] { 1.0f, 30.0f, 10.0f, 30.0f, 3.0f, 5.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 800.0f, 20.0f, 10.0f, 1.0f, 0.0f };
            for (var i = 0; i < expected.Length; i++)
            {
                Expect.Near(expected[i], saved[i], "number " + i);
            }

            var other = Footman(world);
            world.SetMaxLife(other.Handle, 500.0f);
            set.Restore(other, saved);
            Expect.Near(30.0f, other.GetBase(rage), "the base");
            Expect.Near(161.0f, other.Get(rage), "(30 + 10) x 1.3 x 3 + 5, the item's and the buff's left out");
            Expect.Equal(1, other.Modifiers(rage).Count, "one modifier for the stat, held by the unit");
            Expect.Same(other, other.Modifiers(rage)[0].Parent, "held by the unit");
            Expect.Near(7.0f, other.Get(skill), "no base of its own: the type's base stays");
            Expect.Equal(0, other.Modifiers(skill).Count, "and no modifier for no change");
            Expect.Near(800.0f, other.GetBase(Stat.MaxLife), "a game stat's base");
            Expect.Near(902.0f, other.Get(Stat.MaxLife), "and its total");
            Expect.Near(902.0f, world.MaxLifeOf(other.Handle), "reaches the game");
            Expect.Near(746.0f, hero.Get(rage), "the saved unit is unchanged");
        }

        /// <summary>A player's own bases and modifiers come back on another player; a game stat in the set is a unit's, so it keeps nothing and restores nothing, without a report.</summary>
        public static void RoundTripPlayer(FakeWorld world)
        {
            var rage = Own("Save player rage", 10.0f);
            var set = new StatSet(rage, Stat.MaxLife);
            Player.One.SetBase(rage, 50.0f);
            Player.One.Modify(rage, 5.0f, heldBy: Player.One);
            Player.One.Modify(rage, heldBy: Player.One, percent: 100.0f);
            Player.One.Modify(rage, heldBy: Player.One, times: 0.5f);
            Player.One.Modify(rage, heldBy: Player.One, late: -4.0f);
            var holder = Game.Instance.Add(new LifeHolder());
            Player.One.Modify(rage, 1000.0f, heldBy: holder);
            var saved = set.Save(Player.One);
            Expect.Equal(12, saved.Count, "six numbers for each stat");
            Expect.Near(0.0f, saved[6], "a game stat has no base on a player");
            Expect.Near(1.0f, saved[10], "and no change");
            var printed = world.Printed.Count;
            set.Restore(Player.Two, saved);
            Expect.Equal(printed, world.Printed.Count, "nothing reported");
            Expect.Near(50.0f, Player.Two.GetBase(rage), "the base");
            Expect.Near(51.0f, Player.Two.Get(rage), "(50 + 5) x 2 x 0.5 - 4, what the other holder holds left out");
            Expect.Equal(1, Player.Two.Modifiers(rage).Count, "one modifier");
            Expect.Near(10.0f, Player.Three.Get(rage), "another player is untouched");
        }

        /// <summary>A game component that holds modifiers, ended by the case.</summary>
        private sealed class LifeHolder : GameComponent
        {
        }

        /// <summary>Restoring twice gives the same stat, not twice the modifiers; a second list with no change takes the first one's modifier off; a base the list does not have is left alone.</summary>
        public static void RestoreTwice(FakeWorld world)
        {
            var rage = Own("Twice rage", 10.0f);
            var set = new StatSet(rage);
            var hero = Footman(world);
            Dress(hero, rage, 30.0f);
            var saved = set.Save(hero);
            var other = Footman(world);
            set.Restore(other, saved);
            set.Restore(other, saved);
            Expect.Near(161.0f, other.Get(rage), "not doubled");
            Expect.Equal(1, other.Modifiers(rage).Count, "one modifier");
            var again = set.Save(other);
            for (var i = 0; i < saved.Count; i++)
            {
                Expect.Near(saved[i], again[i], "saved again, number " + i);
            }

            other.Modify(rage, 1.0f, heldBy: other);
            set.Restore(other, saved);
            Expect.Equal(2, other.Modifiers(rage).Count, "a modifier the map made itself stays");
            var calm = Footman(world);
            calm.Modify(rage, 3.0f, heldBy: calm);
            set.Restore(calm, saved);
            set.Restore(calm, set.Save(Footman(world)));
            Expect.Equal(1, calm.Modifiers(rage).Count, "a list with no change takes the restored modifier off and leaves the map's own");
            Expect.Near(33.0f, calm.Get(rage), "the base the first list set, which a list with none leaves, and the 3");
            var based = Footman(world);
            based.SetBase(rage, 99.0f);
            set.Restore(based, set.Save(Footman(world)));
            Expect.Near(99.0f, based.GetBase(rage), "a list with no base of its own leaves the unit's");
        }

        /// <summary>A list from before a stat was added to the end restores the stats it has; a longer list and a damaged one restore what fits; each is reported in a development build, and none throws; a null list does nothing.</summary>
        public static void ShortAndLong(FakeWorld world)
        {
            var first = Own("Fit first", 1.0f);
            var second = Own("Fit second", 1.0f);
            var third = Own("Fit third", 1.0f);
            var old = new StatSet(first, second);
            var hero = Footman(world);
            hero.SetBase(first, 5.0f);
            hero.SetBase(second, 6.0f);
            var saved = old.Save(hero);

            var now = new StatSet(first, second, third);
            var short1 = Footman(world);
            short1.SetBase(third, 8.0f);
            var printed = world.Printed.Count;
            now.Restore(short1, saved);
#if DEBUG
            Expect.True(world.Printed.Count > printed, "a short list is reported in a development build");
#else
            Expect.Equal(printed, world.Printed.Count, "a release build reports nothing");
#endif
            Expect.Near(5.0f, short1.GetBase(first), "the first");
            Expect.Near(6.0f, short1.GetBase(second), "the second");
            Expect.Near(8.0f, short1.GetBase(third), "the stat after the list is left as it is");

            var fewer = new StatSet(first);
            var long1 = Footman(world);
            printed = world.Printed.Count;
            fewer.Restore(long1, saved);
#if DEBUG
            Expect.True(world.Printed.Count > printed, "a long list is reported in a development build");
#endif
            Expect.Near(5.0f, long1.GetBase(first), "what fits");
            Expect.Near(1.0f, long1.GetBase(second), "what the set does not name is not restored");

            var damaged = new List<float>(saved);
            damaged.Add(1.0f);
            var cut = Footman(world);
            printed = world.Printed.Count;
            old.Restore(cut, damaged);
#if DEBUG
            Expect.True(world.Printed.Count > printed, "a list with a number too many is reported");
#endif
            Expect.Near(5.0f, cut.GetBase(first), "the whole stats of it restore");
            Expect.Near(6.0f, cut.GetBase(second), "all of them");

            var none = Footman(world);
            old.Restore(none, new List<float>());
            old.Restore(none, null!);
            Expect.Near(1.0f, none.GetBase(first), "an empty or a null list does nothing");
        }

        /// <summary>The stats go through a real save class: written to a code and read back, then restored.</summary>
        public static void ThroughSave(FakeWorld world)
        {
            Register(world);
            var rage = Own("Code rage", 10.0f);
            var set = new StatSet(rage, Stat.MaxLife);
            var hero = Footman(world);
            world.SetMaxLife(hero.Handle, 500.0f);
            Dress(hero, rage, 30.0f);
            hero.SetBase(Stat.MaxLife, 640.0f);
            Player.One.SetBase(rage, 70.0f);
            Player.One.Modify(rage, 0.25f, heldBy: Player.One);
            var save = new StatSave { Hero = set.Save(hero), Account = set.Save(Player.One) };
            SaveGame.Save(Player.One, save);

            var read = SaveGame.FromCode<StatSave>(Player.One, save.Code);
            Expect.True(read != null, "the code reads back");
            Expect.Equal(save.Hero.Count, read!.Hero.Count, "the list's length");
            var other = Footman(world);
            world.SetMaxLife(other.Handle, 500.0f);
            set.Restore(other, read.Hero);
            set.Restore(Player.Two, read.Account);
            Expect.Near(161.0f, other.Get(rage), "the unit's stat");
            Expect.Near(640.0f, world.MaxLifeOf(other.Handle), "and its most life");
            Expect.Near(70.25f, Player.Two.Get(rage), "the player's stat");
        }

        /// <summary>A base by text: a four-letter id, or a declared class's name in any case (which reaches what inherits it, as SetBase of T does); text that is neither sets nothing and is reported in a development build.</summary>
        public static void BaseByText(FakeWorld world)
        {
            ObjectTypes.Unit(Id("H001"), () => new Striker());
            var stat = Own("Text base", 3.0f);
            var plain = Footman(world);
            var striker = Unit.Of(world.NewUnit(Id("H001")))!;
            stat.SetBase("hfoo", 5.0f);
            Expect.Near(5.0f, plain.GetBase(stat), "a four-letter id");
            Expect.Near(3.0f, striker.GetBase(stat), "another type is not touched");
            stat.SetBase("H001", 6.0f);
            Expect.Near(6.0f, striker.GetBase(stat), "the id of a declared class");
            stat.SetBase("Striker", 8.0f);
            Expect.Near(8.0f, striker.GetBase(stat), "a class's name, and a class's base comes before its type's");
            Expect.Near(5.0f, plain.GetBase(stat), "other classes are not touched");
            stat.SetBase("striker", 9.0f);
            Expect.Near(9.0f, striker.GetBase(stat), "in any case");
            var printed = world.Printed.Count;
            stat.SetBase("nobody at all", 1.0f);
            stat.SetBase("", 1.0f);
            Expect.Near(5.0f, plain.GetBase(stat), "unknown text sets nothing");
            Expect.Near(9.0f, striker.GetBase(stat), "for any unit");
#if DEBUG
            Expect.True(world.Printed.Count > printed, "and is reported in a development build");
#else
            Expect.Equal(printed, world.Printed.Count, "a release build reports nothing");
#endif
        }

        /// <summary>The guide's data recipe: one loop makes the stats from the rows of a stats file, a second sets the bases from the rows of a bases file, with rows made by hand as the build writes them.</summary>
        public static void DataRecipe(FakeWorld world)
        {
            ObjectTypes.Unit(Id("H001"), () => new Striker());
            var rage = "Recipe rage " + (++round);
            var dodge = "Recipe dodge " + (++round);
            var stats = new StatRow[] { new StatRow(rage, 0.0f, 0.0f, 100.0f, 1), new StatRow(dodge, 5.0f, 0.0f, 60.0f, 0) };
            var bases = new BaseRow[] { new BaseRow(20.0f, rage, "hfoo"), new BaseRow(35.0f, rage, "Striker"), new BaseRow(12.5f, dodge, "hfoo") };
            foreach (var row in stats) { new Stat(row.Name, row.Base, least: row.Least, most: row.Most, whole: row.Whole == 1); }
            foreach (var row in bases) { Stat.Named(row.Stat)!.SetBase(row.Unit, row.Value); }

            var plain = Footman(world);
            var striker = Unit.Of(world.NewUnit(Id("H001")))!;
            var rageStat = Stat.Named(rage)!;
            var dodgeStat = Stat.Named(dodge)!;
            Expect.Near(20.0f, plain.Get(rageStat), "a footman's rage, by its id");
            Expect.Near(35.0f, striker.Get(rageStat), "a Striker's, by its class's name");
            Expect.Near(12.5f, plain.Get(dodgeStat), "the second stat");
            Expect.Near(5.0f, striker.Get(dodgeStat), "and a unit it has no row for: the stat's own base");
            plain.Modify(rageStat, 0.4f, heldBy: plain);
            Expect.Near(20.0f, plain.Get(rageStat), "whole, from the row");
            plain.Modify(rageStat, 500.0f, heldBy: plain);
            Expect.Near(100.0f, plain.Get(rageStat), "the most, from the row");
            plain.Modify(dodgeStat, 500.0f, heldBy: plain);
            Expect.Near(60.0f, plain.Get(dodgeStat), "the most of the other");
        }
    }
}
