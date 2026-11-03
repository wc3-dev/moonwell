using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A panel that puts the frames added to it top to bottom, in the order added: the first at its top less Padding, each
    /// next one's top at the one before's bottom less Gap, each in the middle of the column's width. It draws nothing unless
    /// a Template is set, and its size is the map's. When one of its frames ends, the next moves up to the gap.
    /// </summary>
    public class Column : Panel
    {
        internal readonly List<Frame> items = new List<Frame>();

        internal float gap;

        internal float padding;

        /// <summary>Makes a column, to be added to the screen or to another frame.</summary>
        public Column()
        {
            Template = "";
        }

        /// <summary>The space between two frames, in the game's units; 0 unless set.</summary>
        [LocalSafe]
        public float Gap
        {
            get { return gap; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                gap = value;
                LayFrom(0);
            }
        }

        /// <summary>The space above the first frame, in the game's units; 0 unless set.</summary>
        [LocalSafe]
        public float Padding
        {
            get { return padding; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                padding = value;
                LayFrom(0);
            }
        }

        internal override void Adopted(Frame child)
        {
            items.Add(child);
            Lay(items.Count - 1);
        }

        internal override void ChildEnded(Frame child)
        {
            base.ChildEnded(child);
            var index = items.IndexOf(child);
            if (index >= 0)
            {
                items.RemoveAt(index);
                LayFrom(index);
            }
        }

        private void LayFrom(int index)
        {
            for (var i = index; i < items.Count; i++)
            {
                Lay(i);
            }
        }

        private void Lay(int index)
        {
            if (index == 0)
            {
                items[0].PlaceBy(FramePoint.Top, this, FramePoint.Top, 0.0f, -padding);
            }
            else
            {
                items[index].PlaceBy(FramePoint.Top, items[index - 1], FramePoint.Bottom, 0.0f, -gap);
            }
        }
    }
}
