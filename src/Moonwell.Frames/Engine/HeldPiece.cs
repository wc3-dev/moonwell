namespace Moonwell
{
    /// <summary>A part of the game's selection panel or inventory that the game shows again by itself, hidden in a way it does not undo and set again every second on its player's machine: the unit details, the unit's name, the inventory's text and cover.</summary>
    internal sealed class HeldPiece : GamePiece
    {
        /// <summary>How a piece is hidden. None is Visible on these, as the game shows them again.</summary>
        internal enum Way
        {
            /// <summary>A scale of nothing, as the command buttons are hidden (seen 11-03); for frames, never a String or Texture.</summary>
            Scale,

            /// <summary>Its points moved off the screen and put back, for a String, which takes only points and size (seen 11-13).</summary>
            Points,

            /// <summary>An alpha of nothing, for InventoryText (seen 11-13).</summary>
            Alpha,
        }

        private readonly Way way;

        /// <summary>For a String, the frame its top is tied to, as its .fdf ties it: SimpleNameValue's "Anchor TOP" in SimpleInfoPanelUnitDetail.</summary>
        private framehandle? home;

        /// <summary>Whether this machine has the piece hidden now.</summary>
        private bool off;

        internal HeldPiece(Player player, framehandle[] frames, Way way, framehandle? home)
            : base(player, frames)
        {
            this.way = way;
            this.home = home;
        }

        internal override void Rebind(GamePiece fresh)
        {
            base.Rebind(fresh);
            home = ((HeldPiece)fresh).home;
            off = false;
        }

        /// <summary>Hides or shows the piece by its way, as the HUD allows: with the whole HUD hidden it shows only when set Visible over it.</summary>
        internal override void Show(bool shown)
        {
            Apply(shown && FramesSystem.GameUiOf(player).Shows(this));
        }

        internal override void Hold(GameUi ui)
        {
            if (Here())
            {
                Apply(ui.Shows(this));
            }
        }

        private void Apply(bool shown)
        {
            var world = FramesSystem.World;
            if (shown && !off)
            {
                return;
            }

            for (var i = 0; i < frames.Length; i++)
            {
                switch (way)
                {
                    case Way.Scale:
                        world.ScaleFrame(frames[i], shown ? 1.0f : 0.00001f);
                        break;
                    case Way.Alpha:
                        world.FadeFrame(frames[i], shown ? 255 : 0);
                        break;
                    default:
                        if (shown)
                        {
                            world.ClearFramePoints(frames[i]);
                            world.PlaceFrame(frames[i], (int)FramePoint.Top, home!, (int)FramePoint.Top, 0.0f, 0.0f);
                        }
                        else
                        {
                            Engine.HudFrames.Off(world, frames[i], false);
                        }

                        break;
                }
            }

            off = !shown;
        }
    }
}
