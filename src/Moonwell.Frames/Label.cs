using System;

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

        /// <summary>The text ShowLocal shows on this machine, until Text is set; null for the shared text.</summary>
        private string? local;

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
                local = null;
                Write();
            }
        }

        /// <summary>The text's colour, the same on every machine; white unless set. It is a colour code around the text, as a label turned off (so clicks pass) draws the game's text colour grey (seen 11-07), and codes in the text (B9's Text) draw over it. Its alpha is not drawn: text draws solid (seen), so fade a label with Alpha.</summary>
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
                Write();
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

        /// <summary>Moves the text's Color to a new one over the seconds, eased (linear unless given); it stops the label's last ColorTo.</summary>
        public Tween ColorTo(Color to, float seconds, Func<float, float>? ease = null)
        {
            var from = color;
            return Tweened(TweenColour, seconds, ease, t => Color = new Color(Mix(from.R, to.R, t), Mix(from.G, to.G, t), Mix(from.B, to.B, t), Mix(from.A, to.A, t)));
        }

        private static int Mix(int from, int to, float t)
        {
            return (int)(Maths.Lerp(from, to, t) + 0.5f);
        }

        /// <summary>Shows a text on this machine only, inside Local.For, as for a player's own language or a number only they see. Text keeps the shared text, and setting it shows that on every machine again. Does nothing until the label is made or once it has ended.</summary>
        [MachineLocal]
        public void ShowLocal(string localText)
        {
            if (handle != null && !HasEnded)
            {
                local = localText;
                Write();
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

        /// <summary>Clears an ended label's text, shared or local, with its colour, when this one has none; Place writes what this one has.</summary>
        internal override void TakeOver(Frame old)
        {
            base.TakeOver(old);
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
                Write();
            }

            if (!foundInTemplate || alignment != FramePoint.TopLeft)
            {
                Align();
            }

            base.Place();
        }

        /// <summary>Gives the game this machine's text in the label's colour: a code around it, again after each reset in it, as its own codes end with one.</summary>
        private void Write()
        {
            if (handle == null)
            {
                return;
            }

            var shown = local ?? text;
            if (!color.Equals(Color.White) && shown.Length > 0)
            {
                var code = "|cff" + color.ToHtml(false);
                shown = code + shown.Replace("|r", "|r" + code).Replace("|R", "|R" + code) + "|r";
            }

            FramesSystem.World.WriteFrameText(handle, shown);
        }

        /// <summary>Gives the game the alignment: the point's row is the vertical, its column the horizontal.</summary>
        private void Align()
        {
            var point = (int)alignment;
            FramesSystem.World.AlignFrameText(handle!, point / 3, 3 + point % 3);
        }
    }
}
