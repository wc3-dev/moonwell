using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases for the unit and player reads a map asks the game: a unit's food, attack numbers, range kind, a mine's gold and whether it can cast; a player's units, hero limits and what it sees.</summary>
    public static class UnitPlayerReadCases
    {
        /// <summary>A unit's FoodUsed, LiveAttackCooldown, AcquireRange, IsRanged and a mine's Resources read what the world holds; a mine's gold is set; all read 0 or false once the unit is removed.</summary>
        public static void UnitReads(FakeWorld world)
        {
            var rifleman = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hrif"), 0))!;
            Expect.True(rifleman.FoodUsed == 0 && !rifleman.IsRanged, "nothing until the world gives it");
            world.SetFoodUsed(rifleman.Handle, 3);
            world.SetAttackCooldown(rifleman.Handle, 1.5f);
            world.SetAcquireRange(rifleman.Handle, 600.0f);
            world.MakeKind(rifleman.Handle, UnitKind.Ranged);
            Expect.Equal(3, rifleman.FoodUsed, "the food it uses");
            Expect.Near(1.5f, rifleman.LiveAttackCooldown, "its attack cooldown");
            Expect.Near(600.0f, rifleman.AcquireRange, "its acquisition range");
            Expect.True(rifleman.IsRanged, "a ranged attacker");

            var mine = Unit.Of(world.NewUnit(ObjectTypes.FourCC("ngol"), 15))!;
            Expect.Equal(0, mine.Resources, "no gold until given");
            mine.Resources = 12500;
            mine.Resources -= 500;
            Expect.Equal(12000, mine.Resources, "set and read");
            Expect.Equal(12000, world.ResourcesOf(mine.Handle), "in the world");

            rifleman.Remove();
            mine.Remove();
            Expect.True(rifleman.FoodUsed == 0 && rifleman.LiveAttackCooldown == 0.0f && rifleman.AcquireRange == 0.0f && !rifleman.IsRanged, "none once removed");
            mine.Resources = 7;
            Expect.Equal(0, mine.Resources, "a removed mine holds none, and a set does nothing");
            Expect.Equal(12000, world.ResourcesOf(mine.Handle), "the world was not called");
        }

        /// <summary>CanCast holds when the unit has the ability, its cooldown is over and the unit's mana is at least its cost; CanCast&lt;T&gt; asks the same of a registered class.</summary>
        public static void CanCast(FakeWorld world)
        {
            ObjectTypes.Ability(ObjectTypes.FourCC("A001"), () => new TestBolt());
            var id = ObjectTypes.FourCC("A001");
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), 0))!;
            Expect.True(!unit.CanCast(id) && !unit.CanCast<TestBolt>(), "not without the ability");
            var bolt = unit.AddAbility<TestBolt>();
            Expect.True(unit.CanCast(id) && unit.CanCast<TestBolt>(), "it has it, with no cooldown and no cost");
            bolt.StartCooldown(5.0f);
            Expect.True(!unit.CanCast<TestBolt>(), "not on its cooldown");
            bolt.ResetCooldown();
            unit.MaxMana = 100.0f;
            unit.Mana = 20.0f;
            bolt.LiveManaCost = 50;
            Expect.True(!unit.CanCast<TestBolt>(), "not with 20 mana for a cost of 50");
            unit.Mana = 50.0f;
            Expect.True(unit.CanCast<TestBolt>(), "mana at the cost");
            unit.Remove();
            Expect.True(!unit.CanCast<TestBolt>(), "never once removed");
        }

        /// <summary>A player's Units are its living units in the order made: another's, a dead one, a removed one and Moonwell's dummy are left out; each read is a new list, and a unit given away leaves it.</summary>
        public static void PlayerUnits(FakeWorld world)
        {
            ObjectTypes.DummyType(ObjectTypes.FourCC("hMWd"));
            var footman = world.NewUnit(ObjectTypes.FourCC("hfoo"), 1);
            world.NewUnit(ObjectTypes.FourCC("hfoo"), 2);
            world.NewUnit(ObjectTypes.FourCC("hMWd"), 1);
            var dying = world.NewUnit(ObjectTypes.FourCC("hfoo"), 1);
            var removed = world.NewUnit(ObjectTypes.FourCC("hfoo"), 1);
            var peasant = world.NewUnit(ObjectTypes.FourCC("hpea"), 1);
            Unit.Of(dying)!.Kill();
            Unit.Of(removed)!.Remove();

            var units = Player.Two.Units;
            Expect.Equal(2, units.Count, "the living units");
            Expect.Same(Unit.Of(footman), units[0], "in the order made");
            Expect.Same(Unit.Of(peasant), units[1], "the second");
            Expect.True(units != Player.Two.Units, "a new list at each read");
            Unit.Of(footman)!.Owner = Player.Four;
            Expect.Equal(1, Player.Two.Units.Count, "a unit given away leaves the list");
            Expect.Same(Unit.Of(footman), Player.Four.Units[0], "and is in its new owner's");
        }

        /// <summary>MaxHeroes is the limit of the 'HERO' tech, NoLimit until set, each player's own; HeroTokens is the player's hero token state.</summary>
        public static void PlayerHeroLimits(FakeWorld world)
        {
            Expect.Equal(Player.NoLimit, Player.One.MaxHeroes, "no limit until set");
            Player.One.MaxHeroes = 3;
            Expect.Equal(3, Player.One.MaxHeroes, "a limit of 3");
            Expect.Equal(3, world.TechMaxOf(world.PlayerAt(0), ObjectTypes.FourCC("HERO")), "the 'HERO' tech's limit, as SetPlayerMaxHeroesAllowed sets it");
            Expect.Equal(Player.NoLimit, Player.Two.MaxHeroes, "another player's own");

            Player.One.HeroTokens = 1;
            Expect.Equal(1, Player.One.HeroTokens, "a hero token");
            Expect.Equal(1, Runtime.World.StateOf(world.PlayerAt(0), PlayerState.HeroTokens), "kept as the player's state");
            Expect.Equal(0, Player.One.Gold, "apart from the gold");
        }

        /// <summary>A player sees a unit unless the world hides it from that player; never a removed one.</summary>
        public static void PlayerSees(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), 1))!;
            Expect.True(Player.One.Sees(unit), "seen unless hidden");
            world.Dummies.Hide(world.PlayerAt(0), unit.Handle);
            Expect.True(!Player.One.Sees(unit), "hidden from Player One");
            Expect.True(Player.Three.Sees(unit), "not from another");
            unit.Remove();
            Expect.True(!Player.Three.Sees(unit), "never a removed unit");
        }
    }
}
