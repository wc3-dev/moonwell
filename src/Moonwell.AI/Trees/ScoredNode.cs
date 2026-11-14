using System;

namespace Moonwell.AI
{
    /// <summary>An option of a Pick: a score and the child it runs. Outside a Pick it just runs its child.</summary>
    internal sealed class ScoredNode : Branch
    {
        /// <summary>Gives the option's score, read by its Pick at each pass.</summary>
        internal readonly Func<float> score;

        /// <summary>The child it runs.</summary>
        private readonly Node child;

        /// <summary>An option of a score and a child.</summary>
        internal ScoredNode(Func<float> score, Node child)
            : base(new Node[] { child })
        {
            this.score = score;
            this.child = child;
        }

        /// <summary>Runs the child.</summary>
        protected override Status Run()
        {
            return child.Tick();
        }
    }
}
