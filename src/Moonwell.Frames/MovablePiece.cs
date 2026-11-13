using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>A piece of the game's HUD that the map can also move and scale: the minimap, a command button, an item button or the selection panel's icons (moved, seen 10-15, 11-05 and 11-13).</summary>
    public sealed class MovablePiece : GamePiece
    {
        /// <summary>A command button's hiding: Visible false on one does not hold past the next selection, a scale of nothing does (seen 11-03).</summary>
        private readonly bool hiddenByScale;

        /// <summary>The parent the piece is given when first moved: the console's backdrop for the minimap, which draws there over the console's art (seen 11-03); null to keep its own.</summary>
        private framehandle? movedUnder;

        /// <summary>The game's SIMPLEBUTTON the piece sits in, CommandButton_N or InventoryButton_N: moving the piece leaves it behind taking clicks, so it is put off the screen with the first move, and again every second (seen 11-13); null for the others.</summary>
        private framehandle? holder;

        /// <summary>The selection panel's way: moved at no size, as given a size it takes clicks over all of it (seen 11-13), and placed again every second, with Visible set again while it shows over the hidden HUD.</summary>
        private readonly bool atNoSize;

        /// <summary>The points tied to map frames, in the order given, to be tied again when a saved game loads; null until the first Attach.</summary>
        private List<Tie>? ties;

        internal float scale = 1.0f;

        /// <summary>Whether the piece has been moved on this machine; written on its player's machine only, and again after a load.</summary>
        private bool moved;

        internal MovablePiece(Player player, framehandle frame, bool hiddenByScale, framehandle? movedUnder, framehandle? holder = null, bool atNoSize = false)
            : base(player, new[] { frame })
        {
            this.hiddenByScale = hiddenByScale;
            this.movedUnder = movedUnder;
            this.holder = holder;
            this.atNoSize = atNoSize;
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

        /// <summary>Ties a point of the piece to a point of a map's frame, moved by an offset, in place of where the game puts it; another Attach adds a point. The frame must be added first; the piece moves on its player's screen only, and the first move stops the game placing its HUD again when the window changes. The selection icons are moved at no size, so every point is the same: a group's icons draw up and left of it, one unit's name on it (seen 11-13).</summary>
        public void Attach(FramePoint point, Frame other, FramePoint otherPoint, float x = 0.0f, float y = 0.0f)
        {
            if (other.handle == null)
            {
                Errors.Refuse("GamePiece.Attach", Names.NameOf(other) + " is not added yet: add it to the screen first");
                return;
            }

            ties ??= new List<Tie>();
            ties.Add(new Tie(point, other, null, otherPoint, x, y));
            if (Here())
            {
                Tie(ties[ties.Count - 1]);
            }

            FramesSystem.GameUiOf(player).Held();
        }

        /// <summary>Gives the game one point on this machine, moving the piece out of the game's place the first time.</summary>
        private void Tie(Tie tie)
        {
            var world = FramesSystem.World;
            if (!moved)
            {
                moved = true;
                world.AutoPlaceHud(false);
                if (movedUnder != null)
                {
                    world.ParentFrame(frames[0], movedUnder);
                }

                if (holder != null)
                {
                    Engine.HudFrames.Off(world, holder, true);
                }

                world.ClearFramePoints(frames[0]);
            }

            world.PlaceFrame(frames[0], (int)tie.Point, tie.Other!.handle!, (int)tie.OtherPoint, tie.X, tie.Y);
            if (atNoSize)
            {
                world.SizeFrame(frames[0], 0.0001f, 0.0001f);
            }
        }

        /// <summary>Takes the piece's game frame and the parent it is moved under, looked up again.</summary>
        internal override void Rebind(GamePiece fresh)
        {
            base.Rebind(fresh);
            movedUnder = ((MovablePiece)fresh).movedUnder;
            holder = ((MovablePiece)fresh).holder;
        }

        /// <summary>Sets the piece's look again, and ties it again to the map frames it was tied to that have not ended, made again by now.</summary>
        internal override void Restore()
        {
            base.Restore();
            if (scale != 1.0f && (visible || !hiddenByScale))
            {
                FramesSystem.World.ScaleFrame(frames[0], scale);
            }

            moved = false;
            TieAll();
        }

        /// <summary>Puts the holder off the screen again, and the selection panel back where it was tied, shown or hidden as set, as the game puts them back (seen 11-13).</summary>
        internal override void Hold(GameUi ui)
        {
            if (!Here())
            {
                return;
            }

            var world = FramesSystem.World;
            if (moved && holder != null)
            {
                Engine.HudFrames.Off(world, holder, true);
            }

            if (!atNoSize)
            {
                return;
            }

            if (!visible || (!ui.Visible && overHud))
            {
                Show(visible);
            }

            if (moved)
            {
                world.ClearFramePoints(frames[0]);
                TieAll();
            }
        }

        private void TieAll()
        {
            if (ties == null)
            {
                return;
            }

            for (var i = 0; i < ties.Count; i++)
            {
                if (!ties[i].Other!.HasEnded && ties[i].Other!.handle != null)
                {
                    Tie(ties[i]);
                }
            }
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
