using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A panel that puts the frames added to it in cells, in the order added, left to right and then the next line: each
    /// frame's top left at its cell's, the cells CellWidth by CellHeight with Gap between them and Padding around them. It
    /// draws nothing unless a Template is set, and neither its size nor its frames' is set by it. When one of its frames
    /// ends, those after it move up a cell.
    /// </summary>
    public class Grid : Panel
    {
        internal readonly List<Frame> items = new List<Frame>();

        internal int columns = 1;

        internal float cellWidth;

        internal float cellHeight;

        internal float gap;

        internal float padding;

        /// <summary>Makes a grid, to be added to the screen or to another frame.</summary>
        public Grid()
        {
            Template = "";
        }

        /// <summary>How many cells a line has; 1 unless set, and never less.</summary>
        [LocalSafe]
        public int Columns
        {
            get { return columns; }
            set { Set(ref columns, value < 1 ? 1 : value); }
        }

        /// <summary>A cell's width, in the game's units; 0 unless set.</summary>
        [LocalSafe]
        public float CellWidth
        {
            get { return cellWidth; }
            set { Set(ref cellWidth, value); }
        }

        /// <summary>A cell's height, in the game's units; 0 unless set.</summary>
        [LocalSafe]
        public float CellHeight
        {
            get { return cellHeight; }
            set { Set(ref cellHeight, value); }
        }

        /// <summary>The space between two cells, in the game's units; 0 unless set.</summary>
        [LocalSafe]
        public float Gap
        {
            get { return gap; }
            set { Set(ref gap, value); }
        }

        /// <summary>The space around the cells, in the game's units; 0 unless set.</summary>
        [LocalSafe]
        public float Padding
        {
            get { return padding; }
            set { Set(ref padding, value); }
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

        private void Set<T>(ref T field, T value)
        {
            if (HasEnded)
            {
                return;
            }

            field = value;
            LayFrom(0);
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
            var column = index % columns;
            var line = index / columns;
            items[index].PlaceBy(FramePoint.TopLeft, this, FramePoint.TopLeft, padding + column * (cellWidth + gap), -(padding + line * (cellHeight + gap)));
        }
    }
}
