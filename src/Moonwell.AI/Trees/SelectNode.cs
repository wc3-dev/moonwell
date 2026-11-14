namespace Moonwell.AI
{
    /// <summary>
    /// Runs its children in order, going on at the next pass where it left off: Succeeded at the first that succeeds, Failed
    /// once all have failed. A When among its children watches: while a child below it runs on from an earlier pass, each
    /// When above it whose condition holds, top first, runs; the first whose branch does not fail at once cuts in, and the
    /// running child is stopped after that branch has started. A When whose branch fails at once leaves the running child
    /// to go on, neither stopped nor started again.
    /// </summary>
    internal sealed class SelectNode : Branch
    {
        /// <summary>The child it is at.</summary>
        private int at;

        /// <summary>A select of these children.</summary>
        internal SelectNode(Node[] children)
            : base(children)
        {
        }

        /// <summary>Starts at the first child.</summary>
        protected override void Start()
        {
            at = 0;
        }

        /// <summary>Lets a When above a child that runs on cut in, then runs the child it is at, and the next ones in the same pass while they fail.</summary>
        protected override Status Run()
        {
            if (at < children.Length && children[at].IsRunning)
            {
                for (var i = 0; i < at; i++)
                {
                    var when = children[i] as WhenNode;
                    if (when == null)
                    {
                        continue;
                    }

                    // A When whose condition is false gives Failed without starting its child.
                    var cut = when.Tick();
                    if (cut != Status.Failed)
                    {
                        children[at].Halt();
                        at = i;
                        return cut;
                    }
                }
            }

            while (at < children.Length)
            {
                var status = children[at].Tick();
                if (status != Status.Failed)
                {
                    return status;
                }

                at++;
            }

            return Status.Failed;
        }
    }
}
