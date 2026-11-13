namespace Moonwell
{
    /// <summary>
    /// A bar that fills from the left: its Texture drawn over the share of its width that Value is of the range from Min to
    /// Max. The bar itself draws nothing. The fill is as high as the bar, and its width is the map's: the bar's Width, or that
    /// of the frame it covers with Fill, as the game reads a size from points late (seen 11-05). It is two images, not the
    /// game's status bar, which follows no parent (seen 10-14). A bar's look from a .looks.csv (its path ends in .Bar.blp) is
    /// cut, not squashed: the fill shows the build's copy of the look's left part nearest its share, in 64ths (decision 322).
    /// </summary>
    public class ProgressBar : Frame
    {
        internal float min;

        internal float max = 1.0f;

        internal float value;

        internal string texture = "";

        /// <summary>The 64ths of a bar's look the fill shows; -1 until painted.</summary>
        internal int step = -1;

        /// <summary>The image that draws the filled share; null until the bar is made.</summary>
        internal framehandle? fill;

        /// <summary>Makes an empty bar from 0 to 1, to be added to the screen or to another frame, then sized.</summary>
        public ProgressBar()
        {
        }

        /// <summary>The value at the empty end; 0 unless set.</summary>
        [LocalSafe]
        public float Min
        {
            get { return min; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                min = value;
                Keep();
            }
        }

        /// <summary>The value at the full end; 1 unless set.</summary>
        [LocalSafe]
        public float Max
        {
            get { return max; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                max = value;
                Keep();
            }
        }

        /// <summary>How full it is, kept within Min and Max; 0 unless set.</summary>
        [LocalSafe]
        public float Value
        {
            get { return value; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                this.value = value;
                Keep();
            }
        }

        /// <summary>The texture of the filled share, as ReplaceableTextures\TeamColor\TeamColor00.blp, squashed over it; a bar's look (Looks.UIStatusBar.Green) is cut instead. Empty unless set, which draws nothing.</summary>
        [LocalSafe]
        public string Texture
        {
            get { return texture; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                texture = value;
                if (fill != null)
                {
                    Paint(true);
                }
            }
        }

        /// <summary>A BACKDROP painted blank, which draws nothing and lets the mouse through.</summary>
        internal override string GameType => "BACKDROP";

        internal override bool DrawnFromParts()
        {
            return true;
        }

        /// <summary>Paints the bar blank and makes the fill, tied by its left corners to the bar's, so it is as high as the bar however the bar is sized (review G3).</summary>
        internal override void MakeParts()
        {
            var world = FramesSystem.World;
            world.PaintFrame(handle!, Image.Blank);
            fill = world.MakeFrame("BACKDROP", "", handle!)!;
            world.PlaceFrame(fill, (int)FramePoint.TopLeft, handle!, (int)FramePoint.TopLeft, 0.0f, 0.0f);
            world.PlaceFrame(fill, (int)FramePoint.BottomLeft, handle!, (int)FramePoint.BottomLeft, 0.0f, 0.0f);
        }

        /// <summary>Takes an ended bar's fill with its frame.</summary>
        internal override void TakeOver(Frame old)
        {
            fill = ((ProgressBar)old).fill;
            base.TakeOver(old);
        }

        /// <summary>Gives the game the fill's texture and size, then the points and looks.</summary>
        internal override void Place()
        {
            Paint(true);
            Refill();
            base.Place();
        }

        /// <summary>The fill follows a new size, the bar's own or that of the frame it covers.</summary>
        internal override void Resized()
        {
            Refill();
            base.Resized();
        }

        /// <summary>Keeps the value within the range and sizes the fill to it.</summary>
        private void Keep()
        {
            var low = min < max ? min : max;
            var high = min < max ? max : min;
            value = value < low ? low : value > high ? high : value;
            Refill();
        }

        private void Refill()
        {
            if (fill == null)
            {
                return;
            }

            // The game reads a frame's size only some time after it is placed (seen 11-05), so the size is the map's: the bar's own, or that of the frame it covers, or of the one that one covers.
            Frame size = this;
            while (!size.sized && size.filled != null)
            {
                size = size.filled;
            }

            FramesSystem.World.SizeFrame(fill, size.width * Share(), size.height);
            Paint(false);
        }

        private float Share()
        {
            return max == min ? 0.0f : (value - min) / (max - min);
        }

        // Paints the fill: a bar's look its step copy nearest the share (the build's LookFiles.StepPath), again only when the step
        // changes unless always; any other texture whole, only when always.
        private void Paint(bool always)
        {
            const string barEnd = ".Bar.blp";
            const int steps = 64;
            var shown = texture.Length > 0 ? texture : Image.Blank;
            if (texture.Length > barEnd.Length && texture.Substring(texture.Length - barEnd.Length) == barEnd)
            {
                var at = (int)((Share() * steps) + 0.5f);
                if (!always && at == step)
                {
                    return;
                }

                step = at;
                shown = at == 0 ? Image.Blank : at >= steps ? texture : texture.Substring(0, texture.Length - ".blp".Length) + at + ".blp";
            }
            else if (!always)
            {
                return;
            }

            FramesSystem.World.PaintFrame(fill!, shown);
        }
    }
}
