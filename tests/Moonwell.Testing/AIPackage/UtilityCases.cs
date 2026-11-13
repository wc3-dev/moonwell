using System;
using System.Collections.Generic;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>Cases for the AI package's utility AI (decisions 306 to 309): curves, an option's score, a choice's pick and its keep margin, and a brain that chooses.</summary>
    public static class UtilityCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>A written number of 0..1.</summary>
        internal static readonly Fact<float> Life = new Fact<float>("life");

        /// <summary>A written count, scaled by a range.</summary>
        internal static readonly Fact<int> Enemies = new Fact<int>("enemies");

        /// <summary>A written number, of 0..1 or scaled by a range.</summary>
        private static readonly Fact<float> Danger = new Fact<float>("danger");

        /// <summary>A written flag.</summary>
        private static readonly Fact<bool> Alarm = new Fact<bool>("alarm");

        /// <summary>An option's action that does nothing.</summary>
        private static void Nothing()
        {
        }

        /// <summary>Rising, Falling, Line, Power, S and Step at a few inputs, with inputs below 0 and above 1 clamped and results clamped to 0..1; Power(0) throws.</summary>
        public static void Curves(FakeWorld world)
        {
            Expect.Near(0f, Curve.Rising.Of(0f), "Rising at 0");
            Expect.Near(0.25f, Curve.Rising.Of(0.25f), "Rising at 0.25");
            Expect.Near(1f, Curve.Rising.Of(1f), "Rising at 1");
            Expect.Near(0f, Curve.Rising.Of(-0.5f), "Rising below 0, clamped");
            Expect.Near(1f, Curve.Rising.Of(1.5f), "Rising above 1, clamped");
            Expect.Near(1f, Curve.Falling.Of(0f), "Falling at 0");
            Expect.Near(0.75f, Curve.Falling.Of(0.25f), "Falling at 0.25");
            Expect.Near(0f, Curve.Falling.Of(1f), "Falling at 1");
            Expect.Near(1f, Curve.Falling.Of(-1f), "Falling below 0, clamped");
            Expect.Near(0f, Curve.Falling.Of(2f), "Falling above 1, clamped");

            var line = Curve.Line(0.2f, 0.6f);
            Expect.Near(0.2f, line.Of(0f), "Line(0.2, 0.6) at 0");
            Expect.Near(0.4f, line.Of(0.5f), "Line(0.2, 0.6) at 0.5");
            Expect.Near(0.6f, line.Of(1f), "Line(0.2, 0.6) at 1");
            Expect.Near(0.2f, line.Of(-1f), "Line(0.2, 0.6) below 0");
            Expect.Near(0.6f, line.Of(2f), "Line(0.2, 0.6) above 1");
            Expect.Near(0.75f, Curve.Line(1f, 0f).Of(0.25f), "Line(1, 0) falls");
            var steep = Curve.Line(-1f, 2f);
            Expect.Near(0f, steep.Of(0f), "Line(-1, 2) at 0 gives -1, clamped to 0");
            Expect.Near(0f, steep.Of(0.25f), "Line(-1, 2) at 0.25 gives -0.25, clamped to 0");
            Expect.Near(0.5f, steep.Of(0.5f), "Line(-1, 2) at 0.5");
            Expect.Near(1f, steep.Of(0.8f), "Line(-1, 2) at 0.8 gives 1.4, clamped to 1");
            Expect.Near(1f, steep.Of(1f), "Line(-1, 2) at 1 gives 2, clamped to 1");

            var square = Curve.Power(2f);
            Expect.Near(0.25f, square.Of(0.5f), "Power(2) at 0.5");
            Expect.Near(0.01f, square.Of(0.1f), "Power(2) at 0.1");
            Expect.Near(0f, square.Of(-1f), "Power(2) below 0");
            Expect.Near(1f, square.Of(3f), "Power(2) above 1");
            Expect.Near(0.5f, Curve.Power(0.5f).Of(0.25f), "Power(0.5) at 0.25");
            Expect.Throws<ArgumentException>(() => Curve.Power(0f), "Power(0)");
            Expect.Throws<ArgumentException>(() => Curve.Power(-1f), "Power(-1)");

            var s = Curve.S(0.5f, 10f);
            Expect.Near(0.5f, s.Of(0.5f), "S(0.5, 10) at its middle");
            Expect.Near(0.8808f, s.Of(0.7f), "S(0.5, 10) at 0.7: 1 / (1 + e^-2)");
            Expect.Near(0.1192f, s.Of(0.3f), "S(0.5, 10) at 0.3: 1 / (1 + e^2)");
            Expect.Near(0.0067f, s.Of(-1f), "S(0.5, 10) below 0, at 0: 1 / (1 + e^5)");
            Expect.Near(0.9933f, s.Of(2f), "S(0.5, 10) above 1, at 1: 1 / (1 + e^-5)");
            Expect.Near(0.5f, Curve.S(0.3f, 10f).Of(0.3f), "S(0.3, 10) at its middle");
            var fallingS = Curve.S(0.5f, -10f);
            Expect.Near(0.5f, fallingS.Of(0.5f), "S(0.5, -10) at its middle");
            Expect.Near(0.1192f, fallingS.Of(0.7f), "S(0.5, -10) falls: at 0.7, 1 / (1 + e^2)");

            var step = Curve.Step(0.25f);
            Expect.Near(0f, step.Of(0.24f), "Step(0.25) below it");
            Expect.Near(1f, step.Of(0.25f), "Step(0.25) at exactly 0.25");
            Expect.Near(1f, step.Of(0.9f), "Step(0.25) above it");
            Expect.Near(0f, step.Of(-1f), "Step(0.25) below 0");
            Expect.Near(1f, step.Of(2f), "Step(0.25) above 1");
        }

        /// <summary>Points: straight lines between points, the end points' y outside them, a jump, a float[] passed in and copied, results clamped, and the throws.</summary>
        public static void Points(FakeWorld world)
        {
            var two = Curve.Points(0.2f, 0.1f, 0.6f, 0.9f);
            Expect.Near(0.1f, two.Of(0.2f), "at the first point");
            Expect.Near(0.5f, two.Of(0.4f), "halfway between the points");
            Expect.Near(0.7f, two.Of(0.5f), "three quarters of the way");
            Expect.Near(0.9f, two.Of(0.6f), "at the last point");
            Expect.Near(0.1f, two.Of(0.1f), "before the first point, its y");
            Expect.Near(0.9f, two.Of(0.8f), "past the last point, its y");
            Expect.Near(0.1f, two.Of(-1f), "below 0, clamped, then before the first point");
            Expect.Near(0.9f, two.Of(2f), "above 1, clamped, then past the last point");

            var jump = Curve.Points(0f, 0f, 0.5f, 0.2f, 0.5f, 0.8f, 1f, 1f);
            Expect.Near(0.16f, jump.Of(0.4f), "before the jump, on the line to its first point");
            Expect.Near(0.8f, jump.Of(0.5f), "at the jump's x, the later point's y");
            Expect.Near(0.9f, jump.Of(0.75f), "after the jump, on the line from its later point");

            var column = new float[] { 0f, 1f, 1f, 0f };
            var fromData = Curve.Points(column);
            Expect.Near(0.75f, fromData.Of(0.25f), "a float[] passed in, as a data column");
            column[1] = 0f;
            Expect.Near(0.75f, fromData.Of(0.25f), "the array is copied: changing it after does not change the curve");

            var one = Curve.Points(0.5f, 0.3f);
            Expect.Near(0.3f, one.Of(0f), "one point: its y before it");
            Expect.Near(0.3f, one.Of(1f), "and past it");

            var wide = Curve.Points(0f, -1f, 1f, 2f);
            Expect.Near(0f, wide.Of(0f), "a y of -1 clamped to 0");
            Expect.Near(0.5f, wide.Of(0.5f), "halfway from -1 to 2");
            Expect.Near(1f, wide.Of(1f), "a y of 2 clamped to 1");

            Expect.Throws<ArgumentException>(() => Curve.Points(), "no points");
            Expect.Throws<ArgumentException>(() => Curve.Points(0.5f), "one number, an odd count");
            Expect.Throws<ArgumentException>(() => Curve.Points(0f, 0f, 0.5f), "three numbers, an odd count");
            Expect.Throws<ArgumentException>(() => Curve.Points(0.5f, 0f, 0.4f, 1f), "an x smaller than the one before");
        }

        /// <summary>An option with no inputs scores its weight; one input its curve's score; two or three the root of their product, times the weight; Option enumerates its inputs' fact names; a weight below 0 throws.</summary>
        public static void Scores(FakeWorld world)
        {
            var board = Game.Instance.Blackboard();
            var rest = new Option("rest", Nothing, weight: 0.2f);
            Expect.Equal("rest", rest.Name, "its name");
            Expect.Near(0.2f, rest.Weight, "its weight");
            Expect.Near(0.2f, rest.Score(board), "no inputs: its weight");
            Expect.Near(1f, new Option("idle", Nothing).Score(board), "the weight is 1 when not given");
            rest.Weight = 0.5f;
            Expect.Near(0.5f, rest.Score(board), "Weight set");
            Expect.True(new Option("never", Nothing, 0f).Score(board) == 0f, "a weight of 0 scores 0");

            board.Set(Life, 0.25f);
            var flee = new Option("flee", Nothing) { { Life, Curve.Falling } };
            Expect.Near(0.75f, flee.Score(board), "one input: its curve's score");

            board.Set(Life, 0.8f);
            board.Set(Danger, 0.5f);
            var both = new Option("both", Nothing) { { Life, Curve.Rising }, { Danger, Curve.Rising } };
            Expect.Near(0.6325f, both.Score(board), "two inputs: the square root of 0.8 times 0.5");
            both.Weight = 0.5f;
            Expect.Near(0.3162f, both.Score(board), "times the weight: 0.6325 times 0.5");

            board.Set(Alarm, true);
            var three = new Option("three", Nothing)
            {
                { Life, Curve.Rising },
                { Danger, Curve.Rising },
                { Alarm, Curve.Rising },
            };
            Expect.Near(0.7368f, three.Score(board), "three inputs: the cube root of 0.8 times 0.5 times 1");

            var names = new List<string>();
            foreach (object name in three)
            {
                names.Add((string)name);
            }

            Expect.Equal("life danger alarm", string.Join(" ", names), "an option enumerates its inputs' fact names, in the order added");

            Expect.Throws<ArgumentException>(() => new Option("bad", Nothing, -0.1f), "a weight below 0 in the constructor");
            Expect.Throws<ArgumentException>(() => rest.Weight = -1f, "a weight below 0 through Weight");
        }

        /// <summary>A range scales an int fact and a float fact to 0..1, clamped outside it; a reversed range falls; a bool fact is 0 or 1; min equal to max throws.</summary>
        public static void Ranges(FakeWorld world)
        {
            var board = Game.Instance.Blackboard();
            var crowd = new Option("crowd", Nothing) { { Enemies, 0, 6, Curve.Rising } };
            board.Set(Enemies, 3);
            Expect.Near(0.5f, crowd.Score(board), "3 in 0..6");
            board.Set(Enemies, 2);
            Expect.Near(0.3333f, crowd.Score(board), "2 in 0..6");
            board.Set(Enemies, 9);
            Expect.Near(1f, crowd.Score(board), "9 in 0..6, clamped to 1");
            board.Set(Enemies, -2);
            Expect.True(crowd.Score(board) == 0f, "-2 in 0..6, clamped to 0");

            var heat = new Option("heat", Nothing) { { Danger, 10, 30, Curve.Rising } };
            board.Set(Danger, 15f);
            Expect.Near(0.25f, heat.Score(board), "15 in 10..30");
            board.Set(Danger, 40f);
            Expect.Near(1f, heat.Score(board), "40 in 10..30, clamped to 1");
            board.Set(Danger, 5f);
            Expect.True(heat.Score(board) == 0f, "5 in 10..30, clamped to 0");

            var few = new Option("few", Nothing) { { Enemies, 6, 0, Curve.Rising } };
            board.Set(Enemies, 2);
            Expect.Near(0.6667f, few.Score(board), "2 in 6..0, reversed: (2 - 6) / (0 - 6)");
            board.Set(Enemies, 0);
            Expect.Near(1f, few.Score(board), "0 in 6..0");
            board.Set(Enemies, 9);
            Expect.True(few.Score(board) == 0f, "9 in 6..0, clamped to 0");

            var alarmed = new Option("alarmed", Nothing) { { Alarm, Curve.Line(0.2f, 0.6f) } };
            board.Set(Alarm, false);
            Expect.Near(0.2f, alarmed.Score(board), "false is 0: Line(0.2, 0.6) at 0");
            board.Set(Alarm, true);
            Expect.Near(0.6f, alarmed.Score(board), "true is 1: Line(0.2, 0.6) at 1");

            Expect.Throws<ArgumentException>(() => crowd.Add(Enemies, 3f, 3f, Curve.Rising), "an int fact's min equal to its max");
            Expect.Throws<ArgumentException>(() => heat.Add(Danger, 0.5f, 0.5f, Curve.Rising), "a float fact's min equal to its max");
        }

        /// <summary>An option scored with a unit brain's blackboard reads its facts up the chain: the owner's, then the game's, with the brain's own shadowing them.</summary>
        public static void Chain(FakeWorld world)
        {
            var brain = Unit.Of(world.NewUnit(Footman, slot: 0))!.Add(new StepBrain());
            var flee = new Option("flee", Nothing)
            {
                { Life, Curve.Falling },
                { Enemies, 0, 6, Curve.Rising },
            };
            Player.Get(0).Blackboard().Set(Life, 0.5f);
            Game.Instance.Blackboard().Set(Enemies, 6);
            Expect.Near(0.7071f, flee.Score(brain.Blackboard), "life from the owner's, enemies from the game's: the square root of 0.5 times 1");
            brain.Blackboard.Set(Life, 0.75f);
            Expect.Near(0.5f, flee.Score(brain.Blackboard), "the brain's own life shadows the owner's: the square root of 0.25 times 1");
        }

        /// <summary>An input scoring 0 rules the option out, and the facts after it are not read; a worked-out fact counts its reads.</summary>
        public static void RuledOut(FakeWorld world)
        {
            var reads = 0;
            var costly = new Fact<float>("costly", 0f, on =>
            {
                reads++;
                return 0.5f;
            });
            var board = Game.Instance.Blackboard();
            var charge = new Option("charge", Nothing)
            {
                { Alarm, Curve.Rising },
                { costly, Curve.Rising },
            };
            board.Set(Alarm, false);
            Expect.True(charge.Score(board) == 0f, "a zero rules the option out");
            Expect.Equal(0, reads, "and the facts after it are not read");
            board.Set(Alarm, true);
            Expect.Near(0.7071f, charge.Score(board), "not ruled out: the square root of 1 times 0.5");
            Expect.Equal(1, reads, "the worked-out fact read once");

            board.Set(Life, 0.6f);
            var late = new Option("late", Nothing)
            {
                { Life, Curve.Rising },
                { Enemies, 0, 6, Curve.Rising },
            };
            board.Set(Enemies, 0);
            Expect.True(late.Score(board) == 0f, "a zero after another input rules it out too");
        }

        /// <summary>Pick gives the best, the first added of a tie, and null when every option scores 0 or there are none, setting Current to what it gives; foreach gives the options in the order added.</summary>
        public static void Pick(FakeWorld world)
        {
            var board = Game.Instance.Blackboard();
            var a = new Option("a", Nothing, 0.5f);
            var b = new Option("b", Nothing, 0.7f);
            var c = new Option("c", Nothing, 0.7f);
            var choice = new Choice { a, b, c };
            Expect.True(choice.Current == null, "no Current before any Pick");
            Expect.Near(0.1f, choice.Keep, "Keep is 0.1 by default");

            var names = new List<string>();
            foreach (var option in choice)
            {
                names.Add(option.Name);
            }

            Expect.Equal("a b c", string.Join(" ", names), "foreach gives the options in the order added");

            Expect.Same(b, choice.Pick(board), "the best, and of a tie the one added first");
            Expect.Same(b, choice.Current, "Current is what Pick gave");
            c.Weight = 0.9f;
            Expect.Same(c, choice.Pick(board), "0.9 beats the current 0.7 times 1.1");
            Expect.Same(c, choice.Current, "Current follows");

            a.Weight = 0f;
            b.Weight = 0f;
            c.Weight = 0f;
            Expect.True(choice.Pick(board) == null, "every option at 0 gives null");
            Expect.True(choice.Current == null, "and Current becomes null");
            a.Weight = 0.01f;
            Expect.Same(a, choice.Pick(board), "with no Current, the best wins when above 0");

            var empty = new Choice();
            Expect.True(empty.Pick(board) == null, "no options give null");
            Expect.True(empty.Current == null, "with no Current");
        }

        /// <summary>The keep margin: a current option at 0.73 stays against 0.80 and gives way to 0.81, the best of those past it wins, one at 0 gives way to anything above 0; with Keep 0 a tie stays; Keep below 0 throws.</summary>
        public static void Keep(FakeWorld world)
        {
            var board = Game.Instance.Blackboard();
            var other = new Option("other", Nothing, 0.5f);
            var held = new Option("held", Nothing, 0.73f);
            var third = new Option("third", Nothing, 0f);
            var choice = new Choice { other, held, third };
            Expect.Same(held, choice.Pick(board), "0.73 against 0.5 and 0");
            other.Weight = 0.8f;
            Expect.Same(held, choice.Pick(board), "the current 0.73 stays against 0.80, under 0.73 times 1.1");
            other.Weight = 0.81f;
            Expect.Same(other, choice.Pick(board), "and gives way to 0.81");

            held.Weight = 0.95f;
            third.Weight = 0.99f;
            Expect.Same(third, choice.Pick(board), "of two past the current 0.81 times 1.1, the best wins");

            third.Weight = 0f;
            other.Weight = 0f;
            held.Weight = 0.01f;
            Expect.Same(held, choice.Pick(board), "a current option at 0 gives way to anything above 0");

            choice.Keep = 0f;
            Expect.Near(0f, choice.Keep, "Keep set to 0");
            held.Weight = 0.8f;
            other.Weight = 0.8f;
            Expect.Same(held, choice.Pick(board), "with Keep 0, a tie with the current stays, though the other was added first");
            other.Weight = 0.81f;
            Expect.Same(other, choice.Pick(board), "and anything more wins");

            choice.Keep = 0.5f;
            Expect.Near(0.5f, choice.Keep, "Keep set");
            Expect.Throws<ArgumentException>(() => choice.Keep = -0.1f, "Keep below 0");
        }

        /// <summary>Run runs only the winner's action, and nothing when none wins; Pick runs nothing; an option's Run runs its action.</summary>
        public static void Run(FakeWorld world)
        {
            var board = Game.Instance.Blackboard();
            var ran = new List<string>();
            var low = new Option("low", () => ran.Add("low"), 0.3f);
            var high = new Option("high", () => ran.Add("high"), 0.6f);
            var choice = new Choice { low, high };
            Expect.Same(high, choice.Pick(board), "Pick gives the winner");
            Expect.Equal(0, ran.Count, "and runs nothing");
            choice.Run(board);
            Expect.Equal("high", string.Join(" ", ran), "Run runs only the winner's action");
            low.Run();
            Expect.Equal("high low", string.Join(" ", ran), "an option's Run runs its action");

            low.Weight = 0f;
            high.Weight = 0f;
            choice.Run(board);
            Expect.Equal("high low", string.Join(" ", ran), "nothing runs when none wins");
            Expect.True(choice.Current == null, "and Current is null");
        }

        /// <summary>A unit brain whose choice is built in its constructor chooses at each think by facts the case changes between steps, its current option kept by the margin.</summary>
        public static void InABrain(FakeWorld world)
        {
            var brain = Unit.Of(world.NewUnit(Footman, slot: 0))!.Add(new ChoosingBrain());
            var mine = Player.Get(0).Blackboard();

            brain.Blackboard.Set(Life, 1f);
            mine.Set(Enemies, 0);
            BrainCases.Steps(world, 1);
            Expect.Equal("fight", string.Join(" ", brain.Ran), "full life: fight at 0.999 beats rest's 0.2, and flee is ruled out");

            brain.Blackboard.Set(Life, 0.2f);
            mine.Set(Enemies, 6);
            BrainCases.Steps(world, 1);
            Expect.Equal("fight flee", string.Join(" ", brain.Ran), "life 0.2, six enemies: flee at the square root of 0.8, 0.894, beats fight's 0.269 times 1.1");

            brain.Blackboard.Set(Life, 0.1f);
            mine.Set(Enemies, 0);
            BrainCases.Steps(world, 1);
            Expect.Equal("fight flee rest", string.Join(" ", brain.Ran), "no enemies rules flee out, and rest's 0.2 beats fight's 0.119");

            brain.Blackboard.Set(Life, 0.5f);
            BrainCases.Steps(world, 1);
            Expect.Equal("fight flee rest fight", string.Join(" ", brain.Ran), "life 0.5: fight at 0.881 beats rest's 0.2 times 1.1");

            brain.Blackboard.Set(Life, 0.4f);
            mine.Set(Enemies, 6);
            BrainCases.Steps(world, 1);
            Expect.Equal("fight flee rest fight fight", string.Join(" ", brain.Ran), "life 0.4, six enemies: flee's 0.775 is above fight's 0.731 but not above 0.731 times 1.1, so fight stays");
        }
    }

    /// <summary>A unit brain that thinks every step and chooses, as the spec's grunt does, between fleeing, fighting and resting, noting each action run.</summary>
    public sealed class ChoosingBrain : UnitBrain
    {
        /// <summary>The name of each action run, in order.</summary>
        public List<string> Ran = new List<string>();

        /// <summary>Its options: flee by low life and many enemies, fight by life, rest at 0.2.</summary>
        private readonly Choice choice;

        /// <summary>Makes the brain and its choice.</summary>
        public ChoosingBrain()
        {
            choice = new Choice
            {
                new Option("flee", Flee)
                {
                    { UtilityCases.Life, Curve.Falling },
                    { UtilityCases.Enemies, 0, 6, Curve.Rising },
                },
                new Option("fight", Fight) { { UtilityCases.Life, Curve.S(0.3f, 10f) } },
                new Option("rest", Rest, weight: 0.2f),
            };
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            choice.Run(Blackboard);
        }

        /// <summary>Notes a flee.</summary>
        private void Flee()
        {
            Ran.Add("flee");
        }

        /// <summary>Notes a fight.</summary>
        private void Fight()
        {
            Ran.Add("fight");
        }

        /// <summary>Notes a rest.</summary>
        private void Rest()
        {
            Ran.Add("rest");
        }
    }
}
