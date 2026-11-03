using System.ComponentModel;
using Moonwell.Engine;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>Starts the frames package; a map's generated Main calls Install when the map uses the package.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class FramesSystem
    {
        private static IFramesWorld? world;

        private static framehandle? screenRoot;

        private static framehandle? overRoot;

        private static FramePool pool = new FramePool();

        /// <summary>The window the screen root was last spanned to, in pixels; 0 until it is.</summary>
        private static int windowWidth, windowHeight;

        /// <summary>How often the window is checked for a new size, in seconds.</summary>
        internal const float WindowCheck = 0.5f;

        /// <summary>Installs the package on the game. It makes no frame: a frame made while the script loads can desync (a claim in the game facts), so the roots are made at their first use, in a reaction.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Install()
        {
            Install(new NativeFramesWorld());
        }

        /// <summary>Installs the package on a world; tests give a fake one.</summary>
        internal static void Install(IFramesWorld host)
        {
            world = host;
            screenRoot = null;
            overRoot = null;
            pool = new FramePool();
        }

        /// <summary>The world the package talks to.</summary>
        internal static IFramesWorld World => world!;

        /// <summary>The ended frames, kept for reuse.</summary>
        internal static FramePool Pool => pool;

        /// <summary>The whole-width root, under the console's backdrop, beneath the HUD; made at its first use, spanned to each machine's window and checked for a new size every half second.</summary>
        internal static framehandle ScreenRoot
        {
            get
            {
                if (screenRoot == null)
                {
                    screenRoot = World.ScreenRoot();
                    windowWidth = 0;
                    windowHeight = 0;
                    FitScreen();
                    Game.Instance.Every(WindowCheck, FitScreen);
                }

                return screenRoot;
            }
        }

        /// <summary>Spans the screen root to this machine's window when its size changed: x from 0.4 - 0.3 * width / height to 0.4 + 0.3 * width / height (seen 11-03), y 0 to 0.6. A window with no size, as a minimised one, is left as it was. Each machine spans its own; nothing shared changes.</summary>
        private static void FitScreen()
        {
            if (screenRoot == null)
            {
                return;
            }

            var width = World.ClientWidth();
            var height = World.ClientHeight();
            if (width <= 0 || height <= 0 || (width == windowWidth && height == windowHeight))
            {
                return;
            }

            windowWidth = width;
            windowHeight = height;
            var half = 0.3f * width / height;
            World.SpanFrame(screenRoot, 0.4f - half, 0.0f, 0.4f + half, 0.6f);
        }

        /// <summary>The over-the-HUD root, under the game UI, in the 4:3 middle; made at its first use.</summary>
        internal static framehandle OverRoot
        {
            get
            {
                if (overRoot == null)
                {
                    overRoot = World.OverRoot();
                }

                return overRoot;
            }
        }
    }
}
