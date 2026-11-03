namespace Moonwell
{
    /// <summary>
    /// A frame that holds others, with the game's window look (EscMenuBackdrop) unless Template says otherwise. A map's own
    /// panel inherits it, keeping its frames and their state together. A panel with a look catches the clicks on it, so they
    /// do not reach the units under it; Template = "" makes an empty panel, which draws nothing and lets every click through.
    /// </summary>
    public class Panel : Frame
    {
        /// <summary>An empty text over the look that catches its clicks, as the game's window lets them through (seen 11-03); null for an empty panel.</summary>
        internal framehandle? catcher;

        /// <summary>Makes a panel, to be added to the screen or to another frame.</summary>
        public Panel()
        {
            Template = "EscMenuBackdrop";
        }

        /// <summary>A BACKDROP, which an empty panel is made as: a FRAME by type takes every click on the world, even turned off (seen 11-03).</summary>
        internal override string GameType => "BACKDROP";

        /// <summary>Makes the catcher over a panel's look; an empty panel is painted blank instead, so it draws nothing.</summary>
        internal override void MakeParts()
        {
            var world = FramesSystem.World;
            if (madeTemplate.Length == 0)
            {
                catcher = null;
                world.PaintFrame(handle!, Image.Blank);
                return;
            }

            catcher = world.MakeFrame("TEXT", "", handle!)!;
            world.FillFrame(catcher, handle!);
        }

        /// <summary>Takes an ended panel's catcher with its frame.</summary>
        internal override void TakeOver(Frame old)
        {
            catcher = ((Panel)old).catcher;
            base.TakeOver(old);
        }

        /// <summary>Turns the catcher on or off: an empty panel never takes the mouse, whatever Enabled reads.</summary>
        internal override void TakeMouse(bool on)
        {
            if (catcher != null)
            {
                FramesSystem.World.EnableFrame(catcher, on);
            }
        }
    }
}
