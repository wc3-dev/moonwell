namespace Moonwell.Hosting
{
    /// <summary>The game as the pathing package sees it: a hidden helper unit of a square size and movement put at a place, what a unit's size and movement are, and the deaths of buildings and destructables that change what blocks paths.</summary>
    internal interface IPathingWorld
    {
        /// <summary>Puts a hidden helper at a point with SetUnitPosition and reads where the game left it. The helper is one of 1 to 4 cells across (cells) that moves on the ground, floats or is amphibious. A unit given as leftOut does not count against it.</summary>
        void Try(int cells, Movement movement, float x, float y, unit? leftOut, out float landedX, out float landedY);

        /// <summary>A unit's collision size.</summary>
        float CollisionOf(unit handle);

        /// <summary>How a unit moves.</summary>
        Movement MovementOf(unit handle);

        /// <summary>Whether a unit is a living building.</summary>
        bool IsLiveBuilding(unit handle);

        /// <summary>Where a unit stands.</summary>
        void PlaceOf(unit handle, out float x, out float y);

        /// <summary>Starts telling the package of changes the game makes by itself: a building's death, and the death of a destructable watched with WatchDeathsAround, each by its place.</summary>
        void Listen(System.Action<float, float> changed);

        /// <summary>Watches the deaths of the destructables standing in and near the cell at a point, once each.</summary>
        void WatchDeathsAround(float x, float y);
    }
}
