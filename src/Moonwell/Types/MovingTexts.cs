using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The redraws of every text with a TextEffect, labels and floating texts, run together on one timer of the game's while there are any.</summary>
    internal static class MovingTexts
    {
        /// <summary>The seconds between two redraws.</summary>
        private const float Interval = 1.0f / 32.0f;

        private static readonly List<Func<bool>> redrawn = new List<Func<bool>>();

        private static Timer? redrawer;

        /// <summary>Forgets the redraws of the game before.</summary>
        internal static void Reset()
        {
            redrawn.Clear();
            redrawer = null;
        }

        /// <summary>Adds a redraw, run every step until it answers false, and starts the timer when it is the first.</summary>
        internal static void Join(Func<bool> redraw)
        {
            redrawn.Add(redraw);
            redrawer ??= Game.Instance.OwnEvery(Interval, Step);
        }

        /// <summary>Runs each redraw in the order they joined, drops those done, and ends the timer when none is left.</summary>
        private static void Step()
        {
            var kept = 0;
            var count = redrawn.Count;
            for (var i = 0; i < count; i++)
            {
                if (redrawn[i]())
                {
                    redrawn[kept] = redrawn[i];
                    kept++;
                }
            }

            // A redraw that joined during the loop is after count.
            for (var i = count; i < redrawn.Count; i++)
            {
                redrawn[kept] = redrawn[i];
                kept++;
            }

            redrawn.RemoveRange(kept, redrawn.Count - kept);
            if (redrawn.Count == 0 && redrawer != null)
            {
                redrawer.End();
                redrawer = null;
            }
        }
    }
}
