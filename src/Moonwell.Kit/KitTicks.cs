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

        /// <summary>Sizes a grid to its columns and to the lines its shown items take, at least one: a list of no size showed none of its entries in the game (11-14).</summary>
        internal static void Fit(Grid grid, int shown)
        {
            var lines = shown < 1 ? 1 : (shown + grid.columns - 1) / grid.columns;
            grid.Width = (2.0f * grid.padding) + (grid.columns * grid.cellWidth) + ((grid.columns - 1) * grid.gap);
            grid.Height = (2.0f * grid.padding) + (lines * grid.cellHeight) + ((lines - 1) * grid.gap);
        }

        /// <summary>Puts a frame at a place and size inside its parent, from the parent's top left, y down, in the game's units whatever Screen.DesignHeight is; adds it unless it is the parent's already.</summary>
        internal static T Place<T>(Frame parent, T child, float x, float y, float width, float height)
            where T : Frame
        {
            child.Anchor = FramePoint.TopLeft;
            child.Offset = new Vector2(x, -y);
            child.Width = width;
            child.Height = height;
            return child.Parent == parent ? child : parent.Add(child);
        }
    }
}
