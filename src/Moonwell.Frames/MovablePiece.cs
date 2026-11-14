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

        /// <summary>The size the piece draws at whatever size it is given, so Fill scales it: the minimap's, 0.139375 by 0.138125 (read 11-13); 0 for a piece that takes a size.</summary>
        private readonly float drawnWidth, drawnHeight;

        /// <summary>The map frame the piece fills, centred on it at its size; null unless Fill was called.</summary>
        private Frame? filling;

        /// <summary>How long after a scale change the minimap is tied again: scaled in the instant it was tied, it drew only a white dot, and tied a 0.1 s later it drew whole (seen 11-13 and 14).</summary>
        private const float RetieAfter = 0.1f;

        /// <summary>The game time after which the minimap is tied again on this machine; 0 when it need not be.</summary>
        private float retieAt;

        /// <summary>The scale Fill gave the minimap on this machine, from a size the game may give only here; 0 until then, and after a load. Scale reads what the map set, the same everywhere.</summary>
        private float fitted;

        /// <summary>The points tied to map frames, in the order given, to be tied again when a saved game loads; null until the first Attach.</summary>
        private List<Tie>? ties;

        internal float scale = 1.0f;

        /// <summary>Whether the piece has been moved on this machine; written on its player's machine only, and again after a load.</summary>
        private bool moved;

        internal MovablePiece(Player player, framehandle frame, bool hiddenByScale, framehandle? movedUnder, framehandle? holder = null, bool atNoSize = false, float drawnWidth = 0.0f, float drawnHeight = 0.0f)
            : base(player, new[] { frame })
        {
            this.drawnWidth = drawnWidth;
            this.drawnHeight = drawnHeight;
            this.hiddenByScale = hiddenByScale;
            this.movedUnder = movedUnder;
            this.holder = holder;
            this.atNoSize = atNoSize;
        }

        /// <summary>The piece's scale, which sizes it; 1 unless set. A moved minimap is tied again a moment after, as scaled in the instant it was tied it draws only a white dot (seen 11-13 and 14).</summary>
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
                    RetieLater();
                }

                if (drawnWidth > 0.0f)
                {
                    Game.Instance.After(RetieAfter, Retie);
                }
            }
        }

        /// <summary>Puts the piece on a map's frame, centred, at the frame's size, in place of its ties: a command or item button takes the size, and the minimap, which draws at one size (about 0.139 square) and only scales, is scaled to fit inside the frame. The size is the frame's Width and Height, or what the game gives it when they are not set, and is taken again every second. The selection icons move at no size, so Fill is refused for them; as Attach, the frame must be added first.</summary>
        public void Fill(Frame other)
        {
            if (atNoSize)
            {
                Errors.Refuse("GamePiece.Fill", "the selection icons move at no size: tie them with Attach");
                return;
            }

            if (other.handle == null)
            {
                Errors.Refuse("GamePiece.Fill", Names.NameOf(other) + " is not added yet: add it to the screen first");
                return;
            }

            filling = other;
            ties = new List<Tie> { new Tie(FramePoint.Center, other, null, FramePoint.Center, 0.0f, 0.0f) };
            if (Here())
            {
                Fit(true);
            }

            if (drawnWidth > 0.0f)
            {
                Game.Instance.After(RetieAfter, Retie);
            }

            FramesSystem.GameUiOf(player).Held();
        }

        /// <summary>Sizes the piece to the frame it fills on this machine, the minimap by its scale before it is tied, and ties it when asked.</summary>
        private void Fit(bool tie)
        {
            var world = FramesSystem.World;
            var width = filling!.width > 0.0f ? filling.width : world.FrameWidth(filling.handle!);
            var height = filling.height > 0.0f ? filling.height : world.FrameHeight(filling.handle!);
            if (drawnWidth > 0.0f)
            {
                var fits = System.Math.Min(width / drawnWidth, height / drawnHeight);
                if (fits > 0.0f && fits != fitted)
                {
                    fitted = fits;
                    world.ScaleFrame(frames[0], fits);
                    RetieLater();
                    tie = true;
                }
            }
            else if (width > 0.0f && height > 0.0f)
            {
                // A frame placed by points can read no size at first (seen 11-05): sized a second later.
                world.SizeFrame(frames[0], width, height);
            }

            if (tie)
            {
                if (moved)
                {
                    world.ClearFramePoints(frames[0]);
                }

                TieAll();
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

        /// <summary>Marks the minimap, on this machine, to be tied again once RetieAfter has passed.</summary>
        private void RetieLater()
        {
            if (drawnWidth > 0.0f)
            {
                retieAt = Game.Time + RetieAfter;
            }
        }

        /// <summary>Ties the minimap again on its player's machine once its time has come: from a timer made on every machine after a scale set or Fill, and from the hold every second, after a load or a frame filled that changed size.</summary>
        private void Retie()
        {
            if (retieAt <= 0.0f || Game.Time < retieAt || !Here())
            {
                return;
            }

            retieAt = 0.0f;
            if (moved)
            {
                FramesSystem.World.ClearFramePoints(frames[0]);
                TieAll();
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
            fitted = 0.0f;
            TieAll();
            RetieLater();
            if (filling != null && !filling.HasEnded && filling.handle != null)
            {
                Fit(false);
            }
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

            if (filling != null && !filling.HasEnded && filling.handle != null)
            {
                Fit(false);
            }

            Retie();
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
