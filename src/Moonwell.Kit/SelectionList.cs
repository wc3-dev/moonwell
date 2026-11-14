using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A list of small unit frames following a player's Selection, of the UI kit's HUD (decision 342): one entry per unit
    /// selected, up to 24 (the game's most), in the order selected, one column down unless Columns says otherwise. It
    /// changes when the selection does, with OnSelectionChanged (decision 341), and drops a unit that dies or goes. A click
    /// on an entry selects that unit alone, for the player who clicked. Entry makes a map's own UnitFrame class for each;
    /// Kit's small UnitFrame unless set.
    /// </summary>
    public class SelectionList : Grid
    {
        /// <summary>The most units the game selects at once.</summary>
        internal const int Most = 24;

        internal readonly List<UnitFrame> entries = new List<UnitFrame>();

        internal readonly List<Unit> shown = new List<Unit>();

        /// <summary>Each entry's click, an empty icon button over it.</summary>
        internal readonly List<IconButton> clicks = new List<IconButton>();

        internal Player? followed;

        internal bool watching;

        internal bool ticking;

        /// <summary>Makes a list that follows no one yet, to be added to the screen or to another frame.</summary>
        public SelectionList()
        {
            Gap = 0.004f;
        }

        /// <summary>Makes each entry: a map's own UnitFrame class, as () => new PartyFrame(); Kit's small UnitFrame (icon, name, life and mana) unless set. Set it before Follow: entries made already stay.</summary>
        public Func<UnitFrame>? Entry { get; set; }

        /// <summary>The player whose selection the list follows; null when none.</summary>
        [LocalSafe]
        public Player? Followed => followed;

        /// <summary>The entries showing a unit, in the order selected.</summary>
        [LocalSafe]
        public List<UnitFrame> Entries
        {
            get
            {
                var showing = new List<UnitFrame>();
                for (var i = 0; i < shown.Count; i++)
                {
                    showing.Add(entries[i]);
                }

                return showing;
            }
        }

        /// <summary>Follows a player's selection from now on; null follows no one and empties the list.</summary>
        public void Follow(Player? player)
        {
            if (HasEnded)
            {
                return;
            }

            followed = player;
            if (player != null && !watching)
            {
                watching = true;
                SelectionChanges.Watch(Changed);
            }

            Sync();
            if (player != null && !ticking)
            {
                ticking = true;
                KitTicks.Join(Tick);
            }
        }

        private bool Changed(Player player)
        {
            if (HasEnded)
            {
                watching = false;
                return false;
            }

            if (player == followed)
            {
                Sync();
            }

            return true;
        }

        // A unit the game drops with no event (it died, went, was hidden) leaves at once, not at the next change.
        private bool Tick()
        {
            if (HasEnded || followed == null)
            {
                ticking = false;
                return false;
            }

            for (var i = 0; i < shown.Count; i++)
            {
                if (!shown[i].IsAlive)
                {
                    Sync();
                    break;
                }
            }

            return true;
        }

        /// <summary>Shows the followed player's selection, reusing the entries and hiding those left over.</summary>
        internal void Sync()
        {
            var units = followed == null ? new List<Unit>() : followed.Selection;
            var count = units.Count < Most ? units.Count : Most;
            shown.Clear();
            for (var i = 0; i < count; i++)
            {
                shown.Add(units[i]);
                EntryAt(i).Show(units[i]);
            }

            for (var i = count; i < entries.Count; i++)
            {
                entries[i].Show(null);
            }
        }

        /// <summary>The entry at a place, made with a click that selects its unit, and the cells sized to the first.</summary>
        private UnitFrame EntryAt(int index)
        {
            if (index < entries.Count)
            {
                return entries[index];
            }

            UnitFrame entry;
            if (Entry != null)
            {
                entry = Entry();
            }
            else
            {
                entry = new UnitFrame();
                entry.Compact();
            }

            if (entries.Count == 0)
            {
                CellWidth = entry.Width;
                CellHeight = entry.Height;
            }

            entries.Add(Add(entry));
            var click = entry.Add(new IconButton());
            click.Fill(entry);
            clicks.Add(click);
            click.Clicked += who =>
            {
                if (entry.Unit != null)
                {
                    who.Select(entry.Unit);
                }
            };
            return entry;
        }
    }
}
