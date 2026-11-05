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

        /// <summary>Makes the catcher, before the frames added to the panel so it lies beneath them: on over a panel's look, and off over an empty panel, which is painted blank and draws nothing, until it is listened to or given a tooltip.</summary>
        internal override void MakeParts()
        {
            var world = FramesSystem.World;
            if (madeTemplate.Length == 0)
            {
                world.PaintFrame(handle!, Image.Blank);
            }
            else
            {
                // A template's own parts are made before the catcher, which would lie over them and take their clicks.
                var count = world.ChildCount(handle!);
                for (var i = 0; i < count; i++)
                {
                    world.LevelFrame(world.ChildAt(handle!, i), 1);
                }
            }

            MakeCatcher();
            if (!Catches(enabled))
            {
                world.EnableFrame(catcher!, false);
            }
        }

        /// <summary>The catcher; an empty panel's own frame until it is listened to, which gets no events.</summary>
        internal override framehandle? MouseHandle => catcher ?? handle;

        /// <summary>An empty panel listened to gets a catcher, or has an ended one's turned back on.</summary>
        internal override void PrepareMouse()
        {
            if (handle == null)
            {
                return;
            }

            if (catcher == null)
            {
                MakeCatcher();
            }
            else
            {
                FramesSystem.World.EnableFrame(catcher, enabled);
            }
        }

        /// <summary>Takes an ended panel's catcher with its frame: on as Enabled says, and for an empty panel only when it is listened to.</summary>
        internal override void TakeOver(Frame old)
        {
            catcher = ((Panel)old).catcher;
            base.TakeOver(old);
            if (catcher != null)
            {
                FramesSystem.World.EnableFrame(catcher, Catches(enabled));
            }
            else if (WantsMouse())
            {
                MakeCatcher();
            }
        }

        /// <summary>Turns the catcher on or off: an empty panel takes the mouse only when it is listened to, whatever Enabled reads.</summary>
        internal override void TakeMouse(bool on)
        {
            if (catcher != null)
            {
                FramesSystem.World.EnableFrame(catcher, Catches(on));
            }
        }

        private bool Catches(bool on)
        {
            return on && (madeTemplate.Length > 0 || WantsMouse());
        }

        private void MakeCatcher()
        {
            catcher = FramesSystem.World.MakeFrame("TEXT", "", handle!)!;
            FramesSystem.World.FillFrame(catcher, handle!);
        }
    }
}
