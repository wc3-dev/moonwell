using System;
using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell
{
    /// <summary>
    /// A check box, one player's only (decision 156): Moonwell draws it as the game's, a game button with the escape menu's
    /// check box textures (seen 11-03), as the game's own cannot be set from code (seen). A click toggles it, gives the keyboard
    /// back and runs Toggled; setting Checked draws it and runs nothing.
    /// </summary>
    public class CheckBox : Frame
    {
        internal const string Background = "UI\\Widgets\\EscMenu\\Human\\checkbox-background.blp";

        internal const string Check = "UI\\Widgets\\EscMenu\\Human\\checkbox-check.blp";

        internal bool isChecked;

        /// <summary>The images that draw the box and its check; null until the check box is made.</summary>
        internal framehandle? backFrame, checkFrame;

        internal List<Action<Player, bool>>? toggled;

        /// <summary>Makes a check box, unchecked, the game's size (0.024), to be added to a frame for one player.</summary>
        public CheckBox()
        {
            listened = 1 << FrameEvents.Click;
            width = 0.024f;
            height = 0.024f;
            sized = true;
        }

        /// <summary>Whether it is checked: what the last click made it, or what the map last set, the same on every machine; false unless set.</summary>
        [LocalSafe]
        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                isChecked = value;
                if (checkFrame != null)
                {
                    FramesSystem.World.ShowFrame(checkFrame, value);
                }
            }
        }

        /// <summary>Runs when the player clicks it, with the player and whether it is now checked. Not inside Local.For.</summary>
        public event Action<Player, bool>? Toggled
        {
            add { Subscribe(ref toggled, value, FrameEvents.Click, FrameEvents.Click); }
            remove { Unsubscribe(toggled, value); }
        }

        /// <summary>A GLUEBUTTON, which takes clicks and draws nothing of its own.</summary>
        internal override string GameType => "GLUEBUTTON";

        internal override bool ForOnePlayerOnly() => true;

        internal override bool DrawnFromParts()
        {
            return true;
        }

        /// <summary>Makes the box and its check, each filled over the button.</summary>
        internal override void MakeParts()
        {
            backFrame = Paint(Background);
            checkFrame = Paint(Check);
        }

        private framehandle Paint(string texture)
        {
            var image = FramesSystem.World.MakeFrame("BACKDROP", "", handle!)!;
            FramesSystem.World.FillFrame(image, handle!);
            FramesSystem.World.PaintFrame(image, texture);
            return image;
        }

        /// <summary>Takes an ended check box's images with its frame.</summary>
        internal override void TakeOver(Frame old)
        {
            var ended = (CheckBox)old;
            backFrame = ended.backFrame;
            checkFrame = ended.checkFrame;
            base.TakeOver(old);
        }

        /// <summary>Draws the check as Checked says, then the points and looks.</summary>
        internal override void Place()
        {
            FramesSystem.World.ShowFrame(checkFrame!, isChecked);
            base.Place();
        }

        /// <summary>A click gives the keyboard back, toggles it and runs Toggled.</summary>
        internal override void Receive(int kind, Player who, float value, string text)
        {
            if (kind != FrameEvents.Click)
            {
                base.Receive(kind, who, value, text);
                return;
            }

            GiveKeyboardBack(who);
            isChecked = !isChecked;
            FramesSystem.World.ShowFrame(checkFrame!, isChecked);
            Raise(toggled, who, isChecked, "Toggled");
        }
    }
}
