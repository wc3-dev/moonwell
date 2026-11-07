using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check the frames made again when a saved game loads, as the load takes every frame a map made (game facts, 11-06).</summary>
    public static class FramesLoadCases
    {
        private const int Click = 1;

        private const int SliderChanged = 13;

        /// <summary>Loads a saved game: the fake world loses its frames, and the package hears the load, reporting nothing.</summary>
        private static void Load(FakeWorld world, FakeFramesWorld frames)
        {
            frames.Load();
            var printed = world.Printed.Count;
            Inbound.GameLoaded();
            Expect.Equal(printed, world.Printed.Count, "nothing reported at the load" + (world.Printed.Count > printed ? ": " + world.Printed[printed] : ""));
        }

        /// <summary>The fake of a frame's game frame after the load: one the load did not take.</summary>
        private static FakeFrame Remade(FakeFramesWorld frames, Frame frame, framehandle? before, string what)
        {
            Expect.True(frame.Handle != null && frame.Handle != before, what + ": a new game frame");
            var fake = frames.FrameOf(frame.Handle!);
            Expect.Equal(false, fake.Lost, what + ": not one the load took");
            return fake;
        }

        /// <summary>Every frame on the screen is made again under new roots, in its place and with what the map set: text, colour, alignment, size, offset, texture, visibility, a bar's fill, a slider's value, a check and a tooltip.</summary>
        public static void MakesAgain(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var panel = Screen.Add(new Panel { Width = 0.3f, Height = 0.2f, Offset = new Vector2(0.1f, 0.05f) }, Game.Instance);
            var label = panel.Add(new Label { Text = "Gold", Color = new Color(255, 200, 0), Alignment = FramePoint.Center });
            label.Tooltip = new Tooltip { Text = "tip" };
            var image = panel.Add(new Image { Texture = "a.blp", Visible = false });
            var bar = panel.Add(new ProgressBar { Width = 0.2f, Height = 0.02f, Value = 0.5f });
            var mine = Screen.Add(new Panel { Template = "" }, Game.Instance, shownTo: Player.One);
            var slider = mine.Add(new Slider { Value = 40.0f });
            var check = mine.Add(new CheckBox { Checked = true });
            var over = Screen.AddOver(new Label { Text = "over" }, Game.Instance);
            var before = new[] { panel.Handle, label.Handle, image.Handle, bar.Handle, slider.Handle, check.Handle, over.Handle };

            Load(world, frames);

            var fakePanel = Remade(frames, panel, before[0], "the panel");
            Expect.Equal("ConsoleUIBackdrop", fakePanel.Parent!.Under, "under the screen's root");
            Expect.Equal(false, fakePanel.Parent.Lost, "the screen's root made again");
            Expect.Near(0.3f, fakePanel.Width, "its width");
            Expect.Near(0.1f, fakePanel.Points[0].X, "its offset");
            var fakeLabel = Remade(frames, label, before[1], "the label");
            Expect.Same(fakePanel, fakeLabel.Parent, "the label under the panel made again");
            Expect.Equal("|cffffc800Gold|r", fakeLabel.Text, "its text in its colour");
            Expect.Equal(4, fakeLabel.Horizontal, "its alignment");
            Expect.True(fakeLabel.Tip != null && !fakeLabel.Tip.Lost, "its tooltip's slot made again");
            Expect.Equal("tip", frames.FrameOf(label.Tooltip!.words.Handle!).Text, "the tooltip's text");
            var fakeImage = Remade(frames, image, before[2], "the image");
            Expect.Equal("a.blp", fakeImage.Texture, "its texture");
            Expect.Equal(false, fakeImage.Shown, "still hidden");
            var fakeBar = Remade(frames, bar, before[3], "the bar");
            Expect.Near(0.1f, fakeBar.Children[0].Width, "its fill at half");
            Expect.Near(40.0f, Remade(frames, slider, before[4], "the slider").Value, "the slider's value");
            Expect.Equal(true, Remade(frames, check, before[5], "the check box").Children[1].Shown, "the check drawn");
            Expect.Equal("GameUI", Remade(frames, over, before[6], "the frame over the HUD").Parent!.Under, "under the over root");
        }

        /// <summary>The frames made again are heard: a click runs Clicked once, and the slider's value given back runs nothing.</summary>
        public static void Events(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var mine = Screen.Add(new Panel(), Game.Instance, shownTo: Player.One);
            var button = mine.Add(new Button { Text = "Buy" });
            var slider = mine.Add(new Slider { Value = 40.0f });
            var clicks = new List<Player>();
            var values = new List<float>();
            button.Clicked += who => clicks.Add(who);
            slider.ValueChanged += (who, value) => values.Add(value);

            Load(world, frames);

            var fakeButton = frames.FrameOf(button.Handle!);
            Expect.Equal("Buy", fakeButton.Text, "the button's text");
            frames.Fire(fakeButton, Click, 0);
            Expect.Equal(1, clicks.Count, "a click heard once");
            var fakeSlider = frames.FrameOf(slider.Handle!);
            frames.Fire(fakeSlider, SliderChanged, 0, 40.0f);
            Expect.Equal(0, values.Count, "its own value given back runs nothing");
            frames.Fire(fakeSlider, SliderChanged, 0, 55.0f);
            Expect.Equal(1, values.Count, "a player's drag runs ValueChanged");
        }

        /// <summary>An ended frame is not made again, and a frame added after the load is made anew, not from the frames the load took.</summary>
        public static void EndedStayGone(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var gone = Screen.Add(new Panel(), Game.Instance);
            var kept = Screen.Add(new Panel(), Game.Instance);
            gone.End();

            var count = frames.Frames.Count;
            Load(world, frames);
            var madeAtLoad = frames.Frames.Count - count;

            Expect.True(!frames.FrameOf(kept.Handle!).Lost, "the live panel made again");
            Expect.Equal(3, madeAtLoad, "the screen's root, and the live panel with its catcher");
            var added = Screen.Add(new Panel(), Game.Instance);
            Expect.Equal(false, frames.FrameOf(added.Handle!).Lost, "a panel added after the load made anew");
        }

        /// <summary>A window from the map's .fdf is made again whole, and the parts found in it are found again with what the map set; a part the map never placed keeps its template's place.</summary>
        public static void TemplateParts(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            frames.TemplateParts["ShopWindow"] = new[] { "ShopTitle", "ShopBody" };
            frames.TemplateParts["ShopBody"] = new[] { "ShopBuy" };
            var window = Screen.Add(new Panel { Template = "ShopWindow" }, Game.Instance, shownTo: Player.One);
            var title = window.Child<Label>("ShopTitle")!;
            title.Text = "Shop";
            var buy = window.Child<Button>("ShopBuy")!;
            buy.Anchor = FramePoint.Bottom;
            var clicks = new List<Player>();
            buy.Clicked += who => clicks.Add(who);
            var badge = buy.Add(new Label { Text = "new" });

            Load(world, frames);

            var fakeWindow = frames.FrameOf(window.Handle!);
            var fakeTitle = frames.FrameOf(title.Handle!);
            Expect.Equal(false, fakeTitle.Lost, "the title found again");
            Expect.Same(fakeWindow, fakeTitle.Parent, "in the window made again");
            Expect.Equal("Shop", fakeTitle.Text, "its text");
            Expect.Equal(0, fakeTitle.Points.Count, "not placed by the map: the template's place");
            Expect.Equal(-1, fakeTitle.Horizontal, "nor aligned");
            var fakeBuy = frames.FrameOf(buy.Handle!);
            Expect.Equal(1, fakeBuy.Points.Count, "placed by the map: its anchor again");
            Expect.Same(fakeBuy, frames.FrameOf(badge.Handle!).Parent, "a frame added to a found part made again under it");
            Expect.Same(buy, window.Child<Button>("ShopBuy"), "the same object at the next call");
            frames.Fire(fakeBuy, Click, 0);
            Expect.Equal(1, clicks.Count, "its click heard");
        }

        /// <summary>The HUD is looked up again, and the player's machine sets it as the map left it: hidden, a piece hidden or scaled, a piece moved to a frame made again, and a frame tied to a piece.</summary>
        public static void Hud(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var ui = Player.One.GameUi();
            ui.Visible = false;
            ui.Portrait.Visible = false;
            ui.CommandButtons[3].Scale = 1.5f;
            ui.ItemButtons[0].Attach(FramePoint.TopLeft, panel, FramePoint.TopLeft, 0.01f, -0.01f);
            var label = Screen.Add(new Label { Text = "map" }, Game.Instance);
            label.Attach(FramePoint.Bottom, ui.Minimap, FramePoint.Top, 0.0f, 0.01f);
            Player.Two.GameUi().Minimap.Visible = false;

            Load(world, frames);

            Expect.Equal(true, frames.HudHidden, "the HUD hidden again");
            Expect.Equal(false, frames.GameFrames["origin 16 0"].Shown, "the portrait hidden again");
            Expect.Near(1.5f, frames.GameFrames["origin 1 3"].Scale, "a command button's scale");
            var item = frames.GameFrames["origin 7 0"];
            Expect.Equal(1, item.Points.Count, "the item button tied again");
            Expect.Same(frames.FrameOf(panel.Handle!), item.Points[0].Other, "to the panel made again");
            Expect.Same(frames.GameFrames["MiniMapFrame"], frames.FrameOf(label.Handle!).Points[0].Other, "a frame tied to the minimap looked up again");
            Expect.Equal(true, frames.GameFrames["MiniMapFrame"].Shown, "another player's minimap: nothing set here");
        }

        /// <summary>The game components' OnGameLoaded runs once after the frames are made again, and a component that has it listens for the load though the map has no frame.</summary>
        public static void Reaction(FakeWorld world)
        {
            var watch = Game.Instance.Add(new LoadWatch());
            Expect.True(world.Listening.Contains(Moonwell.Hosting.GameEvent.Loaded), "a load listened for");
            var frames = FrameCases.Installed();
            watch.Label = Screen.Add(new Label { Text = "x" }, Game.Instance);

            Load(world, frames);

            Expect.Equal(1, watch.Loads, "run once");
            Expect.Same(watch.Label.Handle, watch.Seen, "after the frames were made again");
        }
    }

    /// <summary>Counts the loads, and keeps the handle its label has when one runs.</summary>
    public class LoadWatch : GameComponent
    {
        /// <summary>A label of the case's.</summary>
        public Label? Label;

        /// <summary>The loads heard.</summary>
        public int Loads;

        /// <summary>The label's handle at the last load.</summary>
        public framehandle? Seen;

        /// <summary>Counts the load and keeps the label's handle.</summary>
        public override void OnGameLoaded()
        {
            Loads++;
            Seen = Label?.Handle;
        }
    }
}
