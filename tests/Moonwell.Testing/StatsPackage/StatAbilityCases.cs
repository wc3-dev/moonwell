namespace Moonwell.Testing
{
    /// <summary>Cases for the stats package's ability stats: cooldown reduction, mana cost reduction and cast range on every ability of a unit, and cast speed on its cast point and backswing.</summary>
    public static class StatAbilityCases
    {
        private static readonly int Slow = ObjectTypes.FourCC("Aslo");
        private static readonly int Curse = ObjectTypes.FourCC("Acrs");
        private static readonly int Fireball = ObjectTypes.FourCC("Afbk");

        /// <summary>A footman of player one with two abilities: Slow (3 levels; cooldown 10 and 20, mana 50 and 80, range 600 and 700) and Curse (cooldown 4, mana 20, range 300); the package installed.</summary>
        private static Unit Caster(FakeWorld world)
        {
            StatsSystem.Install();
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
            world.SetLevelCount(Slow, 3);
            world.SetAbilityLevel(unit.Handle, Slow, 1);
            world.SetAbilityCooldown(unit.Handle, Slow, 0, 10.0f);
            world.SetAbilityCooldown(unit.Handle, Slow, 1, 20.0f);
            world.SetAbilityManaCost(unit.Handle, Slow, 0, 50);
            world.SetAbilityManaCost(unit.Handle, Slow, 1, 80);
            world.SetAbilityRange(unit.Handle, Slow, 0, 600.0f);
            world.SetAbilityRange(unit.Handle, Slow, 1, 700.0f);
            world.SetAbilityLevel(unit.Handle, Curse, 1);
            world.SetAbilityCooldown(unit.Handle, Curse, 0, 4.0f);
            world.SetAbilityManaCost(unit.Handle, Curse, 0, 20);
            world.SetAbilityRange(unit.Handle, Curse, 0, 300.0f);
            unit.AbilityOf(Slow);
            unit.AbilityOf(Curse);
            return unit;
        }

        /// <summary>Cooldown reduction takes that percent off every ability's cooldown, set again it does not compound, and at no change the scales are back to 1.</summary>
        public static void CooldownReduction(FakeWorld world)
        {
            var unit = Caster(world);
            var slow = unit.AbilityOf(Slow);
            var curse = unit.AbilityOf(Curse);
            unit.SetBase(Stat.CooldownReduction, 25.0f);
            Expect.Near(7.5f, slow.LiveCooldown, "25 percent off 10");
            Expect.Near(3.0f, curse.LiveCooldown, "and off 4");
            unit.SetBase(Stat.CooldownReduction, 25.0f);
            unit.SetBase(Stat.CooldownReduction, 40.0f);
            Expect.Near(6.0f, slow.LiveCooldown, "40 percent works on the ability's own cooldown, not on the 7.5");
            Expect.Near(2.4f, curse.LiveCooldown, "the same for the other");
            unit.SetBase(Stat.CooldownReduction, 0.0f);
            Expect.Near(10.0f, slow.LiveCooldown, "back to the own cooldown");
            Expect.Near(4.0f, curse.LiveCooldown, "both");
            Expect.Near(1.0f, slow.CooldownScale, "the scale is 1 again");
            Expect.Near(1.0f, curse.CooldownScale, "on both");
            Expect.Near(1.0f, slow.ManaCostScale, "the other scales were never moved");
        }

        /// <summary>Mana cost reduction takes that percent off every ability's mana cost, to a whole number, and returns at no change.</summary>
        public static void ManaCostReduction(FakeWorld world)
        {
            var unit = Caster(world);
            var slow = unit.AbilityOf(Slow);
            var curse = unit.AbilityOf(Curse);
            unit.SetBase(Stat.ManaCostReduction, 20.0f);
            Expect.Equal(40, slow.LiveManaCost, "20 percent off 50");
            Expect.Equal(16, curse.LiveManaCost, "and off 20");
            unit.SetBase(Stat.ManaCostReduction, 25.0f);
            unit.SetBase(Stat.ManaCostReduction, 25.0f);
            Expect.Equal(38, slow.LiveManaCost, "25 percent off 50 is 37.5, rounded half up, and set again it does not compound");
            Expect.Equal(15, curse.LiveManaCost, "25 percent off 20");
            unit.SetBase(Stat.ManaCostReduction, 0.0f);
            Expect.Equal(50, slow.LiveManaCost, "back");
            Expect.Equal(20, curse.LiveManaCost, "both");
        }

        /// <summary>Cast range adds that percent to every ability's range, and a negative one takes it away.</summary>
        public static void CastRange(FakeWorld world)
        {
            var unit = Caster(world);
            var slow = unit.AbilityOf(Slow);
            var curse = unit.AbilityOf(Curse);
            unit.SetBase(Stat.CastRange, 50.0f);
            Expect.Near(900.0f, slow.LiveRange, "50 percent more than 600");
            Expect.Near(450.0f, curse.LiveRange, "and than 300");
            unit.SetBase(Stat.CastRange, -50.0f);
            Expect.Near(300.0f, slow.LiveRange, "50 percent less, of the own range");
            Expect.Near(150.0f, curse.LiveRange, "both");
            unit.SetBase(Stat.CastRange, 0.0f);
            Expect.Near(600.0f, slow.LiveRange, "back");
            Expect.Near(300.0f, curse.LiveRange, "both");
        }

        /// <summary>The three stats together; a new ability and a new level are scaled when the unit's abilities change, and a map's own set number stays the base.</summary>
        public static void NewAbilitiesAndLevels(FakeWorld world)
        {
            var unit = Caster(world);
            var slow = unit.AbilityOf(Slow);
            unit.SetBase(Stat.CooldownReduction, 40.0f);
            unit.SetBase(Stat.ManaCostReduction, 50.0f);
            unit.SetBase(Stat.CastRange, 10.0f);
            world.SetAbilityCooldown(unit.Handle, Fireball, 0, 8.0f);
            world.SetAbilityManaCost(unit.Handle, Fireball, 0, 30);
            world.SetAbilityRange(unit.Handle, Fireball, 0, 500.0f);
            var fireball = unit.AddAbility(Fireball);
            Expect.Near(4.8f, fireball.LiveCooldown, "an ability added is scaled: 40 percent off 8");
            Expect.Equal(15, fireball.LiveManaCost, "half the mana");
            Expect.Near(550.0f, fireball.LiveRange, "10 percent more range");
            slow.Level = 2;
            Expect.Near(12.0f, slow.LiveCooldown, "a new level is scaled: 40 percent off 20");
            Expect.Equal(40, slow.LiveManaCost, "half of 80");
            Expect.Near(770.0f, slow.LiveRange, "10 percent over 700");
            slow.LiveCooldown = 30.0f;
            Expect.Near(18.0f, slow.LiveCooldown, "a map's own set is the new base, still reduced");
            unit.SetBase(Stat.CooldownReduction, 0.0f);
            Expect.Near(30.0f, slow.LiveCooldown, "and it is what the cooldown returns to");
            Expect.Near(10.0f, unit.AbilityOf(Slow).CooldownScale * 10.0f, "the scale is 1 again");
            slow.Level = 1;
            Expect.Near(10.0f, slow.LiveCooldown, "level 1 had its own number, put when the level was set");
        }

        /// <summary>A unit that never had one of the three stats is never walked: no sheet of stats, and its abilities keep their numbers when they change.</summary>
        public static void UnitWithoutStatsUntouched(FakeWorld world)
        {
            var unit = Caster(world);
            var slow = unit.AbilityOf(Slow);
            slow.Level = 2;
            unit.AddAbility(Fireball);
            Expect.True(unit.stats == null, "no sheet of stats was made");
            Expect.Near(20.0f, slow.LiveCooldown, "the cooldown is the game's own");
            Expect.Near(1.0f, slow.CooldownScale, "the scale was never set");
        }

        /// <summary>A unit made after a type's base of cast speed and cooldown reduction was set, with nothing else changed on it, has both from the start.</summary>
        public static void TypedBases(FakeWorld world)
        {
            StatsSystem.Install();
            var type = ObjectTypes.FourCC("h0tz");
            Stat.CooldownReduction.SetBase(type, 20.0f);
            Stat.CastSpeed.SetBase(type, 100.0f);
            var handle = world.NewUnit(type, slot: 0);
            world.SetCastPoint(handle, 0.5f);
            world.SetCastBackswing(handle, 1.0f);
            world.SetAbilityLevel(handle, Slow, 1);
            world.SetAbilityCooldown(handle, Slow, 0, 10.0f);
            var unit = Unit.Of(handle)!;
            Expect.Near(8.0f, unit.AbilityOf(Slow).LiveCooldown, "20 percent off 10, from the type's base alone");
            Expect.Near(0.25f, world.CastPointOf(handle), "the cast point halved");
            Expect.Near(0.5f, world.CastBackswingOf(handle), "and the backswing");
            var plain = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
            world.SetAbilityLevel(plain.Handle, Slow, 1);
            world.SetAbilityCooldown(plain.Handle, Slow, 0, 10.0f);
            Expect.Near(10.0f, plain.AbilityOf(Slow).LiveCooldown, "a unit of another type is untouched");
            Expect.True(plain.stats == null, "with no sheet of stats");
        }

        /// <summary>Cast speed divides the unit's own cast point and backswing, and the own numbers come back exactly: up, further up, down to the least, and back.</summary>
        public static void CastSpeed(FakeWorld world)
        {
            StatsSystem.Install();
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
            var other = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
            world.SetCastPoint(unit.Handle, 0.5f);
            world.SetCastBackswing(unit.Handle, 1.0f);
            world.SetCastPoint(other.Handle, 0.3f);
            unit.SetBase(Stat.CastSpeed, 100.0f);
            Expect.Near(0.25f, world.CastPointOf(unit.Handle), "100 percent faster halves the cast point");
            Expect.Near(0.5f, world.CastBackswingOf(unit.Handle), "and the backswing");
            unit.SetBase(Stat.CastSpeed, 300.0f);
            Expect.Near(0.125f, world.CastPointOf(unit.Handle), "300 percent divides by 4, from the own number, not from the 0.25");
            Expect.Near(0.25f, world.CastBackswingOf(unit.Handle), "the same for the backswing");
            unit.SetBase(Stat.CastSpeed, -50.0f);
            Expect.Near(1.0f, world.CastPointOf(unit.Handle), "50 percent slower doubles it");
            Expect.Near(2.0f, world.CastBackswingOf(unit.Handle), "both");
            unit.SetBase(Stat.CastSpeed, -300.0f);
            Expect.Near(5.0f, world.CastPointOf(unit.Handle), "the least is -90: divided by 0.1");
            Expect.Near(10.0f, world.CastBackswingOf(unit.Handle), "both");
            unit.SetBase(Stat.CastSpeed, 0.0f);
            Expect.Near(0.5f, world.CastPointOf(unit.Handle), "back to the unit's own cast point");
            Expect.Near(1.0f, world.CastBackswingOf(unit.Handle), "and its own backswing");
            Expect.Near(0.3f, world.CastPointOf(other.Handle), "another unit was not touched");
            unit.SetBase(Stat.CastSpeed, 100.0f);
            Expect.Near(0.25f, world.CastPointOf(unit.Handle), "and it can go again");
        }
    }
}
