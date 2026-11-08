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

        private static Drags drags = new Drags();

        private static Tips tips = new Tips();

        private static HudFrames? hud;

        /// <summary>The frames added to the screen that have not ended, in the order added: the trees made again when a saved game loads.</summary>
        private static List<Frame> roots = new List<Frame>();

        /// <summary>Each player's view of the HUD, by slot, made at its first use.</summary>
        private static Dictionary<int, GameUi> gameUis = new Dictionary<int, GameUi>();

        /// <summary>The window the screen root was last spanned to, in pixels; 0 until it is.</summary>
        private static int windowWidth, windowHeight;

        /// <summary>How often the window is checked for a new size, in seconds.</summary>
        internal const float WindowCheck = 0.5f;

        /// <summary>The TOC the build writes into the map, listing the game's template files the kinds are made from.</summary>
        internal const string TocFile = "MoonwellFrames.toc";

        /// <summary>The TOC the build writes for the map's own .fdf files in its assets, loaded after the game's; a map with none has none, and loading it does nothing.</summary>
        internal const string MapTocFile = "MoonwellMap.toc";

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
            drags = new Drags();
            tips = new Tips();
            gameUis = new Dictionary<int, GameUi>();
            roots = new List<Frame>();
            host.LoadToc(TocFile);
            host.LoadToc(MapTocFile);
            hud = new HudFrames(host);
            Moonwell.Events.LoadListener = Loaded;
            Cinematic.ScreenChanged = CinematicChanged;
        }

        /// <summary>
        /// A saved game loaded: the game has dropped every frame the map made, though their handles stay the same (game facts,
        /// 11-06). On every machine, loads the TOCs again and looks up the HUD again, as a claim says a load needs, then makes the
        /// roots and every frame on the screen again from what Moonwell keeps, in the order added, and sets each player's HUD as
        /// the map left it. The ended frames kept for reuse and the tooltips' slots went with the rest.
        /// </summary>
        internal static void Loaded()
        {
            var host = World;
            host.LoadToc(TocFile);
            host.LoadToc(MapTocFile);
            hud = new HudFrames(host);
            foreach (var ui in gameUis.Values)
            {
                ui.Rebind(hud);
            }

            slots = new Dictionary<framehandle, framehandle>();
            pool = new FramePool();
            events.Forget();
            drags.Loaded();
            tips.Loaded();
            if (screenRoot != null)
            {
                // The window check made with the first root goes on, and spans the new one.
                screenRoot = host.ScreenRoot();
                windowWidth = 0;
                windowHeight = 0;
                FitScreen();
            }

            if (overRoot != null)
            {
                overRoot = host.OverRoot();
            }

            // Every tree forgets first, so no frame is placed against a frame of the save.
            for (var i = 0; i < roots.Count; i++)
            {
                roots[i].Forget();
            }

            for (var i = 0; i < roots.Count; i++)
            {
                roots[i].MakeTree(roots[i].over ? OverRoot : ScreenRoot, roots[i].player);
            }

            foreach (var ui in gameUis.Values)
            {
                ui.Restore();
            }
        }

        /// <summary>A cinematic's letterbox came or went on this machine: the frames over the HUD hide or show again.</summary>
        internal static void CinematicChanged()
        {
            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i].over)
                {
                    roots[i].Reshow();
                }
            }
        }

        /// <summary>Keeps a frame added to the screen, to be made again when a saved game loads.</summary>
        internal static void Rooted(Frame frame)
        {
            frame.rooted = true;
            roots.Add(frame);
        }

        /// <summary>Lets an ended frame of the screen go.</summary>
        internal static void Unrooted(Frame frame)
        {
            roots.Remove(frame);
        }

        /// <summary>A player's view of the game's HUD, made at its first use.</summary>
        internal static GameUi GameUiOf(Player player)
        {
            if (!gameUis.TryGetValue(player.Slot, out var ui))
            {
                Moonwell.Events.Listen(GameEvent.Loaded);
                ui = new GameUi(player, hud!);
                gameUis[player.Slot] = ui;
            }

            return ui;
        }

        /// <summary>The world the package talks to.</summary>
        internal static IFramesWorld World => world!;

        /// <summary>The game's frame events, sent to the frames that own them.</summary>
        internal static FrameEvents Events => events;

        /// <summary>Where this machine's cursor is, in the game's units, from its pixels from the window's top left (seen 11-07); false for a window with no size, as a minimised one. Differs between machines.</summary>
        internal static bool CursorAt(out Vector2 at)
        {
            float width = World.ClientWidth();
            float height = World.ClientHeight();
            if (width <= 0.0f || height <= 0.0f)
            {
                at = Vector2.Zero;
                return false;
            }

            at = new Vector2(0.4f + (World.MouseScreenX() / width - 0.5f) * 0.6f * width / height, 0.6f * (1.0f - World.MouseScreenY() / height));
            return true;
        }

        /// <summary>The players' drags: what each holds, and the icon that follows their cursor.</summary>
        internal static Drags Drags => drags;

        /// <summary>The tooltips placed by their frames, moved to the side with room as they show.</summary>
        internal static Tips Tips => tips;

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
                    Moonwell.Events.Listen(GameEvent.Loaded);
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
                    Moonwell.Events.Listen(GameEvent.Loaded);
                    overRoot = World.OverRoot();
                }

                return overRoot;
            }
        }
    }
}
