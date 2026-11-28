namespace Moonwell.Hosting
{
    /// <summary>A unit standing where a building would go, as the game gives it to placement (game facts 11-28).</summary>
    internal sealed class Standing
    {
        /// <summary>Its type id.</summary>
        internal int Type;

        /// <summary>Its place.</summary>
        internal float X;

        /// <summary>Its place.</summary>
        internal float Y;

        /// <summary>Its collision size.</summary>
        internal float Collision;

        /// <summary>Its owner's slot.</summary>
        internal int Owner;

        /// <summary>Whether it is a building.</summary>
        internal bool Building;

        /// <summary>Whether it flies.</summary>
        internal bool Flies;

        /// <summary>Whether it is paused.</summary>
        internal bool Paused;
    }
}
