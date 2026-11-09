using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check the UI kit's ready windows (decision 212): the shop and the settings window.</summary>
    public static class KitWindowCases
    {
        private const int Click = 1, SliderChanged = 13;

        private static void ClickOn(FakeFramesWorld frames, Frame frame, int slot)
        {
            frames.Fire(frames.FrameOf(frame.MouseHandle!), Click, slot);
        }

        /// <summary>A shop lays its goods four a line, each with a rich tooltip of its name, text and cost; a click runs Bought with the good; Remove takes one out.</summary>
        public static void Shop(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var shop = Screen.Add(new ShopWindow { Title = "Blacksmith" }, Game.Instance, shownTo: Player.One);
            var sword = shop.Sell(new ShopEntry { Icon = "sword.blp", Name = "Sword", Text = "+10 damage", Cost = 150 });
            for (var i = 0; i < 4; i++)
            {
                shop.Sell(new ShopEntry { Icon = "gem.blp", Name = "Gem " + i });
            }

            Expect.Equal(5, shop.Entries.Count, "five goods");
            Expect.Near(4 * ShopWindow.GoodSize + 3 * ShopWindow.GoodGap, shop.goods.Width, "four a line");
            Expect.Near(2 * ShopWindow.GoodSize + ShopWindow.GoodGap, shop.goods.Height, "two lines");
            var tip = (RichTooltip)shop.buttons[0].Tooltip!;
            Expect.Equal("Sword", tip.Title, "the tooltip's title");
            Expect.Equal("150 gold", tip.Cost, "its cost");
            Expect.Equal("", ((RichTooltip)shop.buttons[1].Tooltip!).Cost, "no cost shown for 0");
            var bought = new List<string>();
            shop.Bought += (who, entry) => bought.Add(who.Slot + " " + entry.Name);
            ClickOn(frames, shop.buttons[0], 0);
            Expect.Equal("0 Sword", string.Join(", ", bought), "Bought with the player and the good");
            shop.Remove(sword);
            Expect.Equal(4, shop.Entries.Count, "taken out");
            Expect.Near(ShopWindow.GoodSize, shop.goods.Height, "one line left");
        }

        /// <summary>An item class the shop sells.</summary>
        private sealed class ShopPotion : Item
        {
        }

        /// <summary>A good put in with Sell&lt;T&gt; is sold by the window: checked, paid in gold and lumber and given to the Buyer; sold out it is greyed and refused, and comes back after Restock; without the gold it is refused; Bought does not run.</summary>
        public static void ShopSells(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            ObjectTypes.Item(ObjectTypes.FourCC("pman"), () => new ShopPotion());
            var hero = Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), slot: 0))!;
            var shop = Screen.Add(new ShopWindow { Buyer = hero }, Game.Instance, shownTo: Player.One);
            var potion = shop.Sell<ShopPotion>(new ShopEntry { Name = "Potion", Cost = 100, Lumber = 10, Stock = 1, Restock = 5.0f });
            var bought = 0;
            shop.Bought += (who, entry) => bought++;
            Expect.Equal("100 gold, 10 lumber", ((RichTooltip)shop.buttons[0].Tooltip!).Cost, "the tooltip's cost");
            Player.One.Gold = 250;
            Player.One.Lumber = 100;
            ClickOn(frames, shop.buttons[0], 0);
            Expect.Equal(150, Player.One.Gold, "the gold taken");
            Expect.Equal(90, Player.One.Lumber, "the lumber taken");
            Expect.True(hero.ItemIn(0) is ShopPotion, "the item given to the buyer");
            Expect.Equal(0, potion.Left, "none left");
            Expect.True(!shop.buttons[0].Enabled, "sold out is greyed");
            ClickOn(frames, shop.buttons[0], 0);
            Expect.Equal(150, Player.One.Gold, "sold out takes nothing");
            world.Advance(5.1f);
            Expect.Equal(1, potion.Left, "one back after Restock");
            Expect.True(shop.buttons[0].Enabled, "and shown again");
            Player.One.Gold = 50;
            ClickOn(frames, shop.buttons[0], 0);
            Expect.Equal(50, Player.One.Gold, "without the gold nothing is taken");
            Expect.Equal(1, potion.Left, "nor sold");
            Expect.Equal(0, bought, "Bought does not run");
        }

        /// <summary>A settings window shows a line for each setting; the player's changes run Changed and are written to their file, and the next game's window brings them back, running Changed.</summary>
        public static void Settings(FakeWorld world)
        {
            world.LocalSlot = 0;
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var frames = FrameCases.Installed();
            var settings = new SettingsWindow();
            var damage = settings.Check("Show damage", on: true);
            var zoom = settings.Slider("Camera distance", 1000.0f, 3000.0f, 1650.0f);
            var speed = settings.Choice("Speed", "Slow", "Normal", "Fast");
            var heard = new List<string>();
            damage.Changed += (who, on) => heard.Add("damage " + on);
            zoom.Changed += (who, value) => heard.Add("zoom " + value);
            speed.Changed += (who, index) => heard.Add("speed " + index);
            Screen.Add(settings, Game.Instance, shownTo: Player.One);
            world.Advance(0.1f);
            Expect.Equal("", string.Join(", ", heard), "no file: nothing runs");
            Expect.Equal(true, damage.Value, "made on");

            ClickOn(frames, FindIn<CheckBox>(settings), 0);
            frames.Fire(frames.FrameOf(FindIn<Slider>(settings).Handle!), SliderChanged, 0, 2000.0f);
            ClickOn(frames, FindIn<Button>(settings, "Slow"), 0);
            ClickOn(frames, FindIn<Button>(settings, "Normal"), 0);
            Expect.Equal("damage False, zoom 2000, speed 1, speed 2", string.Join(", ", heard), "each change ran Changed");
            Expect.Equal("Fast", frames.FrameOf(FindIn<Button>(settings, "Fast").Handle!).Text, "the choice's button shows it");
            Expect.True(world.Files.ContainsKey("Map\\kit-settings.txt"), "written to the player's file");

            settings.End();
            heard.Clear();
            var next = new SettingsWindow();
            var damage2 = next.Check("Show damage", on: true);
            var gone = next.Check("Old setting");
            var zoom2 = next.Slider("Camera distance", 1000.0f, 3000.0f, 1650.0f);
            var speed2 = next.Choice("Speed", "Slow", "Normal", "Fast");
            damage2.Changed += (who, on) => heard.Add("damage " + on);
            zoom2.Changed += (who, value) => heard.Add("zoom " + value);
            speed2.Changed += (who, index) => heard.Add("speed " + index);
            Screen.Add(next, Game.Instance, shownTo: Player.One);
            world.Advance(0.1f);
            Expect.Equal("damage False, zoom 2000, speed 2", string.Join(", ", heard), "the file's values came back, running Changed");
            Expect.Equal(false, FindIn<CheckBox>(next).Checked, "the check box shows it");
            Expect.Near(2000.0f, FindIn<Slider>(next).Value, "the slider shows it");
            Expect.Equal(false, gone.Value, "a setting the file lacks keeps its own");
        }

        /// <summary>A spoiled file, or one with a value out of range, is let be.</summary>
        public static void SettingsSpoiled(FakeWorld world)
        {
            world.LocalSlot = 0;
            world.SetSlot(0, SlotState.Playing, Controller.User);
            FrameCases.Installed();
            PlayerFile.Write(Player.One, "kit-settings.txt", "not a code at all");
            var settings = new SettingsWindow();
            var zoom = settings.Slider("Camera distance", 1000.0f, 3000.0f, 1650.0f);
            var printed = world.Printed.Count;
            Screen.Add(settings, Game.Instance, shownTo: Player.One);
            world.Advance(0.1f);
            Expect.Near(1650.0f, zoom.Value, "kept its own value");
            Expect.Equal(printed, world.Printed.Count, "nothing reported");

            var bytes = new ByteWriter();
            bytes.WriteInt(1);
            bytes.WriteString("Camera distance");
            bytes.WriteInt(SettingsWindow.KindSlider);
            bytes.WriteFloat(99999.0f);
            PlayerFile.Write(Player.One, "kit-settings.txt", bytes.ToCode());
            var again = new SettingsWindow();
            var zoom2 = again.Slider("Camera distance", 1000.0f, 3000.0f, 1650.0f);
            Screen.Add(again, Game.Instance, shownTo: Player.One);
            world.Advance(0.1f);
            Expect.Near(1650.0f, zoom2.Value, "out of range let be");
        }

        /// <summary>The first frame of a kind in a frame's tree, by its text for a button.</summary>
        private static T FindIn<T>(Frame root, string text = "")
            where T : Frame
        {
            var found = Search<T>(root, text);
            Expect.True(found != null, "a frame of the kind " + text);
            return found!;
        }

        private static T? Search<T>(Frame frame, string text)
            where T : Frame
        {
            if (frame is T match && (text == "" || (match is Button button && button.Text == text)))
            {
                return match;
            }

            foreach (var child in Kids(frame))
            {
                var found = Search<T>(child, text);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static List<Frame> Kids(Frame frame)
        {
            var kids = new List<Frame>();
            for (var scoped = frame.newest; scoped != null; scoped = scoped.before)
            {
                if (scoped is Frame child)
                {
                    kids.Add(child);
                }
            }

            return kids;
        }

        private sealed class Strike : Talent
        {
            public Strike()
            {
                Name = "Strike";
                Tree = "Arms";
            }
        }

        private sealed class Rend : Talent
        {
            public Rend()
            {
                Name = "Rend";
                Tree = "Arms";
                Row = 1;
            }
        }

        private sealed class Guard : Talent
        {
            public Guard()
            {
                Name = "Guard";
                Tree = "Guard";
                Column = 2;
            }
        }

        private sealed class Toughness : Talent
        {
            public Toughness()
            {
                Name = "Toughness";
                Icon = "toughness.blp";
                Text = "+2 armor a rank.";
                MaxRank = 2;
            }

            protected override void OnRankChanged()
            {
                Hero.Modify(Stat.Armor, 2.0f * Rank, heldBy: RankScope);
            }
        }

        private sealed class Cleave : Talent
        {
            public Cleave()
            {
                Name = "Cleave";
                Row = 1;
                Column = 1;
                Needs<Toughness>(2);
            }
        }

        /// <summary>A scoreboard has a header, a team line with totals when a team has more than one player, and a row per player sorted by the first number column; a score column follows its score, SortBy picks another, and its end lets go of the score.</summary>
        public static void Scoreboard(FakeWorld world)
        {
            FrameCases.Installed();
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            world.SetSlot(2, SlotState.Playing, Controller.Computer);
            var kills = new Score("Kills");
            var board = Screen.Add(new Scoreboard(), Game.Instance);
            board.Column(kills);
            board.Column("Gold", who => who.Slot * 10);
            board.Column("Hero", who => "Paladin");
            Expect.Equal("Player", board.cells[0].Text, "the header");
            Expect.Equal("Gold", board.cells[2].Text, "a column's name");
            Expect.Near(Window.Inset + Moonwell.Scoreboard.NameWidth + Moonwell.Scoreboard.ValueWidth, board.cells[2].Offset.X, "a header made before its column sits in that column");
            Expect.Near(-Moonwell.Scoreboard.Top, board.cells[2].Offset.Y, "on the header's row");
            Expect.Near(-Moonwell.Scoreboard.Top - Moonwell.Scoreboard.RowHeight, board.cells[4 + 3].Offset.Y, "the team line one row down");
            Expect.Equal(5 * 4, board.cells.Count, "a header, a team line and three players");
            Expect.Equal("0", board.cells[4 + 1].Text, "the team's kills");
            Expect.Equal("30", board.cells[4 + 2].Text, "the team's gold");
            Expect.Equal("", board.cells[4 + 3].Text, "no total of a text");
            kills.Add(Player.Two, 3);
            kills.Add(Player.Three, 1);
            Expect.Equal(Player.Two.ColoredName, board.cells[8].Text, "most kills first, at once");
            Expect.Equal("3", board.cells[9].Text, "their kills");
            Expect.Equal("Paladin", board.cells[11].Text, "a text column");
            Expect.Equal("4", board.cells[5].Text, "the team's total");
            world.MoveToTeam(Player.Two.Handle, 1);
            world.MoveToTeam(Player.Three.Handle, 2);
            board.SortBy("Gold");
            Expect.Equal(4 * 4, board.cells.Count, "no team lines once each team has one player");
            Expect.Equal(Player.Three.ColoredName, board.cells[4].Text, "the most gold first");
            Expect.Near(2.0f * Window.Inset + Moonwell.Scoreboard.NameWidth + 3 * Moonwell.Scoreboard.ValueWidth, board.Width, "sized to the columns");
            board.End();
            Expect.Equal(0, kills.changed!.Count, "the score let go");
        }

        /// <summary>A talent tree learns a rank on its hero's owner's click when the hero has a point, the row is open and the talent needed is at its rank; a talent's modifiers last a rank; Ranks, Reset and Restore keep, give back and bring back the ranks.</summary>
        public static void Talents(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var handle = world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true);
            var hero = (Hero)Unit.Of(handle)!;
            world.SetArmor(handle, 4.0f);
            var tree = Screen.Add(new TalentWindow(hero), Game.Instance, shownTo: Player.One);
            tree.PointsPerRow = 2;
            tree.Add(() => new Toughness());
            tree.Add(() => new Cleave());
            var heard = new List<string>();
            tree.Learned += (who, talent) => heard.Add(talent.Name + " " + talent.Rank);
            ClickOn(frames, tree.buttons[0], 0);
            Expect.Equal(0, tree.Ranks()[0], "no point, nothing learned");
            tree.Points = 3;
            ClickOn(frames, tree.buttons[1], 0);
            Expect.Equal(0, tree.Ranks()[1], "row 1 is shut and Toughness is not at 2");
            Expect.True(((RichTooltip)tree.buttons[1].Tooltip!).Text.Contains("Needs Toughness 2"), "the tooltip says what it needs");
            ClickOn(frames, tree.buttons[0], 0);
            Expect.Near(6.0f, world.ArmorOf(handle), "rank 1");
            ClickOn(frames, tree.buttons[0], 1);
            Expect.Equal(1, tree.Ranks()[0], "another player's click learns nothing");
            ClickOn(frames, tree.buttons[0], 0);
            ClickOn(frames, tree.buttons[0], 0);
            Expect.Near(8.0f, world.ArmorOf(handle), "rank 2 replaces rank 1's modifier");
            Expect.Equal("2/2", tree.rankLabels[0].Text, "the rank shown");
            ClickOn(frames, tree.buttons[1], 0);
            Expect.Equal("2 1", string.Join(" ", tree.Ranks()), "Cleave learned once open");
            Expect.Equal(0, tree.Points, "every point spent");
            Expect.Equal("Toughness 1, Toughness 2, Cleave 1", string.Join(", ", heard), "Learned after each rank");
            Expect.True(hero.Has<Cleave>(), "a learned talent is on the hero");
            tree.Reset();
            Expect.Equal(3, tree.Points, "the points back");
            Expect.Near(4.0f, world.ArmorOf(handle), "the modifiers off");
            Expect.True(!hero.Has<Toughness>(), "the talents off the hero");
            tree.Restore(new List<int> { 2, 1 });
            Expect.Equal("2 1", string.Join(" ", tree.Ranks()), "the ranks back");
            Expect.Near(8.0f, world.ArmorOf(handle), "and their modifiers");
            Expect.Equal(3, tree.Points, "points as they were");
            Expect.Equal("Points: 3", tree.pointsLabel.Text, "the unspent points shown");
        }

        /// <summary>Talents that name a tree go on its tab, sharing the points, and a row opens by the points spent in its own tree; a talent naming no tree among ones that do is refused.</summary>
        public static void TalentTabs(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var hero = (Hero)Unit.Of(world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true))!;
            var tree = Screen.Add(new TalentWindow(hero), Game.Instance, shownTo: Player.One);
            tree.PointsPerRow = 1;
            tree.Add(() => new Strike());
            tree.Add(() => new Rend());
            tree.Add(() => new Guard());
            Expect.Equal("Arms Guard", string.Join(" ", tree.trees), "a tab a tree, in the order named");
            Expect.Equal(2, tree.tabs!.Count, "two pages");
            Expect.True(frames.FrameOf(tree.buttons[2].Handle!).Parent == frames.FrameOf(tree.tabs.pages[1].Handle!), "a talent on its tree's page");
            Expect.True(tree.Width >= 2.0f * Window.Inset + 2 * tree.tabs.TabWidth, "wide enough for the tabs");
            tree.Points = 3;
            ClickOn(frames, tree.buttons[2], 0);
            ClickOn(frames, tree.buttons[1], 0);
            Expect.Equal("0 0 1", string.Join(" ", tree.Ranks()), "a point in Guard opens no row of Arms");
            ClickOn(frames, tree.buttons[0], 0);
            ClickOn(frames, tree.buttons[1], 0);
            Expect.Equal("1 1 1", string.Join(" ", tree.Ranks()), "a point in Arms opens its second row");
            Expect.Equal("Points: 0", tree.pointsLabel.Text, "one pool of points");
            var before = world.Printed.Count;
            tree.Add(() => new Toughness());
            Expect.Equal(3, tree.kinds.Count, "a talent of no tree refused");
            Expect.True(world.Printed.Count > before, "and reported");
        }
    }
}
