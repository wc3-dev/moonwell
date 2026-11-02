namespace Moonwell
{
    /// <summary>The total experience of each hero level, from level 1's (0) to the top level's: the standard table, or the one the build installs when the map's gameplay constants change it.</summary>
    internal static class HeroTable
    {
        /// <summary>The table; null until it is first read or installed, so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static int[]? table;

        /// <summary>The table: its last level is the top.</summary>
        internal static int[] Experience
        {
            get
            {
                if (table == null)
                {
                    // The standard gameplay constants' table, seen 11-02.
                    table = new int[] { 0, 200, 500, 900, 1400, 2000, 2700, 3500, 4400, 5400 };
                }

                return table;
            }
        }

        /// <summary>Puts the map's table in place of the standard one.</summary>
        internal static void Install(int[] experience)
        {
            table = experience;
        }

        /// <summary>Goes back to the standard table; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            table = null;
        }
    }
}
