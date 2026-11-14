using System;
using System.Collections.Generic;
using Moonwell.AI;
using static Moonwell.AI.Nodes;

namespace Moonwell.Testing
{
    /// <summary>Cases for the AI package's behaviour trees: each node's rules, the watching of When in a Select, and the tree's Run and Stop.</summary>
    public static class BehaviourTreeCases
    {
        /// <summary>The type id of the cases' units.</summary>
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        /// <summary>A written flag.</summary>
        private static readonly Fact<bool> Alarm = new Fact<bool>("alarm");

        /// <summary>A written unit, which may be null.</summary>
        private static readonly Fact<Unit?> Raider = new Fact<Unit?>("raider");

        /// <summary>A tree on a game brain that never thinks by itself, for a case to run pass by pass.</summary>
        private static Tree TreeOf(Node root)
        {
            return new Tree(Game.Instance.Add(new StepGameBrain()), root);
        }

        /// <summary>Checks a status.</summary>
        private static void Is(Status expected, Status actual, string what)
        {
            Expect.Equal((int)expected, (int)actual, what);
        }

        /// <summary>Checks the log, then clears it.</summary>
        private static void Logged(string expected, List<string> log, string what)
        {
            Expect.Equal(expected, string.Join(", ", log), what);
            log.Clear();
        }

        /// <summary>Children in order, going on where it left off; Failed at the first failure, Succeeded when all succeed, and Succeeded with none. A node that ends by itself is never stopped.</summary>
        public static void Sequence(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log) { Gives = Status.Succeeded };
            var b = new LogNode("b", log);
            var c = new LogNode("c", log) { Gives = Status.Succeeded };
            var tree = TreeOf(Nodes.Sequence(a, b, c));

            Is(Status.Running, tree.Run(), "b runs");
            Logged("a start, a run, b start, b run", log, "in order, up to the running child");
            Is(Status.Running, tree.Run(), "b runs on");
            Logged("b run", log, "where it left off");
            b.Gives = Status.Succeeded;
            Is(Status.Succeeded, tree.Run(), "all succeeded");
            Logged("b run, c start, c run", log, "the next child in the same pass");
            b.Gives = Status.Failed;
            Is(Status.Failed, tree.Run(), "b fails");
            Logged("a start, a run, b start, b run", log, "started again, and c is never reached");
            Is(Status.Succeeded, TreeOf(Nodes.Sequence()).Run(), "none succeeds");
        }

        /// <summary>Children in order, going on where it left off; Succeeded at the first success, Failed when all fail, and Failed with none.</summary>
        public static void Select(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log) { Gives = Status.Failed };
            var b = new LogNode("b", log);
            var c = new LogNode("c", log) { Gives = Status.Succeeded };
            var tree = TreeOf(Nodes.Select(a, b, c));

