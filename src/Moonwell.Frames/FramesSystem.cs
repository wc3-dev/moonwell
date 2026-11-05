using System.Collections.Generic;
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

        /// <summary>The tooltip slot of each game frame that takes a frame's mouse, by that frame.</summary>
        private static Dictionary<framehandle, framehandle> slots = new Dictionary<framehandle, framehandle>();

        private static FramePool pool = new FramePool();

        private static FrameEvents events = new FrameEvents();

        private static HudFrames? hud;

        /// <summary>Each player's view of the HUD, by slot, made at its first use.</summary>
        private static Dictionary<int, GameUi> gameUis = new Dictionary<int, GameUi>();

        /// <summary>The window the screen root was last spanned to, in pixels; 0 until it is.</summary>
        private static int windowWidth, windowHeight;

        /// <summary>How often the window is checked for a new size, in seconds.</summary>
        internal const float WindowCheck = 0.5f;

        /// <summary>The TOC the build writes into the map, listing the game's template files the kinds are made from.</summary>
        internal const string TocFile = "MoonwellFrames.toc";

        /// <summary>Installs the package on the game, loads its TOC, which the script load allows (seen 10-15), and looks up the game's HUD on every machine. It makes no frame: a frame made while the script loads can desync (a claim in the game facts), so the roots are made at their first use, in a reaction.</summary>
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
            slots = new Dictionary<framehandle, framehandle>();
            pool = new FramePool();
            events = new FrameEvents();
            gameUis = new Dictionary<int, GameUi>();
            host.LoadToc(TocFile);
            hud = new HudFrames(host);
        }

        /// <summary>A player's view of the game's HUD, made at its first use.</summary>
        internal static GameUi GameUiOf(Player player)
        {
            if (!gameUis.TryGetValue(player.Slot, out var ui))
            {
                ui = new GameUi(player, hud!);
                gameUis[player.Slot] = ui;
            }

            return ui;
        }

        /// <summary>The world the package talks to.</summary>
        internal static IFramesWorld World => world!;

        /// <summary>The game's frame events, sent to the frames that own them.</summary>
        internal static FrameEvents Events => events;

        /// <summary>
        /// The slot a tooltip is made in for a game frame that takes a frame's mouse: a blank frame of no size on the
        /// frame's layer, given to the game as that frame's tooltip the first time and never to another, as a frame given
        /// to a second crashes the game (seen 11-03). The game shows the slot while the mouse is on the frame, and the
        /// tooltip in it with it; with none in it, nothing. A game frame keeps its layer, as the pool gives it out again
        /// only under its parent, so the slot never moves.
        /// </summary>
        internal static framehandle SlotFor(framehandle mouse, bool over)
        {
            if (slots.TryGetValue(mouse, out var slot))
            {
                return slot;
            }

            var root = over ? OverRoot : ScreenRoot;
            var made = World.MakeFrame("BACKDROP", "", root)!;
            World.PaintFrame(made, Image.Blank);
            World.SizeFrame(made, 0.0f, 0.0f);
            World.PlaceFrame(made, (int)FramePoint.Center, root, (int)FramePoint.Center, 0.0f, 0.0f);
            World.TipFrame(mouse, made);
            slots[mouse] = made;
            return made;
        }

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
