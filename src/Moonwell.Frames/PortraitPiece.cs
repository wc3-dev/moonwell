namespace Moonwell
{
    /// <summary>
    /// The game's portrait of the first unit selected, which a map's frame can hold with Fill. The game places the portrait in
    /// a space of its own: its x and width as parts of the whole window's width, 0 at the left edge and 0.8 at the right, in
    /// classic and Reforged graphics alike (seen 11-13 and 14), so Moonwell works out the frame's place and squeezes it for
    /// the window's shape on its player's machine.
    /// </summary>
    public sealed class PortraitPiece : GamePiece
    {
        /// <summary>The map frame the portrait fills; null unless Fill was called.</summary>
        private Frame? filling;

        /// <summary>Whether this machine has stopped the game placing its HUD again, as the portrait moved after it was not seen to vanish (seen 11-13).</summary>
        private bool moved;

        internal PortraitPiece(Player player, framehandle frame)
            : base(player, new[] { frame })
        {
        }

        /// <summary>Puts the portrait on a map's frame at its place and size, on its player's screen, and again every second, when the window changes size and after a load. Moonwell works out where the frame is from how the map placed it (Anchor and Offset, Add, Fill, Attach to other map frames, sizes); refused for a frame not added yet, or whose place cannot be known: one tied to a piece of the HUD, under one of the game's frames, or with no size. The game has one portrait, so it shows in one frame at a time.</summary>
        public void Fill(Frame other)
        {
            if (other.handle == null)
            {
                Errors.Refuse("Portrait.Fill", Names.NameOf(other) + " is not added yet: add it to the screen first");
                return;
            }

            if (!Placeable(other))
            {
                Errors.Refuse("Portrait.Fill", "where " + Names.NameOf(other) + " is cannot be worked out: it is tied to a piece of the HUD, under one of the game's frames, or has no size");
                return;
            }

            filling = other;
            Place();
            FramesSystem.GameUiOf(player).Held();
        }

        /// <summary>Whether the frame's place can be worked out, the same on every machine: the window may differ by machine, but not the frames' setup.</summary>
        private static bool Placeable(Frame frame)
        {
            return frame.ScreenPlace(true, out _, out _, out _, out _);
        }

        /// <summary>Places the portrait on its player's machine over the frame it fills: x and width squeezed by 0.8 over the window's width in the game's units.</summary>
        internal void Place()
        {
            if (filling == null || filling.HasEnded || !Here())
            {
                return;
            }

            var world = FramesSystem.World;
            var width = world.ClientWidth();
            var height = world.ClientHeight();
            if (width <= 0 || height <= 0 || !filling.ScreenPlace(false, out var left, out var bottom, out var right, out var top))
            {
                return;
            }

            if (!moved)
            {
                moved = true;
                world.AutoPlaceHud(false);
            }

            var across = 0.6f * width / height;
            var squeeze = 0.8f / across;
            world.ClearFramePoints(frames[0]);
            world.PinFrame(frames[0], (int)FramePoint.Center, ((left + right) / 2.0f - 0.4f + across / 2.0f) * squeeze, (bottom + top) / 2.0f);
            world.SizeFrame(frames[0], (right - left) * squeeze, top - bottom);
        }

        /// <summary>Places the portrait again after a load, as the load may give it back as the game starts it.</summary>
        internal override void Restore()
        {
            base.Restore();
            moved = false;
            Place();
        }

        /// <summary>Places the portrait again, as the game puts it back after a cinematic and when its HUD is placed again.</summary>
        internal override void Hold(GameUi ui)
        {
            Place();
        }
    }
}
