namespace Moonwell.AI
{
    /// <summary>
    /// A behaviour tree a brain runs: made in the brain's constructor from the helpers of <c>Nodes</c>
    /// (<c>tree = new Tree(this, Select(...))</c>) and run in its <c>Think</c> (<c>tree.Run()</c>). Each <c>Run</c> is one
    /// pass from the root, going on where the last left off. Its nodes read the brain's blackboard, and on a unit brain's
    /// tree they give the unit orders through the brain.
    /// </summary>
    public sealed class Tree
    {
        /// <summary>The blackboard of the brain that runs the tree.</summary>
        internal readonly Blackboard board;

        /// <summary>The unit brain that runs the tree; null when a player or game brain runs it. Its unit is read when a node asks, as the brain is not added yet when the tree is made.</summary>
        internal readonly UnitBrain? unitBrain;

        /// <summary>Whether a Run is going on, so that a Run from inside it is refused.</summary>
        private bool running;

        /// <summary>Whether Stop was called during a Run, so the root is halted once that pass is done.</summary>
        private bool stopAfter;

        /// <summary>A tree a unit brain runs: its nodes read the brain's blackboard and give the unit orders. A node already in a tree throws an ArgumentException.</summary>
        public Tree(UnitBrain brain, Node root)
            : this(brain.Blackboard, brain, root)
        {
        }

        /// <summary>A tree a player brain runs: its nodes read the brain's blackboard. A node already in a tree throws an ArgumentException.</summary>
        public Tree(PlayerBrain brain, Node root)
            : this(brain.Blackboard, null, root)
        {
        }

        /// <summary>A tree a game brain runs: its nodes read the brain's blackboard. A node already in a tree throws an ArgumentException.</summary>
        public Tree(GameBrain brain, Node root)
            : this(brain.Blackboard, null, root)
        {
        }

        /// <summary>Puts every node from the root down in the tree.</summary>
        private Tree(Blackboard board, UnitBrain? unitBrain, Node root)
        {
            this.board = board;
            this.unitBrain = unitBrain;
            Root = root;
            root.Bind(this);
#if DEBUG
            board.AddTree(this);
#endif
        }

        /// <summary>The node every pass starts from.</summary>
        [LocalSafe]
        public Node Root { get; }

        /// <summary>One pass: runs the root and gives its status. After the root ended, the next pass starts it again. A Run from inside a Run of the same tree is reported and refused, and gives Failed.</summary>
        public Status Run()
        {
            if (running)
            {
                Errors.Refuse("Tree.Run", "the tree is running already: a node ran the tree it is in");
                return Status.Failed;
            }

            running = true;
            Status status;
            try
            {
                status = Root.Tick();
            }
            finally
            {
                running = false;
                if (stopAfter)
                {
                    stopAfter = false;
                    Root.Halt();
                }
            }

            return status;
        }

        /// <summary>Cuts the tree off: every running node stops, so the next Run starts from the root afresh. During a Run of the tree, as from one of its nodes, it is put off to the end of that pass: the pass goes on as it would and gives its status, then every node still running stops.</summary>
        public void Stop()
        {
            if (running)
            {
                stopAfter = true;
                return;
            }

            Root.Halt();
        }
    }
}
