namespace Moonwell
{
    /// <summary>
    /// The game's text area (EscMenuTextAreaTemplate), with a scroll bar: AddLine adds a line, and the game shows the last line
    /// added at the bottom (seen 11-03). Text reads every line, joined by line breaks, the same on every machine.
    /// </summary>
    public class TextArea : Frame
    {
        internal string text = "";

        /// <summary>Makes an empty text area, 0.25 by 0.12, to be added to the screen or to another frame.</summary>
        public TextArea()
        {
            Template = "EscMenuTextAreaTemplate";
            width = 0.25f;
            height = 0.12f;
            sized = true;
        }

        /// <summary>The whole text, its lines joined by line breaks; setting it replaces them all. Empty unless set.</summary>
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

        /// <summary>A TEXTAREA, for a text area whose template the game lacks.</summary>
        internal override string GameType => "TEXTAREA";

        /// <summary>Adds a line at the end, which the game scrolls to. Does nothing once it has ended.</summary>
        public void AddLine(string line)
        {
            if (HasEnded)
            {
                return;
            }

            text = text.Length == 0 ? line : text + "\n" + line;
            if (handle != null)
            {
                FramesSystem.World.AddFrameText(handle, line);
            }
        }

        /// <summary>Clears an ended text area's text when this one has none; Place writes one it has.</summary>
        internal override void TakeOver(Frame old)
        {
            base.TakeOver(old);
            if (text.Length == 0)
            {
                FramesSystem.World.WriteFrameText(handle!, "");
            }
        }

        /// <summary>Gives the game the text, then the points and looks.</summary>
        internal override void Place()
        {
            if (text.Length > 0)
            {
                FramesSystem.World.WriteFrameText(handle!, text);
            }

            base.Place();
        }
    }
}
