using System;

namespace Moonwell
{
    /// <summary>The kinds of pathing <c>Terrain.Block</c> and <c>Terrain.Allow</c> change; combine them with <c>|</c>.</summary>
    [Flags]
    public enum Pathing
    {
        /// <summary>No kind.</summary>
        None = 0,

        /// <summary>Units can walk there.</summary>
        Walk = 1,

        /// <summary>Flying units can fly there.</summary>
        Fly = 2,

        /// <summary>Buildings can be built there.</summary>
        Build = 4,

        /// <summary>Ships can float there.</summary>
        Float = 8,

        /// <summary>Every kind.</summary>
        All = 15,
    }
}
