using System;
using System.Collections.Generic;

namespace Moonwell.AI
{
    /// <summary>Walks a route's points in turn with an order to a place, through the unit's brain: Succeeded once past the last, Failed for a null or empty route, a dead or removed unit, a refused order, or a unit idle short of its point just after the order. A route new to the node is taken up after the point nearest the unit.</summary>
    internal sealed class FollowRouteNode : Node
    {
        /// <summary>Gives the route, read at every pass.</summary>
        private readonly Func<Route?> route;

        /// <summary>The order to each point: "move", or "attack" for an attack-move.</summary>
        private readonly string order;

        /// <summary>The route the node walks; null at its start, so the first is new.</summary>
        private Route? walked;

        /// <summary>The point of the walked route the unit heads for.</summary>
        private int next;

        /// <summary>A walk along the route a function gives, with an order to each point.</summary>
        internal FollowRouteNode(Func<Route?> route, string order)
        {
            this.route = route;
            this.order = order;
        }

        /// <summary>Starts with no route, so the first read is new.</summary>
        protected override void Start()
        {
            walked = null;
        }

        /// <summary>Takes up a new route, skips the points reached, then orders the unit to the next, which the brain drops while the unit carries it.</summary>
        protected override Status Run()
        {
            var unit = Unit;
            if (unit == null || !unit.IsAlive)
            {
                return Status.Failed;
            }

            var now = route();
            if (now == null || now.Points.Count == 0)
            {
                return Status.Failed;
            }

            var points = now.Points;
            var at = new Vector2(unit.X, unit.Y);
            if (now != walked)
            {
                walked = now;
                next = Nearest(points, at) + 1;
                if (next == points.Count)
                {
                    next = points.Count - 1;
                }
            }

            while (next < points.Count && at.DistanceSquaredTo(points[next]) <= UnitBrain.Reached)
            {
                next++;
            }

            if (next == points.Count)
            {
                return Status.Succeeded;
            }

            var point = points[next];
            if (!OrderAt(order, point.X, point.Y) || unit.CurrentOrder == "")
            {
                return Status.Failed;
            }

            return Status.Running;
        }

        /// <summary>The index of the point nearest a place, the first among equals.</summary>
        private static int Nearest(IReadOnlyList<Vector2> points, Vector2 at)
        {
            var nearest = 0;
            var best = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var distance = at.DistanceSquaredTo(points[i]);
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }
    }
}
