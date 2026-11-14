using System.Collections.Generic;

namespace Moonwell.AI.Combat
{
    /// <summary>
    /// Pulls the fighter back with move orders, which never fight on the way: to its PullBack's place when it has one, else
    /// back along its route, from the point before the one nearest the unit to the start, else to where its fight started.
    /// Within 64 of where it goes, as near as it gets when the brain finds its last move done short of it, or with nowhere
    /// to go, it stands on hold position. Running while the unit lives; Failed for a dead or removed unit and a refused
    /// order.
    /// </summary>
    internal sealed class PullBackNode : Node
    {
        /// <summary>The fighter that pulls back.</summary>
        private readonly Fighter fighter;

        /// <summary>The route it walks back along; null at its start, so the first is new.</summary>
        private Route? walked;

        /// <summary>The point of the walked route the unit heads for; below 0 once at the start.</summary>
        private int back;

        /// <summary>A pull-back of a fighter.</summary>
        internal PullBackNode(Fighter fighter)
        {
            this.fighter = fighter;
        }

        /// <summary>Starts with no route, so the first read is new.</summary>
        protected override void Start()
        {
            walked = null;
        }

        /// <summary>Orders the unit toward where it pulls back to, which the brain drops while the unit carries it, or stands it there.</summary>
        protected override Status Run()
        {
            var unit = Unit;
            if (unit == null || !unit.IsAlive)
            {
                return Status.Failed;
            }

            var x = unit.X;
            var y = unit.Y;
            var settings = fighter.PullBack;
            var route = fighter.Route;
            float toX;
            float toY;
            var last = true;
            if (settings.To != null)
            {
                var to = settings.To.Value;
                toX = to.X;
                toY = to.Y;
            }
            else if (route != null && route.Points.Count > 0)
            {
                var points = route.Points;
                if (route != walked)
                {
                    walked = route;
                    back = Nearest(points, x, y) - 1;
                    if (back < 0)
                    {
                        back = 0;
                    }
                }

                while (back >= 0 && Squared(points[back].X - x, points[back].Y - y) <= Fighter.Reached)
                {
                    back--;
                }

                if (back < 0)
                {
                    return Stand();
                }

                toX = points[back].X;
                toY = points[back].Y;
                last = back == 0;
            }
            else if (fighter.hasSpot)
            {
                toX = fighter.spotX;
                toY = fighter.spotY;
            }
            else
            {
                return Stand();
            }

            if (fighter.home || Squared(toX - x, toY - y) <= Fighter.Reached)
            {
                return Stand();
            }

            if (!OrderAt(Orders.Move, toX, toY))
            {
                return Status.Failed;
            }

            // Idle just after: the brain found its move done short of the last place, as near as the unit gets, as in a crowd.
            if (last && unit.CurrentOrder == "")
            {
                fighter.home = true;
                return Stand();
            }

            return Status.Running;
        }

        /// <summary>Stands the unit on hold position, which the brain drops while it holds.</summary>
        private Status Stand()
        {
            return Order(Orders.HoldPosition) ? Status.Running : Status.Failed;
        }

        /// <summary>The index of the point nearest a place, the first among equals.</summary>
        private static int Nearest(IReadOnlyList<Vector2> points, float x, float y)
        {
            var nearest = 0;
            var best = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var distance = Squared(points[i].X - x, points[i].Y - y);
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static float Squared(float dx, float dy)
        {
            return dx * dx + dy * dy;
        }
    }
}
