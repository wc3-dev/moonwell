using System;

namespace Moonwell.AI
{
    /// <summary>A leaf that runs a function at each pass and gives what it gives.</summary>
    internal sealed class DoNode : Node
    {
        /// <summary>The node's work at each pass.</summary>
        private readonly Func<Status> run;

        /// <summary>A do of a function.</summary>
        internal DoNode(Func<Status> run)
        {
            this.run = run;
        }

        /// <summary>Runs the function.</summary>
        protected override Status Run()
        {
            return run();
        }
    }
}
