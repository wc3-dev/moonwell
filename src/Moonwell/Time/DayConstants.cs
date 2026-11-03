namespace Moonwell
{
    /// <summary>The day's dawn, dusk and hours: the standard gameplay constants, or the map's, which the build installs when they differ.</summary>
    internal static class DayConstants
    {
        /// <summary>Dawn, dusk and hours; null until installed, so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static float[]? installed;

        /// <summary>The hour the day starts at.</summary>
        internal static float Dawn => installed == null ? 6.0f : installed[0];

        /// <summary>The hour the night starts at.</summary>
        internal static float Dusk => installed == null ? 18.0f : installed[1];

        /// <summary>The hours in a day.</summary>
        internal static float Hours => installed == null ? 24.0f : installed[2];

        /// <summary>Puts the map's constants in place of the standard ones.</summary>
        internal static void Install(float dawn, float dusk, float hours)
        {
            installed = new[] { dawn, dusk, hours };
        }

        /// <summary>Goes back to the standard constants; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            installed = null;
        }
    }
}
