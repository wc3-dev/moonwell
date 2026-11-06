using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The visions in the game, oldest first, which a new one makes again after its own.</summary>
    internal static class LiveVisions
    {
        internal static readonly List<Vision> All = new List<Vision>();

        /// <summary>Forgets the visions of the game before.</summary>
        internal static void Reset()
        {
            All.Clear();
        }
    }
}
