namespace Moonwell
{
    /// <summary>
    /// A line or block of text, in the game's font; Scale makes it bigger. A label lets clicks through to what is under it
    /// unless Enabled is set. A font of its own is not offered: BlzFrameSetFont on a TEXT by type changes neither its height
    /// nor its letters (seen 11-03).
    /// </summary>
    public class Label : Frame
    {
        internal string text = "";

        internal FramePoint alignment = FramePoint.TopLeft;

        internal Color color = Color.White;

        /// <summary>Makes a label, to be added to the screen or to another frame.</summary>
        public Label()
        {
            enabled = false;
        }

        /// <summary>The label's text, the same on every machine; empty unless set.</summary>
        [LocalSafe]
        public string Text
        {
            get { return text; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                text = value;
                if (handle != null)
                {
                    FramesSystem.World.WriteFrameText(handle, value);
                }
            }
        }

        /// <summary>The text's colour, its alpha the text's opacity, the same on every machine; white unless set. Colour codes in the text (B9's Text) draw over it.</summary>
        [LocalSafe]
        public Color Color
        {
            get { return color; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                color = value;
                if (handle != null)
                {
                    FramesSystem.World.ColourFrameText(handle, value.R, value.G, value.B, value.A);
                }
            }
        }

        /// <summary>Where the text sits in the label: Left for the middle of the left edge, Center for the centre; TopLeft unless set.</summary>
        [LocalSafe]
        public FramePoint Alignment
        {
            get { return alignment; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                alignment = value;
                if (handle != null)
                {
                    Align();
                }
            }
        }

        /// <summary>A TEXT, which draws its text in the game's font (seen 10-14).</summary>
        internal override string GameType => "TEXT";

        /// <summary>Shows a text on this machine only, inside Local.For, as for a player's own language or a number only they see. Text keeps the shared text, and setting it shows that on every machine again. Does nothing until the label is made or once it has ended.</summary>
        [MachineLocal]
        public void ShowLocal(string localText)
        {
            if (handle != null && !HasEnded)
            {
                FramesSystem.World.WriteFrameText(handle, localText);
            }
        }

        /// <summary>A label listened to takes the mouse: it is turned on for it, and Enabled reads so.</summary>
        internal override void PrepareMouse()
        {
            if (!enabled)
            {
                enabled = true;
                if (handle != null)
                {
                    TakeMouse(true);
                }
            }
        }

        /// <summary>Clears an ended label's text, shared or local, when this one has none, and its colour; Place writes what this one has.</summary>
        internal override void TakeOver(Frame old)
        {
            base.TakeOver(old);
            if (!((Label)old).color.Equals(Color.White) && color.Equals(Color.White))
            {
                FramesSystem.World.ColourFrameText(handle!, 255, 255, 255, 255);
            }

            if (text.Length == 0)
            {
                FramesSystem.World.WriteFrameText(handle!, "");
            }
        }

        /// <summary>Gives the game the label's text and alignment, then its points and looks.</summary>
        internal override void Place()
        {
            if (text.Length > 0)
            {
                FramesSystem.World.WriteFrameText(handle!, text);
            }

            if (!color.Equals(Color.White))
            {
                FramesSystem.World.ColourFrameText(handle!, color.R, color.G, color.B, color.A);
            }

            if (!foundInTemplate || alignment != FramePoint.TopLeft)
            {
                Align();
            }

            base.Place();
        }

        /// <summary>Gives the game the alignment: the point's row is the vertical, its column the horizontal.</summary>
        private void Align()
        {
            var point = (int)alignment;
            FramesSystem.World.AlignFrameText(handle!, point / 3, 3 + point % 3);
        }
    }
}
