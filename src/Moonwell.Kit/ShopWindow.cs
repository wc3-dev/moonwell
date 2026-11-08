using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A ready shop window of the UI kit (decision 212): goods as icon buttons in a grid, each with a rich tooltip of its
    /// name, text and cost. A good put in with Sell runs Bought with the player and the good, and the map checks and takes the
    /// gold and gives the item; one put in with Sell&lt;T&gt; the window sells itself to the Buyer, with its stock (decision 214).
    /// Four goods a line unless Columns is set.
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

        /// <summary>The unit that receives what goods put in with Sell&lt;T&gt; sell, set by the map (the hero of the player it is shown to, say); with none, or a dead one, nothing is sold.</summary>
        [LocalSafe]
        public Unit? Buyer { get; set; }

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

        /// <summary>Puts a good in the shop, after the others, and gives it back; a click runs Bought.</summary>
        public ShopEntry Sell(ShopEntry entry)
        {
            Put(entry).Clicked += who => Raise(bought, who, entry, "Bought");
            return entry;
        }

        /// <summary>
        /// Puts a good in the shop that the window sells itself, after the others, and gives it back. A click checks that there is
        /// a Buyer, that the good is in stock and that the player has its Cost in gold and its Lumber, takes them and gives the
        /// Buyer an item of the class, in the game's scope; a refusal shows the player the game's error line. With a Stock, a
        /// good sold out is greyed and comes back one at a time, each Restock seconds (never with 0). The stock is the window's,
        /// shared by everyone it is shown to. Bought does not run: the item's and the Buyer's pick-up reactions see the sale.
        /// The game gives no item's cost at runtime, so the entry carries it (Item.Default&lt;T&gt;() reads the class's).
        /// </summary>
        public ShopEntry Sell<T>(ShopEntry entry) where T : Item
        {
            entry.Left = entry.Stock;
            var button = Put(entry);
            button.Clicked += who => Buy<T>(who, entry, button);
            return entry;
        }

        /// <summary>Makes a good's button with its tooltip, after the others.</summary>
        private IconButton Put(ShopEntry entry)
        {
            var cost = entry.Cost > 0 ? entry.Cost + " gold" : "";
            if (entry.Lumber > 0)
            {
                cost += (cost.Length > 0 ? ", " : "") + entry.Lumber + " lumber";
            }

            var button = goods.Add(new IconButton { Icon = entry.Icon, Width = GoodSize, Height = GoodSize });
            button.Tooltip = new RichTooltip { Icon = entry.Icon, Title = entry.Name, Text = entry.Text, Cost = cost };
            entries.Add(entry);
            buttons.Add(button);
            Fit();
            return button;
        }

        /// <summary>A click on a good the window sells: checked, paid and given, then the stock counted down and its restock started.</summary>
        private void Buy<T>(Player who, ShopEntry entry, IconButton button) where T : Item
        {
            var buyer = Buyer;
            if (buyer == null || buyer.HasEnded || !buyer.IsAlive)
            {
                who.Error("No hero to buy for.");
                return;
            }

            if (entry.Stock > 0 && entry.Left == 0)
            {
                who.Error("Sold out.");
                return;
            }

            if (who.Gold < entry.Cost)
            {
                who.Error("Not enough gold.");
                return;
            }

            if (who.Lumber < entry.Lumber)
            {
                who.Error("Not enough lumber.");
                return;
            }

            who.Gold -= entry.Cost;
            who.Lumber -= entry.Lumber;
            Item.Create<T>(buyer, Game.Instance);
            if (entry.Stock > 0)
            {
                entry.Left--;
                button.Enabled = entry.Left > 0;
                Restock(entry, button);
            }
        }

        /// <summary>Starts the good's restock, unless it is full, never restocks, or one is under way: one back each Restock seconds, on the window's scope.</summary>
        private void Restock(ShopEntry entry, IconButton button)
        {
            if (entry.restocking || entry.Restock <= 0.0f || entry.Left >= entry.Stock)
            {
                return;
            }

            entry.restocking = true;
            After(entry.Restock, () =>
            {
                entry.restocking = false;
                entry.Left++;
                button.Enabled = true;
                Restock(entry, button);
            });
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

        /// <summary>The cost in gold the tooltip shows; 0 shows none. A good put in with Sell takes nothing (Bought does); one put in with Sell&lt;T&gt; takes it.</summary>
        [LocalSafe]
        public int Cost { get; set; }

        /// <summary>The cost in lumber, shown and taken as Cost is.</summary>
        [LocalSafe]
        public int Lumber { get; set; }

        /// <summary>For a good put in with Sell&lt;T&gt;: how many the window holds at most and starts with; 0, no limit.</summary>
        [LocalSafe]
        public int Stock { get; set; }

        /// <summary>For a good put in with Sell&lt;T&gt; with a Stock: the seconds until one more comes back after a sale; 0, never.</summary>
        [LocalSafe]
        public float Restock { get; set; }

        /// <summary>For a good put in with Sell&lt;T&gt; with a Stock: how many are left.</summary>
        [LocalSafe]
        public int Left { get; internal set; }

        /// <summary>Whether a restock is under way.</summary>
        internal bool restocking;
    }
}
