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
