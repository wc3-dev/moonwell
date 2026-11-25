using System;
using System.Collections.Generic;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>Cases for a choice over a list (J56): options scored for each thing the brain gives at a think, the best pair picked and kept by the margin as a choice's option is (decisions 306 to 308), and a brain that takes jobs.</summary>
    public static class ListChoiceCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>An option's action that does nothing.</summary>
        private static void Nothing(ListJob job)
        {
        }

        /// <summary>
        /// An option's score for a thing: its inputs read the thing, named or by their place, or a fact, scaled and put through
        /// their curves, multiplied and evened out, times the weight; the weight alone with no inputs; a min equal to the max
        /// and a weight below 0 throw.
        /// </summary>
        public static void Scores(FakeWorld world)
        {
            var board = Game.Instance.Blackboard();
            var farm = new ListJob("farm", 0.9f, 1000f);
            var build = new Option<ListJob>("build", Nothing)
            {
                { "urgency", job => job.Urgency, Curve.Rising },
                { "distance", job => job.Distance, 0, 4000, Curve.Falling },
            };
            Expect.Near(0.8216f, build.Score(board, farm), "the square root of 0.9 times 0.75");
            build.Weight = 0.5f;
            Expect.Near(0.4108f, build.Score(board, farm), "times the weight");
            Expect.Near(0.7f, new Option<ListJob>("idle", Nothing, 0.7f).Score(board, farm), "no inputs: the weight");

            var unnamed = new Option<ListJob>("unnamed", Nothing) { { job => job.Urgency, Curve.Rising }, { job => job.Distance, 0, 2000, Curve.Falling } };
            var names = new List<string>();
            foreach (var name in unnamed)
            {
                names.Add((string)name);
            }

            Expect.Equal("input 1, input 2", string.Join(", ", names), "an input with no name is named by its place");
            Expect.Near(0.6708f, unnamed.Score(board, farm), "the square root of 0.9 times 0.5");

            board.Set(UtilityCases.Life, 0.25f);
            var hungry = new Option<ListJob>("hungry", Nothing) { { UtilityCases.Life, Curve.Rising }, { "urgency", job => job.Urgency, Curve.Rising } };
            Expect.Near(0.4743f, hungry.Score(board, farm), "a fact input with a thing's: the square root of 0.25 times 0.9");
            farm.Urgency = 0f;
            Expect.True(hungry.Score(board, farm) == 0f, "an input at 0 rules the pair out");

            Expect.Throws<ArgumentException>(() => new Option<ListJob>("bad", Nothing) { { job => job.Urgency, 3, 3, Curve.Rising } }, "a min equal to the max");
            Expect.Throws<ArgumentException>(() => new Option<ListJob>("bad", Nothing, -1f), "a weight below 0");
        }

        /// <summary>
        /// Pick scores every option for every thing and gives the best pair, an option and its thing, kept against another
        /// unless it scores more than the current pair's score times 1.1; a current thing gone from the list gives way; of a
        /// tie the option added first, then the thing first in the list; null with an empty list or every pair at 0; Run runs
        /// the winner with its thing.
        /// </summary>
        public static void Pick(FakeWorld world)
        {
            var board = Game.Instance.Blackboard();
            var farm = new ListJob("farm", 0.9f, 1000f);
            var mill = new ListJob("mill", 0.5f, 200f);
            var wall = new ListJob("wall", 0.2f, 100f);
            var jobs = new List<ListJob> { farm, mill, wall };
            var took = new List<string>();
            var build = new Option<ListJob>("build", job => took.Add("build " + job.Name))
            {
                { "urgency", job => job.Urgency, Curve.Rising },
                { "distance", job => job.Distance, 0, 4000, Curve.Falling },
            };
            var repair = new Option<ListJob>("repair", job => took.Add("repair " + job.Name), 0.5f);
            var choice = new Choice<ListJob> { build, repair };
            Expect.True(choice.Current == null && choice.CurrentItem == null, "nothing current before any Pick");
            Expect.Near(0.1f, choice.Keep, "Keep is 0.1 by default");

            Expect.Same(build, choice.Pick(board, jobs), "build scores the farm 0.82, over repair's 0.5 for any job");
            Expect.Same(farm, choice.CurrentItem, "with the farm");

            mill.Urgency = 0.8f;
            mill.Distance = 0f;
            choice.Pick(board, jobs);
            Expect.Same(farm, choice.CurrentItem, "the mill at 0.894 stays under the farm's 0.82 times 1.1");
            mill.Urgency = 0.85f;
            choice.Pick(board, jobs);
            Expect.Same(mill, choice.CurrentItem, "and at 0.922 takes over");

            jobs.Remove(mill);
            choice.Pick(board, jobs);
            Expect.Same(farm, choice.CurrentItem, "the current mill gone from the list gives way to the farm");

            build.Weight = 0f;
            Expect.Same(repair, choice.Pick(board, jobs), "every build at 0: repair, at 0.5 for every job");
            Expect.Same(farm, choice.CurrentItem, "with the first job in the list of a tie");

            choice.Run(board, jobs);
            Expect.Equal("repair farm", string.Join(" ", took), "Run runs the winner with its thing; Pick ran nothing");

            Expect.True(choice.Pick(board, new List<ListJob>()) == null, "an empty list gives null");
            Expect.True(choice.Current == null && choice.CurrentItem == null, "and nothing current");
            repair.Weight = 0f;
            Expect.True(choice.Pick(board, jobs) == null, "every pair at 0 gives null");
            var none = new Choice<ListJob>();
            Expect.True(none.Pick(board, jobs) == null, "no options give null");

            var names = new List<string>();
            foreach (var option in choice)
            {
                names.Add(option.Name);
            }

            Expect.Equal("build repair", string.Join(" ", names), "foreach gives the options in the order added");
            Expect.Throws<ArgumentException>(() => choice.Keep = -0.1f, "Keep below 0");
        }

        /// <summary>A unit brain takes the best of the jobs it is given at each think, sticking to its job by the margin and moving on when it is gone.</summary>
        public static void InABrain(FakeWorld world)
        {
            var brain = Unit.Of(world.NewUnit(Footman, slot: 0))!.Add(new WorkerBrain());
            var farm = new ListJob("farm", 0.9f, 1000f);
            var mill = new ListJob("mill", 0.5f, 200f);
            brain.Jobs.Add(farm);
            brain.Jobs.Add(mill);
            BrainCases.Steps(world, 1);
            Expect.Equal("farm", string.Join(" ", brain.Took), "the farm");

            mill.Urgency = 0.8f;
            mill.Distance = 0f;
            BrainCases.Steps(world, 1);
            Expect.Equal("farm farm", string.Join(" ", brain.Took), "kept against the mill by the margin");

            brain.Jobs.Remove(farm);
            BrainCases.Steps(world, 1);
            Expect.Equal("farm farm mill", string.Join(" ", brain.Took), "the farm done, the mill");
        }
    }

    /// <summary>A job of a colony, for the cases: how urgent, how far.</summary>
    public sealed class ListJob
    {
        /// <summary>A job.</summary>
        public ListJob(string name, float urgency, float distance)
        {
            Name = name;
            Urgency = urgency;
            Distance = distance;
        }

        /// <summary>Its name, which the AI tools show.</summary>
        public string Name { get; }

        /// <summary>How urgent, 0..1.</summary>
        public float Urgency { get; set; }

        /// <summary>How far, 0..4000.</summary>
        public float Distance { get; set; }

        /// <summary>Its name.</summary>
        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>A unit brain that thinks every step and takes the best of its jobs, by urgency and nearness, noting each it takes; a repair at 0.3 for any job.</summary>
    public sealed class WorkerBrain : UnitBrain
    {
        /// <summary>The jobs it is given.</summary>
        public List<ListJob> Jobs = new List<ListJob>();

        /// <summary>The name of each job taken, in order.</summary>
        public List<string> Took = new List<string>();

        /// <summary>Build or repair a job.</summary>
        private readonly Choice<ListJob> work;

        /// <summary>Makes the brain and its choice.</summary>
        public WorkerBrain()
        {
            work = new Choice<ListJob>
            {
                new Option<ListJob>("build", Take)
                {
                    { "urgency", job => job.Urgency, Curve.Rising },
                    { "distance", job => job.Distance, 0, 4000, Curve.Falling },
                },
                new Option<ListJob>("repair", Take, 0.3f),
            };
        }

        protected override void OnAdded()
        {
            ThinkEvery = 0.02f;
        }

        protected override void Think()
        {
            work.Run(Blackboard, Jobs);
        }

        /// <summary>Notes a job taken.</summary>
        private void Take(ListJob job)
        {
            Took.Add(job.Name);
        }
    }
}
