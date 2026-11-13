using System.Collections.Generic;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>Cases for the AI package's brains and their loop (decision 297): who thinks, ticks, the spread, wakes, errors and the order of a step.</summary>
    public static class BrainCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>Runs the loop for a number of its steps.</summary>
        internal static void Steps(FakeWorld world, int count)
        {
            for (var i = 0; i < count; i++)
            {
                world.Advance(Minds.StepSeconds);
            }
        }

        /// <summary>The thinks of a list of brains, by step, from 0 up to the last step.</summary>
        private static int[] PerStep(List<StepGameBrain> brains, int last)
        {
            var counts = new int[last + 1];
            for (var i = 0; i < brains.Count; i++)
            {
                for (var k = 0; k < brains[i].Thought.Count; k++)
                {
                    counts[brains[i].Thought[k]]++;
                }
            }

            return counts;
        }

        /// <summary>A brain on a unit, a player and the game thinks; the unit's ends with the unit and never thinks after, and the loop ends with the last brain and starts again with the next.</summary>
        public static void ThinkOnEachActor(FakeWorld world)
        {
            Player.Get(0);
            Player.Get(1);
            var before = Game.Instance.ChildCount;
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var unitBrain = unit.Add(new StepBrain { ThinkEvery = 0.02f });
            var playerBrain = Player.Get(1).Add(new StepPlayerBrain());
            var gameBrain = Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.02f });
            Expect.Near(0.02f, playerBrain.ThinkEvery, "ThinkEvery set in OnAdded");
            Steps(world, 3);
            Expect.Equal("1 2 3", string.Join(" ", unitBrain.Thought), "the unit's brain");
            Expect.Equal("1 2 3", string.Join(" ", playerBrain.Thought), "the player's brain");
            Expect.Equal("1 2 3", string.Join(" ", gameBrain.Thought), "the game's brain");
            Expect.Same(unit, unitBrain.Blackboard.Unit, "a unit brain's blackboard has its unit");
            Expect.Same(Player.Get(0), unitBrain.Blackboard.Player, "and its owner");
            Expect.Same(Player.Get(1), playerBrain.Blackboard.Player, "a player brain's has its player");
            Expect.True(playerBrain.Blackboard.Unit == null, "and no unit");
            Expect.True(gameBrain.Blackboard.Player == null && gameBrain.Blackboard.Unit == null, "a game brain's has neither");

            unit.Remove();
            Expect.True(unitBrain.HasEnded, "the unit's brain ends with the unit");
            Steps(world, 3);
            Expect.Equal(3, unitBrain.Thought.Count, "and never thinks after");
            Expect.Equal(6, playerBrain.Thought.Count, "the player's thinks on");
            Expect.Equal(6, gameBrain.Thought.Count, "the game's thinks on");

            playerBrain.End();
            gameBrain.End();
            Expect.Equal(before, Game.Instance.ChildCount, "the loop's timer ends with the last brain");
            var next = Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.02f });
            Steps(world, 2);
            Expect.Equal(2, next.Thought.Count, "a new brain starts it again");
        }

        /// <summary>0.25 s is 13 steps, as 12.5 rounds a half up, through whole milliseconds; 0 thinks only when woken; a change of ThinkEvery while running places the next tick from then.</summary>
        public static void Ticks(FakeWorld world)
        {
            Expect.Equal(13, Minds.StepsOf(0.25f), "a quarter second, 12.5 steps, rounds up");
            Expect.Equal(2, Minds.StepsOf(0.03f), "1.5 steps round up, in the game's floats and in Lua's doubles");
            Expect.Equal(1, Minds.StepsOf(0.01f), "half a step rounds up");
            Expect.Equal(1, Minds.StepsOf(0.005f), "at least one step");
            Expect.Equal(50000000, Minds.StepsOf(3000000f), "capped at 1,000,000 s");
            Expect.Equal(1, Minds.StepsOf(0.02f), "one step");
            Expect.Equal(50, Minds.StepsOf(1f), "a second");
            Expect.Equal(0, Minds.StepsOf(0f), "0 is no tick");
            Expect.Equal(0, Minds.StepsOf(-1f), "nor is below 0");

            var ticking = Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.25f });
            var woken = Game.Instance.Add(new StepGameBrain());
            Steps(world, 40);
            Expect.Equal("13 26 39", string.Join(" ", ticking.Thought), "every 13 steps, on the steps of its phase");
            Expect.Equal(0, woken.Thought.Count, "0 never thinks unwoken");
            woken.Wake();
            Steps(world, 20);
            Expect.Equal("41", string.Join(" ", woken.Thought), "woken, it thinks once at the next step");

            Expect.Equal(60, Minds.Current().Step, "the steps run");
            ticking.ThinkEvery = 0.04f;
            Expect.Near(0.04f, ticking.ThinkEvery, "ThinkEvery as set");
            var before = ticking.Thought.Count;
            Steps(world, 10);
            var after = ticking.Thought.GetRange(before, ticking.Thought.Count - before);
            Expect.Equal(5, after.Count, "every 2 steps from the change");
            Expect.True(after[0] > 60 && after[0] <= 62, "the first within the new tick, not at the old tick's 65: " + after[0]);
            for (var i = 1; i < after.Count; i++)
            {
                Expect.Equal(2, after[i] - after[i - 1], "2 steps apart");
            }

            ticking.ThinkEvery = 0f;
            var stopped = ticking.Thought.Count;
            Steps(world, 30);
            Expect.Equal(stopped, ticking.Thought.Count, "0 stops the tick");
        }

        /// <summary>100 brains added in one instant at 0.25 s think over the 13 steps of their tick, 7 or 8 a step, not 100 in one.</summary>
        public static void Spread(FakeWorld world)
        {
            var brains = new List<StepGameBrain>();
            for (var i = 0; i < 100; i++)
            {
                brains.Add(Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.25f }));
            }

            Steps(world, 26);
            var counts = PerStep(brains, 26);
            var total = 0;
            for (var step = 1; step <= 26; step++)
            {
                Expect.True(counts[step] == 7 || counts[step] == 8, "step " + step + " has 7 or 8, not " + counts[step]);
                total += counts[step];
            }

            Expect.Equal(200, total, "each thinks once in each 13 steps");
        }

        /// <summary>13 brains at 0.25 s, added one every 0.5 s, think in 13 different phases (their steps modulo 13), not all in one.</summary>
        public static void SpreadAtASteadyRate(FakeWorld world)
        {
            var brains = new List<StepGameBrain>();
            for (var k = 0; k < 13; k++)
            {
                brains.Add(Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.25f }));
                Steps(world, 25);
            }

            var phases = new List<int>();
            for (var k = 0; k < brains.Count; k++)
            {
                var phase = brains[k].Thought[0] % 13;
                Expect.True(!phases.Contains(phase), "brain " + k + " has a phase of its own, not " + phase);
                phases.Add(phase);
            }
        }

        /// <summary>A brain that another wakes in each of its every-step thinks is never starved: a wake that comes in a step before its waiting think is that think, so it thinks every second step.</summary>
        public static void WokenByAnother(FakeWorld world)
        {
            var woken = Game.Instance.Add(new StepGameBrain());
            var waker = Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.02f });
            waker.During = () => woken.Wake();
            Steps(world, 10);
            Expect.Equal("2 4 6 8 10", string.Join(" ", woken.Thought), "each wake thinks within a step");
        }

        /// <summary>An ended brain's tick, and a tick a change replaced, leave their step's list at once, not when the loop reaches it.</summary>
        public static void TicksLeaveTheirStep(FakeWorld world)
        {
            var keeper = Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.02f });
            Steps(world, 3);
            var far = Game.Instance.Add(new StepGameBrain { ThinkEvery = 100f });
            var first = far.mind.TickAt;
            Expect.Equal(1, Minds.Current().DueAt(first), "its tick waits at its step");
            far.ThinkEvery = 200f;
            var second = far.mind.TickAt;
            Expect.Equal(0, Minds.Current().DueAt(first), "a replaced tick leaves its step");
            Expect.Equal(1, Minds.Current().DueAt(second), "the new one waits at its own");
            far.End();
            Expect.Equal(0, Minds.Current().DueAt(second), "an ended brain's leaves too");
            Steps(world, 2);
            Expect.Equal(5, keeper.Thought.Count, "the loop runs on");
        }

        /// <summary>Three wakes in an instant give one think at the next step; a wake and a tick due at one step give one think; a wake inside a step goes to the next.</summary>
        public static void WakeOnce(FakeWorld world)
        {
            var woken = Game.Instance.Add(new StepGameBrain());
            woken.Wake();
            woken.Wake();
            woken.Wake();
            Steps(world, 6);
            Expect.Equal("1", string.Join(" ", woken.Thought), "three wakes, one think");

            var ticking = Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.25f });
            Steps(world, 6);
            ticking.Wake();
            Steps(world, 1);
            Expect.Equal("13", string.Join(" ", ticking.Thought), "a wake at its first tick's step is that think");
            Steps(world, 12);
            ticking.Wake();
            Steps(world, 1);
            Expect.Equal("13 26", string.Join(" ", ticking.Thought), "and at a later tick's step, too");

            var self = Game.Instance.Add(new StepGameBrain());
            self.During = () =>
            {
                if (self.Thought.Count == 1)
                {
                    self.Wake();
                }
            };
            self.Wake();
            Steps(world, 4);
            Expect.Equal("27 28", string.Join(" ", self.Thought), "a wake inside a step goes to the next");
        }

        /// <summary>A Think that throws is reported, the other brains still think, and it thinks again at its next tick.</summary>
        public static void ThrowingThink(FakeWorld world)
        {
            var thrower = Game.Instance.Add(new ThrowingBrain { ThinkEvery = 0.02f });
            var other = Game.Instance.Add(new StepGameBrain { ThinkEvery = 0.02f });
            Steps(world, 2);
            Expect.Equal(2, other.Thought.Count, "the other brain thinks");
            Expect.Equal(2, thrower.Runs, "the thrower thinks again");
            Expect.True(world.Logged.Count >= 1, "reported");
            Expect.Equal("Moonwell: ThrowingBrain.Think failed: boom", world.Logged[0], "the logged line");
        }

        /// <summary>Within a step, brains think in the order they became due.</summary>
        public static void DueOrder(FakeWorld world)
        {
            var log = new List<string>();
            var a = Game.Instance.Add(new StepGameBrain { Label = "a", Log = log });
            var b = Game.Instance.Add(new StepGameBrain { Label = "b", Log = log });
            var c = Game.Instance.Add(new StepGameBrain { Label = "c", Log = log });
            c.Wake();
            a.Wake();
            b.Wake();
            Steps(world, 1);
            Expect.Equal("c a b", string.Join(" ", log), "in the order woken");

            log.Clear();
            b.ThinkEvery = 0.02f;
            c.ThinkEvery = 0.02f;
            Steps(world, 1);
            a.Wake();
            Steps(world, 1);
            Expect.Equal("b c b c a", string.Join(" ", log), "ticks in the order placed, then a wake that came after them");
        }
    }
}
