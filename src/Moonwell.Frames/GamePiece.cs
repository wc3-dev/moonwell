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

        /// <summary>The game's frames of the piece: one, or several for the top bar and the unit details; looked up again when a saved game loads.</summary>
        internal framehandle[] frames;

        internal bool visible = true;

        /// <summary>Whether the map set the piece Visible while the whole HUD was hidden, which shows it over the hidden HUD; cleared when the HUD is hidden again.</summary>
        internal bool overHud;

        internal GamePiece(Player player, framehandle[] frames)
        {
            this.player = player;
            this.frames = frames;
        }

        /// <summary>The game's frame, the same on every machine; each machine's own piece of its HUD. Looked up again when a saved game loads.</summary>
        [LocalSafe]
        public framehandle Handle => frames[0];

        /// <summary>Whether the piece shows on its player's screen; true unless set. With the whole HUD hidden, setting it true shows the piece over it, with nothing else of the HUD coming along: the item buttons come without the inventory's text, cover and click blockers. What the game shows again by itself is set again every second.</summary>
        [LocalSafe]
        public bool Visible
        {
            get { return visible; }
            set
            {
                visible = value;
                var ui = FramesSystem.GameUiOf(player);
                if (value && !ui.Visible)
                {
                    overHud = true;
                }

                if (Here())
                {
                    Show(value);
                }

                ui.Held();
            }
        }

        /// <summary>Whether this machine is the piece's player's.</summary>
        internal bool Here()
        {
            return Runtime.World.IsLocal(player.Handle);
        }

        /// <summary>A saved game loaded: takes the game frames of the same piece, looked up again.</summary>
        internal virtual void Rebind(GamePiece fresh)
        {
            frames = fresh.frames;
        }

        /// <summary>A saved game loaded: sets again on this machine what the map set, as the load may give the HUD back as the game starts it.</summary>
        internal virtual void Restore()
        {
            if (!visible)
            {
                Show(false);
            }
            else if (overHud)
            {
                Show(true);
            }
        }

        /// <summary>Shows or hides the piece on this machine.</summary>
        internal virtual void Show(bool shown)
        {
            for (var i = 0; i < frames.Length; i++)
            {
                FramesSystem.World.ShowFrame(frames[i], shown);
            }
        }

        /// <summary>Sets again, on this machine, what the game shows or places again by itself; nothing for most pieces.</summary>
        internal virtual void Hold(GameUi ui)
        {
        }
    }
}
