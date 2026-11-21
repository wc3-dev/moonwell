using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check stats and scores (decisions 221 and 222): how modifiers add up, how each game stat reaches the game, and what ends them.</summary>
    public static class StatCases
    {
        /// <summary>A game component that holds modifiers, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        private static int Id(string code) => ObjectTypes.FourCC(code);

        /// <summary>A map's own stat starts at its base; flat amounts add, percents add up and then multiply; End and the holder's end take a modifier off; units and players each have their own.</summary>
        public static void Own(FakeWorld world)
        {
            var power = new Stat("Spell power", 10.0f);
            var unit = Unit.Of(world.NewUnit(Id("hfoo")))!;
            var holder = Game.Instance.Add(new Holder());
            Expect.Near(10.0f, unit.Get(power), "the base");
            var flat = unit.Modify(power, 5.0f, heldBy: holder);
            Expect.Near(15.0f, unit.Get(power), "a flat amount");
            unit.Modify(power, percent: 20.0f, heldBy: holder);
            unit.Modify(power, percent: 30.0f, heldBy: unit);
            Expect.Near(22.5f, unit.Get(power), "percents add up, then multiply");
            flat.End();
            Expect.True(flat.HasEnded, "ended");
            Expect.Near(15.0f, unit.Get(power), "End takes it off");
            holder.End();
            Expect.Near(13.0f, unit.Get(power), "the holder's end takes its modifiers off");
            var player = Game.Instance.Add(new Holder());
            Player.One.Modify(power, -4.0f, heldBy: player);
            Expect.Near(6.0f, Player.One.Get(power), "a player's own, negative");
            Expect.Near(13.0f, unit.Get(power), "the unit's untouched");
        }

        /// <summary>A modifier for a game stat on a player is refused and reported, and one held by a scope that has ended adds nothing, reported in a development build only; each is given back ended.</summary>
        public static void Refused(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var before = world.Printed.Count;
            var armor = Player.One.Modify(Stat.Armor, 3.0f, heldBy: holder);
            Expect.True(armor.HasEnded, "a player's game stat is ended");
            Expect.True(world.Printed.Count > before, "and reported");
            holder.End();
            var unit = Unit.Of(world.NewUnit(Id("hfoo")))!;
            before = world.Printed.Count;
            var late = unit.Modify(new Stat("Crit"), 5.0f, heldBy: holder);
            Expect.True(late.HasEnded, "a modifier held by an ended scope is ended");
#if DEBUG
            Expect.True(world.Printed.Count > before, "and reported in a development build");
#else
            Expect.Equal(before, world.Printed.Count, "a release build reports nothing");
#endif
        }

        /// <summary>Damage, armor and attack speed set Moonwell's bonus abilities, given once and kept through morphs, so the card shows the green bonus; armor is read back from the game less Moonwell's part, and a total never goes below 0.</summary>
        public static void Green(FakeWorld world)
        {
            var handle = world.NewUnit(Id("hfoo"));
            var unit = Unit.Of(handle)!;
            world.SetAverageDamage(handle, 29.0f);
            world.SetArmor(handle, 4.0f);
            var holder = Game.Instance.Add(new Holder());
            unit.Modify(Stat.Damage, 100.0f, heldBy: holder);
            Expect.Equal(1, world.AbilityLevel(handle, Id("AMWd")), "the damage bonus ability given");
            Expect.Near(100.0f, world.BonusOf(handle, Id("AMWd")), "its field");
            Expect.Near(129.0f, unit.Get(Stat.Damage), "the damage");
            unit.Modify(Stat.Damage, percent: 20.0f, heldBy: holder);
            Expect.Near(126.0f, world.BonusOf(handle, Id("AMWd")), "a percent of the white and flat, as a whole number");
            unit.Modify(Stat.Armor, 10.0f, heldBy: holder);
            Expect.Near(14.0f, world.ArmorOf(handle), "the game's armor");
            unit.Modify(Stat.Armor, percent: 50.0f, heldBy: holder);
            Expect.Near(21.0f, unit.Get(Stat.Armor), "armor less Moonwell's part is the base");
            Expect.Near(17.0f, world.BonusOf(handle, Id("AMWa")), "the armor field");
            unit.Modify(Stat.AttackSpeed, 15.0f, heldBy: holder);
            Expect.Near(0.15f, world.BonusOf(handle, Id("AMWs")), "attack speed as the game's fraction");
            Expect.Near(15.0f, unit.Get(Stat.AttackSpeed), "attack speed in percent points");
            holder.End();
            Expect.Near(0.0f, world.BonusOf(handle, Id("AMWd")), "damage off");
            Expect.Near(4.0f, world.ArmorOf(handle), "armor back");
            Expect.Near(0.0f, world.BonusOf(handle, Id("AMWs")), "attack speed off");
            var cut = unit.Modify(Stat.Armor, -10.0f, heldBy: unit);
            Expect.Near(0.0f, unit.Get(Stat.Armor), "never below 0");
            Expect.Near(-4.0f, world.BonusOf(handle, Id("AMWa")), "a red bonus down to 0");
            cut.End();
            Expect.Near(4.0f, world.ArmorOf(handle), "back again");
        }

        /// <summary>Life, move speed, regeneration and attributes change the unit's own number, keeping what the game changed meanwhile, as a level's life; the most life never goes below 1.</summary>
        public static void Numbers(FakeWorld world)
        {
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetMaxLife(handle, 650.0f);
            world.SetMoveSpeed(handle, 300.0f);
            world.SetRegen(handle, false, 0.25f);
            world.SetStats(handle, 22, 13, 17);
            var holder = Game.Instance.Add(new Holder());
            var life = hero.Modify(Stat.MaxLife, 100.0f, heldBy: holder);
            Expect.Near(750.0f, world.MaxLifeOf(handle), "the most life");
            world.SetMaxLife(handle, 800.0f);
            Expect.Near(800.0f, hero.Get(Stat.MaxLife), "a level's 50 kept in the base");
            life.End();
            Expect.Near(700.0f, world.MaxLifeOf(handle), "taken off, the level's 50 kept");
            hero.Modify(Stat.MaxLife, -5000.0f, heldBy: holder);
            Expect.Near(1.0f, world.MaxLifeOf(handle), "never below 1");
            hero.Modify(Stat.MoveSpeed, percent: -50.0f, heldBy: holder);
            Expect.Near(150.0f, world.MoveSpeedOf(handle), "half of 300");
            hero.Modify(Stat.LifeRegen, 2.0f, heldBy: holder);
            Expect.Near(2.25f, world.RegenOf(handle, false), "the unit's own regeneration");
            hero.Modify(Stat.Strength, 10.0f, heldBy: holder);
            hero.Modify(Stat.Strength, percent: 10.0f, heldBy: holder);
            Expect.Equal(35, world.StrengthOf(handle, false), "(22 + 10) x 1.1, a whole number");
            world.SetMaxMana(handle, 255.0f);
            var mana = hero.Modify(Stat.MaxMana, percent: 10.0f, heldBy: holder);
            world.SetMaxMana(handle, world.MaxManaOf(handle) + 75.0f);
            Expect.Near(world.MaxManaOf(handle), hero.Get(Stat.MaxMana), "what the game added after the percent (intelligence's mana) read as the game shows it, unscaled (I52)");
            mana.End();
            holder.End();
            Expect.Near(700.0f, world.MaxLifeOf(handle), "life back");
            Expect.Near(300.0f, world.MoveSpeedOf(handle), "speed back");
            Expect.Near(0.25f, world.RegenOf(handle, false), "regeneration back");
            Expect.Equal(22, world.StrengthOf(handle, false), "strength back");
        }

        /// <summary>A buff whose OnApply gives its unit a flat modifier of its Strength, held by the buff: the guide's stat buff.</summary>
        private sealed class Might : Buff
        {
            private readonly Stat stat;

            public Might(Stat stat, float strength, Stacking stacking)
            {
                this.stat = stat;
                Strength = strength;
                Stacking = stacking;
            }

            public override void OnApply()
            {
                Unit.Modify(stat, Strength, heldBy: this);
            }
        }

        private static int round;

        private static Unit Footman(FakeWorld world) => Unit.Of(world.NewUnit(Id("hfoo")))!;

        /// <summary>Limits: a least and a most hold the total, whole rounds half up, both can be set afterwards, and the old forms of the constructor mean what they did.</summary>
        public static void Limits(FakeWorld world)
        {
            var unit = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            var plain = new Stat("Limit plain", 7.0f);
            Expect.True(plain.Least == null && plain.Most == null && !plain.Whole, "no limits unless given");
            Expect.Near(7.0f, unit.Get(plain), "the old form's base");
            Expect.Near(0.0f, unit.Get(new Stat("Limit bare")), "the oldest form's base is 0");
            var rage = new Stat("Limit rage", 10.0f, least: 0.0f, most: 100.0f, whole: true);
            Expect.Near(10.0f, unit.Get(rage), "the base");
            unit.Modify(rage, 500.0f, heldBy: holder);
            Expect.Near(100.0f, unit.Get(rage), "held to the most");
            unit.Modify(rage, -900.0f, heldBy: holder);
            Expect.Near(0.0f, unit.Get(rage), "held to the least");
            unit.Modify(rage, 400.5f, heldBy: holder);
            Expect.Near(11.0f, unit.Get(rage), "10 + 500 - 900 + 400.5 = 10.5, rounded half up");
            rage.Most = 5.0f;
            Expect.Near(5.0f, unit.Get(rage), "a most set afterwards");
            rage.Most = null;
            rage.Least = 20.0f;
            Expect.Near(20.0f, unit.Get(rage), "a least set afterwards");
            var ratio = new Stat("Limit ratio", 1.0f);
            ratio.Whole = true;
            unit.Modify(ratio, 0.5f, heldBy: holder);
            Expect.Near(2.0f, unit.Get(ratio), "1.5 rounds up");
            unit.Modify(ratio, -2.0f, heldBy: holder);
            Expect.Near(0.0f, unit.Get(ratio), "-0.5 rounds up to 0");
        }

        /// <summary>CappedBy makes another stat's total on the same holder the most, gives the stat back, follows that stat as it changes, and refuses a loop.</summary>
        public static void CappedBy(FakeWorld world)
        {
            var unit = Footman(world);
            var other = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            var most = new Stat("Cap most energy", 100.0f);
            var energy = new Stat("Cap energy", 0.0f, least: 0.0f);
            Expect.Same(energy, energy.CappedBy(most), "the stat is given back");
            var gain = unit.Modify(energy, 150.0f, heldBy: holder);
            Expect.Near(100.0f, unit.Get(energy), "held to the other stat");
            var more = unit.Modify(most, 50.0f, heldBy: holder);
            Expect.Near(150.0f, unit.Get(energy), "follows it up");
            more.End();
            Expect.Near(100.0f, unit.Get(energy), "and down");
            unit.SetBase(most, 40.0f);
            Expect.Near(40.0f, unit.Get(energy), "and its base");
            Expect.Near(0.0f, other.Get(energy), "the other unit's own");
            gain.End();
            var before = world.Printed.Count;
            most.CappedBy(energy);
            Expect.True(world.Printed.Count > before, "a loop is refused and reported");
            Expect.Near(0.0f, unit.Get(energy), "and left out");
        }

        /// <summary>Stat.Named finds the stat made with a name, the first made when two share one, and nothing for an unknown name; a second stat with a name in use is reported in a development build.</summary>
        public static void Named(FakeWorld world)
        {
            // A name of this run's own: the cases run more than once in a process, and the first stat made with a name is the one found.
            var name = "Named rage " + (++round);
            var first = new Stat(name);
            Expect.Same(first, Stat.Named(name), "found by name");
            Expect.True(Stat.Named("Named nothing at all") == null, "an unknown name finds nothing");
            var before = world.Printed.Count;
            var second = new Stat(name);
            Expect.Same(first, Stat.Named(name), "the first made wins");
            Expect.True(second != first, "the second is a stat of its own");
#if DEBUG
            Expect.True(world.Printed.Count > before, "and a second of the name is reported in a development build");
#else
            Expect.Equal(before, world.Printed.Count, "a release build reports nothing");
#endif
            Expect.Same(Stat.Armor, Stat.Named("Armor"), "a game stat is found once made");
        }

        /// <summary>A unit's own base comes before its class's, its class's before its type's, and its type's before the stat's; AddBase adds to the first found; a player takes its own and the stat's; a game stat's base is the game's number until one is set, then Moonwell's, and a player's is refused.</summary>
        public static void Bases(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H001"), () => new Striker());
            var stat = new Stat("Bases skill", 3.0f);
            var plain = Footman(world);
            var striker = Unit.Of(world.NewUnit(Id("H001")))!;
            var other = Unit.Of(world.NewUnit(Id("H001")))!;
            Expect.Near(3.0f, plain.GetBase(stat), "the stat's own base");
            stat.SetBase(Id("hfoo"), 5.0f);
            Expect.Near(5.0f, plain.GetBase(stat), "a type's base beats the stat's");
            Expect.Near(3.0f, striker.GetBase(stat), "another type is not touched");
            stat.SetBase(Id("H001"), 6.0f);
            Expect.Near(6.0f, striker.GetBase(stat), "the type of a declared class");
            stat.SetBase<Striker>(8.0f);
            Expect.Near(8.0f, striker.GetBase(stat), "a class's base beats its type's");
            Expect.Near(5.0f, plain.GetBase(stat), "other classes are not touched");
            striker.SetBase(stat, 9.0f);
            Expect.Near(9.0f, striker.GetBase(stat), "a unit's own beats them all");
            Expect.Near(8.0f, other.GetBase(stat), "the other unit keeps its class's");
            striker.AddBase(stat, 2.0f);
            Expect.Near(11.0f, striker.GetBase(stat), "AddBase adds to its own");
            other.AddBase(stat, 1.0f);
            Expect.Near(9.0f, other.GetBase(stat), "and to the class's, as a base of its own");
            Expect.Near(8.0f, Unit.Of(world.NewUnit(Id("H001")))!.GetBase(stat), "a new unit of the class starts at the class's");
            var holder = Game.Instance.Add(new Holder());
            striker.Modify(stat, 4.0f, heldBy: holder);
            Expect.Near(15.0f, striker.Get(stat), "modifiers work on the base");
            Expect.Near(4.0f, striker.GetBonus(stat), "the bonus is the total less the base");
            Player.One.AddBase(stat, 10.0f);
            Expect.Near(13.0f, Player.One.GetBase(stat), "a player's base starts at the stat's");
            Expect.Near(3.0f, Player.Two.GetBase(stat), "and is its own");
            Player.One.SetBase(stat, 1.0f);
            Expect.Near(1.0f, Player.One.Get(stat), "set");
            var handle = world.NewUnit(Id("hfoo"));
            var unit = Unit.Of(handle)!;
            world.SetMaxLife(handle, 500.0f);
            Expect.Near(500.0f, unit.GetBase(Stat.MaxLife), "a game stat's base is the game's number until one is set");
            unit.SetBase(Stat.MaxLife, 800.0f);
            Expect.Near(800.0f, world.MaxLifeOf(handle), "a game stat's base is Moonwell's from then on, and the game gets it");
            Expect.Near(800.0f, unit.GetBase(Stat.MaxLife), "and is the base");
            unit.AddBase(Stat.MaxLife, 5.0f);
            Expect.Near(805.0f, world.MaxLifeOf(handle), "an AddBase adds to it");
            var before = world.Printed.Count;
            Player.One.SetBase(Stat.MaxLife, 800.0f);
            Expect.True(world.Printed.Count > before, "a player's game stat base is refused and reported");
            before = world.Printed.Count;
            Player.One.AddBase(Stat.MaxLife, 5.0f);
            Expect.True(world.Printed.Count > before, "and an AddBase");
        }

        /// <summary>The four stages: (base + flat) x (1 + percents / 100) x each multiplier + late flat, then the limits.</summary>
        public static void Stages(FakeWorld world)
        {
            var unit = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            var stat = new Stat("Stages number", 100.0f);
            unit.Modify(stat, 20.0f, heldBy: holder);
            Expect.Near(120.0f, unit.Get(stat), "flat");
            unit.Modify(stat, holder, percent: 50.0f);
            unit.Modify(stat, holder, percent: 50.0f);
            Expect.Near(240.0f, unit.Get(stat), "percents add up");
            unit.Modify(stat, holder, times: 1.5f);
            Expect.Near(360.0f, unit.Get(stat), "a multiplier");
            var late = unit.Modify(stat, holder, late: 10.0f);
            Expect.Near(370.0f, unit.Get(stat), "late flat, outside the percents");
            unit.Modify(stat, holder, times: 2.0f);
            Expect.Near(730.0f, unit.Get(stat), "multipliers multiply each other: 720 + 10");
            Expect.Near(1.0f, late.Times, "a modifier of one number leaves the others at no change");
            Expect.Near(0.0f, late.Amount + late.Percent, "and at 0");
            var bounded = new Stat("Stages bounded", 100.0f, most: 300.0f);
            unit.Modify(bounded, holder, percent: 100.0f, late: 50.0f);
            Expect.Near(250.0f, unit.Get(bounded), "one modifier with two numbers");
            unit.Modify(bounded, holder, late: 100.0f);
            Expect.Near(300.0f, unit.Get(bounded), "the limit holds the late flat too");
            var player = Game.Instance.Add(new Holder());
            Player.One.Modify(stat, player, times: 0.5f);
            Expect.Near(50.0f, Player.One.Get(stat), "a player's stages");
        }

        /// <summary>A game stat uses the same formula, keeps its floors, and is pushed to the game; its base is the game's number.</summary>
        public static void GameStages(FakeWorld world)
        {
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetMaxLife(handle, 600.0f);
            var holder = Game.Instance.Add(new Holder());
            hero.Modify(Stat.MaxLife, 100.0f, heldBy: holder);
            hero.Modify(Stat.MaxLife, holder, percent: 10.0f, times: 2.0f, late: 5.0f);
            Expect.Near(1545.0f, world.MaxLifeOf(handle), "(600 + 100) x 1.1 x 2 + 5 in the game");
            Expect.Near(1545.0f, hero.Get(Stat.MaxLife), "and read back");
            Expect.Near(600.0f, hero.GetBase(Stat.MaxLife), "the base is the game's number");
            Expect.Near(945.0f, hero.GetBonus(Stat.MaxLife), "the bonus is what Moonwell added");
            world.SetRegen(handle, false, 0.25f);
            hero.Modify(Stat.LifeRegen, holder, times: 4.0f, late: 1.0f);
            Expect.Near(2.0f, world.RegenOf(handle, false), "0.25 x 4 + 1: the late flat comes after the multiplier");
            hero.Modify(Stat.LifeRegen, -10.0f, heldBy: holder);
            Expect.Near(0.0f, world.RegenOf(handle, false), "never below 0");
            hero.Modify(Stat.MaxLife, holder, late: -5000.0f);
            Expect.Near(1.0f, world.MaxLifeOf(handle), "the most life never below 1");
        }

        /// <summary>A modifier's numbers can be set after it is made, and the stat follows; a game stat is pushed again; an ended modifier takes no set.</summary>
        public static void Change(FakeWorld world)
        {
            var unit = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            var stat = new Stat("Change number", 10.0f);
            var rank = unit.Modify(stat, 5.0f, heldBy: holder);
            rank.Amount = 8.0f;
            Expect.Near(18.0f, unit.Get(stat), "the amount");
            rank.Percent = 100.0f;
            Expect.Near(36.0f, unit.Get(stat), "the percent");
            rank.Times = 0.5f;
            Expect.Near(18.0f, unit.Get(stat), "the multiplier");
            rank.Late = 2.0f;
            Expect.Near(20.0f, unit.Get(stat), "the late flat");
            Expect.Near(8.0f, rank.Amount, "read back");
            var handle = world.NewUnit(Id("hfoo"));
            var footman = Unit.Of(handle)!;
            world.SetArmor(handle, 4.0f);
            var armor = footman.Modify(Stat.Armor, 3.0f, heldBy: holder);
            armor.Amount = 6.0f;
            Expect.Near(10.0f, world.ArmorOf(handle), "a game stat is put in the game again");
            rank.End();
            rank.Amount = 100.0f;
            rank.Percent = 100.0f;
            Expect.Near(10.0f, unit.Get(stat), "a set on an ended modifier does nothing");
            Expect.Near(8.0f, rank.Amount, "and it keeps its numbers");
        }

        /// <summary>Strongest: only the modifier farthest from no change counts, the next takes over when it ends, and groups, holders and ungrouped modifiers do not mix.</summary>
        public static void StackingStrongest(FakeWorld world)
        {
            var unit = Footman(world);
            var other = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            var stat = new Stat("Strongest number", 0.0f);
            var weak = unit.Modify(stat, 5.0f, heldBy: holder, group: "ring");
            var strong = unit.Modify(stat, 8.0f, heldBy: holder, group: "ring");
            var weaker = unit.Modify(stat, 3.0f, heldBy: holder, group: "ring");
            Expect.Near(8.0f, unit.Get(stat), "two rings give the strongest");
            Expect.True(!weak.Counts && strong.Counts && !weaker.Counts, "Counts says which");
            unit.Modify(stat, 2.0f, heldBy: holder);
            Expect.Near(10.0f, unit.Get(stat), "a modifier with no group adds up");
            other.Modify(stat, 1.0f, heldBy: holder, group: "ring");
            Expect.Near(10.0f, unit.Get(stat), "another unit's is its own");
            unit.Modify(stat, 4.0f, heldBy: holder, group: "boots");
            Expect.Near(14.0f, unit.Get(stat), "another group is its own");
            strong.End();
            Expect.Near(11.0f, unit.Get(stat), "when the strongest ends the next counts");
            Expect.True(weak.Counts && !weaker.Counts, "the next, not all");
            weaker.Amount = 9.0f;
            Expect.Near(15.0f, unit.Get(stat), "a set can make another the strongest");
            var tie = unit.Modify(stat, 9.0f, heldBy: holder, group: "ring");
            Expect.True(weaker.Counts && !tie.Counts, "on a tie the older stays");
            var curse = unit.Modify(stat, -20.0f, heldBy: holder, group: "ring");
            Expect.True(curse.Counts && !weaker.Counts, "farthest from no change, whichever way");
            var key = new object();
            var one = unit.Modify(stat, holder, percent: 10.0f, group: key);
            var two = unit.Modify(stat, holder, percent: 25.0f, group: key);
            Expect.True(!one.Counts && two.Counts, "a group can be any object, and percents compare");
            var slow = unit.Modify(stat, holder, times: 0.5f, group: "speed");
            var fast = unit.Modify(stat, holder, times: 1.25f, group: "speed");
            Expect.True(slow.Counts && !fast.Counts, "multipliers compare as how far they are from 1");
            var many = unit.Modify(stat, holder, late: 7.0f, group: "mixed");
            var few = unit.Modify(stat, holder, percent: 5.0f, late: 3.0f, group: "mixed");
            Expect.True(!many.Counts && few.Counts, "several numbers are added by size: 7 against 5 + 3, so the second wins");
        }

        /// <summary>Refresh ends the older ones of the group, PerSource only those of the same source, Separate and no group add up, and a rule with no group is reported in a development build.</summary>
        public static void StackingRules(FakeWorld world)
        {
            var unit = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            var one = Footman(world);
            var two = Footman(world);
            var stat = new Stat("Rules number", 0.0f);
            var old = unit.Modify(stat, 5.0f, heldBy: holder, group: "aura", stacking: Stacking.Refresh);
            var fresh = unit.Modify(stat, 2.0f, heldBy: holder, group: "aura", stacking: Stacking.Refresh);
            Expect.True(old.HasEnded && !fresh.HasEnded, "a new one ends the older");
            Expect.Near(2.0f, unit.Get(stat), "even when weaker");
            Expect.Equal(1, unit.Modifiers(stat).Count, "one is left");
            fresh.End();
            var a = unit.Modify(stat, 5.0f, heldBy: holder, group: "bleed", stacking: Stacking.PerSource, source: one);
            var b = unit.Modify(stat, 6.0f, heldBy: holder, group: "bleed", stacking: Stacking.PerSource, source: two);
            Expect.Near(11.0f, unit.Get(stat), "each source has its own");
            Expect.Same(one, a.Source, "the source is kept");
            Expect.Equal("bleed", (string)a.Group!, "and the group");
            var a2 = unit.Modify(stat, 7.0f, heldBy: holder, group: "bleed", stacking: Stacking.PerSource, source: one);
            Expect.True(a.HasEnded && !b.HasEnded && !a2.HasEnded, "a new one ends the older of its source only");
            Expect.Near(13.0f, unit.Get(stat), "7 and 6");
            var sep1 = unit.Modify(stat, 1.0f, heldBy: holder, group: "plain", stacking: Stacking.Separate);
            var sep2 = unit.Modify(stat, 1.0f, heldBy: holder, group: "plain", stacking: Stacking.Separate);
            Expect.True(sep1.Counts && sep2.Counts, "Separate adds up");
            Expect.Near(15.0f, unit.Get(stat), "each counted");
            var before = world.Printed.Count;
            var loose1 = unit.Modify(stat, 1.0f, heldBy: holder, stacking: Stacking.Refresh);
            var loose2 = unit.Modify(stat, 1.0f, heldBy: holder, stacking: Stacking.Strongest);
            Expect.True(!loose1.HasEnded && loose2.Counts && loose1.Counts, "a rule with no group is ignored");
            Expect.Near(17.0f, unit.Get(stat), "and adds up");
#if DEBUG
            Expect.True(world.Printed.Count > before, "and reported in a development build");
#else
            Expect.Equal(before, world.Printed.Count, "a release build reports nothing");
#endif
            Expect.True(loose1.Group == null && loose1.Source == null, "no group and no source by default");
        }

        /// <summary>GetBonus is the total less the base, and Modifiers gives a copy of the live modifiers that is safe to end while looping.</summary>
        public static void Reading(FakeWorld world)
        {
            var unit = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            var stat = new Stat("Reading number", 10.0f);
            Expect.Equal(0, unit.Modifiers(stat).Count, "none to start");
            unit.Modify(stat, 5.0f, heldBy: holder);
            unit.Modify(stat, holder, percent: 100.0f);
            unit.Modify(stat, holder, times: 2.0f);
            Expect.Near(10.0f, unit.GetBase(stat), "the base");
            Expect.Near(50.0f, unit.GetBonus(stat), "the total less the base: (10 + 5) x 2 x 2 = 60");
            var found = unit.Modifiers(stat);
            Expect.Equal(3, found.Count, "the live modifiers");
            Expect.Near(5.0f, found[0].Amount, "in the order they were made");
            Expect.Same(holder, found[0].Parent, "with their holder as Parent");
            for (var i = 0; i < found.Count; i++)
            {
                found[i].End();
            }

            Expect.Near(10.0f, unit.Get(stat), "ending them in the loop worked");
            Expect.Equal(0, unit.Modifiers(stat).Count, "and none are left");
            Expect.Equal(3, found.Count, "the copy stayed as it was");
            Player.One.Modify(stat, 4.0f, heldBy: holder);
            Expect.Equal(1, Player.One.Modifiers(stat).Count, "a player's");
            Expect.Near(4.0f, Player.One.GetBonus(stat), "and its bonus");
            Expect.Equal(0, Player.Two.Modifiers(stat).Count, "of its own");
        }

        /// <summary>The ready-made stats have their names, bases and limits; Resistance is one stat for each kind of damage.</summary>
        public static void ReadyMade(FakeWorld world)
        {
            var unit = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            Expect.Equal("Crit chance", Stat.CritChance.Name, "a plain display name");
            Expect.Equal("Mana cost reduction", Stat.ManaCostReduction.Name, "of several words");
            Expect.Near(200.0f, unit.Get(Stat.CritDamage), "crit damage starts at 200");
            Expect.Near(100.0f, unit.Get(Stat.HitChance), "hit chance at 100");
            Expect.Near(4.0f, unit.Get(Stat.StaggerTime), "stagger time at 4");
            Expect.Near(0.0f, unit.Get(Stat.AttackPower), "the rest at 0");
            unit.Modify(Stat.CritChance, 130.0f, heldBy: holder);
            Expect.Near(100.0f, unit.Get(Stat.CritChance), "crit chance holds to 100");
            unit.Modify(Stat.Evasion, -30.0f, heldBy: holder);
            Expect.Near(0.0f, unit.Get(Stat.Evasion), "evasion holds to 0");
            unit.Modify(Stat.CastSpeed, -300.0f, heldBy: holder);
            Expect.Near(-90.0f, unit.Get(Stat.CastSpeed), "cast speed holds to -90");
            unit.Modify(Stat.GoldBonus, -300.0f, heldBy: holder);
            Expect.Near(-100.0f, unit.Get(Stat.GoldBonus), "gold bonus holds to -100");
            unit.Modify(Stat.ItemLevel, 0.5f, heldBy: holder);
            Expect.Near(1.0f, unit.Get(Stat.ItemLevel), "item level is whole");
            Expect.True(Stat.CooldownReduction.Most == 100.0f && Stat.CooldownReduction.Least == 0.0f, "limits are readable");
            Expect.True(Stat.AttackPower.Least == null && !Stat.CritChance.Whole, "and none where there are none");
            Expect.Same(Stat.CritChance, Stat.CritChance, "the same object each time");
            var fire = Stat.Resistance(DamageKind.Fire);
            Expect.Same(fire, Stat.Resistance(DamageKind.Fire), "one resistance for each kind, the same object");
            Expect.True(fire != Stat.Resistance(DamageKind.Cold), "a stat for each kind");
            Expect.Equal("Fire resistance", fire.Name, "named by its kind");
            unit.Modify(fire, 160.0f, heldBy: holder);
            Expect.Near(100.0f, unit.Get(fire), "resistance holds to 100");
            Expect.Near(0.0f, Player.One.Get(fire), "a player's own, at 0");
        }

        /// <summary>A buff that gives a modifier in OnApply does not double it when it is applied again: a refresh ends the modifiers its last OnApply made first.</summary>
        public static void BuffRefresh(FakeWorld world)
        {
            var unit = Footman(world);
            var stat = new Stat("Refresh number", 10.0f);
            var buff = unit.Apply(new Might(stat, 5.0f, Stacking.Refresh));
            Expect.Near(15.0f, unit.Get(stat), "the buff's modifier");
            var again = unit.Apply(new Might(stat, 5.0f, Stacking.Refresh));
            Expect.Same(buff, again, "the same buff refreshed");
            Expect.Near(15.0f, unit.Get(stat), "not doubled by a refresh");
            Expect.Equal(1, unit.Modifiers(stat).Count, "one modifier");
            unit.Modify(stat, 1.0f, heldBy: unit);
            unit.Apply(new Might(stat, 5.0f, Stacking.Refresh));
            Expect.Near(16.0f, unit.Get(stat), "a modifier the buff did not make in OnApply stays");
            buff.End();
            Expect.Near(11.0f, unit.Get(stat), "ending the buff takes its own off");
        }

        /// <summary>A strongest buff runs OnApply again at a new strongest, and its modifier follows the new strength instead of adding to the old one.</summary>
        public static void BuffStrongest(FakeWorld world)
        {
            var unit = Footman(world);
            var first = Game.Instance.Add(new Holder());
            var second = Game.Instance.Add(new Holder());
            var stat = new Stat("Strongest buff number", 10.0f);
            unit.Apply(new Might(stat, 5.0f, Stacking.Strongest), heldBy: first);
            Expect.Near(15.0f, unit.Get(stat), "the first apply");
            unit.Apply(new Might(stat, 8.0f, Stacking.Strongest), heldBy: second);
            Expect.Near(18.0f, unit.Get(stat), "the stronger one replaces it, it does not add");
            second.End();
            Expect.Near(15.0f, unit.Get(stat), "and when it lets go the first's strength is back");
            Expect.Equal(1, unit.Modifiers(stat).Count, "with one modifier");
        }

        /// <summary>What a change reaction heard: which reaction, the stat, the total before, and the total read inside the reaction.</summary>
        private sealed class Heard
        {
            public string Where = "";

            public Stat? Stat;

            public float Before;

            public float Now;
        }

        private static List<Heard>? log;

        private static int At(Stat stat)
        {
            for (var i = log!.Count - 1; i >= 0; i--)
            {
                if (log[i].Stat == stat)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void Note(string where, Stat stat, float before, float now)
        {
            var heard = new Heard();
            heard.Where = where;
            heard.Stat = stat;
            heard.Before = before;
            heard.Now = now;
            log!.Add(heard);
        }

        /// <summary>A unit class that hears its own stats change.</summary>
        private sealed class Warden : Unit
        {
            public override void OnStatChanged(Stat stat, float before)
            {
                Note("unit", stat, before, Get(stat));
            }
        }

        /// <summary>A unit component that hears the unit's stats change.</summary>
        private sealed class UnitWatch : UnitComponent
        {
            public override void OnStatChanged(Stat stat, float before)
            {
                Note("unit component", stat, before, Unit.Get(stat));
            }
        }

        /// <summary>A game component that hears every unit's and player's stats change.</summary>
        private sealed class GameWatch : GameComponent
        {
            public override void OnUnitStatChanged(Unit unit, Stat stat, float before)
            {
                Note("game unit", stat, before, unit.Get(stat));
            }

            public override void OnPlayerStatChanged(Player player, Stat stat, float before)
            {
                Note("game player", stat, before, player.Get(stat));
            }
        }

        /// <summary>A player component that hears the stats of its player and its player's units change.</summary>
        private sealed class PlayerWatch : PlayerComponent
        {
            public override void OnUnitStatChanged(Unit unit, Stat stat, float before)
            {
                Note("player unit", stat, before, unit.Get(stat));
            }

            public override void OnPlayerStatChanged(Player player, Stat stat, float before)
            {
                Note("player player", stat, before, player.Get(stat));
            }
        }

        /// <summary>A game component whose reaction changes the stat again, every time.</summary>
        private sealed class Runaway : GameComponent
        {
            public int Calls;

            public int Stop = 1000;

            public override void OnUnitStatChanged(Unit unit, Stat stat, float before)
            {
                Calls++;
                if (Calls < Stop)
                {
                    unit.Modify(stat, 1.0f, heldBy: this);
                }
            }
        }

        /// <summary>A unit component that reads the game's most life when the hero's level-up reaches it.</summary>
        private sealed class LevelSpy : UnitComponent
        {
            public FakeWorld? World;

            public float Seen;

            public override void OnLevelUp(int level)
            {
                Seen = World!.MaxLifeOf(Unit.Handle);
            }
        }

        /// <summary>The change reactions: on the unit, its components, the game's and the unit's player's components, in that order, once for each change with the total before, none when the total did not change, and a player's own stats to the game's and its own components.</summary>
        public static void Reactions(FakeWorld world)
        {
            log = new List<Heard>();
            ObjectTypes.Unit(Id("H0W1"), () => new Warden());
            var unit = Unit.Of(world.NewUnit(Id("H0W1")))!;
            var holder = Game.Instance.Add(new Holder());
            unit.Add(new UnitWatch());
            Game.Instance.Add(new GameWatch());
            unit.Owner.Add(new PlayerWatch());
            var rage = new Stat("Reaction rage", 10.0f, most: 100.0f);
            var plus = unit.Modify(rage, 5.0f, heldBy: holder);
            Expect.Equal(4, log.Count, "four reactions heard the change");
            Expect.Equal("unit", log[0].Where, "the unit first");
            Expect.Equal("unit component", log[1].Where, "its components");
            Expect.Equal("game unit", log[2].Where, "the game's components");
            Expect.Equal("player unit", log[3].Where, "the unit's player's components");
            Expect.True(log[0].Stat == rage, "the stat");
            Expect.Near(10.0f, log[0].Before, "the total before");
            Expect.Near(15.0f, log[0].Now, "the total is the new one inside the reaction");
            Expect.Near(10.0f, log[3].Before, "every reaction has the same before");
            unit.Modify(rage, 500.0f, heldBy: holder);
            Expect.Equal(8, log.Count, "a change held by the limit still changes the total");
            Expect.Near(15.0f, log[4].Before, "from 15");
            Expect.Near(100.0f, log[4].Now, "to the most");
            unit.Modify(rage, 50.0f, heldBy: holder);
            Expect.Equal(8, log.Count, "no call when a limit holds the total where it was");
            plus.Amount = 7.0f;
            Expect.Equal(8, log.Count, "and none for a set that the limit swallows");
            var rank = new Stat("Reaction rank", 0.0f);
            unit.Modify(rank, 8.0f, heldBy: holder, group: "ring");
            Expect.Equal(12, log.Count, "a modifier of a group that counts");
            var weak = unit.Modify(rank, 3.0f, heldBy: holder, group: "ring");
            Expect.Equal(12, log.Count, "no call for a modifier a stacking rule leaves out");
            weak.Amount = 9.0f;
            Expect.Equal(16, log.Count, "a set that makes it the strongest");
            Expect.Near(8.0f, log[12].Before, "from 8");
            Expect.Near(9.0f, log[12].Now, "to 9");
            weak.End();
            Expect.Equal(20, log.Count, "ending it");
            Expect.Near(9.0f, log[16].Before, "from 9");
            Expect.Near(8.0f, log[16].Now, "back to 8");
            unit.SetBase(rank, 2.0f);
            Expect.Equal(24, log.Count, "a set of the base");
            Expect.Near(8.0f, log[20].Before, "from 8");
            Expect.Near(10.0f, log[20].Now, "to 10");
            var count = log.Count;
            rage.SetBase<Warden>(50.0f);
            rage.Most = 200.0f;
            rage.Least = 1.0f;
            Expect.Equal(count, log.Count, "a type's base and a stat's limits announce nothing");
            Player.One.Modify(rage, 4.0f, heldBy: holder);
            Expect.Equal(count + 2, log.Count, "a player's own stat goes to the game's components and its own");
            Expect.Equal("game player", log[count].Where, "the game's first");
            Expect.Equal("player player", log[count + 1].Where, "then the player's");
            Expect.Near(10.0f, log[count].Before, "the player's total before: the stat's base");
            Expect.Near(14.0f, log[count].Now, "and after");
            Expect.Near(10.0f, Player.Two.Get(rage), "another player is not touched");
        }

        /// <summary>A reaction that changes stats again is allowed; more than 16 nested changes are reported once and the innermost is not announced, and a chain of 16 runs whole.</summary>
        public static void Nested(FakeWorld world)
        {
            var unit = Footman(world);
            var under = Game.Instance.Add(new Runaway());
            under.Stop = 16;
            var steady = new Stat("Nested steady", 0.0f);
            var quiet = world.Printed.Count;
            unit.Modify(steady, 1.0f, heldBy: under);
            Expect.Equal(16, under.Calls, "a chain of 16 runs whole");
            Expect.Equal(quiet, world.Printed.Count, "and is not reported");
            under.End();
            var runaway = Game.Instance.Add(new Runaway());
            var stat = new Stat("Nested number", 0.0f);
            var one = world.Printed.Count;
            Errors.Refuse("Stat.Test", "one report " + (++round));
            one = world.Printed.Count - one;
            var before = world.Printed.Count;
            unit.Modify(stat, 1.0f, heldBy: runaway);
            Expect.Equal(16, runaway.Calls, "the 16th is the last announced");
            Expect.Equal(one, world.Printed.Count - before, "and the chain is reported once");
            Expect.Equal(17, unit.Modifiers(stat).Count, "every change still happened");
            Expect.Near(17.0f, unit.Get(stat), "and counts");
            runaway.End();
            Expect.Near(0.0f, unit.Get(stat), "ending the holder takes them all off");
        }

        /// <summary>From and Formula add to the base, add up, are kept current through a chain, announce each stat once in order with its total before, and a loop through any mix is refused when declared.</summary>
        public static void FromStats(FakeWorld world)
        {
            log = new List<Heard>();
            var unit = Footman(world);
            var holder = Game.Instance.Add(new Holder());
            Game.Instance.Add(new GameWatch());
            var a = new Stat("From a", 3.0f);
            var b = new Stat("From b", 0.0f);
            var c = new Stat("From c", 0.0f);
            var e = new Stat("From e", 0.0f);
            Expect.Same(b, b.From(a, 2.0f), "From gives the stat back");
            Expect.Same(b, b.Formula(read => read(a) + 1.0f, a), "and Formula");
            c.From(b, 10.0f);
            e.Formula(read => read(a) + read(b), a, b);
            Expect.Near(10.0f, unit.Get(b), "two formulas add up: 3 x 2 + (3 + 1)");
            Expect.Near(10.0f, unit.GetBase(b), "GetBase includes what they give");
            Expect.Near(0.0f, unit.GetBonus(b), "and the bonus does not");
            Expect.Near(100.0f, unit.Get(c), "a chain of two");
            Expect.Near(13.0f, unit.Get(e), "a stat reading two that read each other");
            Expect.Near(10.0f, Player.One.Get(b), "a player reads its own");
            unit.Modify(a, 2.0f, heldBy: holder);
            Expect.Near(16.0f, unit.Get(b), "5 x 2 + 6");
            Expect.Near(160.0f, unit.Get(c), "through the chain");
            Expect.Near(21.0f, unit.Get(e), "and the diamond");
            Expect.Equal(4, log.Count, "each stat announced once");
            Expect.True(log[0].Stat == a && log[1].Stat == b && log[2].Stat == c && log[3].Stat == e, "the changed stat first, then those that read it");
            Expect.Near(3.0f, log[0].Before, "a from 3");
            Expect.Near(10.0f, log[1].Before, "b from 10");
            Expect.Near(100.0f, log[2].Before, "c from 100");
            Expect.Near(13.0f, log[3].Before, "e from 13");
            Expect.Near(16.0f, log[1].Now, "b is new inside its reaction");
            Expect.Near(160.0f, log[2].Now, "and c");
            unit.Modify(b, holder, percent: 50.0f);
            Expect.Near(24.0f, unit.Get(b), "percents work on what they give: 16 x 1.5");
            Expect.Near(240.0f, unit.Get(c), "and the chain follows");
            var before = world.Printed.Count;
            a.From(c, 1.0f);
            Expect.True(world.Printed.Count > before, "a loop through From and From");
            before = world.Printed.Count;
            a.Formula(read => read(c), c);
            Expect.True(world.Printed.Count > before, "through Formula");
            before = world.Printed.Count;
            b.CappedBy(c);
            Expect.True(world.Printed.Count > before, "loops are refused and reported");
            var x = new Stat("From x", 0.0f);
            x.CappedBy(a);
            before = world.Printed.Count;
            a.From(x, 1.0f);
            Expect.True(world.Printed.Count > before, "a loop through a cap is refused too");
            Expect.Near(5.0f, unit.Get(a), "and a is left as it was");
            Expect.Near(240.0f, unit.Get(c), "so is c");
            unit.Modify(a, 1.0f, heldBy: holder);
            Expect.Near(6.0f, unit.Get(a), "the refused declarations did nothing");
            var capped = new Stat("From capped", 100.0f).CappedBy(a);
            Expect.Near(6.0f, unit.Get(capped), "a cap is read too");
            a.From(a, 1.0f);
            Expect.Near(6.0f, unit.Get(a), "a stat reading itself is refused");
        }

        /// <summary>A game stat reads a map's stat and keeps current as it changes, and a map's stat reads a game stat.</summary>
        public static void FromGame(FakeWorld world)
        {
            log = new List<Heard>();
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetMaxLife(handle, 650.0f);
            world.SetStats(handle, 22, 13, 17);
            var holder = Game.Instance.Add(new Holder());
            Game.Instance.Add(new GameWatch());
            var stamina = new Stat("From stamina", 0.0f);
            Stat.MaxLife.From(stamina, 10.0f);
            var power = new Stat("From power", 0.0f).From(Stat.Strength, 2.0f);
            Expect.Near(44.0f, hero.Get(power), "a map's stat reads a game stat: 22 x 2");
            hero.Modify(stamina, 5.0f, heldBy: holder);
            Expect.Near(700.0f, world.MaxLifeOf(handle), "a game stat reads a map's stat: 650 + 5 x 10, in the game");
            Expect.Near(700.0f, hero.Get(Stat.MaxLife), "and Get");
            Expect.Near(700.0f, hero.GetBase(Stat.MaxLife), "GetBase includes what it gives");
            Expect.Near(0.0f, hero.GetBonus(Stat.MaxLife), "the bonus does not");
            Expect.True(At(stamina) >= 0 && At(Stat.MaxLife) > At(stamina), "stamina and then the most life");
            Expect.Near(650.0f, log![At(Stat.MaxLife)].Before, "the most life announced with its total before");
            Expect.Near(700.0f, log[At(Stat.MaxLife)].Now, "and inside the reaction its new total");
            hero.Modify(Stat.MaxLife, 100.0f, heldBy: holder);
            hero.Modify(stamina, 5.0f, heldBy: holder);
            Expect.Near(850.0f, world.MaxLifeOf(handle), "the second change follows: 650 + 100 + 100");
            hero.Modify(Stat.Strength, 5.0f, heldBy: holder);
            Expect.Near(27.0f, hero.Get(Stat.Strength), "the strength");
            Expect.Near(54.0f, hero.Get(power), "a change of a game stat reaches the map's: 27 x 2");
            Expect.True(At(Stat.Strength) >= 0 && At(power) > At(Stat.Strength), "strength announced, then what reads it");
            Expect.Near(44.0f, log[At(power)].Before, "with its total before");
        }

        /// <summary>A base set for a game stat on a unit or its type means Moonwell owns it: the base is Moonwell's number, the total goes to the game, and Get is Moonwell's total; a stat nobody set a base for works as before.</summary>
        public static void Ownership(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetMaxLife(handle, 650.0f);
            hero.SetBase(Stat.MaxLife, 800.0f);
            Expect.Near(800.0f, world.MaxLifeOf(handle), "the most life is the base");
            Expect.Near(800.0f, hero.Get(Stat.MaxLife), "Get is Moonwell's total");
            Expect.Near(800.0f, hero.GetBase(Stat.MaxLife), "GetBase is the base");
            hero.Modify(Stat.MaxLife, 100.0f, heldBy: holder);
            Expect.Near(900.0f, world.MaxLifeOf(handle), "the total by the four stages goes to the game");
            Expect.Near(100.0f, hero.GetBonus(Stat.MaxLife), "the bonus is the modifiers'");
            world.SetMaxLife(handle, 5000.0f);
            Expect.Near(900.0f, hero.Get(Stat.MaxLife), "what the game did meanwhile is not in Get");
            hero.AddBase(Stat.MaxLife, 50.0f);
            Expect.Near(950.0f, world.MaxLifeOf(handle), "AddBase sets it again from Moonwell's base");
            hero.SetBase(Stat.LifeRegen, 3.0f);
            Expect.Near(3.0f, world.RegenOf(handle, false), "regeneration is the unit's own number");
            hero.SetBase(Stat.Strength, 30.0f);
            hero.SetBase(Stat.MoveSpeed, 250.0f);
            Expect.Equal(30, world.StrengthOf(handle, false), "and so is an attribute");
            Expect.Near(250.0f, world.MoveSpeedOf(handle), "and the move speed");

            var plain = Unit.Of(world.NewUnit(Id("hfoo")))!;
            var pHandle = plain.Handle;
            world.SetArmor(pHandle, 4.0f);
            world.SetAverageDamage(pHandle, 29.0f);
            plain.SetBase(Stat.Armor, 10.0f);
            Expect.Near(10.0f, world.ArmorOf(pHandle), "armor is the unit's own base armor");
            plain.Modify(Stat.Armor, 5.0f, heldBy: holder);
            Expect.Near(15.0f, world.ArmorOf(pHandle), "with the green bonus for the rest");
            Expect.Near(5.0f, world.BonusOf(pHandle, Id("AMWa")), "the bonus ability");
            Expect.Near(15.0f, plain.Get(Stat.Armor), "Get is the total");
            Expect.Near(10.0f, plain.GetBase(Stat.Armor), "and the base");
            plain.SetBase(Stat.Damage, 50.0f);
            plain.Modify(Stat.Damage, 10.0f, heldBy: holder);
            Expect.Near(50.0f, world.AverageDamageOf(pHandle), "the middle of the white damage is the base");
            Expect.Near(10.0f, world.BonusOf(pHandle, Id("AMWd")), "and the bonus the rest");
            Expect.Near(60.0f, plain.Get(Stat.Damage), "Get is the total");
            plain.SetBase(Stat.AttackSpeed, 30.0f);
            plain.Modify(Stat.AttackSpeed, 15.0f, heldBy: holder);
            Expect.Near(0.45f, world.BonusOf(pHandle, Id("AMWs")), "attack speed has no base in the game: its base joins the bonus");
            Expect.Near(45.0f, plain.Get(Stat.AttackSpeed), "Get is Moonwell's total");

            var free = Unit.Of(world.NewUnit(Id("hfoo")))!;
            world.SetMaxLife(free.Handle, 500.0f);
            free.Modify(Stat.MaxLife, 100.0f, heldBy: holder);
            world.SetMaxLife(free.Handle, 700.0f);
            Expect.Near(600.0f, free.GetBase(Stat.MaxLife), "a stat nobody set a base for: the game's number less Moonwell's part, read live");
            Expect.Near(700.0f, free.Get(Stat.MaxLife), "and Get is what the game shows");
            free.AddBase(Stat.MaxLife, 10.0f);
            Expect.Near(710.0f, world.MaxLifeOf(free.Handle), "an AddBase starts from the game's present base: 600 + 10 + the 100");

            Stat.MaxMana.SetBase(Id("hkni"), 400.0f);
            var knight = Unit.Of(world.NewUnit(Id("hkni")))!;
            Expect.Near(400.0f, world.MaxManaOf(knight.Handle), "a type's base reaches the game as the unit comes in");
            Expect.Near(400.0f, knight.Get(Stat.MaxMana), "and is its total");
            knight.Modify(Stat.MaxMana, percent: 50.0f, heldBy: holder);
            Expect.Near(600.0f, world.MaxManaOf(knight.Handle), "and the stages work on it");
            Expect.Near(0.0f, world.MaxManaOf(free.Handle), "another type is not touched");
            var before = world.Printed.Count;
            Player.One.SetBase(Stat.MaxMana, 5.0f);
            Expect.True(world.Printed.Count > before, "a player still takes no game stat");
        }

        /// <summary>A hero's level-up puts its game stats again before its reactions run: a percent of the most life scales what the level added (I52), and an owned stat goes back to Moonwell's total.</summary>
        public static void LevelUp(FakeWorld world)
        {
            log = new List<Heard>();
            var holder = Game.Instance.Add(new Holder());
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetMaxLife(handle, 650.0f);
            world.SetStats(handle, 22, 13, 17);
            hero.Modify(Stat.Strength, 5.0f, heldBy: holder);
            hero.Modify(Stat.MaxLife, holder, percent: 10.0f);
            Expect.Near(715.0f, world.MaxLifeOf(handle), "650 + 10%");
            world.SetMaxLife(handle, world.MaxLifeOf(handle) + 50.0f);
            Expect.Near(765.0f, hero.Get(Stat.MaxLife), "the level's 50 are unscaled until something puts the stat again");
            world.LevelHero(handle, 2, false);
            Expect.Near(770.0f, world.MaxLifeOf(handle), "the level puts it again: 700 x 1.1");
            Expect.Near(770.0f, hero.Get(Stat.MaxLife), "and Get");

            var spied = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            world.SetMaxLife(spied.Handle, 650.0f);
            var spy = spied.Add(new LevelSpy());
            spy.World = world;
            spied.Modify(Stat.MaxLife, holder, percent: 10.0f);
            world.SetMaxLife(spied.Handle, 765.0f);
            world.LevelHero(spied.Handle, 2, false);
            Expect.Near(770.0f, spy.Seen, "before the map's reactions to the level run");

            var owned = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            world.SetMaxLife(owned.Handle, 650.0f);
            owned.SetBase(Stat.MaxLife, 800.0f);
            world.SetMaxLife(owned.Handle, 900.0f);
            world.LevelHero(owned.Handle, 2, false);
            Expect.Near(800.0f, world.MaxLifeOf(owned.Handle), "an owned stat is Moonwell's again: what the level added is overwritten");

            var watched = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            Game.Instance.Add(new GameWatch());
            var power = new Stat("Level power", 0.0f).From(Stat.Strength, 2.0f);
            world.SetStats(watched.Handle, 22, 13, 17);
            watched.Modify(Stat.Strength, 5.0f, heldBy: holder);
            Expect.Near(54.0f, watched.Get(power), "27 x 2");
            log = new List<Heard>();
            world.SetStats(watched.Handle, 29, 13, 17);
            world.LevelHero(watched.Handle, 2, false);
            Expect.True(At(Stat.Strength) >= 0 && At(power) > At(Stat.Strength), "the level's strength is announced, then what reads it");
            Expect.Near(27.0f, log![At(Stat.Strength)].Before, "from the total it had");
            Expect.Near(58.0f, watched.Get(power), "29 x 2: the strength with the 5, as the game adds the level's two");
        }

        /// <summary>An attribute Moonwell puts on a hero puts its other game stats again, as a level does, because the game adds an attribute's part on top of a number a set left. The fake world does not model an attribute's part in life or damage, so the case sets the game's number by hand where the game would have moved it.</summary>
        public static void AttributeFollows(FakeWorld world)
        {
            log = new List<Heard>();
            var holder = Game.Instance.Add(new Holder());
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetMaxLife(handle, 650.0f);
            world.SetStats(handle, 22, 13, 17);
            hero.Modify(Stat.MaxLife, holder, percent: 10.0f);
            Expect.Near(715.0f, world.MaxLifeOf(handle), "650 + 10%");
            world.SetMaxLife(handle, 765.0f);
            Expect.Near(765.0f, hero.Get(Stat.MaxLife), "the game added 50 on top, unscaled");
            Game.Instance.Add(new GameWatch());
            log = new List<Heard>();
            hero.Modify(Stat.Strength, 5.0f, heldBy: holder);
            Expect.Near(770.0f, world.MaxLifeOf(handle), "a strength modifier puts the most life again: (765 - 65) x 1.1");
            Expect.Near(770.0f, hero.Get(Stat.MaxLife), "and Get");
            Expect.True(At(Stat.MaxLife) >= 0, "its change is announced");
            Expect.Near(715.0f, log![At(Stat.MaxLife)].Before, "once, from the total last put, as a level does");
            var count = 0;
            for (var i = 0; i < log.Count; i++)
            {
                count += log[i].Stat == Stat.MaxLife ? 1 : 0;
            }

            Expect.Equal(1, count, "and only once");
            Expect.Near(27.0f, hero.Get(Stat.Strength), "the strength itself is put once, with no round of its own");

            var owned = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            world.SetStats(owned.Handle, 22, 13, 17);
            owned.SetBase(Stat.MaxLife, 800.0f);
            world.SetMaxLife(owned.Handle, 850.0f);
            owned.Modify(Stat.Strength, 5.0f, heldBy: holder);
            Expect.Near(800.0f, world.MaxLifeOf(owned.Handle), "an owned most life keeps exactly its total after a strength modifier");
            owned.SetBase(Stat.Agility, 20.0f);
            owned.Modify(Stat.Intelligence, 3.0f, heldBy: holder);
            Expect.Near(800.0f, world.MaxLifeOf(owned.Handle), "and after an owned agility and an intelligence modifier");

            var striker = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            world.SetStats(striker.Handle, 22, 13, 17);
            striker.SetBase(Stat.Damage, 50.0f);
            world.SetAverageDamage(striker.Handle, 60.0f);
            striker.Modify(Stat.Strength, 10.0f, heldBy: holder);
            Expect.Near(50.0f, world.AverageDamageOf(striker.Handle), "an owned damage keeps its total after a strength modifier (the fake world has no strength in damage: the 60 was put by hand)");
            Expect.Near(50.0f, striker.Get(Stat.Damage), "and Get");
        }

        /// <summary>An owned most life or mana through a level keeps the life or mana the unit had: the game adds the level's part to both, and Moonwell takes the part back off the current value when it puts its own most again (probe 11-21). A change Moonwell makes itself keeps the percent as before; a most that is not owned keeps the game's gift.</summary>
        public static void OwnedMostThroughLevel(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetStats(handle, 22, 13, 17);
            world.SetMaxLife(handle, 650.0f);
            world.SetLife(handle, 650.0f);
            world.SetMaxMana(handle, 255.0f);
            world.SetMana(handle, 255.0f);
            hero.SetBase(Stat.MaxLife, 800.0f);
            hero.SetBase(Stat.MaxMana, 300.0f);
            hero.Modify(Stat.MaxLife, holder, percent: 10.0f);
            Expect.Near(880.0f, world.MaxLifeOf(handle), "800 and 10 percent");
            world.SetLife(handle, 440.0f);
            world.SetMana(handle, 150.0f);

            // The game's level: 50 more life and 45 more mana, on the most and on the current number alike.
            world.SetMaxLife(handle, 930.0f);
            world.SetLife(handle, 490.0f);
            world.SetMaxMana(handle, 345.0f);
            world.SetMana(handle, 195.0f);
            world.LevelHero(handle, 2, false);
            Expect.Near(880.0f, world.MaxLifeOf(handle), "the most is Moonwell's again");
            Expect.Near(440.0f, world.LifeOf(handle), "and the life is what it was, 50 percent of 880, not 52.75");
            Expect.Near(300.0f, world.MaxManaOf(handle), "the most mana the same");
            Expect.Near(150.0f, world.ManaOf(handle), "and the mana");

            // A change Moonwell makes itself keeps the percent: 440 of 880 is 480 of 960.
            hero.Modify(Stat.MaxLife, holder, percent: 10.0f);
            Expect.Near(960.0f, world.MaxLifeOf(handle), "the most with another 10 percent");
            Expect.Near(480.0f, world.LifeOf(handle), "the percent kept for a change of Moonwell's own");

            // A hero at full life stays at full life through the level.
            var full = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            world.SetMaxLife(full.Handle, 650.0f);
            world.SetLife(full.Handle, 650.0f);
            full.SetBase(Stat.MaxLife, 800.0f);
            Expect.Near(800.0f, world.LifeOf(full.Handle), "a full unit is full at the most it was given");
            world.SetMaxLife(full.Handle, 850.0f);
            world.SetLife(full.Handle, 850.0f);
            world.LevelHero(full.Handle, 2, false);
            Expect.Near(800.0f, world.MaxLifeOf(full.Handle), "the most is 800");
            Expect.Near(800.0f, world.LifeOf(full.Handle), "and the life is still full");

            // A most that is not owned keeps the game's gift: 50 percent of 715, then the level's 50 with it.
            var plain = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            world.SetMaxLife(plain.Handle, 650.0f);
            world.SetLife(plain.Handle, 650.0f);
            plain.Modify(Stat.MaxLife, holder, percent: 10.0f);
            world.SetLife(plain.Handle, 357.5f);
            world.SetMaxLife(plain.Handle, 765.0f);
            world.SetLife(plain.Handle, 407.5f);
            world.LevelHero(plain.Handle, 2, false);
            Expect.Near(770.0f, world.MaxLifeOf(plain.Handle), "700 and 10 percent");
            Expect.Near(407.5f / 765.0f * 770.0f, world.LifeOf(plain.Handle), "the level's part stays in the life, at the same percent");
        }

        /// <summary>Changing the most life or most mana through a stat keeps the current amount at the same percent, up and down; a dead unit is left alone and a most of 0 is not divided by.</summary>
        public static void KeepsPercent(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var handle = world.NewUnit(Id("Hpal"), hero: true);
            var hero = Unit.Of(handle)!;
            world.SetMaxLife(handle, 650.0f);
            world.SetLife(handle, 650.0f);
            var plus = hero.Modify(Stat.MaxLife, 100.0f, heldBy: holder);
            Expect.Near(750.0f, world.LifeOf(handle), "650 of 650 with +100 becomes 750 of 750");
            plus.End();
            Expect.Near(650.0f, world.LifeOf(handle), "and back");
            world.SetLife(handle, 325.0f);
            plus = hero.Modify(Stat.MaxLife, 100.0f, heldBy: holder);
            Expect.Near(375.0f, world.LifeOf(handle), "325 of 650 becomes 375 of 750");
            Expect.Near(750.0f, world.MaxLifeOf(handle), "of 750");
            plus.End();
            Expect.Near(325.0f, world.LifeOf(handle), "and 375 of 750 back to 325 of 650");
            world.SetMaxMana(handle, 200.0f);
            world.SetMana(handle, 50.0f);
            var mana = hero.Modify(Stat.MaxMana, 200.0f, heldBy: holder);
            Expect.Near(100.0f, world.ManaOf(handle), "mana too: 50 of 200 becomes 100 of 400");
            mana.End();
            Expect.Near(50.0f, world.ManaOf(handle), "and back");
            hero.Modify(Stat.MaxLife, holder, percent: -50.0f);
            Expect.Near(162.5f, world.LifeOf(handle), "a drop keeps the percent: 325 of 650 is 162.5 of 325");
            hero.Modify(Stat.MaxLife, -1000.0f, heldBy: holder);
            Expect.True(world.IsAlive(handle) && world.LifeOf(handle) >= 1.0f, "a unit alive keeps at least 1 life");

            var dead = Unit.Of(world.NewUnit(Id("Hpal"), hero: true))!;
            world.SetMaxLife(dead.Handle, 650.0f);
            world.SetLife(dead.Handle, 650.0f);
            world.SlayUnit(dead.Handle);
            dead.Modify(Stat.MaxLife, 100.0f, heldBy: holder);
            Expect.Near(750.0f, world.MaxLifeOf(dead.Handle), "a dead unit's most changes");
            Expect.Near(0.0f, world.LifeOf(dead.Handle), "and its life is left alone");

            var empty = Unit.Of(world.NewUnit(Id("hfoo")))!;
            world.SetMaxMana(empty.Handle, 0.0f);
            world.SetMana(empty.Handle, 0.0f);
            empty.Modify(Stat.MaxMana, 100.0f, heldBy: holder);
            Expect.Near(100.0f, world.MaxManaOf(empty.Handle), "a most mana of 0 rises");
            Expect.Near(0.0f, world.ManaOf(empty.Handle), "and the mana stays what the game gives");
            var full = Unit.Of(world.NewUnit(Id("hfoo")))!;
            world.SetMaxMana(full.Handle, 100.0f);
            world.SetMana(full.Handle, 40.0f);
            full.Modify(Stat.MaxMana, -100.0f, heldBy: holder);
            Expect.Near(0.0f, world.MaxManaOf(full.Handle), "a most mana to 0");
            Expect.Near(0.0f, world.ManaOf(full.Handle), "is 0 mana, with no division by 0");
        }

        /// <summary>Stat.Named finds a ready-made or game stat nothing has used yet, by its display name, and a resistance by its kind.</summary>
        public static void NamedReady(FakeWorld world)
        {
            Expect.Same(Stat.Parry, Stat.Named("Parry"), "a ready-made stat");
            Expect.Same(Stat.MaxMana, Stat.Named("Mana"), "a game stat, by its display name");
            Expect.Same(Stat.CooldownReduction, Stat.Named("Cooldown reduction"), "of several words");
            Expect.Same(Stat.Resistance(DamageKind.Poison), Stat.Named("Poison resistance"), "a resistance by its kind");
            Expect.True(Stat.Named("Poison resistances") == null, "and nothing for a name that is not one");
            Expect.True(Stat.Named("Crit changes") == null, "an unknown name finds nothing");
        }

        /// <summary>A score is 0 until counted, adds and sets, and runs the Scoreboard's handlers only when it changes.</summary>
        public static void Scores(FakeWorld world)
        {
            var kills = new Score("Kills");
            Expect.Equal(0, kills[Player.One], "0 until counted");
            var heard = new List<int>();
            kills.changed = new List<System.Action<Player>> { player => heard.Add(kills[player]) };
            kills.Add(Player.One, 2);
            kills.Add(Player.One, 1);
            kills.Set(Player.One, 3);
            Expect.Equal(3, kills[Player.One], "added up");
            Expect.Equal(2, heard.Count, "a change runs the handlers, a set to the same value does not");
            Expect.Equal(3, heard[1], "after the change");
            kills.Add(Player.One, -3);
            Expect.Equal(0, kills[Player.One], "a negative amount takes away");
        }
    }
}
