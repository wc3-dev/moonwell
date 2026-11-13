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
