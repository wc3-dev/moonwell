using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>The lasting lightning in the game, made again when a saved game loads, as the load drops the game's lightning (seen 11-06).</summary>
    internal static class LastingLightning
    {
        private static readonly List<Lightning> lasting = new List<Lightning>();

        /// <summary>How many saved games have loaded since the start.</summary>
        internal static int Loads;

        /// <summary>Forgets the lightning of the game before.</summary>
        internal static void Reset()
        {
            lasting.Clear();
            Loads = 0;
        }

        /// <summary>Keeps a lasting lightning to be made again at a load.</summary>
        internal static void Keep(Lightning bolt)
        {
            lasting.Add(bolt);
            Events.Listen(GameEvent.Loaded);
        }

        /// <summary>Lets an ended lightning go.</summary>
        internal static void Forget(Lightning bolt)
        {
            lasting.Remove(bolt);
        }

        /// <summary>A saved game loaded: makes each lasting lightning again, where its ends are now.</summary>
        internal static void Loaded()
        {
            Loads++;
            for (var i = 0; i < lasting.Count; i++)
            {
                lasting[i].Make();
            }
        }
    }
}
