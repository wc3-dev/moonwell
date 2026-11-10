using System;

namespace Moonwell
{
    /// <summary>The order events: the reactions to a unit's order.</summary>
    internal static partial class Inbound
    {
        /// <summary>The stunned order, the game's own, given while a unit is stunned or paused.</summary>
        private const int StunnedOrder = 851973;

        /// <summary>Undefend, the game's own, given before a death and in a removal to a unit with Defend.</summary>
        private const int UndefendOrder = 852056;

        /// <summary>Order reactions nested deeper than this are reported and run nothing.</summary>
        private const int MaxOrderDepth = 16;

        /// <summary>How deep order reactions are nested now: an order given inside an order reaction runs its own inside.</summary>
        private static int orderDepth;

        /// <summary>The last orders a unit keeps: the newest and the two before it.</summary>
        private const int KeptOrders = 3;

        /// <summary>The spells' toggles, autocasts and reverts, which are not worth giving again and change nothing a unit does: Wurst's LastOrder list, and those the probe's names add.</summary>
        private static readonly int[] Toggles =
        {
            852055, 852056, 852064, 852065, 852067, 852068, 852076, 852077, 852090, 852091, 852100, 852102, 852103, 852107,
            852108, 852129, 852130, 852133, 852134, 852136, 852137, 852150, 852151, 852158, 852159, 852162, 852163, 852174,
            852175, 852177, 852178, 852191, 852192, 852198, 852199, 852203, 852204, 852212, 852213, 852244, 852245, 852249,
            852250, 852255, 852256, 852458, 852459, 852478, 852479, 852484, 852485, 852515, 852516, 852522, 852523, 852540,
            852541, 852543, 852544, 852546, 852547, 852549, 852550, 852552, 852553, 852562, 852563, 852571, 852578, 852579,
            852589, 852590, 852602, 852603, 852671, 852672,

            // Not in Wurst's list, from the probe's names (11-03): militia, town bell, un-morphs, stand down, mana flare.
            852072, 852073, 852082, 852083, 852087, 852113, 852139, 852156, 852166, 852207, 852494, 852497, 852512, 852513,
            852532, 852534, 852605, 852657, 852696, 852709,
        };

        /// <summary>
        /// A unit is given an order, by a player or by code: the unit's OnOrdered, its components', then the map's and the unit's
        /// player's OnUnitOrdered. A target order's place is its target's when ordered, and one with neither the unit's. The game's
        /// own orders run nothing.
        /// </summary>
        internal static void Ordered(unit handle, int id, OrderKind kind, float x, float y, unit? targetUnit, item? targetItem, destructable? targetDestructable, int queue)
        {
            CheckUnloaded(handle);
            if (handle == ownOrder || id == UndefendOrder || (KnownUnits.Find(handle) == null && !Events.Wants(Hook.OnUnitOrdered)))
            {
                return;
            }

            // One that is ending reacts to none: it is leaving, and its OnLeave has run.
            var ordered = Unit.Of(handle);
            if (ordered == null || ordered.HasEnded || ordered.ending)
            {
                return;
            }

            if (id == StunnedOrder)
            {
                // An idle unit's stunned order counts 1, and its stun's end gives no order back (seen 11-05).
                ordered.StunnedOrderCame = queue > 1;
                return;
            }

            if (orderDepth >= MaxOrderDepth)
            {
                Errors.Report("Moonwell.Orders", new MoonwellException("orders given inside order reactions nested more than " + MaxOrderDepth + " deep, at " + Names.NameOf(ordered) + "; that order runs no reaction"));
                return;
            }

            Widget? target = null;
            if (kind == OrderKind.Target)
            {
                target = TargetOf(targetUnit, targetItem, targetDestructable, ref x, ref y);
            }
            else if (kind == OrderKind.None)
            {
                x = ordered.X;
                y = ordered.Y;
            }

            var last = ordered.LastOrder;
            var repeats = Repeats(last, id, kind, x, y, target);
            var givenBack = ordered.StunnedOrderCame && repeats;
            ordered.StunnedOrderCame = false;
            if (givenBack)
            {
                return;
            }

            var order = new Order(ordered, id, Runtime.World.OrderName(id), kind, x, y, target, queue > 1);
            order.Previous = repeats ? last!.Previous : last;
            var carried = ordered.Carried;
            ordered.OrdersHeard++;
            ordered.LastHeardId = id;
            var heard = ordered.OrdersHeard;
            orderDepth++;
            try
            {
                RunOrdered(order);
            }
            finally
            {
                orderDepth--;
            }

            if (ordered.HasEnded)
            {
                return;
            }

            if (ordered.OrdersHeard != heard)
            {
                // A reaction gave the unit another order. One that replaces this order holds; a stop given inside the event does not
                // (seen 11-03), so Moonwell's own stop makes it hold; an instant order (an item's use, a toggle) replaces nothing.
                if (ordered.LastHeardId == StopOrder)
                {
                    StopLater(ordered);
                    return;
                }

                if (!IsInstant(ordered.LastHeardId))
                {
                    return;
                }
            }

            if (order.IsCancelled)
            {
                Refuse(ordered, carried);
            }
            else if (WorthGivingAgain(id))
            {
                Keep(ordered, order);
                ordered.Carried = order;
            }
            else if (!IsInstant(id))
            {
                ordered.Carried = null;
            }
        }

