using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A ready shop window of the UI kit (decision 212): goods as icon buttons in a grid, each with a rich tooltip of its
    /// name, text and cost; a click on one runs Bought with the player and the good. The shop sells nothing itself: the map
    /// checks and takes the gold and gives the item in Bought. Four goods a line unless Columns is set.
    /// </summary>
    public class ShopWindow : Window
    {
        /// <summary>A good's side and the gap between goods.</summary>
        internal const float GoodSize = 0.04f, GoodGap = 0.004f;

        internal readonly Grid goods;

        internal readonly List<ShopEntry> entries = new List<ShopEntry>();

        internal readonly List<IconButton> buttons = new List<IconButton>();

        internal List<Action<Player, ShopEntry>>? bought;

        /// <summary>Makes an empty shop titled Shop, to be given goods with Sell and added to the screen, best for one buyer: Screen.Add(shop, this, shownTo: buyer).</summary>
        public ShopWindow()
        {
            Title = "Shop";
            goods = Add(new Grid { Columns = 4, CellWidth = GoodSize, CellHeight = GoodSize, Gap = GoodGap, Anchor = FramePoint.Top, Offset = new Vector2(0.0f, -BarTop - BarHeight - 0.012f) });
            Fit();
        }

        /// <summary>How many goods a line has; 4 unless set.</summary>
        [LocalSafe]
        public int Columns
        {
            get { return goods.Columns; }
            set
            {
                goods.Columns = value;
                Fit();
            }
        }

        /// <summary>The goods, in the order sold.</summary>
        [LocalSafe]
        public IReadOnlyList<ShopEntry> Entries
        {
            get { return entries; }
        }

        /// <summary>Runs when a player clicks a good, with the player and the good. Synced: a handler may change the game, as taking the gold. Not inside Local.For.</summary>
        public event Action<Player, ShopEntry>? Bought
        {
            add
            {
                if (!HasEnded && value != null)
                {
                    (bought ??= new List<Action<Player, ShopEntry>>()).Add(value);
                }
            }

            remove { Unsubscribe(bought, value); }
        }

        /// <summary>Puts a good in the shop, after the others, and gives it back.</summary>
        public ShopEntry Sell(ShopEntry entry)
        {
            var button = goods.Add(new IconButton { Icon = entry.Icon, Width = GoodSize, Height = GoodSize });
            button.Tooltip = new RichTooltip { Icon = entry.Icon, Title = entry.Name, Text = entry.Text, Cost = entry.Cost > 0 ? entry.Cost + " gold" : "" };
            button.Clicked += who => Raise(bought, who, entry, "Bought");
            entries.Add(entry);
            buttons.Add(button);
            Fit();
            return entry;
        }

        /// <summary>Takes a good out of the shop; the others close up. One not in it is let be.</summary>
        public void Remove(ShopEntry entry)
        {
            var index = entries.IndexOf(entry);
            if (index < 0)
            {
                return;
            }

            buttons[index].End();
            buttons.RemoveAt(index);
            entries.RemoveAt(index);
            Fit();
        }

        /// <summary>Sizes the grid to its goods, so it sits centred under the title.</summary>
        private void Fit()
        {
            var columns = goods.Columns;
            var lines = Math.Max(1, (entries.Count + columns - 1) / columns);
            goods.Width = columns * GoodSize + (columns - 1) * GoodGap;
            goods.Height = lines * GoodSize + (lines - 1) * GoodGap;
        }
    }

    /// <summary>A good of a ShopWindow: what its button and tooltip show. Inherit it to carry what the map sells, as an item type.</summary>
    public class ShopEntry
    {
        /// <summary>The icon's texture.</summary>
        [LocalSafe]
        public string Icon { get; set; } = "";

        /// <summary>The name, the tooltip's title.</summary>
        [LocalSafe]
        public string Name { get; set; } = "";

        /// <summary>The tooltip's text.</summary>
        [LocalSafe]
        public string Text { get; set; } = "";

        /// <summary>The cost in gold the tooltip shows; 0 shows none. The shop takes nothing: Bought does.</summary>
        [LocalSafe]
        public int Cost { get; set; }
    }
}
