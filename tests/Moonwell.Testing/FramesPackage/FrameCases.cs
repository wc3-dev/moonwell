namespace Moonwell.Testing
{
    /// <summary>Cases that check the frames package: its roots, frames, layout and kinds.</summary>
    public static class FrameCases
    {
        /// <summary>Installs the package on a fake world, and returns the world.</summary>
        public static FakeFramesWorld Installed()
        {
            var world = new FakeFramesWorld();
            FramesSystem.Install(world);
            return world;
        }

        /// <summary>The install makes no frame, as a frame made while the script loads can desync (a claim); the two roots are made at their first use: the whole width under the console's backdrop, and over the HUD under the game UI.</summary>
        public static void Install(FakeWorld world)
        {
            var frames = Installed();
            Expect.Equal(0, frames.Frames.Count, "frames made at the install");
            Expect.Equal("ConsoleUIBackdrop", frames.FrameOf(FramesSystem.ScreenRoot).Under, "the screen's root");
            Expect.Equal("GameUI", frames.FrameOf(FramesSystem.OverRoot).Under, "the over root");
            Expect.Equal(2, frames.Frames.Count, "one of each, made once");
            Expect.Same(FramesSystem.ScreenRoot, FramesSystem.ScreenRoot, "the same root at each use");
        }

        /// <summary>Nothing is made until a frame is added; Screen.Add puts it under the screen's root and AddOver under the over root.</summary>
        public static void AddMakes(FakeWorld world)
        {
            var frames = Installed();
            var panel = new Panel();
            Expect.Equal(0, frames.Frames.Count, "nothing made before Add");
            Expect.True(panel.Handle == null, "no handle before Add");
            Screen.Add(panel, Game.Instance);
            Expect.True(panel.Handle != null, "a handle after Add");
            Expect.Same(frames.FrameOf(FramesSystem.ScreenRoot), frames.FrameOf(panel.Handle!).Parent, "under the screen's root");
            var over = Screen.AddOver(new Panel(), Game.Instance);
            Expect.Same(frames.FrameOf(FramesSystem.OverRoot), frames.FrameOf(over.Handle!).Parent, "under the over root");
            Expect.Same(Game.Instance, panel.Parent, "in the scope named");
        }

        /// <summary>A child is made under its parent, is in its parent's scope, and ends with it.</summary>
        public static void ChildInParent(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var child = panel.Add(new Panel());
            Expect.Same(frames.FrameOf(panel.Handle!), frames.FrameOf(child.Handle!).Parent, "made under its parent");
            Expect.Same(panel, child.Parent, "in its parent's scope");
            panel.End();
            Expect.Equal(true, child.HasEnded, "ended with its parent");
        }

        /// <summary>A tree built before it is added is made whole at the Add, parents before children.</summary>
        public static void TreeBeforeAdd(FakeWorld world)
        {
            var frames = Installed();
            var panel = new Panel();
            var child = panel.Add(new Panel());
            Expect.True(child.Handle == null, "nothing made before the tree is added");
            Screen.Add(panel, Game.Instance);
            Expect.Same(frames.FrameOf(panel.Handle!), frames.FrameOf(child.Handle!).Parent, "the child made under its parent");
        }

        /// <summary>A frame added to a frame that has ended ends at once, and nothing is made in the game.</summary>
        public static void EndedParent(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            panel.End();
            var count = frames.Frames.Count;
            var child = panel.Add(new Panel());
            Expect.Equal(true, child.HasEnded, "ended at once");
            Expect.Equal(count, frames.Frames.Count, "nothing made");
        }

        /// <summary>A frame for one player is made on every machine and shown only on that player's; its Player reads the same everywhere.</summary>
        public static void ShownTo(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var theirs = Screen.Add(new Panel(), Game.Instance, shownTo: Player.Two);
            var mine = Screen.Add(new Panel(), Game.Instance, shownTo: Player.One);
            var everyone = Screen.Add(new Panel(), Game.Instance);
            Expect.Equal(false, frames.FrameOf(theirs.Handle!).Shown, "not shown on another player's machine");
            Expect.Equal(true, frames.FrameOf(mine.Handle!).Shown, "shown on its player's machine");
            Expect.Equal(true, frames.FrameOf(everyone.Handle!).Shown, "shown to everyone");
            Expect.Same(Player.Two, theirs.Player, "its player");
            Expect.True(everyone.Player == null, "no player for everyone's");
        }

        /// <summary>A child of a frame for one player is shown to the same player.</summary>
        public static void ChildShownTo(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance, shownTo: Player.Two);
            var child = panel.Add(new Panel());
            Expect.Same(Player.Two, child.Player, "its parent's player");
            Expect.Equal(false, frames.FrameOf(child.Handle!).Shown, "not shown here");
        }

        /// <summary>A frame placed nowhere sits at its parent's centre.</summary>
        public static void AnchorDefault(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var fake = frames.FrameOf(panel.Handle!);
            Expect.Equal(1, fake.Points.Count, "one point");
            Expect.Equal(4, fake.Points[0].Point, "its centre");
            Expect.Equal(4, fake.Points[0].OtherPoint, "at the parent's centre");
            Expect.Same(fake.Parent, fake.Points[0].Other, "tied to its parent");
            Expect.True(panel.Anchor == FramePoint.Center, "Anchor reads Center");
        }

        /// <summary>Anchor and Offset tie the frame's point to the same point of its parent; a later set replaces the point.</summary>
        public static void AnchorOffset(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel { Anchor = FramePoint.TopRight, Offset = new Vector2(-0.01f, -0.02f) }, Game.Instance);
            var fake = frames.FrameOf(panel.Handle!);
            Expect.Equal(2, fake.Points[0].Point, "its top right");
            Expect.Equal(2, fake.Points[0].OtherPoint, "at the parent's top right");
            Expect.Near(-0.01f, fake.Points[0].X, "the offset's x");
            Expect.Near(-0.02f, fake.Points[0].Y, "the offset's y");
            panel.Anchor = FramePoint.BottomLeft;
            Expect.Equal(1, fake.Points.Count, "the point replaced");
            Expect.Equal(6, fake.Points[0].Point, "its bottom left");
            Expect.Near(-0.01f, fake.Points[0].X, "the offset kept");
        }

        /// <summary>Attach ties a point to a point of another frame; another Attach adds a second point.</summary>
        public static void Attach(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var buy = panel.Add(new Panel());
            var sell = panel.Add(new Panel());
            var far = panel.Add(new Panel());
            sell.Attach(FramePoint.Left, buy, FramePoint.Right, 0.005f, 0f);
            var fake = frames.FrameOf(sell.Handle!);
            Expect.Equal(1, fake.Points.Count, "the anchor replaced by the attach");
            Expect.Same(frames.FrameOf(buy.Handle!), fake.Points[0].Other, "tied to the other");
            Expect.Equal(3, fake.Points[0].Point, "its left");
            Expect.Equal(5, fake.Points[0].OtherPoint, "the other's right");
            Expect.Near(0.005f, fake.Points[0].X, "the offset");
            sell.Attach(FramePoint.Right, far, FramePoint.Left);
            Expect.Equal(2, fake.Points.Count, "a second point");
        }

        /// <summary>A frame of a tree built before the Add can be tied to a sibling added after it.</summary>
        public static void AttachLaterSibling(FakeWorld world)
        {
            var frames = Installed();
            var panel = new Panel();
            var first = panel.Add(new Panel());
            var second = panel.Add(new Panel());
            first.Attach(FramePoint.Top, second, FramePoint.Bottom);
            Screen.Add(panel, Game.Instance);
            Expect.Same(frames.FrameOf(second.Handle!), frames.FrameOf(first.Handle!).Points[0].Other, "tied to the later sibling");
        }

        /// <summary>Fill covers another frame, all points.</summary>
        public static void Fill(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var cover = panel.Add(new Panel());
            cover.Fill(panel);
            var fake = frames.FrameOf(cover.Handle!);
            Expect.Same(frames.FrameOf(panel.Handle!), fake.Filled, "covers the other");
            Expect.Equal(0, fake.Points.Count, "no other point");
        }

        /// <summary>Width, Height and Scale are kept and given to the game; unset, the size reads 0 and the scale 1.</summary>
        public static void SizeScale(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            Expect.Near(0f, panel.Width, "no width unless set");
            Expect.Near(1f, panel.Scale, "scale 1 unless set");
            panel.Width = 0.1f;
            panel.Height = 0.05f;
            panel.Scale = 2f;
            var fake = frames.FrameOf(panel.Handle!);
            Expect.Near(0.1f, fake.Width, "the game's width");
            Expect.Near(0.05f, fake.Height, "the game's height");
            Expect.Near(2f, fake.Scale, "the game's scale");
            Expect.Near(0.1f, panel.Width, "width as set");
        }

        /// <summary>Visible, Enabled, Alpha (kept within 0 to 255) and Level; a frame for another player stays hidden whatever Visible says.</summary>
        public static void VisibleEnabledAlphaLevel(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var fake = frames.FrameOf(panel.Handle!);
            panel.Visible = false;
            Expect.Equal(false, fake.Shown, "hidden");
            panel.Visible = true;
            Expect.Equal(true, fake.Shown, "shown again");
            panel.Enabled = false;
            Expect.Equal(false, frames.Frames.Find(f => f.Filled == fake)!.Enabled, "its catcher takes no mouse");
            panel.Alpha = 300;
            Expect.Equal(255, fake.Alpha, "kept within 255");
            Expect.Equal(255, panel.Alpha, "reads 255");
            panel.Alpha = -5;
            Expect.Equal(0, fake.Alpha, "kept within 0");
            panel.Level = 3;
            Expect.Equal(3, fake.Level, "the game's level");
            var theirs = Screen.Add(new Panel(), Game.Instance, shownTo: Player.Two);
            theirs.Visible = true;
            Expect.Equal(false, frames.FrameOf(theirs.Handle!).Shown, "another player's stays hidden here");
            Expect.Equal(true, theirs.Visible, "Visible reads what was set");
        }

        /// <summary>Everything set before the Add is given to the game when the frame is made.</summary>
        public static void SetBeforeAdd(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel { Width = 0.2f, Height = 0.1f, Scale = 1.5f, Visible = false, Enabled = false, Alpha = 100, Level = 2 }, Game.Instance);
            var fake = frames.FrameOf(panel.Handle!);
            Expect.Near(0.2f, fake.Width, "the width");
            Expect.Near(0.1f, fake.Height, "the height");
            Expect.Near(1.5f, fake.Scale, "the scale");
            Expect.Equal(false, fake.Shown, "hidden");
            Expect.Equal(false, frames.Frames.Find(f => f.Filled == fake)!.Enabled, "its catcher takes no mouse");
            Expect.Equal(100, fake.Alpha, "the alpha");
            Expect.Equal(2, fake.Level, "the level");
        }

        /// <summary>After the end, setting asks nothing of the game, and reads give the last values.</summary>
        public static void AfterEnd(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel { Width = 0.2f }, Game.Instance);
            var other = Screen.Add(new Panel(), Game.Instance);
            panel.End();
            var fake = frames.FrameOf(panel.Handle!);
            var calls = fake.Calls;
            panel.Width = 0.5f;
            panel.Height = 0.5f;
            panel.Scale = 3f;
            panel.Visible = true;
            panel.Enabled = false;
            panel.Alpha = 1;
            panel.Level = 9;
            panel.Anchor = FramePoint.Top;
            panel.Offset = new Vector2(1f, 1f);
            panel.Attach(FramePoint.Left, other, FramePoint.Right);
            panel.Fill(other);
            Expect.Equal(calls, fake.Calls, "nothing asked after the end");
            Expect.Near(0.2f, panel.Width, "the last width");
            Expect.True(panel.Anchor == FramePoint.Center, "the last anchor");
        }

        /// <summary>A label is a TEXT with its text and alignment kept and given to the game.</summary>
        public static void Label(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "Gold", Alignment = FramePoint.Left }, Game.Instance);
            var fake = frames.FrameOf(label.Handle!);
            Expect.Equal("TEXT", fake.Type, "made as a TEXT");
            Expect.Equal("Gold", fake.Text, "its text");
            Expect.Equal(1, fake.Vertical, "middle");
            Expect.Equal(3, fake.Horizontal, "left");
            label.Text = "Gold: 300";
            Expect.Equal("Gold: 300", fake.Text, "a new text");
            Expect.Equal("Gold: 300", label.Text, "reads what was set");
            Expect.True(new Label().Alignment == FramePoint.TopLeft, "top left unless set");
        }

        /// <summary>A label is turned off for the mouse, so a click reaches what is under it.</summary>
        public static void LabelLetsClicksThrough(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            Expect.Equal(false, frames.FrameOf(label.Handle!).Enabled, "turned off for the mouse");
        }

        /// <summary>ShowLocal changes only what this machine draws; Text reads the shared text.</summary>
        public static void ShowLocal(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "shared" }, Game.Instance);
            label.ShowLocal("mine");
            Expect.Equal("mine", frames.FrameOf(label.Handle!).Text, "this machine draws its own");
            Expect.Equal("shared", label.Text, "Text reads the shared text");
        }

        /// <summary>An image is a BACKDROP painted with its texture.</summary>
        public static void Image(FakeWorld world)
        {
            var frames = Installed();
            var image = Screen.Add(new Image { Texture = "ReplaceableTextures\\CommandButtons\\BTNFootman.blp" }, Game.Instance);
            var fake = frames.FrameOf(image.Handle!);
            Expect.Equal("BACKDROP", fake.Type, "made as a BACKDROP");
            Expect.Equal("ReplaceableTextures\\CommandButtons\\BTNFootman.blp", fake.Texture, "its texture");
            image.Texture = "x.blp";
            Expect.Equal("x.blp", fake.Texture, "a new texture");
        }

        /// <summary>A panel has the game's window look, and an empty text over it that catches clicks, as the window lets them through.</summary>
        public static void PanelLook(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var fake = frames.FrameOf(panel.Handle!);
            Expect.Equal("EscMenuBackdrop", fake.Template, "the game's window");
            var catcher = frames.Frames.Find(f => f.Filled == fake);
            Expect.True(catcher != null, "a catcher over it");
            Expect.Equal("TEXT", catcher!.Type, "an empty text");
            Expect.Same(fake, catcher.Parent, "made under the panel");
        }

        /// <summary>An empty panel is a BACKDROP painted blank, which draws nothing and lets clicks through, as a FRAME by type takes them (seen 11-03).</summary>
        public static void PanelEmpty(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel { Template = "" }, Game.Instance);
            var fake = frames.FrameOf(panel.Handle!);
            Expect.Equal("BACKDROP", fake.Type, "made as a BACKDROP");
            Expect.Equal("UI\\Widgets\\EscMenu\\Human\\blank-background.blp", fake.Texture, "painted blank");
            Expect.True(frames.Frames.Find(f => f.Filled == fake) == null, "no catcher");
        }

        /// <summary>An ended frame is hidden with its children, and nothing is destroyed.</summary>
        public static void EndHides(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var label = panel.Add(new Label { Text = "x" });
            var made = frames.Frames.Count;
            panel.End();
            Expect.Equal(false, frames.FrameOf(panel.Handle!).Shown, "hidden");
            Expect.Equal(false, frames.FrameOf(label.Handle!).Shown, "its child hidden");
            Expect.Equal(made, frames.Frames.Count, "nothing made or destroyed");
        }

        /// <summary>The next frame of a kind and template takes an ended one, moved to its new parent, with none of the old one's values, points or text.</summary>
        public static void PoolReuse(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var old = panel.Add(new Label { Text = "old", Alignment = FramePoint.Center, Width = 0.1f, Height = 0.05f, Scale = 2.0f, Alpha = 10, Level = 3, Enabled = true, Offset = new Vector2(0.1f, 0.2f) });
            old.ShowLocal("mine");
            var oldHandle = old.Handle;
            panel.End();
            var made = frames.Frames.Count;
            var next = Screen.Add(new Label(), Game.Instance);
            Expect.Same(oldHandle, next.Handle, "the ended label given out again");
            Expect.Equal(made, frames.Frames.Count, "nothing new made");
            var fake = frames.FrameOf(next.Handle!);
            var screen = frames.Frames.Find(f => f.Under == "ConsoleUIBackdrop");
            Expect.Same(screen, fake.Parent, "moved to its new parent");
            Expect.Equal("", fake.Text, "no old text");
            Expect.Equal(true, fake.Shown, "shown");
            Expect.Near(0.0f, fake.Width, "no old width");
            Expect.Near(0.0f, fake.Height, "no old height");
            Expect.Near(1.0f, fake.Scale, "no old scale");
            Expect.Equal(255, fake.Alpha, "no old alpha");
            Expect.Equal(0, fake.Level, "no old level");
            Expect.Equal(false, fake.Enabled, "a label's mouse, off");
            Expect.Equal(0, fake.Vertical, "top");
            Expect.Equal(3, fake.Horizontal, "left");
            Expect.Equal(1, fake.Points.Count, "one point");
            Expect.Equal(4, fake.Points[0].Point, "its centre");
            Expect.Near(0.0f, fake.Points[0].X, "no old offset");
            var panelHandle = panel.Handle;
            var again = Screen.Add(new Panel(), Game.Instance);
            Expect.Same(panelHandle, again.Handle, "the ended panel given out again");
            Expect.Equal(made, frames.Frames.Count, "with its own catcher");
            var image = Screen.Add(new Image(), Game.Instance);
            Expect.Equal(made + 1, frames.Frames.Count, "another kind is made new");
            image.Texture = "x.blp";
            image.End();
            var blank = Screen.Add(new Image(), Game.Instance);
            Expect.Equal("UI\\Widgets\\EscMenu\\Human\\blank-background.blp", frames.FrameOf(blank.Handle!).Texture, "no old texture");
        }

        /// <summary>An ended frame that was given a template's size goes only to a frame that sets its own: one that does not is made new.</summary>
        public static void PoolFits(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel { Width = 0.3f, Height = 0.2f }, Game.Instance);
            panel.End();
            var made = frames.Frames.Count;
            var plainPanel = Screen.Add(new Panel(), Game.Instance);
            Expect.True(plainPanel.Handle != panel.Handle, "a panel with no size is made new");
            Expect.Equal(made + 2, frames.Frames.Count, "a panel with its catcher");
            var sizedPanel = Screen.Add(new Panel { Width = 0.1f, Height = 0.1f }, Game.Instance);
            Expect.Same(panel.Handle, sizedPanel.Handle, "a sized panel takes the ended one");
            Expect.Equal(made + 2, frames.Frames.Count, "nothing more made");
            Expect.Near(0.1f, frames.FrameOf(sizedPanel.Handle!).Width, "its own width");
        }

        /// <summary>A made frame holds none of the frames added to it, so ended ones are not kept alive by it.</summary>
        public static void MadeKeepsNoChildren(FakeWorld world)
        {
            Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            for (var i = 0; i < 3; i++)
            {
                panel.Add(new Label { Text = "row" }).End();
            }

            var early = new Panel();
            early.Add(new Label());
            Screen.Add(early, Game.Instance);
            Expect.Equal(0, panel.children.Count, "none added after it was made");
            Expect.Equal(0, early.children.Count, "none added before, once made");
        }

        /// <summary>A template the game does not have is reported, and the frame is made by its type, so it still has a frame and every machine the same.</summary>
        public static void UnknownTemplate(FakeWorld world)
        {
            var frames = Installed();
            frames.MissingTemplates.Add("EscMenuLabelTextTemplate");
            frames.MissingTemplates.Add("NoSuchWindow");
            var label = Screen.Add(new Label { Template = "EscMenuLabelTextTemplate", Text = "x" }, Game.Instance);
            Expect.True(label.Handle != null, "made all the same");
            var fake = frames.FrameOf(label.Handle!);
            Expect.Equal("TEXT", fake.Type, "by its type");
            Expect.Equal("x", fake.Text, "with its text");
            Expect.True(world.Logged.Exists(line => line.Contains("EscMenuLabelTextTemplate")), "reported: " + string.Join(" | ", world.Logged));
            var panel = Screen.Add(new Panel { Template = "NoSuchWindow" }, Game.Instance);
            var window = frames.FrameOf(panel.Handle!);
            Expect.Equal("BACKDROP", window.Type, "an empty panel");
            Expect.Equal("UI\\Widgets\\EscMenu\\Human\\blank-background.blp", window.Texture, "painted blank");
            Expect.True(frames.Frames.Find(f => f.Filled == window) == null, "no catcher over nothing");
        }

        /// <summary>The screen spans this machine's window, x 0.4 - 0.3 * w / h to 0.4 + 0.3 * w / h, and follows it when it changes, checked every half second.</summary>
        public static void ScreenEdges(FakeWorld world)
        {
            var frames = Installed();
            frames.WindowWidth = 1920;
            frames.WindowHeight = 1080;
            Screen.Add(new Panel(), Game.Instance);
            var screen = frames.Frames.Find(f => f.Under == "ConsoleUIBackdrop")!;
            Expect.Near(0.4f - 0.3f * 1920.0f / 1080.0f, screen.Left, "the window's left edge");
            Expect.Near(0.4f + 0.3f * 1920.0f / 1080.0f, screen.Right, "the window's right edge");
            Expect.Near(0.0f, screen.Bottom, "the bottom");
            Expect.Near(0.6f, screen.Top, "the top");
            frames.WindowWidth = 1280;
            frames.WindowHeight = 1024;
            world.Advance(0.5f);
            Expect.Near(0.4f - 0.3f * 1280.0f / 1024.0f, screen.Left, "follows the window");
            Expect.Near(0.4f + 0.3f * 1280.0f / 1024.0f, screen.Right, "both edges");
            frames.WindowHeight = 0;
            world.Advance(0.5f);
            Expect.Near(0.4f - 0.3f * 1280.0f / 1024.0f, screen.Left, "a window with no height is left as it was");
            Screen.AddOver(new Panel(), Game.Instance);
            var over = frames.Frames.Find(f => f.Under == "GameUI")!;
            Expect.Near(0.0f, over.Left, "the layer over the HUD stays in the 4:3 middle");
        }
    }
}
