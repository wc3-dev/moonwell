using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Testing
{
    /// <summary>Cases that check summons (decision 227): summoned by code with the summoned type and timed life, linked to the summoner, ended by the scope, limited, dying with the summoner, the game's own summons, and the reactions.</summary>
    public static class SummonCases
    {
        private static readonly int Wolf = ObjectTypes.FourCC("osw1");
        private static readonly int Bear = ObjectTypes.FourCC("ngz1");
        private static readonly int Druid = ObjectTypes.FourCC("H002");

        /// <summary>A summoner class that logs OnSummon.</summary>
        private sealed class Druidic : Unit
        {
            public override void OnSummon(Unit summonedUnit)
            {
                TestLog.Lines.Add("summoner " + Names.IdText(summonedUnit.TypeId));
            }
        }

        /// <summary>A unit component that logs OnSummon.</summary>
        private sealed class Pack : UnitComponent
        {
            public override void OnSummon(Unit summonedUnit)
            {
                TestLog.Lines.Add("component");
            }
        }

        /// <summary>A map component that logs OnUnitSummoned with its name.</summary>
        private sealed class Watch : GameComponent
        {
            public override void OnUnitSummoned(Unit summoningUnit, Unit summonedUnit)
            {
                TestLog.Lines.Add("game " + (summonedUnit.Summoner == summoningUnit));
            }
        }

        /// <summary>A player component that logs OnUnitSummoned.</summary>
        private sealed class PlayerWatch : PlayerComponent
        {
            public override void OnUnitSummoned(Unit summoningUnit, Unit summonedUnit)
            {
                TestLog.Lines.Add("player");
            }
        }

        /// <summary>A footman of player 0.</summary>
        private static Unit Summoner(FakeWorld world)
        {
            TestLog.Lines.Clear();
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: 0))!;
        }

        /// <summary>A summon has the summoned type, the game's timed life for its seconds, the summoner's player and place, and is linked to its summoner.</summary>
        public static void MadeAndLinked(FakeWorld world)
        {
            var summoner = Summoner(world);
            var wolf = summoner.Summon(Wolf, 100f, 200f, 30f, Game.Instance);
            Expect.True(world.Summoned.Contains(wolf.Handle), "the summoned type");
            Expect.Near(30f, world.TimedLives[wolf.Handle], "the timed life");
            Expect.Same(summoner.Owner, wolf.Owner, "the summoner's player");
            Expect.Near(100f, wolf.X, "the x");
            Expect.Same(summoner, wolf.Summoner, "the summoner");
            Expect.Equal(1, summoner.Summons.Count, "the summoner's summons");
            Expect.Same(wolf, summoner.Summons[0], "the summon in the list");
            Expect.True(summoner.Summoner == null, "nobody summoned the summoner");
        }

        /// <summary>No seconds give no timed life.</summary>
        public static void NoTimeNoTimedLife(FakeWorld world)
        {
            var wolf = Summoner(world).Summon(Wolf, 0f, 0f, 0f, Game.Instance);
            Expect.True(!world.TimedLives.ContainsKey(wolf.Handle), "no timed life");
            Expect.True(world.Summoned.Contains(wolf.Handle), "still summoned");
        }

        /// <summary>The scope's end kills the summon, as its time running out does, and the corpse stays; the dead leave Summons.</summary>
        public static void ScopeEndKills(FakeWorld world)
        {
            var summoner = Summoner(world);
            var scope = new TestScope("cast", new List<string>());
            Game.Instance.Adopt(scope);
            var wolf = summoner.Summon(Wolf, 0f, 0f, 30f, scope);
            scope.End();
            Expect.True(!wolf.IsAlive, "the summon died");
            Expect.True(!wolf.IsGone, "its corpse is still in the game");
            Expect.Equal(0, summoner.Summons.Count, "the dead leave Summons");
        }

        /// <summary>A summon removed before its scope ends lets the scope go: the scope's end does nothing more.</summary>
        public static void RemovedLetsGo(FakeWorld world)
        {
            var summoner = Summoner(world);
            var scope = new TestScope("cast", new List<string>());
            Game.Instance.Adopt(scope);
            var wolf = summoner.Summon(Wolf, 0f, 0f, 30f, scope);
            wolf.Remove();
            Expect.Equal(0, scope.ChildCount, "the scope holds nothing");
            scope.End();
            Expect.Equal(0, summoner.Summons.Count, "no summons");
        }

        /// <summary>Timed life running out kills the summon with no killer.</summary>
        public static void TimedLifeKills(FakeWorld world)
        {
            var summoner = Summoner(world);
            var wolf = summoner.Summon(Wolf, 0f, 0f, 2f, Game.Instance);
            world.ExpireTimedLife(wolf.Handle);
            Expect.True(!wolf.IsAlive, "the summon died");
            Expect.Equal(0, summoner.Summons.Count, "the dead leave Summons");
        }

        /// <summary>Past the limit the summoner's oldest of the type die; other types do not count.</summary>
        public static void LimitKillsOldest(FakeWorld world)
        {
            var summoner = Summoner(world);
            var first = summoner.Summon(Wolf, 0f, 0f, 0f, Game.Instance, limit: 2);
            var bear = summoner.Summon(Bear, 0f, 0f, 0f, Game.Instance, limit: 2);
            var second = summoner.Summon(Wolf, 0f, 0f, 0f, Game.Instance, limit: 2);
            Expect.True(first.IsAlive, "two wolves: the first lives");
            var third = summoner.Summon(Wolf, 0f, 0f, 0f, Game.Instance, limit: 2);
            Expect.True(!first.IsAlive, "the oldest wolf died");
            Expect.True(second.IsAlive && third.IsAlive && bear.IsAlive, "the others live");
            Expect.Equal(3, summoner.Summons.Count, "two wolves and the bear");
            Expect.Same(bear, summoner.Summons[0], "oldest first");
        }

        /// <summary>A summon made to die with its summoner dies when the summoner dies, or leaves the game; one not made so lives on.</summary>
        public static void DiesWithSummoner(FakeWorld world)
        {
            var summoner = Summoner(world);
            var bound = summoner.Summon(Wolf, 0f, 0f, 0f, Game.Instance, diesWithSummoner: true);
            var free = summoner.Summon(Wolf, 0f, 0f, 0f, Game.Instance);
            summoner.Kill();
            Expect.True(!bound.IsAlive, "the bound summon died");
            Expect.True(free.IsAlive, "the other lives");
            Expect.Same(summoner, free.Summoner, "the summoner is kept");

            var second = Summoner(world);
            var bound2 = second.Summon(Wolf, 0f, 0f, 0f, Game.Instance, diesWithSummoner: true);
            second.Remove();
            Expect.True(!bound2.IsAlive, "a removed summoner's bound summon died");
        }

        /// <summary>A summon runs the summoner's OnSummon, its components', the game's OnUnitSummoned, then its player's.</summary>
        public static void ReactionOrder(FakeWorld world)
        {
            ObjectTypes.Unit(Druid, () => new Druidic());
            Game.Instance.Add(new Watch());
            Player.Get(0).Add(new PlayerWatch());
            var druid = Unit.Of(world.NewUnit(Druid, slot: 0))!;
            druid.Add(new Pack());
            TestLog.Lines.Clear();
            druid.Summon(Wolf, 0f, 0f, 0f, Game.Instance);
            Expect.Equal("summoner osw1,component,game True,player", string.Join(",", TestLog.Lines), "the reactions");
        }

        /// <summary>The game's summon, once listened for, is taken in, linked, and runs the reactions; reading Summoner listens from the start.</summary>
        public static void GameSummonLinks(FakeWorld world)
        {
            Moonwell.Runtime.ListenForSummons();
            Expect.True(world.Listening.Contains(GameEvent.Summon), "listens for summons");
            Game.Instance.Add(new Watch());
            var summoner = Summoner(world);
            var elemental = world.NewUnit(ObjectTypes.FourCC("hwat"), slot: 0);
            Summoning.FromGame(summoner.Handle, elemental);
            var summon = Unit.Of(elemental)!;
            Expect.Same(summoner, summon.Summoner, "the summoner");
            Expect.Same(summon, summoner.Summons[0], "in the summoner's summons");
            Expect.Equal("game True", string.Join(",", TestLog.Lines), "the reactions");
            Summoning.FromGame(summoner.Handle, elemental);
            Expect.Equal(1, summoner.Summons.Count, "a second event for it links nothing more");
        }

        /// <summary>A summon event with no summoned unit (Mirror Image's image the hero took the place of) links nothing and runs no reaction.</summary>
        public static void GameSummonOfNoUnit(FakeWorld world)
        {
            Moonwell.Runtime.ListenForSummons();
            Game.Instance.Add(new Watch());
            var summoner = Summoner(world);
            Summoning.FromGame(summoner.Handle, null);
            Expect.Equal(0, summoner.Summons.Count, "no summon");
            Expect.Equal("", string.Join(",", TestLog.Lines), "no reaction");
        }

        /// <summary>A summon in a scope that has ended dies at once and runs no reaction.</summary>
        public static void EndedScopeDiesAtOnce(FakeWorld world)
        {
            Game.Instance.Add(new Watch());
            var summoner = Summoner(world);
            var scope = new TestScope("over", new List<string>());
            Game.Instance.Adopt(scope);
            scope.End();
            var wolf = summoner.Summon(Wolf, 0f, 0f, 30f, scope);
            Expect.True(!wolf.IsAlive, "the summon died at once");
            Expect.Equal(0, TestLog.Lines.Count, "no reaction");
        }
    }
}
