namespace Moonwell.Testing
{
    /// <summary>Cases that check the game's HUD as each player's own, from player.GameUi().</summary>
    public static class HudCases
    {
        /// <summary>The install looks up the HUD's frames on every machine and makes no frame; a player's view of it looks nothing up again.</summary>
        public static void LookedUpAtInstall(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            Expect.Equal(36, frames.Lookups, "four by name, 32 by origin");
            Expect.True(frames.GameFrames.ContainsKey("MiniMapFrame"), "the minimap by its name");
            Expect.Equal(0, frames.Frames.Count, "no frame made");
            Player.One.GameUi();
            Player.Two.GameUi();
            Expect.Equal(36, frames.Lookups, "no lookup at a player's first use");
            Expect.Same(Player.One.GameUi(), Player.One.GameUi(), "one view per player");
        }

        /// <summary>A piece set for one player changes that player's screen only, and reads what was set everywhere.</summary>
        public static void PieceForOnePlayer(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            Player.Two.GameUi().Minimap.Visible = false;
            Expect.Equal(true, frames.GameFrames["MiniMapFrame"].Shown, "another player's minimap: nothing changes here");
            Expect.Equal(false, Player.Two.GameUi().Minimap.Visible, "reads what was set");
            Player.One.GameUi().Portrait.Visible = false;
            Expect.Equal(false, frames.GameFrames["origin 16 0"].Shown, "this player's portrait hidden");
            Player.One.GameUi().TopBar.Visible = false;
            Expect.Equal(false, frames.GameFrames["ResourceBarFrame"].Shown, "the top bar's numbers");
            Expect.Equal(false, frames.GameFrames["UpperButtonBarFrame"].Shown, "and its buttons");
        }

        /// <summary>A command button is hidden by a scale of nothing, which holds where Visible does not (seen 11-03), and shown at its own scale.</summary>
        public static void CommandButtonByScale(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var button = Player.One.GameUi().CommandButtons[3];
            var fake = frames.GameFrames["origin 1 3"];
            button.Visible = false;
            Expect.Near(0.00001f, fake.Scale, "scaled to nothing");
            Expect.Equal(true, fake.Shown, "not hidden by Visible");
            button.Scale = 1.5f;
            Expect.Near(0.00001f, fake.Scale, "still hidden after a new scale");
            button.Visible = true;
            Expect.Near(1.5f, fake.Scale, "shown at its scale");
            var item = Player.One.GameUi().ItemButtons[2];
            item.Visible = false;
            Expect.Equal(false, frames.GameFrames["origin 7 2"].Shown, "an item button hidden by Visible, which holds (seen 11-05)");
        }

        /// <summary>The whole HUD hidden lets clicks through where the console was, and shown again leaves hidden the pieces the map hid.</summary>
        public static void WholeHud(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var ui = Player.One.GameUi();
            var backdrop = frames.GameFrames["ConsoleUIBackdrop"];
            ui.Portrait.Visible = false;
            ui.Visible = false;
            Expect.Equal(true, frames.HudHidden, "hidden");
            Expect.Near(0.0f, backdrop.Width, "the console's backdrop shrunk");
            Expect.Near(0.0001f, backdrop.Height, "to nothing");
            Expect.Equal(false, ui.Visible, "reads what was set");
            ui.Visible = true;
            Expect.Equal(false, frames.HudHidden, "shown");
            Expect.Near(0.8f, backdrop.Width, "the backdrop's width again");
            Expect.Near(0.13f, backdrop.Height, "and height");
            Expect.Equal(false, frames.GameFrames["origin 16 0"].Shown, "the portrait the map hid stays hidden");
        }

        /// <summary>A movable piece ties itself to a map's frame on its player's machine; the minimap is given the console's backdrop as its parent first; a frame not added yet is refused.</summary>
        public static void MovePiece(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var item = Player.One.GameUi().ItemButtons[0];
            item.Attach(FramePoint.TopLeft, panel, FramePoint.TopLeft, 0.01f, -0.01f);
            var fake = frames.GameFrames["origin 7 0"];
            Expect.Equal(1, fake.Points.Count, "its points replaced by one");
            Expect.Same(frames.FrameOf(panel.Handle!), fake.Points[0].Other, "tied to the panel");
            item.Attach(FramePoint.BottomRight, panel, FramePoint.BottomRight);
            Expect.Equal(2, fake.Points.Count, "a second point");
            Player.One.GameUi().Minimap.Attach(FramePoint.BottomLeft, panel, FramePoint.BottomLeft);
            Expect.Same(frames.GameFrames["ConsoleUIBackdrop"], frames.GameFrames["MiniMapFrame"].Parent, "the minimap under the console's backdrop");
            Player.Two.GameUi().ItemButtons[1].Attach(FramePoint.TopLeft, panel, FramePoint.TopLeft);
            Expect.Equal(0, frames.GameFrames["origin 7 1"].Points.Count, "another player's piece: nothing here");
            Player.One.GameUi().CommandButtons[0].Attach(FramePoint.TopLeft, new Panel(), FramePoint.TopLeft);
            Expect.True(world.Logged.Exists(line => line.Contains("not added")), "refused: " + string.Join(" | ", world.Logged));
        }

        /// <summary>A map's frame ties itself to a piece of the HUD, on each machine to that machine's own.</summary>
        public static void FrameToPiece(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var label = Screen.Add(new Label { Text = "map" }, Game.Instance);
            label.Attach(FramePoint.Bottom, Player.One.GameUi().Minimap, FramePoint.Top, 0.0f, 0.01f);
            var fake = frames.FrameOf(label.Handle!);
            Expect.Equal(1, fake.Points.Count, "one point");
            Expect.Same(frames.GameFrames["MiniMapFrame"], fake.Points[0].Other, "tied to the minimap");
            Expect.Equal(1, fake.Points[0].OtherPoint, "its top");
        }
    }
}
