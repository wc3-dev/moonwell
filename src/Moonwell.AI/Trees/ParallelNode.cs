namespace Moonwell.AI
{
    /// <summary>Runs every child at each pass, but those that have succeeded: Failed as soon as one fails, halting the others, and Succeeded once all have succeeded.</summary>
    internal sealed class ParallelNode : Branch
    {
        /// <summary>Which children have succeeded since it started.</summary>
        private readonly bool[] done;

        /// <summary>A parallel of these children.</summary>
        internal ParallelNode(Node[] children)
            : base(children)
        {
            done = new bool[children.Length];
        }

        /// <summary>No child has succeeded yet.</summary>
        protected override void Start()
        {
            for (var i = 0; i < done.Length; i++)
            {
                done[i] = false;
            }
        }

        /// <summary>Runs each child that has not succeeded.</summary>
        protected override Status Run()
        {
            var all = true;
            for (var i = 0; i < children.Length; i++)
            {
                if (!done[i])
                {
                    var status = children[i].Tick();
                    if (status == Status.Failed)
                    {
                        HaltChildren();
                        return Status.Failed;
                    }

                    if (status == Status.Succeeded)
                    {
                        done[i] = true;
                    }
                    else
                    {
                        all = false;
                    }
                }
            }

            return all ? Status.Succeeded : Status.Running;
        }
    }
}
