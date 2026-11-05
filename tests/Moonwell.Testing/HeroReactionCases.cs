using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check a hero's reactions: levelling up, once for each level gained, and learning a skill.</summary>
    public static class HeroReactionCases
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

        /// <summary>Registers the level hero and the learned ability, a game watch, and a player watch for player 0.</summary>
        private static void Arena()
        {
            ObjectTypes.Unit(ObjectTypes.FourCC("H0lv"), () => new LevelHero());
            ObjectTypes.Ability(ObjectTypes.FourCC("A0ln"), () => new LearnedAbility());
            Game.Instance.Add(new GameLevelWatch());
            Player.Get(0).Add(new PlayerLevelWatch("p0"));
            LevelHero.RemoveAt = 0;
            LevelHero.RaiseAt = 0;
        }

        /// <summary>Makes a level hero of player 0, taken in.</summary>
        private static LevelHero NewHero(FakeWorld world)
        {
            return (LevelHero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("H0lv"), slot: 0, hero: true))!;
        }

        /// <summary>A level-up runs the hero's OnLevelUp, then its components', then the game's, then the hero's player's OnHeroLevelUp.</summary>
        public static void LevelUpOrder(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            TestLog.Lines.Clear();

            world.LevelHero(hero.Handle, 2, true);

            ExpectLog(new string[] { "hero level 2 reads 2", "comp level 2", "game level 2", "p0 level 2" }, "the log");
        }

        /// <summary>The game fires one event for a jump of several levels; the reactions run once for each level gained, in order, while Level reads the level the jump ended at.</summary>
        public static void LevelJump(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            TestLog.Lines.Clear();

            world.LevelHero(hero.Handle, 4, true);

            ExpectLog(new string[]
            {
                "hero level 2 reads 4", "comp level 2", "game level 2", "p0 level 2",
                "hero level 3 reads 4", "comp level 3", "game level 3", "p0 level 3",
                "hero level 4 reads 4", "comp level 4", "game level 4", "p0 level 4",
            }, "the log");
            Expect.Equal(1, world.LevelEvents, "the game's level events");
        }

        /// <summary>A level-up reaction that lowers the hero in a jump of several levels ends the jump: the levels taken away run nothing, and the hero's next real level-up runs its level once.</summary>
        public static void LoweredInJump(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            LevelHero.RaiseAt = 2;
            LevelHero.RaiseTo = 2;
            TestLog.Lines.Clear();

            world.LevelHero(hero.Handle, 4, true);

            ExpectLog(new string[] { "hero level 2 reads 4", "comp level 2", "game level 2", "p0 level 2" }, "the jump");
            LevelHero.RaiseAt = 0;
            TestLog.Lines.Clear();

            world.LevelHero(hero.Handle, 3, true);

            ExpectLog(new string[] { "hero level 3 reads 3", "comp level 3", "game level 3", "p0 level 3" }, "the next level-up");
        }

        /// <summary>Experience that gives several levels fires one level event, and the reactions run once for each level gained.</summary>
        public static void LevelUpFromExperience(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            TestLog.Lines.Clear();

            hero.AddExperience(500);

            ExpectLog(new string[]
            {
                "hero level 2 reads 3", "comp level 2", "game level 2", "p0 level 2",
                "hero level 3 reads 3", "comp level 3", "game level 3", "p0 level 3",
            }, "the log");
            Expect.Equal(1, world.LevelEvents, "the game's level events");
        }

        /// <summary>SetLevel runs the level-up reactions inside the call; a lower level runs none, and the levels gained again after it run again.</summary>
        public static void LevelUpThroughSetLevel(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            TestLog.Lines.Clear();

            hero.SetLevel(3);
            ExpectLog(new string[]
            {
                "hero level 2 reads 3", "comp level 2", "game level 2", "p0 level 2",
                "hero level 3 reads 3", "comp level 3", "game level 3", "p0 level 3",
            }, "raised to 3");

            TestLog.Lines.Clear();
            hero.SetLevel(1);
            ExpectLog(new string[0], "lowered to 1");

            hero.SetLevel(2);
            ExpectLog(new string[] { "hero level 2 reads 2", "comp level 2", "game level 2", "p0 level 2" }, "raised to 2 again");
        }

        /// <summary>A hero taken in at a level above 1 runs the reactions only for the levels it gains after.</summary>
        public static void LevelUpFromTakenIn(FakeWorld world)
        {
            Arena();
            var handle = world.NewUnit(ObjectTypes.FourCC("H0lv"), slot: 0, hero: true);
            world.SetLevel(handle, 5);
            Unit.Of(handle);
            TestLog.Lines.Clear();

            world.LevelHero(handle, 6, true);

            ExpectLog(new string[] { "hero level 6 reads 6", "comp level 6", "game level 6", "p0 level 6" }, "the log");
        }

        /// <summary>Levels a known hero gained while nothing listened ran no reactions, so the first reaction added later runs only the levels gained after it.</summary>
        public static void LevelUpAfterQuietLevels(FakeWorld world)
        {
            var handle = world.NewUnit(ObjectTypes.FourCC("Hpal"), slot: 0, hero: true);
            var hero = (Hero)Unit.Of(handle)!;
            world.LevelHero(handle, 5, true);
            Expect.Equal(0, world.LevelEvents, "no level event while nothing listens");
            hero.Add(new LevelComp());
            TestLog.Lines.Clear();

            world.LevelHero(handle, 6, true);

            ExpectLog(new string[] { "comp level 6" }, "the log");
        }

        /// <summary>A reaction that raises the hero again in the middle of a jump: each level runs once, and each level's reactions begin in order.</summary>
        public static void LevelUpRaisedInReaction(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            LevelHero.RaiseAt = 3;
            LevelHero.RaiseTo = 5;
            TestLog.Lines.Clear();

            world.LevelHero(hero.Handle, 4, true);

            ExpectLog(new string[]
            {
                "hero level 2 reads 4", "comp level 2", "game level 2", "p0 level 2",
                "hero level 3 reads 4",
                "hero level 4 reads 5", "comp level 4", "game level 4", "p0 level 4",
                "hero level 5 reads 5", "comp level 5", "game level 5", "p0 level 5",
                "comp level 3", "game level 3", "p0 level 3",
            }, "the log");
        }

        /// <summary>A hero Moonwell first sees at its level event runs the reactions once, for the level it reached: the levels it gained in that event are not known.</summary>
        public static void LevelUpFirstSeen(FakeWorld world)
        {
            Game.Instance.Add(new GameLevelWatch());
            var handle = world.NewUnit(ObjectTypes.FourCC("Hpal"), slot: 0, hero: true);
            TestLog.Lines.Clear();

            world.LevelHero(handle, 3, true);

            ExpectLog(new string[] { "game level 3" }, "the log");
        }

        /// <summary>While something wants OnHeroLevelUp, a hero is taken in as it enters, so every level it gains later runs.</summary>
        public static void LevelUpTakesInEnteringHeroes(FakeWorld world)
        {
            Game.Instance.Add(new GameLevelWatch());
            var handle = world.NewUnit(ObjectTypes.FourCC("Hpal"), slot: 0, hero: true);
            var footman = world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0);
            Inbound.Entered(handle);
            Inbound.Entered(footman);
            Expect.True(KnownUnits.Find(handle) != null, "the entering hero is taken in");
            Expect.True(KnownUnits.Find(footman) == null, "a unit that is not a hero is not");
            TestLog.Lines.Clear();

            world.LevelHero(handle, 3, true);

            ExpectLog(new string[] { "game level 2", "game level 3" }, "the log");
        }

        /// <summary>A hero removed by its own level-up reaction: its components, which ended with it, run nothing, the map's reactions still see that level, as they see a death, and no level after it runs.</summary>
        public static void LevelUpStopsWhenRemoved(FakeWorld world)
        {
            Arena();
            LevelHero.RemoveAt = 2;
            var hero = NewHero(world);
            TestLog.Lines.Clear();

            world.LevelHero(hero.Handle, 4, true);

            Expect.True(hero.HasEnded, "the hero ended");
            ExpectLog(new string[] { "hero level 2 reads 4", "game level 2", "p0 level 2" }, "the log");
        }

        /// <summary>A level-up reaction is listened for only once a reaction needs it: a hero class, a unit component or a map component that overrides one.</summary>
        public static void LevelUpListens(FakeWorld world)
        {
            Expect.True(!world.Listening.Contains(GameEvent.HeroLevel), "the level event is not listened for before a reaction needs it");
            Expect.True(!world.Listening.Contains(GameEvent.HeroSkill), "the skill event is not listened for before a reaction needs it");
            Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!.Add(new LevelComp());
            Expect.True(world.Listening.Contains(GameEvent.HeroLevel), "the level event is listened for");
            Expect.True(world.Listening.Contains(GameEvent.HeroSkill), "the skill event is listened for");
        }

        /// <summary>A map component's OnHeroSkillLearned alone makes the world deliver the skill event, and not the level event.</summary>
        public static void SkillListens(FakeWorld world)
        {
            Game.Instance.Add(new SkillWatch());
            Expect.True(world.Listening.Contains(GameEvent.HeroSkill), "the skill event is listened for");
            Expect.True(!world.Listening.Contains(GameEvent.HeroLevel), "the level event is not");
        }

        /// <summary>Learning a skill runs the ability's OnLearned, then the hero's OnSkillLearned, its components', the game's and its player's; inside them the skill point is not yet spent, as in the game.</summary>
        public static void SkillOrder(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            world.SetSkillPoints(hero.Handle, 1);
            TestLog.Lines.Clear();

            Expect.True(world.LearnSkill(hero.Handle, ObjectTypes.FourCC("A0ln")), "the skill is learned");

            ExpectLog(new string[] { "ability learned level 1", "hero learned level 1 points 1", "comp learned level 1", "game learned level 1", "p0 learned level 1" }, "the log");
            Expect.Equal(0, hero.SkillPoints, "the points after");
            Expect.True(hero.AbilityOf(ObjectTypes.FourCC("A0ln")) is LearnedAbility, "the ability object is its class's");
        }

        /// <summary>Raising a learned skill runs the reactions with its new level.</summary>
        public static void SkillRaised(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            world.SetSkillPoints(hero.Handle, 2);
            world.LearnSkill(hero.Handle, ObjectTypes.FourCC("A0ln"));
            TestLog.Lines.Clear();

            world.LearnSkill(hero.Handle, ObjectTypes.FourCC("A0ln"));

            ExpectLog(new string[] { "ability learned level 2", "hero learned level 2 points 1", "comp learned level 2", "game learned level 2", "p0 learned level 2" }, "the log");
        }

        /// <summary>A registered ability class that overrides OnLearned makes the world deliver the skill event, though no hero has it yet.</summary>
        public static void AbilityLearnedListens(FakeWorld world)
        {
            ObjectTypes.Ability(ObjectTypes.FourCC("A0ln"), () => new LearnedAbility());
            Expect.True(world.Listening.Contains(GameEvent.HeroSkill), "the skill event is listened for");
            var hero = Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!;
            world.SetSkillPoints(hero.Handle, 1);
            TestLog.Lines.Clear();

            world.LearnSkill(hero.Handle, ObjectTypes.FourCC("A0ln"));

            ExpectLog(new string[] { "ability learned level 1" }, "the log");
        }

        /// <summary>With no skill point, the game learns nothing and fires nothing, so no reaction runs.</summary>
        public static void SkillWithoutPoint(FakeWorld world)
        {
            Arena();
            var hero = NewHero(world);
            TestLog.Lines.Clear();

            Expect.True(!world.LearnSkill(hero.Handle, ObjectTypes.FourCC("A0ln")), "nothing is learned");

            ExpectLog(new string[0], "the log");
        }

        /// <summary>A game component whose only hero reaction is OnHeroSkillLearned.</summary>
        private sealed class SkillWatch : GameComponent
        {
            /// <summary>Does nothing; overriding it is what counts.</summary>
            public override void OnHeroSkillLearned(Hero learningHero, Ability learnedAbility)
            {
            }
        }
    }
}
