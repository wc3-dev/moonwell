using System;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>Cases for the AI package's facts and blackboards (decisions 298 and 299): the chain, written and worked-out facts, ages, and a unit given to another player.</summary>
    public static class FactCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>A written number, as a map makes one.</summary>
        private static readonly Fact<int> Food = new Fact<int>("food");

        /// <summary>A written flag.</summary>
        private static readonly Fact<bool> Alarm = new Fact<bool>("alarm");

        /// <summary>A written unit, which may be null.</summary>
        private static readonly Fact<Unit?> Leader = new Fact<Unit?>("leader");

        /// <summary>The chain's order and shadowing, Remove uncovering the next, Age, and float.MaxValue and default when nothing holds a fact.</summary>
        public static void Written(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain());
            var playerBrain = Player.Get(0).Add(new StepPlayerBrain());
            var gameBrain = Game.Instance.Add(new StepGameBrain());
            var game = Game.Instance.Blackboard();
            var mine = Player.Get(0).Blackboard();
            var other = Player.Get(1).Blackboard();
            Expect.Same(game, Game.Instance.Blackboard(), "the game's is made once");
            Expect.Same(mine, Player.Get(0).Blackboard(), "a player's is made once");
            Expect.True(game.Player == null && game.Unit == null, "the game's has no player or unit");
            Expect.Same(Player.Get(1), other.Player, "a player's has its player");

            Expect.Equal(0, brain.Read(Food), "0 when nothing holds it");
            Expect.Equal(false, brain.Read(Alarm), "false");
            Expect.True(brain.Read(Leader) == null, "null");
            Expect.True(brain.Age(Food) == float.MaxValue, "no age when nothing holds it");

            game.Set(Food, 1);
            Expect.Equal(1, brain.Read(Food), "the game's ends a unit brain's chain");
            Expect.Equal(1, playerBrain.Read(Food), "a player brain's");
            Expect.Equal(1, gameBrain.Read(Food), "a game brain's");
            Expect.Equal(1, other.Read(Food), "and a player's");
            mine.Set(Food, 2);
            Expect.Equal(2, brain.Read(Food), "the owner's shadows the game's");
            Expect.Equal(2, playerBrain.Read(Food), "for its player's brain too");
            Expect.Equal(1, gameBrain.Read(Food), "a game brain's chain has no player");
            Expect.Equal(1, other.Read(Food), "nor has another player's");
            brain.Blackboard.Set(Food, 3);
            Expect.Equal(3, brain.Read(Food), "its own first");
            Expect.Equal(2, playerBrain.Read(Food), "a brain's own is only its own");

            brain.Blackboard.Remove(Food);
            Expect.Equal(2, brain.Read(Food), "Remove uncovers the owner's");
            mine.Remove(Food);
            Expect.Equal(1, brain.Read(Food), "then the game's");
            game.Remove(Food);
            Expect.Equal(0, brain.Read(Food), "then nothing");
            Expect.True(brain.Age(Food) == float.MaxValue, "with no age");

            game.Set(Food, 5);
            world.Advance(1f);
            Expect.Near(1f, brain.Age(Food), "the age of the value found up the chain");
            Expect.Near(1f, game.Age(Food), "on the blackboard that holds it");
            brain.Blackboard.Set(Food, 6);
            Expect.Near(0f, brain.Age(Food), "its own, just written");
            mine.Set(Alarm, true);
            Expect.Equal(true, brain.Read(Alarm), "another type");

            brain.Blackboard.Set(Leader, null);
            game.Set(Leader, unit);
            Expect.True(brain.Read(Leader) == null, "a null written shadows too");
            Expect.Near(0f, brain.Age(Leader), "and has an age");
            Expect.Same(unit, playerBrain.Read(Leader), "the game's unit");

            unit.Remove();
            brain.Blackboard.Set(Food, 7);
            Expect.Near(1f, brain.Age(Food), "an ended brain's blackboard holds nothing more, so the game's is found");
        }

        /// <summary>A worked-out fact runs once within its keep and again after it, kept per blackboard, never chained; Set on one is refused, and a keep below 0 throws.</summary>
        public static void WorkedOut(FakeWorld world)
        {
            var runs = 0;
            var near = new Fact<int>("near", keep: 0.25f, read: board =>
            {
                runs++;
                if (board.Unit != null)
                {
                    return 7;
                }

                return board.Player != null ? 5 : 3;
            });
            var a = Unit.Of(world.NewUnit(Footman, slot: 0))!.Add(new StepBrain());
            var b = Unit.Of(world.NewUnit(Footman, slot: 0))!.Add(new StepBrain());

            Expect.True(a.Age(near) == float.MaxValue, "no age before it is worked out");
            Expect.Equal(7, a.Read(near), "worked out for the unit brain's blackboard");
            Expect.Equal(7, a.Read(near), "kept");
            Expect.Equal(1, runs, "one run");
            Expect.Near(0f, a.Age(near), "just worked out");
            world.Advance(0.2f);
            Expect.Equal(7, a.Read(near), "within its keep");
            Expect.Equal(1, runs, "no run within the keep");
            Expect.Near(0.2f, a.Age(near), "its age");
            world.Advance(0.1f);
            a.Read(near);
            Expect.Equal(2, runs, "a run once older than the keep");
            Expect.Near(0f, a.Age(near), "worked out again");

            Expect.Equal(7, b.Read(near), "another blackboard");
            Expect.Equal(3, runs, "keeps its own");
            Expect.Equal(3, Game.Instance.Blackboard().Read(near), "the game's");
            Expect.Equal(5, Player.Get(0).Blackboard().Read(near), "a player's");
            Expect.Equal(5, runs, "each its own run");
            var c = Unit.Of(world.NewUnit(Footman, slot: 0))!.Add(new StepBrain());
            Expect.True(c.Age(near) == float.MaxValue, "never chained: the player's and the game's kept values are not its");
            Expect.Equal(7, c.Read(near), "it works out its own");
            Expect.Equal(6, runs, "with a run");

            a.Blackboard.Set(near, 9);
            Expect.Equal(1, world.Logged.Count, "Set on a worked-out fact is reported");
            Expect.Equal("Moonwell: Blackboard.Set failed: the fact \"near\" is worked out by its read function, so it cannot be set", world.Logged[0], "the logged line");
            Expect.Equal(7, a.Read(near), "and refused");
            Expect.Equal(6, runs, "with no run");
            a.Blackboard.Remove(near);
            a.Read(near);
            Expect.Equal(7, runs, "Remove has it worked out again");

            var counted = 0;
            var instant = new Fact<int>("instant", 0f, board => ++counted);
            Expect.Equal(1, a.Read(instant), "keep 0: worked out");
            Expect.Equal(1, a.Read(instant), "and kept for the instant");
            world.Advance(0.02f);
            Expect.Equal(2, a.Read(instant), "worked out again in the next");

            Expect.Throws<ArgumentException>(() => new Fact<int>("bad", -1f, board => 0), "a keep below 0");
        }

        /// <summary>After unit.Owner = other, a unit brain reads the new owner's blackboard, keeps its own facts, and is not woken.</summary>
        public static void OwnerChange(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = unit.Add(new StepBrain());
            Player.Get(0).Blackboard().Set(Food, 10);
            Player.Get(2).Blackboard().Set(Food, 20);
            brain.Blackboard.Set(Alarm, true);
            Expect.Equal(10, brain.Read(Food), "the first owner's");
            Expect.Same(Player.Get(0), brain.Blackboard.Player, "the first owner");

            unit.Owner = Player.Get(2);
            Expect.Equal(20, brain.Read(Food), "the new owner's");
            Expect.Same(Player.Get(2), brain.Blackboard.Player, "the new owner");
            Expect.Equal(true, brain.Read(Alarm), "its own facts stay");
            BrainCases.Steps(world, 5);
            Expect.Equal(0, brain.Thought.Count, "not woken");
        }
    }
}
