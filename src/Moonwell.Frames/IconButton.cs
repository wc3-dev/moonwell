using System;
using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// A button that is an icon: the game's button (a GLUEBUTTON) with the icon drawn over it, the size the map gives it. A
    /// click runs Clicked with the player, and the keyboard goes back to the game on that player's machine (decision 161).
    /// </summary>
    public class IconButton : Frame
    {
        internal string icon = "";

        /// <summary>The image over the button that draws the icon; null until the button is made.</summary>
        internal framehandle? iconFrame;

        internal List<Action<Player>>? clicked;

        /// <summary>Makes an icon button, to be added to the screen or to another frame. It is registered for clicks from the start, to give the keyboard back.</summary>
        public IconButton()
        {
            listened = 1 << FrameEvents.Click;
        }

        /// <summary>The icon's texture, as ReplaceableTextures\CommandButtons\BTNFootman.blp; empty unless set, which draws nothing.</summary>
        [LocalSafe]
        public string Icon
        {
            get { return icon; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                icon = value;
                if (iconFrame != null)
                {
                    FramesSystem.World.PaintFrame(iconFrame, value);
                }
            }
        }

        /// <summary>Runs when a player clicks the button, with the player. Not inside Local.For.</summary>
        public event Action<Player>? Clicked
        {
            add { Subscribe(ref clicked, value, FrameEvents.Click, FrameEvents.Click); }
            remove { Unsubscribe(clicked, value); }
        }

        /// <summary>A GLUEBUTTON, which takes clicks and draws nothing of its own.</summary>
        internal override string GameType => "GLUEBUTTON";

        /// <summary>Makes the image that draws the icon, over the whole button.</summary>
        internal override void MakeParts()
        {
            iconFrame = FramesSystem.World.MakeFrame("BACKDROP", "", handle!)!;
            FramesSystem.World.FillFrame(iconFrame, handle!);
        }

        /// <summary>Takes an ended icon button's image with its frame, painted over when this one has no icon.</summary>
        internal override void TakeOver(Frame old)
        {
            var ended = (IconButton)old;
            iconFrame = ended.iconFrame;
            base.TakeOver(old);
            if (icon.Length == 0 && ended.icon.Length > 0)
            {
                FramesSystem.World.PaintFrame(iconFrame!, Image.Blank);
            }
        }

        /// <summary>Gives the game the icon, then the points and looks.</summary>
        internal override void Place()
        {
            if (icon.Length > 0)
            {
                FramesSystem.World.PaintFrame(iconFrame!, icon);
            }

            base.Place();
        }

        /// <summary>A click gives the keyboard back and runs Clicked.</summary>
        internal override void Receive(int kind, Player who, float value, string text)
        {
            if (kind != FrameEvents.Click)
            {
                base.Receive(kind, who, value, text);
                return;
            }

            GiveKeyboardBack(who);
            Raise(clicked, who, "Clicked");
        }
    }
}
