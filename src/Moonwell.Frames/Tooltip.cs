namespace Moonwell
{
    /// <summary>
    /// A tooltip: a bordered panel with its text, 0.2 by 0.06 unless sized, for a frame's Tooltip. The game shows it while a
    /// player's mouse is on that frame, on that machine alone, with no event sent (BlzFrameSetTooltip).
    /// </summary>
    public class Tooltip : Panel
    {
        /// <summary>How far the text sits in from the border, which covers 0.008 and leaves 0.024 clear (seen 10-14).</summary>
        internal const float Inset = 0.024f;

        internal readonly Label words;

        /// <summary>Makes a tooltip, to be given to a frame's Tooltip.</summary>
        public Tooltip()
        {
            width = 0.2f;
            height = 0.06f;
            sized = true;
            words = Add(new Label());
            words.Attach(FramePoint.TopLeft, this, FramePoint.TopLeft, Inset, -Inset);
            words.Attach(FramePoint.BottomRight, this, FramePoint.BottomRight, -Inset, Inset);
        }

        /// <summary>The tooltip's text, wrapped within it, the same on every machine; empty unless set.</summary>
        [LocalSafe]
        public string Text
        {
            get { return words.Text; }
            set { words.Text = value; }
        }
    }
}
