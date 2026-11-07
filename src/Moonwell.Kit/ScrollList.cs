using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A list of items shown a page of rows at a time (decision 212): the game cannot clip a frame, so the rows stay and the
    /// items move through them. Each row is a button; Show writes an item onto its row (its text unless set), and a click on
    /// a row runs Picked with its item. The wheel over the list, or the two buttons at its side, scroll it, on every machine,
    /// as a frame has one set of values. Draws nothing of its own; in a window it covers the body.
    /// </summary>
    public class ScrollList<T> : Panel
    {
        /// <summary>A row's height unless RowHeight is set, and the scroll buttons' width.</summary>
        internal const float DefaultRowHeight = 0.03f, ScrollWidth = 0.024f;

        internal readonly Button up;

        internal readonly Button down;

        internal readonly List<Button> rows = new List<Button>();

        internal List<T> items = new List<T>();

        internal int first;

        internal float rowHeight = DefaultRowHeight;

        internal Action<Button, T>? show;

        internal List<Action<Player, T>>? picked;

        /// <summary>Makes an empty list of 8 rows, to be added to a window or another frame.</summary>
        public ScrollList()
        {
            Template = "";
            up = Add(new Button { Text = "^", Width = ScrollWidth, Height = DefaultRowHeight, Anchor = FramePoint.TopRight });
            down = Add(new Button { Text = "v", Width = ScrollWidth, Height = DefaultRowHeight, Anchor = FramePoint.BottomRight });
            up.Clicked += who => First = first - 1;
            down.Clicked += who => First = first + 1;
            up.Wheel += Scrolled;
            down.Wheel += Scrolled;
            Rows = 8;
        }

        /// <summary>How many rows show at once; 8 unless set.</summary>
        [LocalSafe]
        public int Rows
        {
            get { return rows.Count; }
            set
            {
                while (rows.Count < value)
                {
                    var at = rows.Count;
                    var row = Add(new Button { Height = rowHeight });
                    PlaceRow(row, at);
                    row.Clicked += who => Clicked(who, at);
                    row.Wheel += Scrolled;
                    rows.Add(row);
                }

                while (rows.Count > value && rows.Count > 0)
                {
                    rows[rows.Count - 1].End();
                    rows.RemoveAt(rows.Count - 1);
                }

                Refresh();
            }
        }

        /// <summary>A row's height; 0.03 unless set.</summary>
        [LocalSafe]
        public float RowHeight
        {
            get { return rowHeight; }
            set
            {
                rowHeight = value;
                for (var i = 0; i < rows.Count; i++)
                {
                    rows[i].Height = value;
                    PlaceRow(rows[i], i);
                }
            }
        }

        /// <summary>The items, in order; none unless set. Setting it shows them from where the list was; after changing the list itself, call Refresh.</summary>
        [LocalSafe]
        public List<T> Items
        {
            get { return items; }
            set
            {
                items = value ?? new List<T>();
                Refresh();
            }
        }

        /// <summary>Writes an item onto its row; unless set, the row's Text is the item's ToString().</summary>
        [LocalSafe]
        public Action<Button, T>? Show
        {
            get { return show; }
            set
            {
                show = value;
                Refresh();
            }
        }

        /// <summary>The number of the item on the top row, from 0, kept within the items; 0 unless set.</summary>
        [LocalSafe]
        public int First
        {
            get { return first; }
            set
            {
                var last = Math.Max(0, items.Count - rows.Count);
                first = value < 0 ? 0 : value > last ? last : value;
                Refresh();
            }
        }

        /// <summary>Runs when a player clicks a row, with the player and its item. Synced: a handler may change the game. Not inside Local.For.</summary>
        public event Action<Player, T>? Picked
        {
            add
            {
                if (!HasEnded && value != null)
                {
                    (picked ??= new List<Action<Player, T>>()).Add(value);
                }
            }

            remove { Unsubscribe(picked, value); }
        }

        /// <summary>Writes the items onto the rows again, from First: a row past the last item hides.</summary>
        public void Refresh()
        {
            var last = Math.Max(0, items.Count - rows.Count);
            if (first > last)
            {
                first = last;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var at = first + i;
                row.Visible = at < items.Count;
                if (at >= items.Count)
                {
                    continue;
                }

                if (show != null)
                {
                    show(row, items[at]);
                }
                else
                {
                    var item = items[at];
                    row.Text = item == null ? "" : item.ToString() ?? "";
                }
            }

            up.Enabled = first > 0;
            down.Enabled = first < last;
        }

        /// <summary>Spans a row across the list, left of the scroll buttons, below the rows before it.</summary>
        private void PlaceRow(Button row, int at)
        {
            row.Attach(FramePoint.TopLeft, this, FramePoint.TopLeft, 0.0f, -at * rowHeight);
            row.Attach(FramePoint.TopRight, this, FramePoint.TopRight, -ScrollWidth - 0.004f, -at * rowHeight);
        }

        private void Clicked(Player who, int row)
        {
            var at = first + row;
            if (at < items.Count)
            {
                Raise(picked, who, items[at], "Picked");
            }
        }

        private void Scrolled(Player who, int notches)
        {
            First = first - notches;
        }
    }
}
