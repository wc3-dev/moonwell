namespace Moonwell.AI
{
    /// <summary>A built-in node with children: it puts them in its tree with it, and halts those running when it is cut off.</summary>
    internal abstract class Branch : Node
    {
        /// <summary>The children, in order.</summary>
        internal readonly Node[] children;

        /// <summary>A node with these children.</summary>
        internal Branch(Node[] children)
        {
            this.children = children;
        }

        /// <summary>Puts the node in the tree, then its children.</summary>
        internal override void Bind(Tree owner)
        {
            base.Bind(owner);
            for (var i = 0; i < children.Length; i++)
            {
                children[i].Bind(owner);
            }
        }

        /// <summary>Halts every child still running.</summary>
        internal void HaltChildren()
        {
            for (var i = 0; i < children.Length; i++)
            {
                children[i].Halt();
            }
        }

        /// <summary>Cut off: so are the children still running.</summary>
        protected override void Stop()
        {
            HaltChildren();
        }
    }
}
