namespace Moonwell.Hosting
{
    /// <summary>The game as the pathing package sees it: a hidden helper unit of a square size and movement put at a place, and what a unit's size and movement are.</summary>
    internal interface IPathingWorld
    {
        /// <summary>Puts a hidden helper at a point with SetUnitPosition and reads where the game left it. The helper is one of 1 to 4 cells across (cells) that moves on the ground, floats or is amphibious.</summary>
        void Try(int cells, Movement movement, float x, float y, out float landedX, out float landedY);

        /// <summary>A unit's collision size.</summary>
        float CollisionOf(unit handle);

        /// <summary>How a unit moves.</summary>
        Movement MovementOf(unit handle);
    }
}
