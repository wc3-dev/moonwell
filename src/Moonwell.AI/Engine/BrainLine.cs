#if DEBUG
namespace Moonwell.AI
{
    /// <summary>A line the brain view numbers, for the view, -force and the log alike: a node of a brain's tree, with its parent and its place there, or an option of a brain's choice.</summary>
    internal sealed class BrainLine
    {
        /// <summary>The brain.</summary>
        internal readonly Mind Mind;

        /// <summary>The node; null for an option.</summary>
        internal readonly Node? Node;

        /// <summary>The node's parent; null for a tree's root and for an option.</summary>
        internal readonly Branch? Parent;

        /// <summary>The option's choice; null for a node.</summary>
        internal readonly Choice? Choice;

        /// <summary>The option; null for a node.</summary>
        internal readonly Option? Option;

        /// <summary>The node's place among its parent's children, or the option's among its choice's options.</summary>
        internal readonly int Index;

        /// <summary>The node's depth in its tree, 0 for a root; for an option, the number of its choice among the brain's, from 1.</summary>
        internal readonly int Depth;

        /// <summary>A node's line.</summary>
        internal BrainLine(Mind mind, Node node, Branch? parent, int index, int depth)
        {
            Mind = mind;
            Node = node;
            Parent = parent;
            Index = index;
            Depth = depth;
        }

        /// <summary>An option's line.</summary>
        internal BrainLine(Mind mind, Choice choice, int number, int index)
        {
            Mind = mind;
            Choice = choice;
            Option = choice.options[index];
            Index = index;
            Depth = number;
        }
    }
}
#endif
