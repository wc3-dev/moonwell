using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check the UI kit's parts (decision 212): windows, themes, tabs, lists, slots and rich tooltips.</summary>
    public static class KitCases
    {
        private const int Click = 1, Wheel = 6;

        private static void ClickOn(FakeFramesWorld frames, Frame frame, int slot)
        {
            frames.Fire(frames.FrameOf(frame.MouseHandle!), Click, slot);
        }

        /// <summary>A window is made from the theme's template, 0.3 square, with its title and a close button.</summary>
        public static void WindowMade(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var window = Screen.Add(new Window { Title = "Bag" }, Game.Instance);
            var fake = frames.FrameOf(window.Handle!);
            Expect.Equal("EscMenuBackdrop", fake.Template, "each player's race: the game's Esc menu window");
            Expect.Near(0.3f, fake.Width, "its width");
            Expect.Equal("|cfffcd312Bag|r", frames.FrameOf(window.title.Handle!).Text, "its title, in the theme's gold");
            Expect.Equal(Window.CloseIcon, frames.FrameOf(window.close.iconFrame!).Texture, "the close button's icon");
        }

        /// <summary>Kit.Theme gives the windows made from then on their template and colours; a window's own Theme overrides it.</summary>
        public static void Themes(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            Kit.Theme = Theme.Orc;
            try
            {
                var orc = Screen.Add(new Window(), Game.Instance);
                var human = Screen.Add(new Window { Theme = Theme.Human }, Game.Instance);
                var tip = new RichTooltip();
                Expect.Equal("MoonwellKitWindowOrc", frames.FrameOf(orc.Handle!).Template, "Kit.Theme's template");
                Expect.Equal("MoonwellKitWindowHuman", frames.FrameOf(human.Handle!).Template, "the window's own theme");
                Expect.Equal("MoonwellKitWindowOrc", tip.Template, "a rich tooltip in Kit.Theme");
                Expect.Equal(211, orc.title.Color.G, "the title in the theme's gold");
            }
            finally
            {
                Kit.Theme = Theme.PlayersRace;
            }
        }

        /// <summary>The close button hides the window and runs Closed with the player; Visible shows it again.</summary>
        public static void Close(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var window = Screen.Add(new Window(), Game.Instance);
            var closedBy = -1;
            window.Closed += who => closedBy = who.Slot;
            ClickOn(frames, window.close, 2);
            Expect.Equal(false, window.Visible, "hidden");
            Expect.Equal(2, closedBy, "Closed with the player");
            window.Visible = true;
            Expect.Equal(true, frames.FrameOf(window.Handle!).Shown, "shown again");
        }

        /// <summary>A frame added with no place or size covers the body below the title; one with a size keeps its own place.</summary>
        public static void Body(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var window = Screen.Add(new Window(), Game.Instance);
            var tabs = window.Add(new Tabs());
            var grid = window.Add(new SlotGrid());
            var fake = frames.FrameOf(tabs.Handle!);
            Expect.Equal(2, fake.Points.Count, "tied by two corners");
            Expect.Near(Window.Inset, fake.Points[0].X, "inside the border");
            Expect.True(fake.Points[0].Y < -Window.BarHeight, "below the title");
            Expect.Equal(1, frames.FrameOf(grid.Handle!).Points.Count, "the grid at its own anchor");
        }

        /// <summary>A click on the title picks the window up for that player: its frame follows their cursor on their machine; a second click puts it down there, on every machine, by its Offset. Another player's click meanwhile does nothing, and a window not movable stays.</summary>
        public static void Move(FakeWorld world)
        {
            world.LocalSlot = 0;
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var frames = FrameCases.Installed();
            frames.WindowWidth = 800;
            frames.WindowHeight = 600;
            frames.MouseX = 600;
            frames.MouseY = 150;
            var window = Screen.Add(new Window(), Game.Instance);
            ClickOn(frames, window.bar, 0);
            ClickOn(frames, window.bar, 1);
            world.Advance(0.1f);
            var fake = frames.FrameOf(window.Handle!);
            var drop = 0.15f - Window.BarTop - Window.BarHeight / 2.0f;
            Expect.Near(0.6f - 0.15f, fake.Left, "follows the cursor here");
            Expect.Near(0.45f - drop + 0.15f, fake.Top, "held by its title");
            Expect.Near(0.0f, window.Offset.X, "nothing shared moved yet");
            ClickOn(frames, window.bar, 0);
            world.Advance(0.1f);
            Expect.Near(0.2f, window.Offset.X, "put down: its offset from the screen's centre");
            Expect.Near(0.45f - drop - 0.3f, window.Offset.Y, "and its height");
            frames.MouseX = 100;
            world.Advance(0.1f);
            Expect.Near(0.2f, window.Offset.X, "no longer follows");

            var still = Screen.Add(new Window { Movable = false }, Game.Instance);
            ClickOn(frames, still.bar, 0);
            world.Advance(0.1f);
            Expect.Equal(1, frames.FrameOf(still.Handle!).Points.Count, "not movable: stays at its anchor");
        }

        /// <summary>Tabs show the first page, its button turned off; a click on another shows it and runs Changed; Selected out of range is refused and reported.</summary>
        public static void TabsPages(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var window = Screen.Add(new Window(), Game.Instance);
            var tabs = window.Add(new Tabs());
            var gear = tabs.Page("Gear");
            var stats = tabs.Page("Stats");
            var heard = new List<string>();
            tabs.Changed += (who, page) => heard.Add(who.Slot + ":" + page);
            Expect.Equal(true, gear.Visible, "the first page shows");
            Expect.Equal(false, stats.Visible, "the second does not");
            Expect.Equal(false, tabs.buttons[0].Enabled, "the chosen tab's button off");
            Expect.Equal("Stats", frames.FrameOf(tabs.buttons[1].Handle!).Text, "the tab's title");
            ClickOn(frames, tabs.buttons[1], 0);
            Expect.Equal(true, stats.Visible, "the clicked page shows");
            Expect.Equal(false, gear.Visible, "the other hides");
            Expect.Equal("0:1", string.Join(",", heard), "Changed with the player and the page");
            var printed = world.Printed.Count;
            tabs.Selected = 5;
            Expect.Equal(1, tabs.Selected, "out of range refused");
            Expect.True(world.Printed.Count > printed, "and reported");
            tabs.Selected = 0;
            Expect.Equal("0:1", string.Join(",", heard), "setting it runs no Changed");
        }

        /// <summary>A list shows a page of its items, scrolls with the wheel and its buttons within them, and a row's click runs Picked with its item.</summary>
        public static void List(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var list = Screen.Add(new ScrollList<string> { Rows = 3, Width = 0.2f, Height = 0.1f }, Game.Instance);
            list.Items = new List<string> { "a", "b", "c", "d", "e" };
            string Row(int i) => frames.FrameOf(list.rows[i].Handle!).Text;
            Expect.Equal("a,b,c", Row(0) + "," + Row(1) + "," + Row(2), "the first page");
            Expect.Equal(false, list.up.Enabled, "nothing above");
            frames.Fire(frames.FrameOf(list.rows[0].Handle!), Wheel, 0, -120.0f);
            Expect.Equal(1, list.First, "the wheel down scrolls down");
            ClickOn(frames, list.down, 0);
            ClickOn(frames, list.down, 0);
            Expect.Equal(2, list.First, "kept within the items");
            Expect.Equal(false, list.down.Enabled, "nothing below");
            var picked = "";
            list.Picked += (who, item) => picked = item;
            ClickOn(frames, list.rows[1], 0);
            Expect.Equal("d", picked, "the row's item");
            list.Show = (row, item) => row.Text = item.ToUpper();
            Expect.Equal("C", Row(0), "Show writes the rows");
            list.Items = new List<string> { "x" };
            Expect.Equal(0, list.First, "back to the top when fewer");
            Expect.Equal(false, frames.FrameOf(list.rows[1].Handle!).Shown, "a row past the items hides");
        }

        /// <summary>A slot grid has Columns by Rows slots and its size; dropping a slot on another runs Moved with both, on the target's grid, from this grid or another; an empty slot is not picked up.</summary>
        public static void Slots(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var bag = Screen.Add(new SlotGrid { Columns = 4, Rows = 3 }, Game.Instance);
            var gear = Screen.Add(new SlotGrid { Columns = 2 }, Game.Instance);
            Expect.Equal(12, bag.Count, "Columns by Rows");
            Expect.Near(4 * 0.04f + 3 * SlotGrid.SlotGap, bag.Width, "sized by its slots");
            var look = frames.Frames.Find(f => f.Parent == frames.FrameOf(bag[0].Handle!) && f.Type == "BACKDROP")!;
            Expect.Equal(IconSlot.Empty, look.Texture, "an empty slot draws as the game's empty slots do");
            bag[0].Icon = "sword.blp";
            Expect.Equal("sword.blp", look.Texture, "its icon");
            gear[1].Icon = "shield.blp";
            var heard = new List<string>();
            bag.Moved += (who, from, to) => heard.Add("bag " + (from.Grid == bag ? "bag" : "gear") + from.Index + ">" + to.Index);
            gear.Moved += (who, from, to) => heard.Add("gear " + (from.Grid == bag ? "bag" : "gear") + from.Index + ">" + to.Index);
            ClickOn(frames, bag[0], 0);
            ClickOn(frames, bag[5], 0);
            ClickOn(frames, gear[1], 0);
            ClickOn(frames, bag[2], 0);
            ClickOn(frames, bag[7], 0);
            Expect.Equal("bag bag0>5, bag gear1>2", string.Join(", ", heard), "the moves, on the target's grid; the empty slot not picked up");
            Expect.True(FramesSystem.Drags.HeldBy(Player.Get(0)) == null, "nothing held");
            bag[0].Icon = "";
            Expect.Equal(IconSlot.Empty, look.Texture, "emptied, it draws as an empty slot again");
        }

        /// <summary>SwapWith swaps two slots' icons and tooltips, as a drop's Moved does (Sven, 11-07, I42): a tooltip moves to the other slot, shown there and placed above it, and does not end.</summary>
        public static void SwapWith(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var bag = Screen.Add(new SlotGrid { Columns = 4, Rows = 2 }, Game.Instance);
            var sword = new RichTooltip { Title = "Sword" };
            bag[0].Icon = "sword.blp";
            bag[0].Tooltip = sword;
            bag[0].SwapWith(bag[5]);
            Expect.Equal("|sword.blp", bag[0].Icon + "|" + bag[5].Icon, "the icons swapped");
            Expect.Same(sword, bag[5].Tooltip, "the tooltip went with the sword");
            Expect.Same(null, bag[0].Tooltip, "the slot it left has none");
            Expect.Equal(false, sword.HasEnded, "the tooltip did not end");
            var five = frames.FrameOf(bag[5].Handle!);
            var shown = FrameCases.WindowOf(frames, five);
            Expect.True(shown != null && shown == frames.FrameOf(sword.Handle!), "shown in the slot's tooltip place");
            FrameCases.ExpectPoint(shown!, 7, five, 1, 0.0f, 0.01f, "above its new slot");
            Expect.True(FrameCases.WindowOf(frames, frames.FrameOf(bag[0].Handle!)) == null, "the slot it left shows none");
            var shield = new RichTooltip { Title = "Shield" };
            bag[0].Icon = "shield.blp";
            bag[0].Tooltip = shield;
            bag[5].SwapWith(bag[0]);
            Expect.Equal("sword.blp|shield.blp", bag[0].Icon + "|" + bag[5].Icon, "swapped back");
            Expect.True(bag[0].Tooltip == sword && bag[5].Tooltip == shield && !sword.HasEnded && !shield.HasEnded, "both tooltips moved, neither ended");
            Expect.True(FrameCases.WindowOf(frames, five) == frames.FrameOf(shield.Handle!), "the shield's tooltip in slot 6's place");
        }

        /// <summary>A rich tooltip lays its title beside its icon, or at the corner without one, and shows its cost.</summary>
        public static void RichTip(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var button = Screen.Add(new IconButton(), Game.Instance);
            var tip = new RichTooltip { Title = "Sword", Text = "+10 damage", Cost = "150 gold" };
            button.Tooltip = tip;
            Expect.Near(Moonwell.Tooltip.Inset, tip.title.Offset.X, "the title at the corner with no icon");
            Expect.Equal(false, tip.icon.Visible, "no icon");
            tip.Icon = "sword.blp";
            Expect.Near(Moonwell.Tooltip.Inset + RichTooltip.IconSize + 0.006f, tip.title.Offset.X, "beside the icon");
            Expect.Equal(true, tip.icon.Visible, "the icon shows");
            Expect.Equal("|cfffcd312150 gold|r", frames.FrameOf(tip.cost.Handle!).Text, "the cost, in gold");
            Expect.Equal("+10 damage", tip.Text, "the text");
        }

        /// <summary>A Moved handler that throws is reported with the grid, and the next still runs.</summary>
        public static void MovedThrows(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var bag = Screen.Add(new SlotGrid { Columns = 2 }, Game.Instance);
            bag[0].Icon = "a.blp";
            var second = false;
            bag.Moved += (who, from, to) => { throw new System.InvalidOperationException("boom"); };
            bag.Moved += (who, from, to) => second = true;
            var printed = world.Printed.Count;
            ClickOn(frames, bag[0], 0);
            ClickOn(frames, bag[1], 0);
            Expect.True(world.Printed.Count > printed && world.Printed[printed].Contains("Moved"), "reported with the event");
            Expect.Equal(true, second, "the next handler ran");
        }
    }
}
