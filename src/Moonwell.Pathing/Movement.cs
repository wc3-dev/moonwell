namespace Moonwell
{
    /// <summary>How a unit gets about, for the pathing package: foot, horse and hover units walk (the game treats them alike, probe 11-09), ships float, amphibious units do either, and fliers fly over everything.</summary>
    public enum Movement
    {
        /// <summary>Walks on walkable ground: foot, horse and hover.</summary>
        Ground = 0,

        /// <summary>Floats on water.</summary>
        Float = 1,

        /// <summary>Walks on ground and floats on water.</summary>
        Amphibious = 2,

        /// <summary>Flies over anything flyable.</summary>
        Fly = 3,
    }
}
