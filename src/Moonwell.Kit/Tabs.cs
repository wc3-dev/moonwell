using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// Pages with a row of tab buttons over them (decision 212): Page adds one and gives its panel, to add frames to. One
    /// page shows at a time, the first unless set; its button is turned off, which shows which is chosen. A click on a tab
    /// shows its page on every machine, as a frame has one set of values, so give each player a window of their own.
    /// Draws nothing of its own; in a window it covers the body.
    /// </summary>
    public class Tabs : Panel
    {
        /// <summary>The tab buttons' height, the gap between them, and the gap above the pages.</summary>
        internal const float TabHeight = 0.03f, Gap = 0.004f;

        /// <summary>A tab button's width unless TabWidth is set.</summary>
        internal const float DefaultTabWidth = 0.08f;

        internal readonly Row bar;

        internal readonly List<Button> buttons = new List<Button>();

        internal readonly List<Panel> pages = new List<Panel>();

        internal int selected;

        internal float tabWidth = DefaultTabWidth;

        internal List<Action<Player, int>>? changed;

        /// <summary>Makes tabs with no pages, to be added to a window or another frame.</summary>
        public Tabs()
        {
            Template = "";
            bar = Add(new Row { Template = "", Gap = Gap, Height = TabHeight });
            bar.Attach(FramePoint.TopLeft, this, FramePoint.TopLeft);
            bar.Attach(FramePoint.TopRight, this, FramePoint.TopRight);
        }

        /// <summary>The tab buttons' width; 0.08 unless set, for the tabs added from then on.</summary>
        [LocalSafe]
        public float TabWidth
        {
            get { return tabWidth; }
            set { tabWidth = value; }
        }

        /// <summary>How many pages there are.</summary>
        [LocalSafe]
        public int Count
        {
            get { return pages.Count; }
        }

        /// <summary>The page showing, from 0; the first unless set. Setting it shows that page and runs no Changed; one out of range is refused and reported.</summary>
        [LocalSafe]
        public int Selected
        {
            get { return selected; }
            set
            {
                if (value < 0 || value >= pages.Count)
                {
                    Errors.Warn(this, ".Selected", new MoonwellException("no page " + value + " of " + pages.Count));
                    return;
                }

                Show(value);
            }
        }

        /// <summary>Runs when a player clicks a tab, after its page shows, with the player and the page's number. Synced: a handler may change the game. Not inside Local.For.</summary>
        public event Action<Player, int>? Changed
        {
            add
            {
                if (!HasEnded && value != null)
                {
                    (changed ??= new List<Action<Player, int>>()).Add(value);
                }
            }

            remove { Unsubscribe(changed, value); }
        }

        /// <summary>Adds a page with its tab and gives its panel, which draws nothing and covers the tabs below their buttons.</summary>
        public Panel Page(string title)
        {
            var index = pages.Count;
            var button = bar.Add(new Button { Text = title, Width = tabWidth, Height = TabHeight });
            button.Clicked += who => Clicked(who, index);
            var page = Add(new Panel { Template = "" });
            page.Attach(FramePoint.TopLeft, this, FramePoint.TopLeft, 0.0f, -TabHeight - Gap);
            page.Attach(FramePoint.BottomRight, this, FramePoint.BottomRight);
            buttons.Add(button);
            pages.Add(page);
            page.Visible = index == selected;
            button.Enabled = index != selected;
            return page;
        }

        private void Clicked(Player who, int index)
        {
            if (index == selected)
            {
                return;
            }

            Show(index);
            Raise(changed, who, index, "Changed");
        }

        private void Show(int index)
        {
            selected = index;
            for (var i = 0; i < pages.Count; i++)
            {
                pages[i].Visible = i == index;
                buttons[i].Enabled = i != index;
            }
        }
    }
}
