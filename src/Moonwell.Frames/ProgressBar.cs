namespace Moonwell
{
    /// <summary>
    /// A bar that fills from the left: its Texture drawn over the share of its width that Value is of the range from Min to
    /// Max. The bar itself draws nothing, and its size is the map's. It is two images, not the game's status bar, which follows
    /// no parent (seen 10-14).
    /// </summary>
    public class ProgressBar : Frame
    {
        internal float min;

        internal float max = 1.0f;

        internal float value;

        internal string texture = "";

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

        /// <summary>The texture of the filled share, as ReplaceableTextures\TeamColor\TeamColor00.blp; empty unless set, which draws nothing.</summary>
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
                    FramesSystem.World.PaintFrame(fill, value.Length > 0 ? value : Image.Blank);
                }
            }
        }

        /// <summary>A BACKDROP painted blank, which draws nothing and lets the mouse through.</summary>
        internal override string GameType => "BACKDROP";

        /// <summary>Paints the bar blank and makes the fill, tied by its top left to the bar's.</summary>
        internal override void MakeParts()
        {
            var world = FramesSystem.World;
            world.PaintFrame(handle!, Image.Blank);
            fill = world.MakeFrame("BACKDROP", "", handle!)!;
            world.PlaceFrame(fill, (int)FramePoint.TopLeft, handle!, (int)FramePoint.TopLeft, 0.0f, 0.0f);
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
            FramesSystem.World.PaintFrame(fill!, texture.Length > 0 ? texture : Image.Blank);
            Refill();
            base.Place();
        }

        /// <summary>The fill follows a new size.</summary>
        internal override void Resized()
        {
            Refill();
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

            var share = max == min ? 0.0f : (value - min) / (max - min);
            FramesSystem.World.SizeFrame(fill, width * share, height);
        }
    }
}
