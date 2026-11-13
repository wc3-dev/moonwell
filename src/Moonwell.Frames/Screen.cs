namespace Moonwell
{
    /// <summary>
    /// The screen, in two layers. Add puts a frame on the whole width of each player's window, beneath the game's HUD; AddOver
    /// puts one over the HUD, within the 4:3 middle. No frame a map makes can do both (seen 11-03).
    /// </summary>
    public static class Screen
    {
        private static float designHeight = 0.6f;

        /// <summary>The screen's height in the units frame.Add(child, x, y, width, height) takes: 0.6, the game's, unless set; 768 lets a layout made in pixels for a screen 768 high be written as it is. Set it once, before adding; a frame added already keeps its place. Refused unless above 0.</summary>
        public static float DesignHeight
        {
            get { return designHeight; }
            set
            {
                if (value > 0.0f)
                {
                    designHeight = value;
                }
                else
                {
                    Errors.Refuse("Screen.DesignHeight", value + " is not above 0: kept at " + designHeight);
                }
            }
        }

        /// <summary>Adds a frame to the screen's whole width, beneath the HUD, in a scope, which ends it when it ends, and returns it. Given shownTo, only that player sees it: it is made on every machine and shown on that player's. In a scope that has ended it ends at once and nothing is made.</summary>
        public static T Add<T>(T frame, Scope scope, Player? shownTo = null)
            where T : Frame
        {
            return Put(frame, scope, shownTo, false);
        }

        /// <summary>Adds a frame over the HUD, within the screen's 4:3 middle, as Add does otherwise: for what must cover the HUD, such as a tooltip over the command card.</summary>
        public static T AddOver<T>(T frame, Scope scope, Player? shownTo = null)
            where T : Frame
        {
            return Put(frame, scope, shownTo, true);
        }

        /// <summary>
        /// Adds a frame to one of the game's HUD frames, by its name in the game's .fdf files, so it shows only while that
        /// frame shows: "EscMenuMainPanel" (the F10 menu), "SimpleInfoPanelUnitDetail" (one unit selected); otherwise as
        /// Add does. Under the F10 menu's panel, the minimap or the portrait the frame is the game frame's own child, placed
        /// from it. The rest of the HUD is simple frames, under which the game gives a map's frame another parent and it
        /// never draws (seen 11-13), so there the frame is added over the HUD, placed as AddOver places it, and shown and
        /// hidden with the game frame, checked every 0.03 s on each player's machine. A name the game does not have is
        /// reported, and the frame added over the HUD. A String or Texture by name is error MW0057, as the check crashes the
        /// game on one.
        /// </summary>
        public static T AddTo<T>(T frame, string gameFrame, Scope scope, Player? shownTo = null)
            where T : Frame
        {
            if (frame.Parent == null && FramesSystem.World.GameFrame(gameFrame) == null)
            {
                Errors.Refuse("Screen.AddTo", "the game has no frame named " + gameFrame + ": " + Names.NameOf(frame) + " added over the HUD");
                return AddOver(frame, scope, shownTo);
            }

            return Under(frame, () => FramesSystem.World.GameFrame(gameFrame), scope, shownTo);
        }

        /// <summary>Adds a frame to a piece of the game's HUD, from player.GameUi(), as AddTo by name does: the minimap and the portrait keep it as their child, and with any other piece it shows and hides, checked every 0.03 s.</summary>
        public static T AddTo<T>(T frame, GamePiece piece, Scope scope, Player? shownTo = null)
            where T : Frame
        {
            return Under(frame, () => piece.Handle, scope, shownTo);
        }

        private static T Under<T>(T frame, System.Func<framehandle?> gameFrame, Scope scope, Player? shownTo)
            where T : Frame
        {
            if (frame.Parent == null)
            {
                frame.gameFrame = gameFrame;
            }

            return Put(frame, scope, shownTo, true);
        }

        private static T Put<T>(T frame, Scope scope, Player? shownTo, bool over)
            where T : Frame
        {
            var added = frame.Parent != null;
            scope.Adopt(frame);
            if (added || frame.Parent != scope || frame.HasEnded)
            {
                return frame;
            }

            frame.player = shownTo;
            frame.over = over;
            frame.MakeTree(FramesSystem.RootOf(frame), shownTo);
            FramesSystem.Rooted(frame);
            if (over && Cinematic.LetterboxHere() != null)
            {
                frame.Reshow();
            }

            return frame;
        }
    }
}
