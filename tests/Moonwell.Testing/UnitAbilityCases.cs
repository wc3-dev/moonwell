using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check giving and taking a unit's abilities and setting their level, and the learn and lost reactions of a hero's skills (decisions 136 to 140).</summary>
    public static class UnitAbilityCases
    {
        /// <summary>Checks that the log is exactly the expected lines, in order.</summary>
        private static void ExpectLog(string[] expected, string what)
        {
            Expect.Equal(expected.Length, TestLog.Lines.Count, what + ": line count");
            for (var i = 0; i < expected.Length && i < TestLog.Lines.Count; i++)
            {
                Expect.Equal(expected[i], TestLog.Lines[i], what + ": line " + i);
            }
        }

        /// <summary>Registers the level hero and the given ability, a hero skill of three levels, with a game watch and a player watch for player 0.</summary>
        private static void Arena(FakeWorld world)
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H0lv"), () => new LevelHero());
            ObjectTypes.Ability(ObjectTypes.FourCC("A0gv"), () => new GivenAbility());
            Game.Instance.Add(new GameLevelWatch());
            Player.Get(0).Add(new PlayerLevelWatch("p0"));
            world.SetLevelCount(ObjectTypes.FourCC("A0gv"), 3);
            world.MarkHeroAbility(ObjectTypes.FourCC("A0gv"));
            GivenAbility.LowerAt = 0;
            LevelHero.RemoveAt = 0;
            LevelHero.RaiseAt = 0;
        }

        /// <summary>Makes a level hero of player 0, taken in.</summary>
        private static LevelHero NewHero(FakeWorld world)
        {
            return (LevelHero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("H0lv"), slot: 0, hero: true))!;
        }

        /// <summary>Makes a footman of player 0, taken in.</summary>
        private static Unit NewFootman(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
        }

        /// <summary>AddAbility gives the ability at level 1, made permanent, and returns the unit's object of it.</summary>
        public static void AddGivesLevelOne(FakeWorld world)
        {
            var footman = NewFootman(world);
            var slow = ObjectTypes.FourCC("Aslo");

            var given = footman.AddAbility(slow);

            Expect.Same(footman.AbilityOf(slow), given, "the object given back");
            Expect.Equal(1, given.Level, "the level");
            Expect.True(footman.HasAbility(slow), "the unit has it");
            Expect.True(world.AbilityCalls.Contains("permanent " + slow), "the ability is made permanent");
        }

        /// <summary>AddAbility, HasAbility and RemoveAbility with a class work on the object of that class.</summary>
        public static void Generic(FakeWorld world)
        {
            Arena(world);
            var footman = NewFootman(world);

            var given = footman.AddAbility<GivenAbility>();

            Expect.Same(footman.AbilityOf<GivenAbility>(), given, "the object of the class");
            Expect.True(footman.HasAbility<GivenAbility>(), "the unit has it");
            Expect.True(footman.RemoveAbility<GivenAbility>(), "the removal");
            Expect.True(!footman.HasAbility<GivenAbility>(), "the unit has it no more");
        }

        /// <summary>Adding an ability the unit has already keeps its level, as the game answers false.</summary>
        public static void AddWhenHadKeepsLevel(FakeWorld world)
        {
            Arena(world);
            var footman = NewFootman(world);
            var given = footman.AddAbility<GivenAbility>();
            given.Level = 3;

            var again = footman.AddAbility<GivenAbility>();

            Expect.Same(given, again, "the same object");
            Expect.Equal(3, given.Level, "the level after the second add");
        }

        /// <summary>An added ability stays through a morph, which strips one that is not permanent (seen 10-26 and 11-02).</summary>
        public static void AddKeptThroughMorph(FakeWorld world)
        {
            var footman = NewFootman(world);
            var slow = ObjectTypes.FourCC("Aslo");
            footman.AddAbility(slow);

            world.Morph(footman.Handle);

            Expect.True(world.HasAddedAbility(footman.Handle, slow), "the ability after the morph");
        }

        /// <summary>RemoveAbility takes the ability and answers true; a second removal answers false.</summary>
        public static void RemoveTakesIt(FakeWorld world)
        {
            var footman = NewFootman(world);
            var slow = ObjectTypes.FourCC("Aslo");
            var given = footman.AddAbility(slow);

            Expect.True(footman.RemoveAbility(slow), "the first removal");
            Expect.True(!footman.HasAbility(slow), "the unit has it no more");
            Expect.Equal(0, given.Level, "the level after the removal");
            Expect.True(!footman.RemoveAbility(slow), "the second removal");
        }

        /// <summary>Once the unit is gone, no call reaches the game, HasAbility is false, and Level reads the last level read.</summary>
        public static void Gone(FakeWorld world)
        {
            Arena(world);
            var footman = NewFootman(world);
            var given = footman.AddAbility<GivenAbility>();
            given.Level = 2;
            footman.Remove();
            world.AbilityCalls.Clear();

            footman.AddAbility(ObjectTypes.FourCC("Aslo"));
            given.Level = 3;

            Expect.True(!footman.RemoveAbility<GivenAbility>(), "the removal");
            Expect.True(!footman.HasAbility<GivenAbility>(), "HasAbility");
            Expect.Equal(2, given.Level, "the last level read");
            Expect.Equal(0, world.AbilityCalls.Count, "the calls to the game");
        }

        /// <summary>Setting Level raises and lowers the level.</summary>
        public static void LevelSet(FakeWorld world)
        {
            Arena(world);
            var given = NewFootman(world).AddAbility<GivenAbility>();

            given.Level = 3;
            Expect.Equal(3, given.Level, "raised to 3");
            given.Level = 2;
            Expect.Equal(2, given.Level, "lowered to 2");
            given.Level++;
            Expect.Equal(3, given.Level, "raised by one");
        }

        /// <summary>A level the ability cannot take is kept within its levels, as the game does (seen 11-02), and a unit without the ability is not given it.</summary>
        public static void LevelKeptInRange(FakeWorld world)
        {
            Arena(world);
            var footman = NewFootman(world);
            var given = footman.AddAbility<GivenAbility>();

            given.Level = 5;
            Expect.Equal(3, given.Level, "set to 5");
            given.Level = 0;
            Expect.Equal(1, given.Level, "set to 0");
            var slow = footman.AbilityOf(ObjectTypes.FourCC("Aslo"));
            slow.Level = 2;
            Expect.Equal(0, slow.Level, "set on an ability the unit lacks");
        }

        /// <summary>Giving a hero a hero skill runs the learn reactions for level 1, as learning it does.</summary>
        public static void HeroAddRunsLearn(FakeWorld world)
        {
            Arena(world);
            var hero = NewHero(world);
            TestLog.Lines.Clear();

            hero.AddAbility<GivenAbility>();

            ExpectLog(new string[] { "ability learned level 1", "hero learned level 1 points 0", "comp learned level 1", "game learned level 1", "p0 learned level 1" }, "the log");
        }

        /// <summary>Raising a hero skill by two levels runs the learn reactions once for each level, in order, each reading its level.</summary>
        public static void HeroLevelRunsLearnPerLevel(FakeWorld world)
        {
            Arena(world);
            var given = NewHero(world).AddAbility<GivenAbility>();
            TestLog.Lines.Clear();

            given.Level = 3;

            ExpectLog(new string[]
            {
                "ability learned level 2", "hero learned level 2 points 0", "comp learned level 2", "game learned level 2", "p0 learned level 2",
                "ability learned level 3", "hero learned level 3 points 0", "comp learned level 3", "game learned level 3", "p0 learned level 3",
            }, "the log");
        }

        /// <summary>A level past the last runs the learn reactions up to the last level only.</summary>
        public static void HeroLevelPastLastStopsAtLast(FakeWorld world)
        {
            Arena(world);
            var given = NewHero(world).AddAbility<GivenAbility>();
            given.Level = 2;
            TestLog.Lines.Clear();

            given.Level = 5;

            ExpectLog(new string[] { "ability learned level 3", "hero learned level 3 points 0", "comp learned level 3", "game learned level 3", "p0 learned level 3" }, "the log");
        }

        /// <summary>Lowering a hero skill's level runs nothing, as with hero levels (decision 114).</summary>
        public static void HeroLevelLoweredRunsNothing(FakeWorld world)
        {
            Arena(world);
            var given = NewHero(world).AddAbility<GivenAbility>();
            given.Level = 3;
            TestLog.Lines.Clear();

            given.Level = 1;

            ExpectLog(new string[0], "the log");
        }

        /// <summary>A learn reaction that changes the level ends the levels still to come.</summary>
        public static void HeroReactionLoweringEndsSteps(FakeWorld world)
        {
            Arena(world);
            var given = NewHero(world).AddAbility<GivenAbility>();
            GivenAbility.LowerAt = 2;
            TestLog.Lines.Clear();

            given.Level = 3;

            ExpectLog(new string[] { "ability learned level 2", "hero learned level 1 points 0", "comp learned level 1", "game learned level 1", "p0 learned level 1" }, "the log");
            Expect.Equal(1, given.Level, "the level after");
        }

        /// <summary>An ability that is not a hero skill, given to a hero, and a hero skill given to a unit that is not a hero, run no learn reaction.</summary>
        public static void OnlyHeroSkillsOfHeroesLearn(FakeWorld world)
        {
            Arena(world);
            ObjectTypes.Ability(ObjectTypes.FourCC("A0nh"), () => new GivenAbility());
            var hero = NewHero(world);
            var footman = NewFootman(world);
            TestLog.Lines.Clear();

            hero.AddAbility(ObjectTypes.FourCC("A0nh")).Level = 2;
            footman.AddAbility<GivenAbility>().Level = 2;

            ExpectLog(new string[0], "the log");
        }

        /// <summary>Taking a hero skill from a hero runs the ability's OnLost after the removal, reading level 0.</summary>
        public static void RemoveRunsOnLost(FakeWorld world)
        {
            Arena(world);
            var hero = NewHero(world);
            hero.AddAbility<GivenAbility>();
            TestLog.Lines.Clear();

            hero.RemoveAbility<GivenAbility>();

            ExpectLog(new string[] { "ability lost level 0" }, "the log");
        }

        /// <summary>Taking an ability that is not a hero skill, or one from a unit that is not a hero, or one the unit lacks, runs no OnLost.</summary>
        public static void OnlyHeroSkillsOfHeroesAreLost(FakeWorld world)
        {
            Arena(world);
            ObjectTypes.Ability(ObjectTypes.FourCC("A0nh"), () => new GivenAbility());
            var hero = NewHero(world);
            var footman = NewFootman(world);
            hero.AddAbility(ObjectTypes.FourCC("A0nh"));
            footman.AddAbility<GivenAbility>();
            TestLog.Lines.Clear();

            hero.RemoveAbility(ObjectTypes.FourCC("A0nh"));
            footman.RemoveAbility<GivenAbility>();
            hero.RemoveAbility<GivenAbility>();

            ExpectLog(new string[0], "the log");
        }

        /// <summary>Removing an ability ends its cast that is running, as the game fires its end-cast inside the removal (seen 11-02).</summary>
        public static void RemoveEndsRunningCast(FakeWorld world)
        {
            ObjectTypes.Ability(ObjectTypes.FourCC("A000"), () => new TestBolt());
            var footman = NewFootman(world);
            var bolt = footman.AddAbility<TestBolt>();
            Inbound.SpellEffect(footman.Handle, ObjectTypes.FourCC("A000"), null, 0f, 0f);
            var cast = bolt.Given!;

            footman.RemoveAbility<TestBolt>();

            Expect.True(cast.HasEnded, "the cast after the removal");
        }

        /// <summary>A class whose only reaction is OnLost makes the world deliver neither the skill event nor the spell event.</summary>
        public static void LostListensForNothing(FakeWorld world)
        {
            ObjectTypes.Ability(ObjectTypes.FourCC("A0lo"), () => new LostOnly());
            NewFootman(world).AbilityOf(ObjectTypes.FourCC("A0lo"));

            Expect.True(!world.Listening.Contains(GameEvent.HeroSkill), "the skill event is not listened for");
            Expect.True(!world.Listening.Contains(GameEvent.Spell), "the spell event is not listened for");
        }

        /// <summary>An ability whose only reaction is OnLost.</summary>
        private sealed class LostOnly : Ability
        {
            /// <summary>Does nothing; overriding it is what counts.</summary>
            public override void OnLost()
            {
            }
        }
#if DEBUG

        /// <summary>A development build reports a level the game kept within the ability's levels, and the level is still set.</summary>
        public static void LevelOutOfRangeReported(FakeWorld world)
        {
            Arena(world);
            var given = NewFootman(world).AddAbility<GivenAbility>();
            var before = world.Printed.Count;

            given.Level = 5;

            Expect.Equal(before + 1, world.Printed.Count, "the lines printed");
            Expect.True(world.Printed[before].Contains("GivenAbility.Level"), "the report names the ability: " + world.Printed[before]);
            Expect.True(world.Printed[before].Contains("level 5, and the game gave 3"), "the report gives the levels: " + world.Printed[before]);
            Expect.Equal(3, given.Level, "the level set");
        }

        /// <summary>A development build reports a level set on an ability the unit lacks.</summary>
        public static void LevelOfMissingReported(FakeWorld world)
        {
            var slow = NewFootman(world).AbilityOf(ObjectTypes.FourCC("Aslo"));
            var before = world.Printed.Count;

            slow.Level = 2;

            Expect.Equal(before + 1, world.Printed.Count, "the lines printed");
            Expect.True(world.Printed[before].Contains("does not have"), "the report says why: " + world.Printed[before]);
        }
#endif
    }
}
