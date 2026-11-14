using System;

namespace Moonwell.AI
{
    /// <summary>
    /// The nodes a behaviour tree is built of, for <c>using static Moonwell.AI.Nodes;</c>. Each makes a new node, and a node
    /// is in one tree, at one place, so a brain builds its own tree in its constructor. A fact a node reads is read from the
    /// brain's blackboard up its chain.
    /// </summary>
    public static class Nodes
    {
        /// <summary>Runs the children in order, going on at the next pass where it left off: Failed at the first that fails, Succeeded once all have succeeded (at once with none).</summary>
        public static Node Sequence(params Node[] children)
        {
            return new SequenceNode(children);
        }

        /// <summary>
        /// Runs the children in order, going on at the next pass where it left off: Succeeded at the first that succeeds,
        /// Failed once all have failed (at once with none). A <c>When</c> among its children is watched: while a child below it
        /// runs on from an earlier pass, each <c>When</c> above it whose condition holds runs, top first, and the first whose
        /// child does not fail at once cuts in: its branch takes over, and the running child is stopped after it has started.
        /// A <c>When</c> whose child fails at once leaves the running child to go on, neither stopped nor started again. A
        /// running <c>When</c> whose condition no longer holds fails, and the select goes on to the next child in the same
        /// pass. Nothing outside the select is watched.
        /// </summary>
        public static Node Select(params Node[] children)
        {
            return new SelectNode(children);
        }

        /// <summary>Runs every child at each pass, but those that have succeeded: Failed as soon as one fails, halting the others still running, and Succeeded once all have succeeded (at once with none).</summary>
        public static Node Parallel(params Node[] children)
        {
            return new ParallelNode(children);
        }

        /// <summary>Runs the child again each time it succeeds, starting it anew at the next pass, never twice in one: Running until it has succeeded that many times, then Succeeded; Failed when the child fails.</summary>
        /// <param name="child">The node to repeat.</param>
        /// <param name="times">The times the child is to succeed; 0 or less, the default, repeats it forever.</param>
        public static Node Repeat(Node child, int times = 0)
        {
            return new RepeatNode(child, times);
        }

        /// <summary>Runs the child and gives Failed when it succeeds, Succeeded when it fails.</summary>
        public static Node Invert(Node child)
        {
            return new InvertNode(child);
        }

        /// <summary>Runs the child and gives Succeeded however it ends.</summary>
        public static Node Succeed(Node child)
        {
            return new SucceedNode(child);
        }

        /// <summary>Succeeded when the condition holds, Failed when not, at once. A one-time check, never watched: for a sequence's guard.</summary>
        public static Node If(Func<bool> condition)
        {
            return new IfNode(board => condition());
        }

        /// <summary>Succeeded when the fact is true, Failed when not, at once. A one-time check, never watched.</summary>
        public static Node If(Fact<bool> fact)
        {
            return new IfNode(board => board.Read(fact));
        }

        /// <summary>Succeeded when the fact is not null, Failed when it is, at once. A one-time check, never watched.</summary>
        public static Node If<T>(Fact<T> fact)
            where T : class?
        {
            return new IfNode(board => board.Read(fact) != null);
        }

        /// <summary>Runs the child while the condition holds, checked at every pass: false at its start, it gives Failed without starting the child; false while it runs, it halts the child and gives Failed. Directly in a <c>Select</c>, it can cut in on a child below it.</summary>
        public static Node When(Func<bool> condition, Node child)
        {
            return new WhenNode(board => condition(), child);
        }

        /// <summary>Runs the child while the fact is true, as <c>When</c> with a condition.</summary>
        public static Node When(Fact<bool> fact, Node child)
        {
            return new WhenNode(board => board.Read(fact), child);
        }

        /// <summary>Runs the child while the fact is not null, as <c>When</c> with a condition.</summary>
        public static Node When<T>(Fact<T> fact, Node child)
            where T : class?
        {
            return new WhenNode(board => board.Read(fact) != null, child);
        }

        /// <summary>Gives Running until that many seconds have passed since it started, then Succeeded.</summary>
        public static Node Wait(float seconds)
        {
            return new WaitNode(() => seconds);
        }

        /// <summary>Gives Running until the seconds the function gives, read when it starts, have passed, then Succeeded.</summary>
        public static Node Wait(Func<float> seconds)
        {
            return new WaitNode(seconds);
        }

        /// <summary>Runs the child; once the child has ended, by itself or cut off, it gives Failed at once, without starting the child, until that many seconds have passed.</summary>
        public static Node Cooldown(float seconds, Node child)
        {
            return new CooldownNode(seconds, child);
        }

        /// <summary>Runs the child; once it has run that many seconds, it halts the child and gives Failed.</summary>
        public static Node Timeout(float seconds, Node child)
        {
            return new TimeoutNode(seconds, child);
        }

        /// <summary>A leaf that runs the function at each pass and gives what it gives: <c>Do(() =&gt; { Blackboard.Set(Seen, true); return Status.Succeeded; })</c>.</summary>
        public static Node Do(Func<Status> run)
        {
            return new DoNode(run);
        }

