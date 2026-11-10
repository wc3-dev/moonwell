using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>Pathing asked of a unit, by its own collision size and how it moves.</summary>
    public static class PathingExtensions
    {
        /// <summary>Whether the unit could stand at a point: the game would leave it exactly there. Other units standing there count; the unit itself does not, so its own place reads true. It moves a hidden helper, not the unit.</summary>
        [ChangesState]
        public static bool CanStand(this Unit unit, float x, float y)
        {
            return Paths.CanStand(Paths.World.CollisionOf(unit.Handle), x, y, Paths.World.MovementOf(unit.Handle), unit.Handle);
        }

        /// <summary>The spot nearest a point where the unit could stand, as the game would put it there; the unit itself is not in the way. It moves a hidden helper, not the unit.</summary>
        [ChangesState]
        public static Vector2 NearestSpot(this Unit unit, float x, float y)
        {
            return Paths.NearestSpot(Paths.World.CollisionOf(unit.Handle), x, y, Paths.World.MovementOf(unit.Handle), unit.Handle);
        }

        /// <summary>A path the unit could walk from where it is to a point, by its size. Units are not in it; see Route.</summary>
        [ChangesState]
        public static Route FindPath(this Unit unit, float x, float y)
        {
            return Search.Find(Cells.Across(Paths.World.CollisionOf(unit.Handle)), Paths.World.MovementOf(unit.Handle), unit.X, unit.Y, x, y);
        }

        /// <summary>Whether the unit could walk from where it is to a point and stand there, by its size. Units are not in it.</summary>
        [ChangesState]
        public static bool CanReach(this Unit unit, float x, float y)
        {
            return unit.FindPath(x, y).Reaches;
        }
    }
}
