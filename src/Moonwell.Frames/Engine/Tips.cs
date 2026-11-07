using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// The tooltips Moonwell places above their frames. As the game shows one on a machine (it reads shown while the mouse is
    /// on its frame, seen 11-07), that machine puts it on the side of its frame with room, kept in at the screen's sides,
    /// judged from the cursor, as a frame's place on the screen cannot be read (I43).
    /// </summary>
    internal sealed class Tips
    {
        internal const float CheckEvery = 1.0f / 16.0f;

        internal const float Gap = 0.01f;

        // shortcut: the HUD's top bar and console tops in the game's units, judged by eye from 16:9 screenshots (11-07); probe them if tooltips still go under the HUD.
        internal const float BelowTopBar = 0.55f, AboveConsole = 0.16f;

        private readonly List<Tooltip> placed = new List<Tooltip>();

        private Timer? check;

        /// <summary>This machine's: the tooltip the game shows now, placed as it opened. Differs between machines.</summary>
        private Tooltip? open;

        /// <summary>Places a tooltip by its frame each time the game shows it, from now on.</summary>
        internal void Watch(Tooltip tip)
        {
            if (!placed.Contains(tip))
            {
                placed.Add(tip);
            }

            check ??= Game.Instance.Every(CheckEvery, Check);
        }

        /// <summary>A saved game loaded: the tooltips are made again, closed.</summary>
        internal void Loaded()
        {
            open = null;
        }

        // On every machine: lets ended tooltips go; on each, places the one the game has just shown there.
        private void Check()
        {
            for (var i = placed.Count - 1; i >= 0; i--)
            {
                if (placed[i].HasEnded)
                {
                    placed.RemoveAt(i);
                }
            }

            if (placed.Count == 0)
            {
                check?.End();
                check = null;
                open = null;
                return;
            }

            Tooltip? shown = null;
            for (var i = 0; i < placed.Count; i++)
            {
                var tip = placed[i];
                if (tip.handle != null && tip.parentHandle != null && tip.Parent is Frame && FramesSystem.World.FrameShown(tip.parentHandle))
                {
                    shown = tip;
                    break;
                }
            }

            if (shown != open)
            {
                open = shown;
                if (shown != null)
                {
                    Place(shown);
                }
            }
        }

        private static void Place(Tooltip tip)
        {
            var world = FramesSystem.World;
            var frame = (Frame)tip.Parent!;
            if (frame.handle == null || !FramesSystem.CursorAt(out var at))
            {
                return;
            }

            // The cursor is on the frame, so its top is at most a frame's height above the cursor, and its bottom as far below.
            var top = tip.over ? 0.6f : BelowTopBar;
            var bottom = tip.over ? 0.0f : AboveConsole;
            var reach = frame.height + Gap + tip.height;
            var above = at.Y + reach <= top || (at.Y - reach < bottom && top - at.Y >= at.Y - bottom);
            var halfScreen = 0.3f * world.ClientWidth() / world.ClientHeight();
            var side = at.X + tip.width / 2.0f > 0.4f + halfScreen ? 2 : at.X - tip.width / 2.0f < 0.4f - halfScreen ? 0 : 1;
            var point = above ? 6 + side : side;
            var framePoint = above ? side : 6 + side;
            world.ClearFramePoints(tip.handle!);
            world.PlaceFrame(tip.handle!, point, frame.handle, framePoint, 0.0f, above ? Gap : -Gap);
        }
    }
}
