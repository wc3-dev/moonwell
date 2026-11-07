using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A grid of icon slots with drag and drop built in (decision 212): Columns by Rows slots, each an icon button that can be
    /// picked up while it shows an icon and takes a drop. Moved runs when a player drops one slot on another, of this grid
    /// or another, with the two slots: the map moves what they hold, as the grid keeps nothing but icons. Sized by its
    /// slots, 0.04 square unless SlotSize is set.
    /// </summary>
    public class SlotGrid : Grid
    {
        /// <summary>A slot's side unless SlotSize is set, and the gap between slots.</summary>
        internal const float DefaultSlotSize = 0.04f, SlotGap = 0.004f;

        internal readonly List<IconSlot> slots = new List<IconSlot>();

        internal int rows = 1;

        internal float slotSize = DefaultSlotSize;

        internal List<Action<Player, IconSlot, IconSlot>>? moved;

        /// <summary>Makes a grid of one slot, to be given Columns and Rows and added to a window or another frame.</summary>
        public SlotGrid()
        {
            CellWidth = slotSize;
            CellHeight = slotSize;
            Gap = SlotGap;
            Fit();
        }

        /// <summary>How many lines of slots there are; 1 unless set, and never less.</summary>
        [LocalSafe]
        public int Rows
        {
            get { return rows; }
            set
            {
                rows = value < 1 ? 1 : value;
                Fit();
            }
        }

        /// <summary>A slot's side; 0.04 unless set.</summary>
        [LocalSafe]
        public float SlotSize
        {
            get { return slotSize; }
            set
            {
                slotSize = value;
                CellWidth = value;
                CellHeight = value;
                for (var i = 0; i < slots.Count; i++)
                {
                    slots[i].Width = value;
                    slots[i].Height = value;
                }

                Fit();
            }
        }

        /// <summary>How many slots there are: Columns by Rows.</summary>
        [LocalSafe]
        public int Count
        {
            get
            {
                Fit();
                return slots.Count;
            }
        }

        /// <summary>A slot by its number, from 0, across each line then down.</summary>
        [LocalSafe]
        public IconSlot this[int index]
        {
            get
            {
                Fit();
                return slots[index];
            }
        }

        /// <summary>Runs when a player drops a slot on one of this grid's, with the player, the slot dropped and the slot it was dropped on; the slot dropped may be another grid's. Synced: a handler may change the game. Not inside Local.For.</summary>
        public event Action<Player, IconSlot, IconSlot>? Moved
        {
            add
            {
                if (!HasEnded && value != null)
                {
                    (moved ??= new List<Action<Player, IconSlot, IconSlot>>()).Add(value);
                }
            }

            remove { Unsubscribe(moved, value); }
        }

        /// <summary>Makes or ends slots to match Columns by Rows, and sizes the grid to them.</summary>
        internal void Fit()
        {
            if (HasEnded)
            {
                return;
            }

            var count = columns * rows;
            while (slots.Count < count)
            {
                var slot = Add(new IconSlot(this, slots.Count) { Width = slotSize, Height = slotSize });
                slots.Add(slot);
            }

            while (slots.Count > count)
            {
                slots[slots.Count - 1].End();
                slots.RemoveAt(slots.Count - 1);
            }

            var lines = (slots.Count + columns - 1) / columns;
            Width = 2.0f * padding + columns * slotSize + (columns - 1) * gap;
            Height = 2.0f * padding + lines * slotSize + (lines - 1) * gap;
        }

        internal void DroppedOn(Player who, IconSlot from, IconSlot to)
        {
            Kit.Raise(this, moved, who, from, to, "Moved");
        }
    }

    /// <summary>A slot of a SlotGrid: an icon button that can be picked up while it shows an icon, and takes a drop.</summary>
    public class IconSlot : IconButton
    {
        internal IconSlot(SlotGrid grid, int index)
        {
            Grid = grid;
            Index = index;
            Draggable = true;
            Dropped += (who, dragged) =>
            {
                if (dragged is IconSlot from)
                {
                    grid.DroppedOn(who, from, this);
                }
            };
        }

        /// <summary>The grid it is in.</summary>
        [LocalSafe]
        public SlotGrid Grid { get; }

        /// <summary>Its number in its grid, from 0.</summary>
        [LocalSafe]
        public int Index { get; }
    }
}
