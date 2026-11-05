namespace Moonwell
{
    /// <summary>A piece of the game's HUD that the map can also move and scale: the minimap, a command button or an item button (moved, seen 10-15 and 11-05).</summary>
    public sealed class MovablePiece : GamePiece
    {
        /// <summary>A command button's hiding: Visible false on one does not hold past the next selection, a scale of nothing does (seen 11-03).</summary>
        private readonly bool hiddenByScale;

        /// <summary>The parent the piece is given when first moved: the console's backdrop for the minimap, which draws there over the console's art (seen 11-03); null to keep its own.</summary>
        private readonly framehandle? movedUnder;

        internal float scale = 1.0f;

        /// <summary>Whether the piece has been moved on this machine; written on its player's machine only.</summary>
        private bool moved;

        internal MovablePiece(Player player, framehandle frame, bool hiddenByScale, framehandle? movedUnder)
            : base(player, new[] { frame })
        {
            this.hiddenByScale = hiddenByScale;
            this.movedUnder = movedUnder;
        }

        /// <summary>The piece's scale, which sizes it; 1 unless set.</summary>
        [LocalSafe]
        public float Scale
        {
            get { return scale; }
            set
            {
                scale = value;
                if (Here() && (visible || !hiddenByScale))
                {
                    FramesSystem.World.ScaleFrame(frames[0], value);
                }
            }
        }

        /// <summary>Ties a point of the piece to a point of a map's frame, moved by an offset, in place of where the game puts it; another Attach adds a point. The frame must be added first; the piece moves on its player's screen only.</summary>
        public void Attach(FramePoint point, Frame other, FramePoint otherPoint, float x = 0.0f, float y = 0.0f)
        {
            if (other.handle == null)
            {
                Errors.Refuse("GamePiece.Attach", Names.NameOf(other) + " is not added yet: add it to the screen first");
                return;
            }

            if (!Here())
            {
                return;
            }

            var world = FramesSystem.World;
            if (!moved)
            {
                moved = true;
                if (movedUnder != null)
                {
                    world.ParentFrame(frames[0], movedUnder);
                }

                world.ClearFramePoints(frames[0]);
            }

            world.PlaceFrame(frames[0], (int)point, other.handle, (int)otherPoint, x, y);
        }

        /// <summary>Shows or hides the piece on this machine; a command button by its scale.</summary>
        internal override void Show(bool shown)
        {
            if (hiddenByScale)
            {
                FramesSystem.World.ScaleFrame(frames[0], shown ? scale : 0.00001f);
                return;
            }

            base.Show(shown);
        }
    }
}
