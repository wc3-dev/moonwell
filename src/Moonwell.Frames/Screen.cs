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
            frame.MakeTree(over ? FramesSystem.OverRoot : FramesSystem.ScreenRoot, shownTo);
            FramesSystem.Rooted(frame);
            if (over && Cinematic.LetterboxHere() != null)
            {
                frame.Reshow();
            }

            return frame;
        }
    }
}
