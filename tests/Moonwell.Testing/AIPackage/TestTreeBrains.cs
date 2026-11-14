using System.Collections.Generic;
using Moonwell.AI;

namespace Moonwell.Testing
{
    /// <summary>A map's own node: writes its start, each run and its stop in a shared log, and gives the status a case sets.</summary>
    public sealed class LogNode : Node
    {
        /// <summary>The name it writes in the log.</summary>
        public readonly string Label;

        /// <summary>The log several nodes share.</summary>
        public readonly List<string> Log;

        /// <summary>What Run gives; Running unless a case sets it.</summary>
        public Status Gives = Status.Running;

        public LogNode(string label, List<string> log)
        {
            Label = label;
            Log = log;
        }

        protected override void Start()
        {
            Log.Add(Label + " start");
        }

        protected override Status Run()
        {
            Log.Add(Label + " run");
            return Gives;
        }

        protected override void Stop()
        {
            Log.Add(Label + " stop");
        }
    }

    /// <summary>A map's own node that reads its unit and orders it to move at each pass.</summary>
    public sealed class MoveNode : Node
    {
        /// <summary>The unit it read at its last pass.</summary>
        public Unit? Seen;

        /// <summary>What its last order gave.</summary>
        public bool Taken;

        protected override Status Run()
        {
            Seen = Unit;
            Taken = OrderAt("move", 500f, 0f);
            return Status.Running;
        }
    }

    /// <summary>
    /// A map's own node with methods and fields named as the tree engine's internal members. In Lua an object's members
    /// share one table with its base classes', so each must stay apart from the engine's: the engine never calls these
    /// methods, and the fields keep their values, which would break the engine if they were its own.
    /// </summary>
    public sealed class ClashNode : Node
    {
        // A map cannot see the engine's internal members, so these hide nothing there; the tests can see them, and C# warns.
#pragma warning disable CS0108, CS0114

        /// <summary>Its own tree: as the engine's, the node would read as in a tree already when one is made with it.</summary>
        private string tree = "own tree";

        /// <summary>Its own flag: as the engine's, the node would read as running before it started.</summary>
        private bool running = true;

        /// <summary>How many times one of its own methods named as the engine's ran; 0 while the names stay apart.</summary>
        public int OwnCalls;

        /// <summary>How many times it started.</summary>
        public int Starts;

        /// <summary>How many times it ran.</summary>
        public int Runs;

        /// <summary>How many times it was stopped.</summary>
        public int Stops;

        /// <summary>The unit it read at its last run.</summary>
        public Unit? Seen;

        /// <summary>What Run gives; Running unless a case sets it.</summary>
        public Status Gives = Status.Running;

        /// <summary>Named as the engine's pass of a node.</summary>
        public Status Tick()
        {
            OwnCalls++;
            return Status.Failed;
        }

        /// <summary>Named as the engine's cut-off of a node.</summary>
        public void Halt()
        {
            OwnCalls++;
        }

        /// <summary>Named as the engine's putting of a node in a tree.</summary>
        public void Bind(Tree owner)
        {
            OwnCalls++;
        }

        /// <summary>Named as the engine's reading of the brain that runs the tree.</summary>
        public UnitBrain? Brain(string member)
        {
            OwnCalls++;
            return null;
        }

        /// <summary>Named as the engine's check of the time.</summary>
        public bool Passed(float since, float seconds)
        {
            OwnCalls++;
            return true;
        }

#pragma warning restore CS0108, CS0114

        /// <summary>Its starts, runs and stops, as "1 2 0".</summary>
        public string Counts()
        {
            return Starts + " " + Runs + " " + Stops;
        }

        /// <summary>Its own fields, as one line.</summary>
        public string Own()
        {
            return tree + ", " + (running ? "running" : "not running");
        }

        protected override void Start()
        {
            Starts++;
        }

        protected override Status Run()
        {
            Runs++;
            Seen = Unit;
            return Gives;
        }

        protected override void Stop()
        {
            Stops++;
        }
    }

    /// <summary>A unit brain that makes its tree in its constructor, as a map's does, before it is added, and runs it at each think.</summary>
    public sealed class TreeBrain : UnitBrain
    {
        /// <summary>The brain's tree.</summary>
        public readonly Tree Tree;

        public TreeBrain(Node root)
        {
            Tree = new Tree(this, root);
        }

        protected override void Think()
        {
            Tree.Run();
        }
    }

    /// <summary>A game brain that makes its tree in its constructor and runs it at each think.</summary>
    public sealed class TreeGameBrain : GameBrain
    {
        /// <summary>The brain's tree.</summary>
        public readonly Tree Tree;

        public TreeGameBrain(Node root)
        {
            Tree = new Tree(this, root);
        }

        protected override void Think()
        {
            Tree.Run();
        }
    }
}
