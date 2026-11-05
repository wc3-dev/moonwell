namespace Moonwell
{
    /// <summary>
    /// A piece of the game's HUD on one player's screen, from player.GameUi(): what the map sets changes only that player's
    /// screen, and lasts until set again, as a camera's does. Reads give what was set, the same on every machine; setting is
    /// not allowed inside Local.For. A map's frame ties itself to a piece with Attach.
    /// </summary>
    public class GamePiece
    {
        internal readonly Player player;

        /// <summary>The game's frames of the piece: one, or two for the top bar.</summary>
        internal readonly framehandle[] frames;

        internal bool visible = true;

        internal GamePiece(Player player, framehandle[] frames)
        {
            this.player = player;
            this.frames = frames;
        }

        /// <summary>The game's frame, the same on every machine; each machine's own piece of its HUD.</summary>
        [LocalSafe]
        public framehandle Handle => frames[0];

        /// <summary>Whether the piece shows on its player's screen; true unless set. With the whole HUD hidden, setting it true shows the piece again: the minimap, the portrait and the hero bar and buttons come back, the item buttons do not (seen 11-05).</summary>
        [LocalSafe]
        public bool Visible
        {
            get { return visible; }
            set
            {
                visible = value;
                if (Here())
                {
                    Show(value);
                }
            }
        }

        /// <summary>Whether this machine is the piece's player's.</summary>
        internal bool Here()
        {
            return Runtime.World.IsLocal(player.Handle);
        }

        /// <summary>Shows or hides the piece on this machine.</summary>
        internal virtual void Show(bool shown)
        {
            for (var i = 0; i < frames.Length; i++)
            {
                FramesSystem.World.ShowFrame(frames[i], shown);
            }
        }
    }
}