        /// <summary>Stop.</summary>
        private const int StopOrder = 851972;

        /// <summary>Whether an order is instant: using or moving an item in a slot, or a toggle or autocast, which leave what the unit does as it was.</summary>
        private static bool IsInstant(int id)
        {
            if (id >= 852002 && id <= 852013)
            {
                return true;
            }

            for (var i = 0; i < Toggles.Length; i++)
            {
                if (Toggles[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Stops a unit a moment later as Moonwell's own order, unless the unit was given an order heard by then.</summary>
        private static void StopLater(Unit ordered)
        {
            var heard = ordered.OrdersHeard;
            Game.Instance.After(0f, () =>
            {
                if (!ordered.IsGone && ordered.OrdersHeard == heard)
                {
                    GiveOwn(ordered, () => ordered.Order(Orders.Stop));
                }
            });
        }

        /// <summary>The unit Moonwell is giving one of its own orders, whose event no reaction hears; null when none.</summary>
        private static unit? ownOrder;

        /// <summary>
        /// Refuses a cancelled order, after its reactions: the order the unit was carrying out is given back inside the event,
        /// which holds (seen 11-03); with none (a stop, a build or nothing came before), a stop a moment later, as a stop inside the
        /// event does not hold. A queued order refused so ends the queue, as the game drops the orders queued after it.
        /// </summary>
        private static void Refuse(Unit ordered, Order? carried)
        {
            if (carried != null && !carried.Done && GiveBack(ordered, carried))
            {
                return;
            }

            StopLater(ordered);
        }

        /// <summary>Gives a unit an earlier order again as Moonwell's own; false when it can't be given (its target is gone, or the game said no).</summary>
        private static bool GiveBack(Unit ordered, Order earlier)
        {
            if (earlier.Name == "")
            {
                return false;
            }

            switch (earlier.Kind)
            {
                case OrderKind.Place:
                    return GiveOwn(ordered, () => ordered.OrderAt(earlier.Name, earlier.X, earlier.Y));
                case OrderKind.Target:
                    var target = earlier.Target;
                    return target != null && Unit.Stands(target) && GiveOwn(ordered, () => ordered.OrderOn(earlier.Name, target));
                default:
                    return GiveOwn(ordered, () => ordered.Order(earlier.Name));
            }
        }

        /// <summary>Gives a unit an order whose event no reaction hears, and returns the game's answer.</summary>
        private static bool GiveOwn(Unit ordered, Func<bool> give)
        {
            var before = ownOrder;
            ownOrder = ordered.Handle;
            try
            {
                return give();
            }
            finally
            {
                ownOrder = before;
            }
        }

        /// <summary>Whether an order is the one the unit had: the order a stun's end gives back.</summary>
        private static bool Repeats(Order? last, int id, OrderKind kind, float x, float y, Widget? target)
        {
            if (last == null || last.Id != id || last.Kind != kind)
            {
                return false;
            }

            if (kind == OrderKind.Target)
            {
                return last.Target == target;
            }

            return kind == OrderKind.None || (Math.Abs(last.X - x) < 1.0f && Math.Abs(last.Y - y) < 1.0f);
        }

        /// <summary>Whether an order is worth giving again: a right-click, an attack, attacking the ground, a move, a patrol, holding position, or a spell that is not a toggle or an autocast.</summary>
        private static bool WorthGivingAgain(int id)
        {
            if (id == 851971 || id == 851983 || id == 851984 || id == 851986 || id == 851990 || id == 851993)
            {
                return true;
            }

            if (!IsSpellOrder(id))
            {
                return false;
            }

            for (var i = 0; i < Toggles.Length; i++)
            {
                if (Toggles[i] == id)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether an order id is a spell's.</summary>
        private static bool IsSpellOrder(int id)
        {
            return id >= 852055 && id <= 852762;
        }

        /// <summary>Makes an order the unit's last, keeping the two before it.</summary>
        private static void Keep(Unit ordered, Order order)
        {
            ordered.LastOrder = order;
            var kept = order;
            for (var i = 1; i < KeptOrders && kept.Previous != null; i++)
            {
                kept = kept.Previous;
            }

            kept.Previous = null;
        }

        /// <summary>A unit's spell took effect: its last order, when a spell's and the one it carries out, is done, and Reissue does not cast it again. An item's spell reads the item's order (seen 11-05).</summary>
        internal static void SpellOrderDone(Unit caster)
        {
            var last = caster.LastOrder;
            if (last != null && IsSpellOrder(last.Id) && Runtime.World.CurrentOrderOf(caster.Handle) == last.Id)
            {
                last.Done = true;
            }
        }

        /// <summary>A target order's widget, taken in if Moonwell had not yet, and its place.</summary>
        private static Widget? TargetOf(unit? targetUnit, item? targetItem, destructable? targetDestructable, ref float x, ref float y)
        {
            if (targetUnit != null)
            {
                var u = Unit.Of(targetUnit);
                if (u != null)
                {
                    x = u.X;
                    y = u.Y;
                }

                return u;
            }

            if (targetItem != null)
            {
                var i = Item.Of(targetItem);
                if (i != null)
                {
                    x = i.X;
                    y = i.Y;
                }

                return i;
            }

            var d = Destructable.Of(targetDestructable);
            if (d != null)
            {
                x = d.X;
                y = d.Y;
            }

            return d;
        }

        /// <summary>Runs an order's reactions: the unit's, its components', the game's, then the unit's player's.</summary>
        private static void RunOrdered(Order order)
        {
            var ordered = order.Unit;
            var slot = ordered.Owner.Slot;
            if ((ordered.Mask & Hook.OnOrdered) != 0)
            {
                try
                {
                    ordered.OnOrdered(order);
                }
                catch (Exception error)
                {
                    Errors.Report(ordered, ".OnOrdered", error);
                }
            }

            if (ordered.HasEnded)
            {
                return;
            }

            var count = ordered.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = ordered.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnOrdered) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnOrdered(order);
                }
                catch (Exception error)
                {
                    Errors.Report(component, ".OnOrdered", error);
                }
            }

            ordered.EndDispatch();
            if (ordered.HasEnded)
            {
                return;
            }

            MapOrdered(Game.Instance, order);
            var player = Player.Existing(slot);
            if (player != null)
            {
                MapOrdered(player, order);
            }
        }

        /// <summary>Runs OnUnitOrdered on an actor's map components, in add order.</summary>
        private static void MapOrdered(Actor actor, Order order)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask & Hook.OnUnitOrdered) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnUnitOrdered(order);
                }
                catch (Exception error)
                {
                    Errors.Report(component, ".OnUnitOrdered", error);
                }
            }

            actor.EndDispatch();
        }
    }
}
