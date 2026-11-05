using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>The game's HUD frames, looked up on every machine when the package starts, as a frame's first lookup gives it a handle id (a claim); looked up at main they are the same at the game's start (seen 11-05).</summary>
    internal sealed class HudFrames
    {
        internal readonly framehandle ConsoleBackdrop;

        internal readonly framehandle Minimap;

        internal readonly framehandle Portrait;

        internal readonly framehandle HeroBar;

        internal readonly framehandle[] TopBar;

        internal readonly framehandle[] MinimapButtons;

        internal readonly framehandle[] HeroButtons;

        internal readonly framehandle[] CommandButtons;

        internal readonly framehandle[] ItemButtons;

        /// <summary>Looks them up: the minimap by its name, as its origin frame is nil (seen 11-05), the rest by the game's ORIGIN_FRAME_ numbers.</summary>
        internal HudFrames(IFramesWorld world)
        {
            ConsoleBackdrop = world.GameFrame("ConsoleUIBackdrop")!;
            Minimap = world.GameFrame("MiniMapFrame")!;
            TopBar = new[] { world.GameFrame("ResourceBarFrame")!, world.GameFrame("UpperButtonBarFrame")! };
            Portrait = world.OriginFrame(16, 0)!;
            HeroBar = world.OriginFrame(2, 0)!;
            MinimapButtons = Origins(world, 9, 5);
            HeroButtons = Origins(world, 3, 7);
            CommandButtons = Origins(world, 1, 12);
            ItemButtons = Origins(world, 7, 6);
        }

        private static framehandle[] Origins(IFramesWorld world, int kind, int count)
        {
            var found = new framehandle[count];
            for (var i = 0; i < count; i++)
            {
                found[i] = world.OriginFrame(kind, i)!;
            }

            return found;
        }
    }
}
