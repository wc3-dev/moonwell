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

        /// <summary>How often each player's HUD is set again where the game puts back what the map set, in seconds: as often as the map that found what it puts back did it (TheProject's WoW UI, 11-13).</summary>
        internal const float HoldEvery = 1.0f;

        /// <summary>Whether a map's frame made under each game frame keeps it as its parent, by the game frame, found once: the game gives a frame another parent under a simple frame, and it never draws there (seen 11-13).</summary>
        private static Dictionary<framehandle, bool> keepsChildren = new Dictionary<framehandle, bool>();

        /// <summary>The frames added with Screen.AddTo that follow a game frame, in the order added, checked every FollowEvery.</summary>
        private static readonly List<Frame> followers = new List<Frame>();

        /// <summary>How often a frame following a game frame checks it, in seconds: as often as the portrait's flash after a cinematic stopped being seen (11-13).</summary>
        private const float FollowEvery = 0.03f;

        /// <summary>Whether the timer that checks the followed game frames is running.</summary>
        private static bool followingStarted;

        /// <summary>Whether the timer that sets each player's HUD again is running.</summary>
        private static bool holding;

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
            keepsChildren = new Dictionary<framehandle, bool>();
            pool = new FramePool();
            events = new FrameEvents();
            drags = new Drags();
            tips = new Tips();
            gameUis = new Dictionary<int, GameUi>();
            roots = new List<Frame>();
            host.LoadToc(TocFile);
            host.LoadToc(MapTocFile);
            hud = new HudFrames(host);
            holding = false;
            Moonwell.Events.LoadListener = Loaded;
            Cinematic.ScreenChanged = CinematicChanged;
#if DEBUG
            DebugMode.Command("frames", "writes the game's frame tree to CustomMapData\\Moonwell\\frames.txt: names, sizes, shown or not, children", (player, rest) => FrameTree.Write(host, player));
#endif
        }

        /// <summary>Starts setting every player's HUD again every second, on each player's own machine, at the first change of a GameUi; started on every machine, as the change is made on every machine.</summary>
        internal static void HoldHud()
        {
            if (holding)
            {
                return;
            }

            holding = true;
            Game.Instance.Every(HoldEvery, () =>
            {
                foreach (var ui in gameUis.Values)
                {
                    ui.Hold();
                }
            });
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
                roots[i].MakeTree(RootOf(roots[i]), roots[i].player);
            }

            foreach (var ui in gameUis.Values)
            {
                ui.Restore();
            }
        }

        /// <summary>The game frame a frame added to the screen is made under: its root; for one added with Screen.AddTo, its game frame when that keeps it as a child, else the over root, with the game frame followed.</summary>
        internal static framehandle RootOf(Frame frame)
        {
            frame.following = null;
            frame.gameShown = true;
            var game = frame.gameFrame?.Invoke();
            if (game == null)
            {
                return frame.over ? OverRoot : ScreenRoot;
            }

            if (KeepsChildren(game))
            {
                return game;
            }

            frame.following = game;
            frame.gameShown = World.FrameShown(game);
            Follow(frame);
            return OverRoot;
        }

        /// <summary>Whether a map's frame made under a game frame keeps it as its parent: found once, by a hidden frame made under it, on every machine, as the game's frames are one set everywhere.</summary>
        private static bool KeepsChildren(framehandle game)
        {
            if (!keepsChildren.TryGetValue(game, out var keeps))
            {
                var test = World.MakeFrame("BACKDROP", "", game)!;
                World.ShowFrame(test, false);
                keeps = World.ParentOf(test) == game;
                keepsChildren[game] = keeps;
            }

            return keeps;
        }

        /// <summary>Starts checking a frame's game frame every FollowEvery, on every machine, each showing or hiding the frame on its own screen.</summary>
        private static void Follow(Frame frame)
        {
            if (!followers.Contains(frame))
            {
                followers.Add(frame);
            }

            if (followingStarted)
            {
                return;
            }

            followingStarted = true;
            Game.Instance.Every(FollowEvery, () =>
            {
                for (var i = followers.Count - 1; i >= 0; i--)
                {
                    var follower = followers[i];
                    if (follower.HasEnded)
                    {
                        followers.RemoveAt(i);
                        continue;
                    }

                    if (follower.following == null || follower.handle == null)
                    {
                        continue;
                    }

                    var shown = World.FrameShown(follower.following);
                    if (shown != follower.gameShown)
                    {
                        follower.gameShown = shown;
                        follower.Reshow();
                    }
                }
            });
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

            if (Cinematic.LetterboxHere() != null)
            {
                return;
            }

            // The letterbox's end brings back the portrait and the selection panel the map hid, by the end of its fade (seen
            // 11-13), so each player's HUD is set again now and every 0.03 s through the fade and past it: set once after the
            // fade the portrait flashed, and set every tenth of a second it still flashed, shorter (seen 11-13).
            RestoreHuds();
            var left = AfterLetterboxSteps;
            Timer? again = null;
            again = Game.Instance.OwnEvery(AfterLetterboxEvery, () =>
            {
                if (--left <= 0)
                {
                    again?.End();
                }

                if (Cinematic.LetterboxHere() == null)
                {
                    RestoreHuds();
                }
            });
        }

        /// <summary>How often each player's HUD is set again after a cinematic's end, in seconds.</summary>
        internal const float AfterLetterboxEvery = 0.03f;

        /// <summary>How many times each player's HUD is set again after a cinematic's end: for a second, past the letterbox's fade of 0.5 s.</summary>
        internal const int AfterLetterboxSteps = 34;

        private static void RestoreHuds()
        {
            foreach (var ui in gameUis.Values)
            {
                ui.Restore();
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
