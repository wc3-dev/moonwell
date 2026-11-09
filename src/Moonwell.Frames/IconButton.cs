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

        /// <summary>The icon's texture, as ReplaceableTextures\CommandButtons\BTNFootman.blp; empty unless set, which draws nothing (a kit slot draws black).</summary>
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
                    Paint();
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

        internal override bool DrawnFromParts()
        {
            return true;
        }

        /// <summary>Makes the image that draws the icon, over the whole button.</summary>
        internal override void MakeParts()
        {
            iconFrame = FramesSystem.World.MakeFrame("BACKDROP", "", handle!)!;
            FramesSystem.World.FillFrame(iconFrame, handle!);
        }

        /// <summary>Takes an ended icon button's image with its frame; Place paints it.</summary>
        internal override void TakeOver(Frame old)
        {
            iconFrame = ((IconButton)old).iconFrame;
            base.TakeOver(old);
        }

        /// <summary>Gives the game the icon, or the empty look (an unpainted image draws the game's green missing texture), then the points and looks.</summary>
        internal override void Place()
        {
            Paint();
            base.Place();
        }

        /// <summary>Turned off, the button draws its icon greyed (I53): the game's own grey copy of one of its command icons, or the icon faded.</summary>
        internal override void TakeMouse(bool on)
        {
            base.TakeMouse(on);
            if (iconFrame != null)
            {
                Paint();
            }
        }

        private void Paint()
        {
            var look = Look();
            FramesSystem.World.PaintFrame(iconFrame!, look);
            FramesSystem.World.FadeFrame(iconFrame!, enabled || look != icon ? 255 : DisabledAlpha);
        }

        /// <summary>What an empty button draws: nothing.</summary>
        internal virtual string EmptyLook => Image.Blank;

        /// <summary>A turned-off icon's opacity when the game has no grey copy of it.</summary>
        internal const int DisabledAlpha = 110;

        private const string GameIcons = @"ReplaceableTextures\CommandButtons\BTN";

        private string Look()
        {
            if (icon.Length == 0)
            {
                return EmptyLook;
            }

            return enabled || !icon.StartsWith(GameIcons) ? icon : @"ReplaceableTextures\CommandButtonsDisabled\DISBTN" + icon.Substring(GameIcons.Length);
        }

        /// <summary>The icon follows the cursor while the button is held, unless DragIcon is set.</summary>
        internal override string DragTexture()
        {
            return dragIcon != "" ? dragIcon : icon;
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