        /// <summary>
        /// Utility AI: scores every option at each pass and runs the best, the first among equals; a score of 0 or less is never
        /// picked. A running option is kept unless another beats it by more than a tenth (best &gt; current * 1.1) or its own
        /// score drops to 0 or less; a switch halts it. With every score at 0 or less, it halts what runs and gives Failed. It
        /// ends as its option ends. Each option is made with <c>Scored</c>; any other node throws an ArgumentException.
        /// </summary>
        public static Node Pick(params Node[] options)
        {
            return new PickNode(options);
        }

        /// <summary>An option of a <c>Pick</c>: the score it reads at each pass, and the child it runs when picked. Outside a <c>Pick</c> it just runs its child.</summary>
        public static Node Scored(Func<float> score, Node child)
        {
            return new ScoredNode(score, child);
        }

        /// <summary>
        /// Moves the unit to the place the functions give, read at every pass so a moving place is followed (the brain sends
        /// the move again only beyond 32): Succeeded, with no order, once the unit is within 64 of it, and Running while it
        /// walks. Failed for a dead or removed unit, a refused order, or a unit idle short of the place just after the order,
        /// as when a jammed unit has had its one more try. Each unit action orders the unit through its brain, so a repeat the
        /// unit carries out is dropped, and on a tree a player or game brain runs it is reported and gives Failed.
        /// </summary>
        public static Node MoveTo(Func<float> x, Func<float> y)
        {
            return new MoveToNode(x, y);
        }

        /// <summary>
        /// Attacks the unit the function gives, read at every pass so a new target is followed, with Running while it lives.
        /// Failed for a null target, one dead or gone at the node's first pass, a dead or removed unit, a refused order, or a
        /// unit found idle at a later pass with the same target alive, as one that lost sight of it or can't reach it.
        /// Succeeded once the target is dead or gone at a later pass.
        /// </summary>
        public static Node Attack(Func<Unit?> target)
        {
            return new AttackNode(board => target());
        }

        /// <summary>Attacks the unit a fact holds, read from the brain's blackboard at every pass, as <c>Attack</c> with a function.</summary>
        public static Node Attack(Fact<Unit?> target)
        {
            return new AttackNode(board => board.Read(target));
        }

        /// <summary>
        /// Casts a spell with no target by its order string (<c>"thunderclap"</c>), then gives Running while the unit's current
        /// order id is the one it read just after, and Succeeded once it differs, or at once when that was 0. A unit still
        /// carrying out the cast as its brain last gave it, the same spell on the same target or at a place within 32, is not
        /// ordered again, only watched; otherwise the brain's last order is forgotten, so a second cast of the same spell, or
        /// one on another target, is sent. Failed for a refused order or a dead or removed unit. Succeeded means the unit
        /// stopped carrying the cast out, not that the spell went off: a stun reads the same.
        /// </summary>
        public static Node Cast(string order)
        {
            return new CastNode(order, null, null, null);
        }

        /// <summary>Casts a spell at the place the functions give, read at the node's start, as <c>Cast</c> with no target.</summary>
        public static Node Cast(string order, Func<float> x, Func<float> y)
        {
            return new CastNode(order, x, y, null);
        }

        /// <summary>Casts a spell on the unit, item or destructable the function gives, read at the node's start, as <c>Cast</c> with no target; Failed at once for a null, dead or gone target.</summary>
        public static Node Cast(string order, Func<Widget?> target)
        {
            return new CastNode(order, null, null, target);
        }

        /// <summary>
        /// Walks the route the function gives point by point with an order to each, <c>Orders.Move</c> unless given:
        /// <c>Orders.Attack</c> is an attack-move. A route new to the node, at its start or when the function gives another
        /// one, is taken up at the point after the one nearest the unit, or at the last when that is the nearest. Each pass
        /// skips the points within 64 of the unit, giving Succeeded past the last, and orders the unit to the next. Failed for
        /// a null route or one with no points, a dead or removed unit, a refused order, or a unit idle short of the point just
        /// after the order, as <c>MoveTo</c>.
        /// </summary>
        public static Node FollowRoute(Func<Route?> route, string order = Orders.Move)
        {
            return new FollowRouteNode(route, order);
        }

        /// <summary>
        /// Has the unit use the item the function gives, read at the node's start, as the unit's <c>UseItem</c> does, then
        /// watches the unit's current order id as <c>Cast</c> does. A unit already carrying out a use of the item, its current
        /// order id 852008 plus the item's slot (the first slot's seen, the others unmeasured), is not ordered again, only
        /// watched. Failed for a null or gone item, a use the game refused, or a dead or removed unit. Succeeded means the unit
        /// stopped carrying the use out, not that the item took effect; a potion, which leaves no order, succeeds at once.
        /// </summary>
        public static Node UseItem(Func<Item?> item)
        {
            return new UseItemNode(item, null, null, null);
        }

        /// <summary>Has the unit use the item at the place the functions give, read at the node's start, as <c>UseItem</c> with no target.</summary>
        public static Node UseItem(Func<Item?> item, Func<float> x, Func<float> y)
        {
            return new UseItemNode(item, x, y, null);
        }

        /// <summary>Has the unit use the item on the unit, item or destructable the function gives, read at the node's start, as <c>UseItem</c> with no target; Failed for a null or gone target.</summary>
        public static Node UseItem(Func<Item?> item, Func<Widget?> target)
        {
            return new UseItemNode(item, null, null, target);
        }
    }
}
