#if DEBUG
using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>The -frames development command: writes the game's frame tree to the log, so a map maker can see what a frame is and where it sits without a run per guess (TheProject feedback 33).</summary>
    internal static class FrameTree
    {
        /// <summary>The most frames written, so a huge tree does not stall the game; a walk of the game's three roots found 1,817 (seen 11-08).</summary>
        private const int MostWritten = 5000;

        /// <summary>
        /// Walks the game UI, the simple UI's parent and the world frame, writing each frame's index under its parent, name, size,
        /// whether it shows and its count of children. A frame's children are asked for by index, which gives frames only, never
        /// the Strings and Textures the game crashes on (Tasyen's "UI: OriginFrames"; the 11-08 walk read every one with no crash).
        /// Heard on every machine, so each asks for the same frames and the handle ids stay the same everywhere.
        /// </summary>
        internal static void Write(IFramesWorld world)
        {
            var written = 0;
            string[] roots = { "game UI", "world frame", "simple UI parent" };
            int[] kinds = { 0, 17, 18 };
            for (var i = 0; i < roots.Length; i++)
            {
                var root = world.OriginFrame(kinds[i], 0);
                if (root != null)
                {
                    Game.Log("frames: " + roots[i]);
                    Walk(world, root, 0, "  ", ref written);
                }
            }

            Game.Log("frames: " + written + " written" + (written >= MostWritten ? ", stopped at the most" : string.Empty));
        }

        private static void Walk(IFramesWorld world, framehandle frame, int index, string indent, ref int written)
        {
            if (written >= MostWritten)
            {
                return;
            }

            written++;
            var count = world.ChildCount(frame);
            var name = world.NameOf(frame);
            Game.Log(indent + index + " " + (name.Length > 0 ? name : "(no name)") + " " + Round(world.FrameWidth(frame)) + " by " + Round(world.FrameHeight(frame)) + (world.FrameShown(frame) ? string.Empty : " hidden") + (count > 0 ? ", " + count + " children" : string.Empty));
            for (var i = 0; i < count; i++)
            {
                Walk(world, world.ChildAt(frame, i), i, indent + "  ", ref written);
            }
        }

        private static string Round(float value)
        {
            return ((int)(value * 10000.0f + 0.5f) / 10000.0f).ToString();
        }
    }
}
#endif
