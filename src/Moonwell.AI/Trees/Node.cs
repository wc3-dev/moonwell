using System;

namespace Moonwell.AI
{
    /// <summary>
    /// A node of a behaviour tree, made with the helpers of <c>Nodes</c> or as a map's own class. At a pass that reaches it,
    /// a node that is not running starts (<c>Start</c>), then runs (<c>Run</c>): Running goes on at the next pass, and
    /// Succeeded or Failed ends it. <c>Stop</c> runs only when a running node is cut off: by an interrupt, by its parent
    /// ending or being cut off, or by <c>Tree.Stop</c>; never when it ends by itself. A node is in one tree, at one place.
    /// </summary>
    public abstract class Node
    {
        // The instance fields are internal, not private, for the reason Scope gives (PrivateFieldTests).

        /// <summary>The tree the node is in; null until a tree is made with it.</summary>
        internal Tree? tree;

        /// <summary>Whether the node has started and not ended.</summary>
        internal bool running;

        /// <summary>Whether the node has started and not ended: it gave Running at its last pass and has not been cut off.</summary>
        [LocalSafe]
        public bool IsRunning => running;

        /// <summary>The blackboard of the brain that runs the tree, the first of its chain.</summary>
        protected Blackboard Blackboard => tree!.board;

        /// <summary>The unit of the unit brain that runs the tree. On a tree a player or game brain runs, it is reported and refused, and gives null.</summary>
        protected Unit Unit
        {
            get
            {
                var brain = Brain("Unit");
                return brain != null ? brain.Unit : null!;
            }
        }

        /// <summary>The value of a fact, read from the brain's blackboard up its chain, as <c>Blackboard.Read</c>.</summary>
        protected T Read<T>(Fact<T> fact)
        {
            return tree!.board.Read(fact);
        }

        /// <summary>Gives the unit an order with no place and no target through its brain's <c>Order</c>, so a repeat is dropped. On a tree a player or game brain runs, it is reported and refused, and gives false.</summary>
        protected bool Order(string order)
        {
            var brain = Brain("Order");
            return brain != null && brain.Order(order);
        }

        /// <summary>Gives the unit an order to a place through its brain's <c>OrderAt</c>, so a repeat is dropped. On a tree a player or game brain runs, it is reported and refused, and gives false.</summary>
        protected bool OrderAt(string order, float x, float y)
        {
            var brain = Brain("OrderAt");
            return brain != null && brain.OrderAt(order, x, y);
        }

        /// <summary>Gives the unit an order on a unit, an item or a destructable through its brain's <c>OrderOn</c>, so a repeat is dropped. On a tree a player or game brain runs, it is reported and refused, and gives false.</summary>
        protected bool OrderOn(string order, Widget target)
        {
            var brain = Brain("OrderOn");
            return brain != null && brain.OrderOn(order, target);
        }

        /// <summary>Runs when the node starts, just before its first <c>Run</c>: at the first pass that reaches it, and again at the first after it ended or was cut off.</summary>
        protected virtual void Start()
        {
        }

        /// <summary>The node's work at each pass while it runs: Running to go on at the next pass, Succeeded or Failed to end.</summary>
        protected abstract Status Run();

        /// <summary>Runs when the node is cut off while running: by an interrupt, by its parent ending or being cut off, or by <c>Tree.Stop</c>. Never when it ends by itself.</summary>
        protected virtual void Stop()
        {
        }

        /// <summary>One pass of the node: starts it if it is not running, marking it running once Start has returned, then runs it; a status other than Running ends it.</summary>
        internal Status Tick()
        {
            if (!running)
            {
                Start();
                running = true;
            }

            var status = Run();
            if (status != Status.Running)
            {
                running = false;
            }

            return status;
        }

        /// <summary>Cuts the node off: nothing unless it is running; otherwise it is marked not running, then its Stop runs.</summary>
        internal void Halt()
        {
            if (!running)
            {
                return;
            }

            running = false;
            Stop();
        }

        /// <summary>Puts the node in a tree; a node with children puts them in too. A node already in a tree throws.</summary>
        internal virtual void Bind(Tree owner)
        {
            if (tree != null)
            {
                throw new ArgumentException(Names.NameOf(this) + " is already in a tree: a node is in one tree, at one place, so build a tree per brain.");
            }

            tree = owner;
        }

        /// <summary>Whether that many seconds have passed since a time, by the brains' time, to half a millisecond, so that the float error of the game's time never costs a think.</summary>
        internal static bool Passed(float since, float seconds)
        {
            return Minds.Now() - since >= seconds - 0.0005f;
        }

        /// <summary>The unit brain that runs the tree; null, reported and refused, when a player or game brain runs it.</summary>
        private UnitBrain? Brain(string member)
        {
            var brain = tree!.unitBrain;
            if (brain == null)
            {
                Errors.Refuse(Names.NameOf(this) + "." + member, "the tree is run by a player or game brain, which has no unit");
            }

            return brain;
        }
    }
}
