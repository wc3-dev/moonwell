namespace Moonwell.Testing
{
    /// <summary>Cases that check the game's HUD as each player's own, from player.GameUi().</summary>
    public static class HudCases
    {
        /// <summary>The install looks up the HUD's frames on every machine and makes no frame; a player's view of it looks nothing up again.</summary>
        public static void LookedUpAtInstall(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            Expect.Equal(68, frames.Lookups, "29 by name, 39 by origin");
            Expect.True(frames.GameFrames.ContainsKey("MiniMapFrame"), "the minimap by its name");
            Expect.Equal(0, frames.Frames.Count, "no frame made");
            Player.One.GameUi();
            Player.Two.GameUi();
            Expect.Equal(68, frames.Lookups, "no lookup at a player's first use");
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

        /// <summary>Fill puts a piece centred on a map's frame at its size: a button by its size, taken again every second, the minimap by a scale that fits it inside the frame; the selection icons are refused; the minimap is tied again a moment after its scale changes (tied in that instant it drew a white dot, seen 11-13 and 14).</summary>
        public static void FillPiece(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slot = Screen.Add(new Panel { Width = 0.028f, Height = 0.03f }, Game.Instance);
            Player.One.GameUi().CommandButtons[2].Fill(slot);
            var button = frames.GameFrames["origin 1 2"];
            Expect.Near(0.028f, button.Width, "the slot's width");
            Expect.Near(0.03f, button.Height, "and height");
            Expect.Equal(1, button.Points.Count, "one point");
            Expect.Same(frames.FrameOf(slot.Handle!), First(button)?.Other, "tied to the slot");
            Expect.Equal((int)FramePoint.Center, First(button)?.Point ?? -1, "by its centre");
            Expect.Equal(true, frames.GameFrames["CommandButton_2"].Points.Count > 0, "its holder put off the screen");
            slot.Width = 0.04f;
            world.Advance(1.05f);
            Expect.Near(0.04f, button.Width, "the slot's new width a second later");
            Player.Two.GameUi().ItemButtons[0].Fill(slot);
            Expect.Equal(0, frames.GameFrames["origin 7 0"].Points.Count, "another player's piece: nothing here");
            var ring = Screen.Add(new Panel { Width = 0.1f, Height = 0.0690625f }, Game.Instance);
            var minimap = Player.One.GameUi().Minimap;
            minimap.Fill(ring);
            var fake = frames.GameFrames["MiniMapFrame"];
            Expect.Near(0.5f, fake.Scale, "scaled to fit, by its height here");
            Expect.Near(1.0f, minimap.Scale, "Scale reads what the map set, the same on every machine");
            Expect.Near(0.0f, fake.Width, "never sized");
            Expect.Same(frames.FrameOf(ring.Handle!), First(fake)?.Other, "tied to the frame");
            fake.Points.Clear();
            world.Advance(0.15f);
            Expect.Equal(1, fake.Points.Count, "tied again a moment after Fill");
            minimap.Scale = 0.6f;
            Expect.Near(0.6f, fake.Scale, "a scale set after");
            fake.Points.Clear();
            world.Advance(0.15f);
            Expect.Equal(1, fake.Points.Count, "tied again a moment after the scale, one point");
            Expect.Same(frames.FrameOf(ring.Handle!), First(fake)?.Other, "to the frame");
            Player.One.GameUi().SelectionIcons.Fill(ring);
            Expect.True(world.Logged.Exists(line => line.Contains("no size")), "the selection icons refused: " + string.Join(" | ", world.Logged));
        }

        /// <summary>AddTo makes a frame the child of a game frame that keeps one, as the F10 menu's panel does; under a simple frame, where the game would give it another parent, the frame goes over the HUD and shows and hides with the game frame on each machine, checked every 0.03 s (seen 11-13).</summary>
        public static void AddToGameFrame(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var menu = Screen.AddTo(new Panel { Width = 0.1f, Height = 0.1f }, "EscMenuMainPanel", Game.Instance);
            Expect.Same(frames.GameFrames["EscMenuMainPanel"], frames.FrameOf(menu.Handle!).Parent, "the menu panel's own child");
            var detail = frames.GameFrames["SimpleInfoPanelUnitDetail"];
            detail.Shown = false;
            var ring = Screen.AddTo(new Panel { Width = 0.1f, Height = 0.1f }, "SimpleInfoPanelUnitDetail", Game.Instance);
            var fake = frames.FrameOf(ring.Handle!);
            Expect.Equal("GameUI", Root(fake).Under, "over the HUD, not under the simple frame");
            Expect.Equal(false, fake.Shown, "hidden while the panel is");
            detail.Shown = true;
            world.Advance(0.05f);
            Expect.Equal(true, fake.Shown, "shown with it a moment later");
            ring.Visible = false;
            Expect.Equal(false, fake.Shown, "hidden when set");
            detail.Shown = false;
            world.Advance(0.05f);
            detail.Shown = true;
            world.Advance(0.05f);
            Expect.Equal(false, fake.Shown, "still hidden as set");
            ring.Visible = true;
            Expect.Equal(true, fake.Shown, "shown again");
            var other = Screen.AddTo(new Panel(), "SimpleInfoPanelUnitDetail", Game.Instance, Player.Two);
            Expect.Equal(false, frames.FrameOf(other.Handle!).Shown, "another player's frame stays hidden here");
            var portrait = Screen.AddTo(new Panel(), Player.One.GameUi().Portrait, Game.Instance);
            Expect.Same(frames.GameFrames["origin 16 0"], frames.FrameOf(portrait.Handle!).Parent, "the portrait's own child");
        }

        private static FakeFrame Root(FakeFrame frame)
        {
            while (frame.Parent != null)
            {
                frame = frame.Parent;
            }

            return frame;
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

        /// <summary>A moved command or item button puts the game's button it sat in off the screen at no size, as it takes clicks where it was (seen 11-13), and the first move stops the game placing its HUD again; another player's move changes nothing here.</summary>
        public static void MovedButtonLeavesNothing(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            Player.Two.GameUi().CommandButtons[4].Attach(FramePoint.Center, panel, FramePoint.Center);
            Expect.Equal(0, frames.GameFrames["CommandButton_4"].Points.Count, "another player's: nothing here");
            Expect.Equal(true, frames.AutoPlace, "the game still places the HUD here");
            Player.One.GameUi().CommandButtons[4].Attach(FramePoint.Center, panel, FramePoint.Center);
            Off(frames.GameFrames["CommandButton_4"], true, "the command button's holder");
            Expect.Same(frames.FrameOf(panel.Handle!), First(frames.GameFrames["origin 1 4"])?.Other, "the button tied to the panel");
            Expect.Equal(false, frames.AutoPlace, "the game no longer places the HUD");
            Player.One.GameUi().ItemButtons[5].Attach(FramePoint.Center, panel, FramePoint.Center);
            Off(frames.GameFrames["InventoryButton_5"], true, "the item button's holder");
            Expect.Equal(0, frames.GameFrames["InventoryButton_4"].Points.Count, "an item button not moved keeps its holder");
            frames.GameFrames["CommandButton_4"].Points.Clear();
            world.Advance(1.05f);
            Off(frames.GameFrames["CommandButton_4"], true, "put off again a second later, as the game puts it back");
        }

        /// <summary>The whole HUD hidden hides the command card's click blocker and puts it off the screen, every second; shown again, the blocker shows (seen 11-13).</summary>
        public static void HiddenHudHidesBlocker(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            Player.Two.GameUi().Visible = false;
            var blocker = frames.GameFrames["ConsoleUI child 5"];
            Expect.Equal(true, blocker.Shown, "another player's HUD: nothing here");
            Player.One.GameUi().Visible = false;
            Expect.Equal(false, blocker.Shown, "hidden");
            Off(blocker, true, "the blocker");
            blocker.Shown = true;
            world.Advance(1.05f);
            Expect.Equal(false, blocker.Shown, "hidden again a second later");
            Player.One.GameUi().Visible = true;
            Expect.Equal(true, blocker.Shown, "shown with the HUD");
        }

        /// <summary>With the whole HUD hidden, item buttons set Visible show alone: their first holder is shown, the inventory's text and cover stay hidden until set Visible too.</summary>
        public static void ItemButtonsAlone(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var ui = Player.One.GameUi();
            ui.Visible = false;
            frames.GameFrames["InventoryButton_0"].Shown = false;
            ui.ItemButtons[3].Visible = true;
            Expect.Equal(true, frames.GameFrames["InventoryButton_0"].Shown, "the first holder shown, which shows the buttons");
            Expect.Equal(0, frames.GameFrames["InventoryText"].Alpha, "the inventory's text see-through");
            Expect.Near(0.00001f, frames.GameFrames["SimpleInventoryCover"].Scale, "the cover scaled to nothing");
            Expect.Equal(true, ui.InventoryText.Visible, "the text reads as set");
            ui.InventoryText.Visible = true;
            Expect.Equal(255, frames.GameFrames["InventoryText"].Alpha, "the text set Visible over the HUD shows");
            ui.InventoryCover.Visible = false;
            ui.Visible = true;
            Expect.Near(0.00001f, frames.GameFrames["SimpleInventoryCover"].Scale, "the cover the map hid stays hidden with the HUD shown");
            ui.InventoryCover.Visible = true;
            Expect.Near(1.0f, frames.GameFrames["SimpleInventoryCover"].Scale, "and shows when set");
        }

        /// <summary>A hero button hidden, or the whole HUD, puts its glow off the screen, as hiding it does not hold (seen 11-12), every second; set Visible again, the glow is put back on its button.</summary>
        public static void GlowsWithHeroButtons(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var ui = Player.One.GameUi();
            ui.HeroButtons[1].Visible = false;
            Off(frames.GameFrames["origin 6 1"], false, "the hidden button's glow");
            Expect.Equal(0, frames.GameFrames["origin 6 0"].Points.Count, "a shown button's glow left alone");
            ui.Visible = false;
            Off(frames.GameFrames["origin 6 0"], false, "every glow with the HUD hidden");
            ui.HeroButtons[0].Visible = true;
            var back = First(frames.GameFrames["origin 6 0"]);
            Expect.Same(frames.GameFrames["origin 3 0"], back?.Other, "the glow put back on its hero button");
            Expect.Equal(4, back?.Point ?? -1, "by its centre");
            frames.GameFrames["origin 6 1"].Points.Clear();
            world.Advance(1.05f);
            Off(frames.GameFrames["origin 6 1"], false, "put off again a second later");
        }

        /// <summary>The selection icons shown over the hidden HUD show the panel alone, moved at no size; one unit's details and name stay hidden until set Visible, the name put back by its .fdf's point.</summary>
        public static void SelectionIconsAlone(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var ui = Player.One.GameUi();
            var panel = frames.GameFrames["parent of SimpleInfoPanelUnitDetail"];
            var spot = Screen.Add(new Panel(), Game.Instance);
            ui.Visible = false;
            panel.Shown = false;
            ui.SelectionIcons.Visible = true;
            Expect.Equal(true, panel.Shown, "the panel shown over the hidden HUD");
            ui.SelectionIcons.Attach(FramePoint.TopLeft, spot, FramePoint.TopRight);
            Expect.Near(0.0001f, panel.Width, "moved at no size");
            Expect.Same(frames.FrameOf(spot.Handle!), First(panel)?.Other, "tied to the map's frame");
            Expect.Near(0.00001f, frames.GameFrames["SimpleInfoPanelUnitDetail child 13"].Scale, "the details hidden");
            Expect.Equal(14, frames.GameFrames["SimpleInfoPanelUnitDetail"].Children.Count, "all 14 looked up");
            var name = frames.GameFrames["SimpleNameValue"];
            Off(name, false, "the name, a String, moved off");
            Expect.Near(0.0f, name.Width, "never sized");
            Expect.Near(1.0f, name.Scale, "never scaled");
            ui.UnitName.Visible = true;
            Expect.Same(frames.GameFrames["SimpleInfoPanelUnitDetail"], First(name)?.Other, "the name put back in the unit details");
            Expect.Equal(1, First(name)?.Point ?? -1, "by its top");
            panel.Points.Clear();
            panel.Shown = false;
            world.Advance(1.05f);
            Expect.Equal(true, panel.Shown, "shown again a second later");
            Expect.Equal(1, panel.Points.Count, "tied again");
            ui.SelectionIcons.Visible = false;
            Expect.Equal(false, panel.Shown, "hidden when set");
        }

        /// <summary>A building's queue: hidden with the HUD by a scale of nothing on the whole building panel, shown when set Visible over it; the building's name goes with UnitName, put back by its .fdf's point in the building panel.</summary>
        public static void BuildQueueAlone(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var ui = Player.One.GameUi();
            var panel = frames.GameFrames["SimpleInfoPanelBuildingDetail 1"];
            var name = frames.GameFrames["SimpleBuildingNameValue 1"];
            ui.Visible = false;
            Expect.Near(0.00001f, panel.Scale, "the building panel hidden with the HUD");
            Off(name, false, "the building's name, a String, moved off");
            ui.BuildQueue.Visible = true;
            Expect.Near(1.0f, panel.Scale, "the queue set Visible over the HUD");
            Expect.Equal(1, name.Points.Count, "the name still off, with UnitName hidden");
            ui.UnitName.Visible = true;
            Expect.Same(panel, First(name)?.Other, "the name put back in the building panel");
            Expect.Equal(1, First(name)?.Point ?? -1, "by its top");
            Expect.Near(1.0f, name.Scale, "the String never scaled");
            panel.Scale = 0.5f;
            world.Advance(1.05f);
            Expect.Near(0.5f, panel.Scale, "a shown queue left as the game has it");
            ui.BuildQueue.Visible = false;
            Expect.Near(0.00001f, panel.Scale, "hidden when set");
        }

        /// <summary>A letterboxed cinematic's end brings back what the map hid of the HUD (seen 11-13), so each player's HUD is set again at its end and every 0.03 s for a second; a cinematic still playing is left alone.</summary>
        public static void AfterCinematic(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var ui = Player.One.GameUi();
            ui.Visible = false;
            ui.Portrait.Visible = false;
            var cine = Cinematic.Play(c => Short(), Game.Instance);
            world.Advance(0.5f);
            frames.HudHidden = false;
            frames.GameFrames["origin 16 0"].Shown = true;
            world.Advance(0.5f);
            Expect.Equal(false, frames.HudHidden, "left alone while the cinematic plays");
            world.Advance(1.05f);
            Expect.Equal(true, cine.HasEnded, "the scene over");
            Expect.Equal(true, frames.HudHidden, "the HUD hidden again at the end");
            Expect.Equal(false, frames.GameFrames["origin 16 0"].Shown, "the portrait the map hid, hidden again");
            frames.GameFrames["origin 16 0"].Shown = true;
            world.Advance(0.1f);
            Expect.Equal(false, frames.GameFrames["origin 16 0"].Shown, "and again soon after, through the fade");
            world.Advance(0.6f);
            frames.GameFrames["origin 16 0"].Shown = true;
            world.Advance(0.1f);
            Expect.Equal(false, frames.GameFrames["origin 16 0"].Shown, "and past it");
            world.Advance(0.5f);
            frames.GameFrames["origin 16 0"].Shown = true;
            world.Advance(0.5f);
            Expect.Equal(true, frames.GameFrames["origin 16 0"].Shown, "not after the first second");
        }

        private static System.Collections.Generic.IEnumerable<Wait> Short()
        {
            yield return Wait.Seconds(2f);
        }

#if DEBUG
        /// <summary>The -frames command writes the game's frame tree to its own file, as the log keeps too few lines: each frame's index, name, size, whether it shows and its children; the typer is told where.</summary>
        public static void FrameTreeCommand(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            var debug = FakeDebugWorld.Install(world);
            var frames = FrameCases.Installed();
            frames.GameChildCounts["origin 0 0"] = 2;
            var console = frames.FrameOf(frames.ChildAt(frames.GameFrame("origin 0 0")!, 1));
            console.Name = "ConsoleUI";
            console.Width = 0.8f;
            console.Shown = false;
            world.Chat(Player.Get(0), "-frames");
            var file = debug.Files.TryGetValue("Moonwell\\frames.txt", out var lines) ? lines : new System.Collections.Generic.List<string>();
            Expect.Equal("game UI", file.Count > 0 ? file[0] : "(no file)", "the root named first");
            Expect.True(file.Contains("    1 ConsoleUI 0.8 by 0 hidden"), "a child, indented under its root: " + string.Join(" | ", file));
            Expect.True(world.PrintedTexts.Exists(line => line.Contains("frames written to CustomMapData\\Moonwell\\frames.txt")), "the typer told where: " + string.Join(" | ", world.PrintedTexts));
        }
#endif

        /// <summary>A frame's first point; null when it has none, as at a machine where the piece is not moved.</summary>
        private static FakePoint? First(FakeFrame frame)
        {
            return frame.Points.Count > 0 ? frame.Points[0] : null;
        }

        /// <summary>Checks a game frame was put off the screen: one point far above it, at no size when sized.</summary>
        private static void Off(FakeFrame frame, bool sized, string what)
        {
            Expect.Equal(1, frame.Points.Count, what + ": one point");
            if (frame.Points.Count != 1)
            {
                return;
            }

            Expect.True(frame.Points[0].Other == null && frame.Points[0].Y > 1.0f, what + ": off the screen");
            if (sized)
            {
                Expect.Near(0.0001f, frame.Width, what + ": at no size");
            }
        }
    }
}
