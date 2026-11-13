using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>A route a unit of some size can walk, as FindPath gives it, or along roads, as Moonwell.Procgen gives it: its turns from the start to the end, how long it is, and whether it reaches the place asked for. A path that does not reach ends at the reachable spot nearest that place, where the game's own walker would stop.</summary>
    public sealed class Route
    {
        internal Route(List<Vector2> points, bool reaches)
        {
            Points = points;
            Reaches = reaches;
            var length = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                length += points[i - 1].DistanceTo(points[i]);
            }

            Length = length;
        }

        /// <summary>The start, each turn, and the end, in order; straight lines between them are clear for the unit's size.</summary>
        public IReadOnlyList<Vector2> Points { get; }

        /// <summary>The length along the points, in world units.</summary>
        public float Length { get; }

        /// <summary>Whether the path ends at the place asked for; false when that place cannot be reached or stood on.</summary>
        public bool Reaches { get; }
    }
}
