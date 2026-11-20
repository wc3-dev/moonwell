using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>A game component that spares every hit that would kill, in the lethal stage, after the stats package has noted it.</summary>
    public sealed class SparingWatch : GameComponent, IDamageWatch
    {
        /// <summary>Spares the target at 1 life.</summary>
        public void OnUnitDying(Hit hit)
        {
            hit.Spare();
        }
    }

    /// <summary>A hero component that gives its hero 10 experience of its own at each kill, as a map's OnKill would.</summary>
    public sealed class OwnKillGift : UnitComponent
    {
        /// <summary>Gives the experience.</summary>
        public override void OnKill(Unit victim)
        {
            ((Hero)Unit).AddExperience(10, false);
        }
    }

    /// <summary>
    /// Cases for the stats package's experience and gold at a kill. The game gives both between the killing hit's lethal stage and the death
    /// event (seen 11-20); the fake world's KillGift stands in for it at exactly that moment, with Moonwell's own calls, so a case sets what the
    /// game gives each kill: GiftExperience to each hero of player one, GiftGold to the player.
    /// </summary>
    public static class StatGainCases
    {
        private static int giftExperience;

        private static int giftGold;

        private static FakeWorld game = null!;

        private static FakeDamageWorld fake = null!;

        private static Hero first = null!;

        private static Hero second = null!;

        /// <summary>Installs both packages; two heroes of player one (the first the killer in the cases) and the stand-in for the game's gift.</summary>
        private static void Arena(FakeWorld world)
        {
            game = world;
            giftExperience = 0;
            giftGold = 0;
            fake = new FakeDamageWorld(world);
            DamageSystem.Install(fake);
            StatsSystem.Install();
            StatsSystem.ShowTexts = false;
            first = (Hero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), slot: 0, hero: true))!;
            second = (Hero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hmkg"), slot: 0, hero: true))!;
            fake.Body(first.Handle).Melee = true;
            world.KillGift = (dying, killer) =>
            {
                first.AddExperience(giftExperience, false);
                second.AddExperience(giftExperience, false);
                Player.One.Gold += giftGold;
            };
        }

        /// <summary>The first hero kills a new victim of 50 life with a hit of 100 that lands, given what the game gives for it; the victim is given back.</summary>
        private static Unit Kill(int experience, int gold, Unit? by = null)
        {
            giftExperience = experience;
            giftGold = gold;
            var victim = Unit.Of(game.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1))!;
            fake.Body(victim.Handle).Life = 50.0f;
            var killer = by ?? first;
            fake.Hit(killer.Handle, victim.Handle, 100.0f, attackType: 1, damageType: 4, isAttack: true);
            fake.RunZero();
            return victim;
        }

        /// <summary>A hero's experience gain adds that percent of what the game gave; at 0 it adds nothing; another hero is not touched.</summary>
        public static void ExperienceGain(FakeWorld world)
        {
            Arena(world);
            first.SetBase(Stat.ExperienceGain, 50.0f);
            Kill(40, 0);
            Expect.Equal(60, first.Experience, "the game's 40 and half of it more");
            Expect.Equal(40, second.Experience, "a hero with no gain has the game's 40");
            first.SetBase(Stat.ExperienceGain, 0.0f);
            Kill(40, 0);
            Expect.Equal(100, first.Experience, "at 0 only the game's 40 more");
        }

        /// <summary>A gold bonus adds that percent of the bounty to the killer's player.</summary>
        public static void GoldBonus(FakeWorld world)
        {
            Arena(world);
            var start = Player.One.Gold;
            first.SetBase(Stat.GoldBonus, 25.0f);
            Kill(0, 8);
            Expect.Equal(start + 10, Player.One.Gold, "the bounty of 8 and a quarter of it more");
            Expect.Equal(0, first.Experience, "no experience was given");
        }

        /// <summary>A unit's gain and its player's add up: a hero's experience gain and its player's, and a killer's gold bonus and its player's, whichever unit kills.</summary>
        public static void UnitAndPlayerAdd(FakeWorld world)
        {
            Arena(world);
            first.SetBase(Stat.ExperienceGain, 20.0f);
            Player.One.SetBase(Stat.ExperienceGain, 30.0f);
            Kill(40, 0);
            Expect.Equal(60, first.Experience, "50 percent for the hero: 20 and its player's 30");
            Expect.Equal(52, second.Experience, "30 percent for the other hero, its player's alone");
            var start = Player.One.Gold;
            first.SetBase(Stat.GoldBonus, 10.0f);
            Player.One.SetBase(Stat.GoldBonus, 40.0f);
            Kill(0, 8);
            Expect.Equal(start + 12, Player.One.Gold, "50 percent of 8: the killer's 10 and its player's 40");
            var footman = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
            fake.Body(footman.Handle).Melee = true;
            Kill(0, 10, footman);
            Expect.Equal(start + 12 + 14, Player.One.Gold, "another killer has its player's 40 alone: 4 more");
            Player.One.SetBase(Stat.GoldBonus, 0.0f);
            Kill(0, 10, footman);
            Expect.Equal(start + 12 + 14 + 10, Player.One.Gold, "a killer with no bonus and a player with none: the bounty alone");
        }

        /// <summary>A negative percent takes away, down to all of it at -100 and no further.</summary>
        public static void NegativePercent(FakeWorld world)
        {
            Arena(world);
            var start = Player.One.Gold;
            first.SetBase(Stat.ExperienceGain, -50.0f);
            first.SetBase(Stat.GoldBonus, -50.0f);
            Kill(30, 10);
            Expect.Equal(15, first.Experience, "half of the 30 taken back");
            Expect.Equal(start + 5, Player.One.Gold, "half of the bounty taken back");
            first.SetBase(Stat.ExperienceGain, -80.0f);
            Player.One.SetBase(Stat.ExperienceGain, -80.0f);
            first.SetBase(Stat.GoldBonus, -100.0f);
            Kill(30, 10);
            Expect.Equal(15, first.Experience, "-160 is held to -100: all of the 30 taken back, none more");
            Expect.Equal(start + 5, Player.One.Gold, "-100 takes the whole bounty");
        }

        /// <summary>Each kill's amount is whole, round half up, and no fraction is given twice or carried: seven experience at 50 percent is 4 each time, and a small one rounds to nothing.</summary>
        public static void RoundingOverKills(FakeWorld world)
        {
            Arena(world);
            var start = Player.One.Gold;
            first.SetBase(Stat.ExperienceGain, 50.0f);
            first.SetBase(Stat.GoldBonus, 50.0f);
            Kill(7, 3);
            Kill(7, 3);
            Kill(7, 3);
            Expect.Equal(33, first.Experience, "three kills of 7 and 4 more each");
            Expect.Equal(start + 9 + 6, Player.One.Gold, "three bounties of 3 and 2 more each (1.5 rounds up)");
            first.SetBase(Stat.ExperienceGain, 10.0f);
            Kill(4, 0);
            Kill(4, 0);
            Expect.Equal(41, first.Experience, "10 percent of 4 is 0.4: nothing, twice");
            first.SetBase(Stat.ExperienceGain, 40.0f);
            Kill(7, 0);
            Expect.Equal(51, first.Experience, "40 percent of 7 is 2.8: 3");
        }

        /// <summary>A hit that was spared, or did not kill, leaves nothing behind: a later death of the same unit gets no bonus.</summary>
        public static void SparedHitLeavesNothing(FakeWorld world)
        {
            Arena(world);
            Game.Instance.Add(new SparingWatch());
            first.SetBase(Stat.ExperienceGain, 100.0f);
            first.SetBase(Stat.GoldBonus, 100.0f);
            var start = Player.One.Gold;
            var spared = Kill(40, 8);
            Expect.True(spared.IsAlive, "the hit was spared");
            Expect.Equal(0, first.Experience, "nothing was given");
            Expect.Equal(start, Player.One.Gold, "no gold");
            giftExperience = 40;
            giftGold = 8;
            spared.Kill();
            Expect.Equal(40, first.Experience, "its later death by Kill() has the game's gift alone");
            Expect.Equal(start + 8, Player.One.Gold, "and its bounty alone");
            var hurt = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 1))!;
            fake.Body(hurt.Handle).Life = 500.0f;
            fake.Hit(first.Handle, hurt.Handle, 100.0f, attackType: 1, damageType: 4, isAttack: true);
            fake.RunZero();
            hurt.Kill();
            Expect.Equal(80, first.Experience, "a hit that did not kill left nothing either");
        }

        /// <summary>The bonus is on what the game gave: a map's own gift in OnKill comes after, and is not multiplied.</summary>
        public static void MapsOwnGiftComesAfter(FakeWorld world)
        {
            Arena(world);
            first.Add(new OwnKillGift());
            first.SetBase(Stat.ExperienceGain, 50.0f);
            Kill(40, 0);
            Expect.Equal(70, first.Experience, "the game's 40, half of it more, and the map's own 10");
        }

        /// <summary>A hero made with a type's base of experience gain and nothing else set gets its bonus at its first kill: the unit is tracked as it comes in, and the death event asked for then.</summary>
        public static void TypedExperienceGain(FakeWorld world)
        {
            Arena(world);
            var type = ObjectTypes.FourCC("H0tx");
            Stat.ExperienceGain.SetBase(type, 50.0f);
            first = (Hero)Unit.Of(world.NewUnit(type, slot: 0, hero: true))!;
            fake.Body(first.Handle).Melee = true;
            Kill(40, 0);
            Expect.Equal(60, first.Experience, "the game's 40 and half of it more, from the type's base alone");
            Expect.Equal(40, second.Experience, "a hero of another type has the game's 40");
        }

        /// <summary>A killer made with a type's base of gold bonus and nothing else set gets its bonus at its first kill.</summary>
        public static void TypedGoldBonus(FakeWorld world)
        {
            Arena(world);
            var type = ObjectTypes.FourCC("H0ty");
            Stat.GoldBonus.SetBase(type, 25.0f);
            first = (Hero)Unit.Of(world.NewUnit(type, slot: 0, hero: true))!;
            fake.Body(first.Handle).Melee = true;
            var start = Player.One.Gold;
            Kill(0, 8);
            Expect.Equal(start + 10, Player.One.Gold, "the bounty of 8 and a quarter of it more, from the type's base alone");
        }

        /// <summary>A unit of a type that has none of the six stats' bases, made while another type has one, costs nothing as it comes in: no sheet of stats, no death event asked for.</summary>
        public static void NoTypeBaseCostsNothing(FakeWorld world)
        {
            Arena(world);
            Stat.GoldBonus.SetBase(ObjectTypes.FourCC("H0ty"), 25.0f);
            var other = Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
            var hero = Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), slot: 0, hero: true))!;
            Expect.True(other.stats == null && hero.stats == null, "no sheet of stats was made");
            Expect.True(!world.Listening.Contains(GameEvent.Death), "the death event was not asked for");
        }

        /// <summary>A kill in a map that uses neither stat costs nothing: no sheet of stats, no death event asked for, only the game's gift.</summary>
        public static void NeitherStatCostsNothing(FakeWorld world)
        {
            Arena(world);
            var start = Player.One.Gold;
            Kill(40, 8);
            Expect.Equal(40, first.Experience, "the game's gift alone");
            Expect.Equal(start + 8, Player.One.Gold, "and its bounty");
            Expect.True(first.stats == null && second.stats == null, "no sheet of stats was made");
            Expect.True(!world.Listening.Contains(GameEvent.Death), "the death event was never asked for");
        }
    }
}
