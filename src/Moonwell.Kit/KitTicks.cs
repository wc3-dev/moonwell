using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The HUD parts' readings (bars following units, buff rows, sweeps, cast bars, selection lists), run together on one timer of the game's, 20 times a second, while there are any (decision 336).</summary>
    internal static class KitTicks
    {
        /// <summary>The seconds between two readings.</summary>
        internal const float Interval = 0.05f;

        private static readonly List<Func<bool>> ticking = new List<Func<bool>>();

        private static Timer? ticker;

        /// <summary>The game the ticker runs in; a new game (a test's) starts afresh.</summary>
        private static Game? game;

        /// <summary>Adds a reading, run every tick until it answers false, and starts the timer when it is the first.</summary>
        internal static void Join(Func<bool> tick)
        {
            if (game != Game.Instance)
            {
                game = Game.Instance;
                ticking.Clear();
                ticker = null;
            }

            ticking.Add(tick);
            ticker ??= Game.Instance.OwnEvery(Interval, Step);
        }

        /// <summary>Runs each reading in the order they joined, drops those done, and ends the timer when none is left.</summary>
        private static void Step()
        {
            var kept = 0;
            var count = ticking.Count;
            for (var i = 0; i < count; i++)
            {
                if (ticking[i]())
                {
                    ticking[kept] = ticking[i];
                    kept++;
                }
            }

            // A reading that joined during the loop is after count.
            for (var i = count; i < ticking.Count; i++)
            {
                ticking[kept] = ticking[i];
                kept++;
            }

            ticking.RemoveRange(kept, ticking.Count - kept);
            if (ticking.Count == 0 && ticker != null)
            {
                ticker.End();
                ticker = null;
            }
        }

        /// <summary>Puts a frame at a place and size inside its parent, from the parent's top left, y down, in the game's units whatever Screen.DesignHeight is.</summary>
        internal static T Place<T>(Frame parent, T child, float x, float y, float width, float height)
            where T : Frame
        {
            child.Anchor = FramePoint.TopLeft;
            child.Offset = new Vector2(x, -y);
            child.Width = width;
            child.Height = height;
            return parent.Add(child);
        }
    }
}
