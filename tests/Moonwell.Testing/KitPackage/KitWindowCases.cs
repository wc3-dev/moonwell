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
    }
}