            Is(Status.Running, tree.Run(), "b runs");
            Logged("a start, a run, b start, b run", log, "past the failure, up to the running child");
            Is(Status.Running, tree.Run(), "b runs on");
            Logged("b run", log, "where it left off");
            b.Gives = Status.Failed;
            Is(Status.Succeeded, tree.Run(), "c succeeds");
            Logged("b run, c start, c run", log, "the next child in the same pass");
            c.Gives = Status.Failed;
            Is(Status.Failed, tree.Run(), "all fail");
            Logged("a start, a run, b start, b run, c start, c run", log, "started again");
            Is(Status.Failed, TreeOf(Nodes.Select()).Run(), "none fails");
        }

        /// <summary>A When in a Select whose condition turns true cuts in on a child below it that runs on, stopping it; the top one first, and one above a running When cuts in on it.</summary>
        public static void WhenCutsIn(FakeWorld world)
        {
            var log = new List<string>();
            var first = false;
            var second = false;
            var a = new LogNode("a", log);
            var b = new LogNode("b", log);
            var c = new LogNode("c", log);
            var tree = TreeOf(Nodes.Select(When(() => first, a), When(() => second, b), c));

            Is(Status.Running, tree.Run(), "c runs");
            Logged("c start, c run", log, "a When false at its start fails without starting its child");
            second = true;
            Is(Status.Running, tree.Run(), "b runs");
            Logged("c stop, b start, b run", log, "the When cuts in, and c is stopped");
            Expect.True(!c.IsRunning && b.IsRunning, "b runs, c does not");
            first = true;
            Is(Status.Running, tree.Run(), "a runs");
            Logged("b stop, a start, a run", log, "the When above cuts in on the running When");

            first = false;
            second = false;
            Is(Status.Running, tree.Run(), "c runs again");
            Logged("a stop, c start, c run", log, "a When false while it runs ends its branch, and the next fails without starting b");
            first = true;
            second = true;
            Is(Status.Running, tree.Run(), "a runs");
            Logged("c stop, a start, a run", log, "two hold: the top one cuts in");
        }

        /// <summary>A running When whose condition turns false stops its child and fails, and the Select goes on to the next child in the same pass.</summary>
        public static void WhenEndsItsBranch(FakeWorld world)
        {
            var log = new List<string>();
            var alarm = true;
            var a = new LogNode("a", log);
            var b = new LogNode("b", log) { Gives = Status.Succeeded };
            var tree = TreeOf(Nodes.Select(When(() => alarm, a), b));

            Is(Status.Running, tree.Run(), "a runs");
            Logged("a start, a run", log, "the When holds");
            Is(Status.Running, tree.Run(), "a runs on");
            Logged("a run", log, "while it holds");
            alarm = false;
            Is(Status.Succeeded, tree.Run(), "b succeeds in the same pass");
            Logged("a stop, b start, b run", log, "a is stopped, then b runs");
        }

        /// <summary>Only a When directly in a Select is watched: one in a Sequence under it never cuts in, and an If is a one-time check.</summary>
        public static void OnlyASelectWatches(FakeWorld world)
        {
            var log = new List<string>();
            var alarm = false;
            var a = new LogNode("a", log);
            var b = new LogNode("b", log);
            var tree = TreeOf(Nodes.Select(Nodes.Sequence(When(() => alarm, a)), b));

            Is(Status.Running, tree.Run(), "b runs");
            Logged("b start, b run", log, "the When fails, and so does its Sequence");
            alarm = true;
            Is(Status.Running, tree.Run(), "b runs on");
            Logged("b run", log, "a When outside the Select is not watched");

            var ok = true;
            var c = new LogNode("c", log);
            var guarded = TreeOf(Nodes.Sequence(If(() => ok), c));
            Is(Status.Running, guarded.Run(), "c runs");
            Logged("c start, c run", log, "the If holds");
            ok = false;
            Is(Status.Running, guarded.Run(), "c runs on");
            Logged("c run", log, "an If is never checked again while what follows runs");
        }

        /// <summary>Every child each pass but those that succeeded; Failed as soon as one fails, halting the others still running; Succeeded when all succeeded, and with none.</summary>
        public static void Parallel(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log) { Gives = Status.Succeeded };
            var b = new LogNode("b", log);
            var c = new LogNode("c", log);
            var tree = TreeOf(Nodes.Parallel(a, b, c));

            Is(Status.Running, tree.Run(), "b and c run");
            Logged("a start, a run, b start, b run, c start, c run", log, "every child");
            b.Gives = Status.Succeeded;
            Is(Status.Running, tree.Run(), "c runs");
            Logged("b run, c run", log, "a, which succeeded, is skipped");
            c.Gives = Status.Succeeded;
            Is(Status.Succeeded, tree.Run(), "all succeeded");
            Logged("c run", log, "only c");

            a.Gives = Status.Running;
            b.Gives = Status.Running;
            c.Gives = Status.Running;
            Is(Status.Running, tree.Run(), "all run");
            log.Clear();
            b.Gives = Status.Failed;
            Is(Status.Failed, tree.Run(), "b fails");
            Logged("a run, b run, a stop, c stop", log, "the others are stopped, c before its run");
            Is(Status.Succeeded, TreeOf(Nodes.Parallel()).Run(), "none succeeds");
        }

        /// <summary>A child that succeeds counts and starts again at the next pass, never twice in one; Succeeded after the times, forever with 0, and Failed when the child fails.</summary>
        public static void Repeat(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log) { Gives = Status.Succeeded };
            var tree = TreeOf(Nodes.Repeat(a, 2));
            Is(Status.Running, tree.Run(), "one of two");
            Logged("a start, a run", log, "once in the pass");
            Is(Status.Succeeded, tree.Run(), "two of two");
            Logged("a start, a run", log, "started anew");

            var runs = 0;
            var forever = TreeOf(Nodes.Repeat(Do(() =>
            {
                runs++;
                return Status.Succeeded;
            })));
            for (var i = 0; i < 10; i++)
            {
                Is(Status.Running, forever.Run(), "forever runs");
            }

            Expect.Equal(10, runs, "once a pass");
            Is(Status.Failed, TreeOf(Nodes.Repeat(Do(() => Status.Failed), 3)).Run(), "a failing child fails it");
        }

        /// <summary>Invert swaps Succeeded and Failed; Succeed turns Failed into Succeeded; both pass Running on.</summary>
        public static void InvertAndSucceed(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log);
            var b = new LogNode("b", log);
            var invert = TreeOf(Invert(a));
            var succeed = TreeOf(Succeed(b));
            Is(Status.Running, invert.Run(), "Invert passes Running on");
            Is(Status.Running, succeed.Run(), "Succeed passes Running on");
            a.Gives = Status.Succeeded;
            b.Gives = Status.Failed;
            Is(Status.Failed, invert.Run(), "Invert of a success");
            Is(Status.Succeeded, succeed.Run(), "Succeed of a failure");
            a.Gives = Status.Failed;
            b.Gives = Status.Succeeded;
            Is(Status.Succeeded, invert.Run(), "Invert of a failure");
            Is(Status.Succeeded, succeed.Run(), "Succeed of a success");
        }

        /// <summary>If and When on facts read the brain's blackboard up its chain: a flag is true, a reference is not null.</summary>
        public static void Facts(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var brain = Player.Get(0).Add(new StepPlayerBrain());
            var flag = new Tree(brain, If(Alarm));
            var raider = new Tree(brain, If(Raider));
            Is(Status.Failed, flag.Run(), "false when nothing holds it");
            Is(Status.Failed, raider.Run(), "null when nothing holds it");
            Player.Get(0).Blackboard().Set(Alarm, true);
            Game.Instance.Blackboard().Set(Raider, unit);
            Is(Status.Succeeded, flag.Run(), "the player's, up the chain");
            Is(Status.Succeeded, raider.Run(), "the game's");
            brain.Blackboard.Set(Alarm, false);
            Is(Status.Failed, flag.Run(), "the brain's own first");

            var log = new List<string>();
            var a = new LogNode("a", log);
            var b = new LogNode("b", log);
            var whenFlag = new Tree(brain, When(Alarm, a));
            var whenRaider = new Tree(brain, When(Raider, b));
            Is(Status.Failed, whenFlag.Run(), "When on a false fact");
            Is(Status.Running, whenRaider.Run(), "When on a fact that is not null");
            Logged("b start, b run", log, "only b starts");
            Game.Instance.Blackboard().Remove(Raider);
            Is(Status.Failed, whenRaider.Run(), "the fact is null again");
            Logged("b stop", log, "b is stopped");
        }

        /// <summary>Wait succeeds once its seconds have passed by the loop's time, 0.1 s being 5 steps; Wait with a function reads it at its start.</summary>
        public static void Wait(FakeWorld world)
        {
            var steps = new List<int>();
            Game.Instance.Add(new TreeGameBrain(Nodes.Sequence(Nodes.Wait(0.1f), Do(() =>
            {
                steps.Add(Minds.Current().Step);
                return Status.Succeeded;
            })))
            {
                ThinkEvery = 0.02f,
            });
            BrainCases.Steps(world, 12);
            Expect.Equal("6 12", string.Join(" ", steps), "5 steps after it started, then again");

            var seconds = 1f;
            var tree = TreeOf(Nodes.Wait(() => seconds));
            Is(Status.Running, tree.Run(), "it waits a second");
            seconds = 0f;
            Is(Status.Running, tree.Run(), "the seconds were read at its start");
            world.Advance(1f);
            Is(Status.Succeeded, tree.Run(), "a second later");
            Is(Status.Succeeded, tree.Run(), "started again, it reads 0");
        }

        /// <summary>After its child ends, by itself or cut off, Cooldown fails at once without starting the child until its seconds have passed.</summary>
        public static void Cooldown(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log) { Gives = Status.Succeeded };
            var tree = TreeOf(Nodes.Cooldown(1f, a));
            Is(Status.Succeeded, tree.Run(), "a succeeds");
            Logged("a start, a run", log, "a runs");
            Is(Status.Failed, tree.Run(), "cooling");
            world.Advance(0.5f);
            Is(Status.Failed, tree.Run(), "still cooling");
            Logged("", log, "a is never started while cooling");
            world.Advance(0.5f);
            Is(Status.Succeeded, tree.Run(), "a second after");
            Logged("a start, a run", log, "a runs again");

            var alarm = false;
            var b = new LogNode("b", log);
            var x = new LogNode("x", log) { Gives = Status.Succeeded };
            var cut = TreeOf(Nodes.Select(When(() => alarm, x), Nodes.Cooldown(1f, b)));
            Is(Status.Running, cut.Run(), "b runs");
            alarm = true;
            Is(Status.Succeeded, cut.Run(), "x cuts in");
            Logged("b start, b run, b stop, x start, x run", log, "b is cut off");
            alarm = false;
            Is(Status.Failed, cut.Run(), "cooling after a cut-off");
            Logged("", log, "b is not started");
            world.Advance(1f);
            Is(Status.Running, cut.Run(), "b runs again");
            Logged("b start, b run", log, "a second after");
        }

        /// <summary>Timeout halts its child and fails once it has run its seconds; a child that ends in time ends it.</summary>
        public static void Timeout(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log);
            var tree = TreeOf(Nodes.Timeout(1f, a));
            Is(Status.Running, tree.Run(), "a runs");
            world.Advance(0.5f);
            Is(Status.Running, tree.Run(), "a runs on");
            world.Advance(0.5f);
            Is(Status.Failed, tree.Run(), "the second is up");
            Logged("a start, a run, a run, a stop", log, "a is stopped, without a run at that pass");
            Is(Status.Running, tree.Run(), "started again");
            a.Gives = Status.Succeeded;
            Is(Status.Succeeded, tree.Run(), "a succeeds in time");
        }

        /// <summary>Pick runs the best option, keeps a running one within 10%, switches beyond it or when its score drops to 0, fails when none scores above 0, and ends as its option ends.</summary>
        public static void Pick(FakeWorld world)
        {
            var log = new List<string>();
            var scoreA = 1f;
            var scoreB = 0f;
            var a = new LogNode("a", log);
            var b = new LogNode("b", log);
            var tree = TreeOf(Nodes.Pick(Scored(() => scoreA, a), Scored(() => scoreB, b)));

            Is(Status.Running, tree.Run(), "a runs");
            Logged("a start, a run", log, "the best, as 0 is never picked");
            scoreB = 1.05f;
            tree.Run();
            Logged("a run", log, "b beats it by less than 10%: kept");
            scoreB = 1.2f;
            tree.Run();
            Logged("a stop, b start, b run", log, "b beats it by more: switched");
            scoreA = 1.2f;
            tree.Run();
            Logged("b run", log, "a equal: b is kept");
            scoreB = 0f;
            tree.Run();
            Logged("b stop, a start, a run", log, "b's score dropped to 0");
            scoreA = 0f;
            scoreB = -1f;
            Is(Status.Failed, tree.Run(), "none above 0");
            Logged("a stop", log, "what ran is stopped");
            scoreA = 1f;
            scoreB = 1f;
            tree.Run();
            Logged("a start, a run", log, "the first among equals");
            a.Gives = Status.Succeeded;
            Is(Status.Succeeded, tree.Run(), "it ends as its option ends");

            var c = new LogNode("c", log);
            Is(Status.Running, TreeOf(Scored(() => 0f, c)).Run(), "a Scored outside a Pick just runs its child");
            Expect.Throws<ArgumentException>(() => Nodes.Pick(Nodes.Wait(1f)), "an option not made with Scored");
        }

        /// <summary>A node is in one tree, at one place: a second tree with it, or a tree with it twice, throws.</summary>
        public static void OneTreePerNode(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log);
            TreeOf(a);
            Expect.Throws<ArgumentException>(() => TreeOf(Nodes.Sequence(a)), "a node in two trees");
            var twice = Nodes.Wait(1f);
            Expect.Throws<ArgumentException>(() => TreeOf(Nodes.Sequence(twice, twice)), "a node twice in one tree");
        }

        /// <summary>A Run from inside a Run of the same tree is reported and refused, giving Failed; the tree runs on after.</summary>
        public static void NestedRun(FakeWorld world)
        {
            var inner = Status.Running;
            Tree tree = null!;
            tree = TreeOf(Do(() =>
            {
                inner = tree.Run();
                return Status.Succeeded;
            }));
            Is(Status.Succeeded, tree.Run(), "the outer Run");
            Is(Status.Failed, inner, "the inner Run is refused");
            Expect.Equal(1, world.Logged.Count, "and reported");
            Expect.Equal("Moonwell: Tree.Run failed: the tree is running already: a node ran the tree it is in", world.Logged[0], "the logged line");
            Is(Status.Succeeded, tree.Run(), "the next Run runs");
        }

        /// <summary>Tree.Stop stops every running node, and the next Run starts from the root afresh; a node that ended by itself is never stopped.</summary>
        public static void Stop(FakeWorld world)
        {
            var log = new List<string>();
            var a = new LogNode("a", log) { Gives = Status.Succeeded };
            var b = new LogNode("b", log);
            var c = new LogNode("c", log);
            var root = Nodes.Sequence(a, Nodes.Parallel(b, c));
            var tree = TreeOf(root);
            Expect.Same(root, tree.Root, "the root");
            tree.Run();
            log.Clear();
            tree.Stop();
            Logged("b stop, c stop", log, "the running nodes are stopped, not a, which ended");
            Expect.True(!root.IsRunning && !b.IsRunning && !c.IsRunning, "none runs");
            tree.Run();
            Logged("a start, a run, b start, b run, c start, c run", log, "afresh");

            var d = new LogNode("d", log) { Gives = Status.Succeeded };
            var ended = TreeOf(d);
            ended.Run();
            ended.Stop();
            Logged("d start, d run", log, "a root that ended is not stopped");
        }

        /// <summary>A unit brain's tree, made in its constructor before the brain is added, reads the unit when a node asks and orders it through the brain, a repeat dropped; on a game brain's tree, Unit and the orders are reported and refused.</summary>
        public static void UnitBrainTree(FakeWorld world)
        {
            var unit = Unit.Of(world.NewUnit(Footman, slot: 0))!;
            var move = new MoveNode();
            var brain = new TreeBrain(move);
            unit.Add(brain);
            brain.ThinkEvery = 0.02f;
            world.Orders.Clear();
            BrainCases.Steps(world, 3);
            Expect.Same(unit, move.Seen, "the brain's unit");
            Expect.True(move.Taken, "the order is taken");
            Expect.Equal(1, world.Orders.Count, "and sent once, the repeats dropped");

            var gameMove = new MoveNode();
            var gameBrain = Game.Instance.Add(new TreeGameBrain(gameMove));
            gameBrain.Tree.Run();
            Expect.True(gameMove.Seen == null && !gameMove.Taken, "no unit and no order on a game brain's tree");
            Expect.Equal(2, world.Logged.Count, "both reported");
            Expect.Equal("Moonwell: MoveNode.Unit failed: the tree is run by a player or game brain, which has no unit", world.Logged[0], "Unit's line");
            Expect.Equal("Moonwell: MoveNode.OrderAt failed: the tree is run by a player or game brain, which has no unit", world.Logged[1], "OrderAt's line");
        }
    }
}
