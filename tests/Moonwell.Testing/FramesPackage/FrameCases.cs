using System.Collections.Generic;

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

        /// <summary>ShowNameOf draws this machine's own name, a hero's proper name, else the unit's; Text reads the type's name; Text set after shows the shared text again.</summary>
        public static void ShowNameOf(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label(), Game.Instance);
            var footman = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.RenameUnit(footman, "Footman here");
            var unit = Unit.Of(footman)!;
            label.ShowNameOf(unit);
            Expect.Equal("Footman here", frames.FrameOf(label.Handle!).Text, "the unit's name, this machine's");
            Expect.Equal(unit.Default.Name, label.Text, "Text reads the type's name");
            var paladin = world.NewUnit(ObjectTypes.FourCC("Hpal"), hero: true);
            world.SetProperName(paladin, "Uther");
            label.ShowNameOf(Unit.Of(paladin)!);
            Expect.Equal("Uther", frames.FrameOf(label.Handle!).Text, "a hero's proper name");
            label.Text = "shared";
            Expect.Equal("shared", frames.FrameOf(label.Handle!).Text, "Text shows the shared text again");
        }

        /// <summary>A label's Scale sizes its text only: its offset, ties and size are given divided by it, again when it changes; a panel's are given as set.</summary>
        public static void LabelScaleKeepsPlace(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Anchor = FramePoint.TopLeft, Offset = new Vector2(0.08f, -0.04f), Width = 0.2f, Height = 0.02f, Scale = 0.8f }, Game.Instance);
            var fake = frames.FrameOf(label.Handle!);
            Expect.Near(0.1f, fake.Points[0].X, "the offset's x over the scale");
            Expect.Near(-0.05f, fake.Points[0].Y, "the offset's y over the scale");
            Expect.Near(0.25f, fake.Width, "the width over the scale");
            Expect.Near(0.025f, fake.Height, "the height over the scale");
            Expect.Near(0.8f, fake.Scale, "the game's scale");
            Expect.Near(0.08f, label.Offset.X, "Offset reads as set");
            Expect.Near(0.2f, label.Width, "Width reads as set");
            label.Scale = 2f;
            Expect.Near(0.04f, fake.Points[0].X, "placed again for the new scale");
            Expect.Near(0.1f, fake.Width, "sized again for the new scale");
            var other = Screen.Add(new Panel(), Game.Instance);
            label.Attach(FramePoint.Left, other, FramePoint.Right, 0.02f, 0f);
            Expect.Near(0.01f, fake.Points[0].X, "a tie's offset over the scale");
            var panel = Screen.Add(new Panel { Offset = new Vector2(0.08f, 0f), Width = 0.2f, Scale = 0.8f }, Game.Instance);
            Expect.Near(0.08f, frames.FrameOf(panel.Handle!).Points[0].X, "a panel's offset as set");
            Expect.Near(0.2f, frames.FrameOf(panel.Handle!).Width, "a panel's width as set");
        }

        /// <summary>Add with a place and size puts the child from the parent's top left, y down, in Screen.DesignHeight's units; above 0 only.</summary>
        public static void AddPlaced(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var game = panel.Add(new Panel(), 0.01f, 0.02f, 0.1f, 0.05f);
            var fake = frames.FrameOf(game.Handle!);
            Expect.Equal(0, fake.Points[0].Point, "its top left");
            Expect.Equal(0, fake.Points[0].OtherPoint, "at the parent's top left");
            Expect.Near(0.01f, fake.Points[0].X, "x in the game's units unless set");
            Expect.Near(-0.02f, fake.Points[0].Y, "y down");
            Expect.Near(0.1f, fake.Width, "the width");
            Expect.Near(0.05f, fake.Height, "the height");
            Screen.DesignHeight = 768f;
            try
            {
                var pixels = panel.Add(new Panel(), 128f, 64f, 256f, 32f);
                fake = frames.FrameOf(pixels.Handle!);
                Expect.Near(0.1f, fake.Points[0].X, "128 px");
                Expect.Near(-0.05f, fake.Points[0].Y, "64 px down");
                Expect.Near(0.2f, fake.Width, "256 px wide");
                Expect.Near(0.025f, fake.Height, "32 px high");
                Expect.Near(0.1f, pixels.Offset.X, "Offset reads the game's units");
                Screen.DesignHeight = 0f;
                Expect.Near(768f, Screen.DesignHeight, "0 refused");
                Expect.True(world.Logged.Exists(line => line.Contains("DesignHeight")), "reported: " + string.Join(" | ", world.Logged));
            }
            finally
            {
                Screen.DesignHeight = 0.6f;
            }
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
            var catcher = frames.Frames.Find(f => f.Filled == fake);
            Expect.True(catcher != null && !catcher.Enabled, "its catcher made, turned off");
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

        /// <summary>The next frame of a kind and template under the same parent takes an ended one, with none of the old one's values, points or text.</summary>
        public static void PoolReuse(FakeWorld world)
        {
            var frames = Installed();
            var old = Screen.Add(new Label { Text = "old", Alignment = FramePoint.Center, Width = 0.1f, Height = 0.05f, Scale = 2.0f, Alpha = 10, Level = 3, Enabled = true, Offset = new Vector2(0.1f, 0.2f) }, Game.Instance);
            old.ShowLocal("mine");
            var oldHandle = old.Handle;
            old.End();
            var panel = Screen.Add(new Panel(), Game.Instance);
            panel.End();
            var made = frames.Frames.Count;
            var next = Screen.Add(new Label(), Game.Instance);
            Expect.Same(oldHandle, next.Handle, "the ended label given out again");
            Expect.Equal(made, frames.Frames.Count, "nothing new made");
            var fake = frames.FrameOf(next.Handle!);
            var screen = frames.Frames.Find(f => f.Under == "ConsoleUIBackdrop");
            Expect.Same(screen, fake.Parent, "still under the screen's root");
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
            var catcher = frames.Frames.Find(f => f.Filled == window);
            Expect.True(catcher != null && !catcher.Enabled, "its catcher turned off");
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

        /// <summary>The install loads the TOC the build writes into the map, so the game's templates that need one (a slider's, a text box's) can be made.</summary>
        public static void InstallLoadsToc(FakeWorld world)
        {
            var frames = Installed();
            Expect.Equal(2, frames.LoadedTocs.Count, "two TOCs loaded");
            Expect.Equal("MoonwellFrames.toc", frames.LoadedTocs[0], "the build's, with the game's templates");
            Expect.Equal("MoonwellMap.toc", frames.LoadedTocs[1], "then the map's own .fdf files");
        }

        /// <summary>Checks a frame has one point: its point tied to another frame's, moved by an offset.</summary>
        internal static void ExpectPoint(FakeFrame frame, int point, FakeFrame other, int otherPoint, float x, float y, string what)
        {
            Expect.Equal(1, frame.Points.Count, what + ": one point");
            Expect.Equal(point, frame.Points[0].Point, what + ": its point");
            Expect.Same(other, frame.Points[0].Other, what + ": the other frame");
            Expect.Equal(otherPoint, frame.Points[0].OtherPoint, what + ": the other's point");
            Expect.Near(x, frame.Points[0].X, what + ": x");
            Expect.Near(y, frame.Points[0].Y, what + ": y");
        }

        /// <summary>A row draws nothing and puts the frames added to it left to right: the first at its left plus the padding, each next one's left at the one before's right plus the gap.</summary>
        public static void Row(FakeWorld world)
        {
            var frames = Installed();
            var row = Screen.Add(new Row { Width = 0.5f, Height = 0.05f, Gap = 0.01f, Padding = 0.02f }, Game.Instance);
            var a = row.Add(new Label { Text = "a", Width = 0.1f });
            var b = row.Add(new Label { Text = "b", Width = 0.1f });
            var c = row.Add(new Label { Text = "c", Width = 0.1f });
            var fr = frames.FrameOf(row.Handle!);
            Expect.Equal("UI\\Widgets\\EscMenu\\Human\\blank-background.blp", fr.Texture, "draws nothing");
            ExpectPoint(frames.FrameOf(a.Handle!), 3, fr, 3, 0.02f, 0.0f, "the first at the row's left");
            ExpectPoint(frames.FrameOf(b.Handle!), 3, frames.FrameOf(a.Handle!), 5, 0.01f, 0.0f, "the next after it");
            ExpectPoint(frames.FrameOf(c.Handle!), 3, frames.FrameOf(b.Handle!), 5, 0.01f, 0.0f, "and the next");
        }

        /// <summary>A column, its frames added before it is on the screen, puts them top to bottom.</summary>
        public static void Column(FakeWorld world)
        {
            var frames = Installed();
            var column = new Column { Gap = 0.01f, Padding = 0.02f };
            var a = column.Add(new Label { Text = "a" });
            var b = column.Add(new Label { Text = "b" });
            Screen.Add(column, Game.Instance);
            var fc = frames.FrameOf(column.Handle!);
            ExpectPoint(frames.FrameOf(a.Handle!), 1, fc, 1, 0.0f, -0.02f, "the first at the column's top");
            ExpectPoint(frames.FrameOf(b.Handle!), 1, frames.FrameOf(a.Handle!), 7, 0.0f, -0.01f, "the next under it");
        }

        /// <summary>A grid puts its frames in cells, left to right, then the next line; when one ends, those after it move up a cell.</summary>
        public static void Grid(FakeWorld world)
        {
            var frames = Installed();
            var grid = Screen.Add(new Grid { Columns = 2, CellWidth = 0.1f, CellHeight = 0.05f, Gap = 0.01f, Padding = 0.02f }, Game.Instance);
            var a = grid.Add(new Image());
            var b = grid.Add(new Image());
            var c = grid.Add(new Image());
            var fg = frames.FrameOf(grid.Handle!);
            ExpectPoint(frames.FrameOf(b.Handle!), 0, fg, 0, 0.13f, -0.02f, "the second cell");
            ExpectPoint(frames.FrameOf(c.Handle!), 0, fg, 0, 0.02f, -0.08f, "the next line");
            a.End();
            ExpectPoint(frames.FrameOf(b.Handle!), 0, fg, 0, 0.02f, -0.02f, "moved up to the first cell");
            ExpectPoint(frames.FrameOf(c.Handle!), 0, fg, 0, 0.13f, -0.02f, "and the next to the second");
        }

        /// <summary>When a row's frame ends, the next one is tied to the one before it, and to no ended frame; a new gap lays them out again.</summary>
        public static void RowClosesUp(FakeWorld world)
        {
            var frames = Installed();
            var row = Screen.Add(new Row { Gap = 0.01f }, Game.Instance);
            var a = row.Add(new Label { Text = "a" });
            var b = row.Add(new Label { Text = "b" });
            var c = row.Add(new Label { Text = "c" });
            b.End();
            ExpectPoint(frames.FrameOf(c.Handle!), 3, frames.FrameOf(a.Handle!), 5, 0.01f, 0.0f, "tied to the one before the ended one");
            row.Gap = 0.03f;
            ExpectPoint(frames.FrameOf(c.Handle!), 3, frames.FrameOf(a.Handle!), 5, 0.03f, 0.0f, "the new gap");
            row.End();
            Expect.Equal(false, frames.FrameOf(c.Handle!).Shown, "its frames end with it");
        }

        // The game's frame event numbers (common.j).
        private const int Wheel = 6;

        /// <summary>The wheel gives notches: the game's 120 a notch.</summary>
        public static void WheelNotches(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            var notches = 0;
            label.Wheel += (who, n) => notches += n;
            frames.Fire(frames.FrameOf(label.Handle!), Wheel, 0, -240.0f);
            Expect.Equal(-2, notches, "two notches down");
        }

        /// <summary>An event for a frame that has ended reaches no handler (Review focus 2).</summary>
        public static void EventForEndedFrame(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            var runs = 0;
            label.Wheel += (who, n) => runs++;
            var fake = frames.FrameOf(label.Handle!);
            label.End();
            frames.Fire(fake, Wheel, 0, 120.0f);
            Expect.Equal(0, runs, "nothing for an ended frame");
        }

        /// <summary>An event for a game frame the pool gave out again reaches only its new owner's handlers (Review focus 2).</summary>
        public static void EventForReusedFrame(FakeWorld world)
        {
            var frames = Installed();
            var old = Screen.Add(new Label { Text = "old" }, Game.Instance);
            var seen = new List<string>();
            old.Wheel += (who, n) => seen.Add("old");
            old.End();
            var next = Screen.Add(new Label { Text = "next" }, Game.Instance);
            Expect.Same(old.Handle, next.Handle, "the same game frame");
            next.Wheel += (who, n) => seen.Add("next");
            var fake = frames.FrameOf(next.Handle!);
            frames.Fire(fake, Wheel, 0, 120.0f);
            Expect.Equal("next", string.Join(", ", seen), "only the new owner's");
            Expect.Equal(1, fake.Listened.FindAll(kind => kind == Wheel).Count, "registered once");
        }

        /// <summary>A frame for one player is used only by that player: another player's event runs nothing.</summary>
        public static void EventFromAnotherPlayer(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance, shownTo: Player.One);
            var runs = 0;
            label.Wheel += (who, n) => runs++;
            frames.Fire(frames.FrameOf(label.Handle!), Wheel, 1, 120.0f);
            Expect.Equal(0, runs, "nothing from player 2");
        }

        /// <summary>A handler that throws is reported, and the frame's next handler still runs (Review focus 3).</summary>
        public static void HandlerThrows(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            var ran = false;
            label.Wheel += (who, n) => throw new System.InvalidOperationException("broken");
            label.Wheel += (who, n) => ran = true;
            frames.Fire(frames.FrameOf(label.Handle!), Wheel, 0, 120.0f);
            Expect.True(ran, "the next handler ran");
            Expect.True(world.Logged.Exists(line => line.Contains("Wheel") && line.Contains("broken")), "reported: " + string.Join(" | ", world.Logged));
        }

        /// <summary>A label listened to for the mouse takes it, so its events arrive; one taken off again runs nothing.</summary>
        public static void ListeningLabelTakesMouse(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            var fake = frames.FrameOf(label.Handle!);
            Expect.Equal(false, fake.Enabled, "lets clicks through");
            var runs = 0;
            System.Action<Player, int> handler = (who, n) => runs++;
            label.Wheel += handler;
            Expect.Equal(true, fake.Enabled, "takes the mouse once listened to");
            Expect.Equal(true, label.Enabled, "and reads so");
            label.Wheel -= handler;
            frames.Fire(fake, Wheel, 0, 120.0f);
            Expect.Equal(0, runs, "taken off");
        }

        /// <summary>An image listened to for the mouse gets a catcher over it, an empty text, which takes the mouse and gets the events, as an image lets them through.</summary>
        public static void ListeningImageGetsCatcher(FakeWorld world)
        {
            var frames = Installed();
            var image = Screen.Add(new Image { Texture = "x.blp" }, Game.Instance);
            var fake = frames.FrameOf(image.Handle!);
            Expect.True(frames.Frames.Find(f => f.Filled == fake) == null, "no catcher while nothing listens");
            var runs = 0;
            image.Wheel += (who, n) => runs++;
            var catcher = frames.Frames.Find(f => f.Filled == fake);
            Expect.True(catcher != null, "a catcher once listened to");
            Expect.Equal("TEXT", catcher!.Type, "an empty text");
            frames.Fire(catcher, Wheel, 0, 120.0f);
            Expect.Equal(1, runs, "its events reach the image's handlers");
        }

        private const int Click = 1;

        /// <summary>A button is the game's dialog button with its text, registered for clicks from the start, and a click reaches its handlers with the player.</summary>
        public static void Button(FakeWorld world)
        {
            var frames = Installed();
            var button = Screen.Add(new Button { Text = "Buy" }, Game.Instance);
            var fake = frames.FrameOf(button.Handle!);
            Expect.Equal("ScriptDialogButton", fake.Template, "the dialog button");
            Expect.Equal("Buy", fake.Text, "its text");
            Expect.True(fake.Listened.Contains(Click), "registered for clicks");
            var seen = new List<int>();
            button.Clicked += who => seen.Add(who.Slot);
            frames.Fire(fake, Click, 0);
            Expect.Equal("0", string.Join(",", seen), "the click with its player");
            button.Text = "Sold";
            Expect.Equal("Sold", fake.Text, "a new text");
        }

        /// <summary>Two clicks by one player within 0.3 s are a double click as well as two clicks; one 0.4 s after the last is not.</summary>
        public static void DoubleClick(FakeWorld world)
        {
            var frames = Installed();
            var button = Screen.Add(new Button { Text = "x" }, Game.Instance);
            var fake = frames.FrameOf(button.Handle!);
            int clicks = 0, doubles = 0;
            button.Clicked += who => clicks++;
            button.DoubleClicked += who => doubles++;
            frames.Fire(fake, Click, 0);
            frames.Fire(fake, Click, 0);
            Expect.Equal(1, doubles, "a double click");
            world.Advance(0.4f);
            frames.Fire(fake, Click, 0);
            Expect.Equal(1, doubles, "not after 0.4 s");
            world.Advance(0.1f);
            frames.Fire(fake, Click, 0);
            Expect.Equal(2, doubles, "within 0.3 s again");
            Expect.Equal(4, clicks, "every click a click");
        }

        /// <summary>After a click the keyboard is given back on the clicking player's machine only (decision 161), with or without a handler.</summary>
        public static void ClickGivesKeyboardBack(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var button = Screen.Add(new Button { Text = "x" }, Game.Instance);
            var fake = frames.FrameOf(button.Handle!);
            frames.Fire(fake, Click, 0);
            Expect.Equal(1, fake.KeyboardReleases, "given back on the clicker's machine");
            frames.Fire(fake, Click, 1);
            Expect.Equal(1, fake.KeyboardReleases, "not on this machine for another player's click");
        }

        /// <summary>An icon button is a game button with its icon drawn over it, and clicks as a button does.</summary>
        public static void IconButton(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var button = Screen.Add(new IconButton { Icon = "ReplaceableTextures\\CommandButtons\\BTNFootman.blp", Width = 0.04f, Height = 0.04f }, Game.Instance);
            var fake = frames.FrameOf(button.Handle!);
            Expect.Equal("GLUEBUTTON", fake.Type, "a game button");
            var icon = frames.Frames.Find(f => f.Parent == fake && f.Filled == fake);
            Expect.True(icon != null, "an icon over it");
            Expect.Equal("BACKDROP", icon!.Type, "an image");
            Expect.Equal("ReplaceableTextures\\CommandButtons\\BTNFootman.blp", icon.Texture, "its icon");
            var clicks = 0;
            button.Clicked += who => clicks++;
            frames.Fire(fake, Click, 0);
            Expect.Equal(1, clicks, "clicked");
            Expect.Equal(1, fake.KeyboardReleases, "the keyboard given back");
            button.Enabled = false;
            Expect.Equal(@"ReplaceableTextures\CommandButtonsDisabled\DISBTNFootman.blp", icon.Texture, "turned off, the game's grey copy");
            Expect.Equal(255, icon.Alpha, "not faded");
            button.Icon = "x.blp";
            Expect.Equal("x.blp", icon.Texture, "a new icon");
            Expect.Equal(Moonwell.IconButton.DisabledAlpha, icon.Alpha, "one the game has no grey copy of is faded");
            button.Enabled = true;
            Expect.Equal(255, icon.Alpha, "turned on, bright again");
            button.Icon = "";
            Expect.Equal(Moonwell.Image.Blank, icon.Texture, "no icon draws nothing, not the game's missing texture");
            var empty = Screen.Add(new IconButton(), Game.Instance);
            Expect.Equal(Moonwell.Image.Blank, frames.Frames.Find(f => f.Parent == frames.FrameOf(empty.Handle!) && f.Type == "BACKDROP")!.Texture, "made with no icon, it draws nothing");
        }

        private const int Checked = 7, TextChanged = 9, SliderChanged = 13, TextEntered = 16;

        /// <summary>A check box is a game button Moonwell draws from the game's textures: a click toggles it, draws the check, gives the keyboard back and runs Toggled; set by the map, it draws and runs nothing.</summary>
        public static void CheckBox(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var box = Screen.Add(new CheckBox(), Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(box.Handle!);
            Expect.Equal("GLUEBUTTON", fake.Type, "a game button");
            Expect.Near(0.024f, fake.Width, "the game's check box's size");
            var back = frames.Frames.Find(f => f.Parent == fake && f.Texture.EndsWith("checkbox-background.blp"));
            var check = frames.Frames.Find(f => f.Parent == fake && f.Texture.EndsWith("checkbox-check.blp"));
            Expect.True(back != null && check != null, "its background and its check");
            Expect.Equal(false, check!.Shown, "unchecked unless set");
            var seen = new List<string>();
            box.Toggled += (who, on) => seen.Add(who.Slot + " " + on);
            frames.Fire(fake, Click, 0);
            Expect.Equal("0 True", string.Join(", ", seen), "toggled on by player 1");
            Expect.True(box.Checked, "reads checked");
            Expect.Equal(true, check.Shown, "the check drawn");
            Expect.Equal(1, fake.KeyboardReleases, "the keyboard given back");
            box.Checked = false;
            Expect.Equal(false, check.Shown, "set by the map, drawn");
            Expect.Equal(1, seen.Count, "and no event");
        }

        /// <summary>A slider is the game's, given its range, step and value; a change reaches ValueChanged with the player and the value, which Value then reads.</summary>
        public static void Slider(FakeWorld world)
        {
            var frames = Installed();
            var slider = Screen.Add(new Slider { Min = 0.0f, Max = 10.0f, Step = 1.0f, Value = 3.0f }, Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(slider.Handle!);
            Expect.Equal("EscMenuSliderTemplate", fake.Template, "the game's slider");
            Expect.Near(10.0f, fake.Max, "its range");
            Expect.Near(1.0f, fake.Step, "its step");
            Expect.Near(3.0f, fake.Value, "its value");
            var seen = new List<float>();
            slider.ValueChanged += (who, value) => seen.Add(value);
            frames.Fire(fake, SliderChanged, 0, 5.0f);
            Expect.Equal(1, seen.Count, "a change");
            Expect.Near(5.0f, slider.Value, "Value reads it");
        }

        /// <summary>A text box is the game's, given its text and limit; typing reaches TextChanged and Enter reaches Submitted, with the whole text.</summary>
        public static void TextBox(FakeWorld world)
        {
            var frames = Installed();
            var box = Screen.Add(new TextBox { Text = "abc", MaxLength = 10 }, Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(box.Handle!);
            Expect.Equal("EscMenuEditBoxTemplate", fake.Template, "the game's text box");
            Expect.Equal("abc", fake.Text, "its text");
            Expect.Equal(10, fake.TextLimit, "its limit");
            var seen = new List<string>();
            box.TextChanged += (who, text) => seen.Add("changed " + text);
            box.Submitted += (who, text) => seen.Add("submitted " + text);
            frames.Fire(fake, TextChanged, 0, 0.0f, "abcx");
            frames.Fire(fake, TextEntered, 0, 0.0f, "abcx");
            Expect.Equal("changed abcx, submitted abcx", string.Join(", ", seen), "typed, then Enter");
            Expect.Equal("abcx", box.Text, "Text reads it");
        }

        /// <summary>A value control made for everyone is reported in a development build: it is one player's only (decision 156).</summary>
        public static void ValueControlForEveryoneReported(FakeWorld world)
        {
            Installed();
            Screen.Add(new Slider(), Game.Instance);
#if DEBUG
            Expect.True(world.Logged.Exists(line => line.Contains("one player")), "reported: " + string.Join(" | ", world.Logged));
#endif
        }

        /// <summary>The event the game sends back for a value the map set, and a value the same as the last, run nothing.</summary>
        public static void SetValueRunsNothing(FakeWorld world)
        {
            var frames = Installed();
            var slider = Screen.Add(new Slider(), Game.Instance, shownTo: Player.One);
            var box = Screen.Add(new TextBox(), Game.Instance, shownTo: Player.One);
            var runs = 0;
            slider.ValueChanged += (who, value) => runs++;
            box.TextChanged += (who, text) => runs++;
            slider.Value = 7.0f;
            frames.Fire(frames.FrameOf(slider.Handle!), SliderChanged, 0, 7.0f);
            box.Text = "q";
            frames.Fire(frames.FrameOf(box.Handle!), TextChanged, 0, 0.0f, "q");
            Expect.Equal(0, runs, "the echoes run nothing");
            frames.Fire(frames.FrameOf(slider.Handle!), SliderChanged, 0, 8.0f);
            frames.Fire(frames.FrameOf(slider.Handle!), SliderChanged, 0, 8.0f);
            Expect.Equal(1, runs, "a repeat runs nothing");
        }

        /// <summary>A slider dragged fast: each change runs its handler once, and no frame is made for it (Review focus 1).</summary>
        public static void SliderFast(FakeWorld world)
        {
            var frames = Installed();
            var slider = Screen.Add(new Slider { Max = 1000.0f }, Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(slider.Handle!);
            var made = frames.Frames.Count;
            var runs = 0;
            var read = 0.0f;
            slider.ValueChanged += (who, value) => { runs++; read = slider.Value; };
            for (var i = 1; i <= 200; i++)
            {
                frames.Fire(fake, SliderChanged, 0, i);
            }

            Expect.Equal(200, runs, "once a change");
            Expect.Near(200.0f, read, "Value reads the event's inside the handler");
            Expect.Equal(made, frames.Frames.Count, "nothing made");
        }

        /// <summary>A text box keeps the keyboard while typed in: typing and Enter give nothing back.</summary>
        public static void TextBoxKeepsKeyboard(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var box = Screen.Add(new TextBox(), Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(box.Handle!);
            frames.Fire(fake, TextChanged, 0, 0.0f, "a");
            frames.Fire(fake, TextEntered, 0, 0.0f, "a");
            Expect.Equal(0, fake.KeyboardReleases, "kept");
        }

        /// <summary>A text area is the game's: AddLine adds a line, which the game shows at the bottom (seen), and Text reads them all.</summary>
        public static void TextArea(FakeWorld world)
        {
            var frames = Installed();
            var area = Screen.Add(new TextArea { Text = "one" }, Game.Instance);
            var fake = frames.FrameOf(area.Handle!);
            Expect.Equal("EscMenuTextAreaTemplate", fake.Template, "the game's text area");
            area.AddLine("two");
            Expect.Equal("one\ntwo", fake.Text, "the line added");
            Expect.Equal("one\ntwo", area.Text, "Text reads them all");
            area.Text = "";
            area.AddLine("three");
            Expect.Equal("three", area.Text, "a line in an empty one has no break before it");
        }

        /// <summary>A progress bar draws its texture over the share of its width its value is of its range, kept within the range.</summary>
        public static void ProgressBar(FakeWorld world)
        {
            var frames = Installed();
            var bar = Screen.Add(new ProgressBar { Width = 0.2f, Height = 0.02f, Texture = "x.blp", Value = 0.25f }, Game.Instance);
            var fb = frames.FrameOf(bar.Handle!);
            var fill = frames.Frames.Find(f => f.Parent == fb && f.Texture == "x.blp");
            Expect.True(fill != null, "the fill");
            Expect.Equal(2, fill!.Points.Count, "tied by its left corners, so it is as high as the bar");
            Expect.True(fill.Points[0].Other == fb && fill.Points[0].Point == 0 && fill.Points[0].OtherPoint == 0, "its top left at the bar's");
            Expect.True(fill.Points[1].Other == fb && fill.Points[1].Point == 6 && fill.Points[1].OtherPoint == 6, "its bottom left at the bar's");
            Expect.Near(0.05f, fill.Width, "a quarter of the width");
            Expect.Near(0.02f, fill.Height, "the bar's height");
            bar.Value = 2.0f;
            Expect.Near(1.0f, bar.Value, "kept within the range");
            Expect.Near(0.2f, fill.Width, "the whole width");
            bar.Width = 0.4f;
            Expect.Near(0.4f, fill.Width, "a new width");
            bar.Max = 4.0f;
            Expect.Near(0.1f, fill.Width, "a new range");
        }

        /// <summary>A bar's look (its path ends in .Bar.blp) is cut: the fill shows the build's step copy nearest its share in 64ths, the look itself when full and nothing when empty, painted again only when the step changes (decision 322); any other texture is squashed whole.</summary>
        public static void ProgressBarCutsABarLook(FakeWorld world)
        {
            var frames = Installed();
            var bar = Screen.Add(new ProgressBar { Width = 0.2f, Height = 0.02f, Texture = "w\\Bar.Green.Bar.blp", Value = 0.25f }, Game.Instance);
            var fb = frames.FrameOf(bar.Handle!);
            var fill = frames.Frames.Find(f => f.Parent == fb && f.Type == "BACKDROP")!;
            Expect.Equal("w\\Bar.Green.Bar16.blp", fill.Texture, "a quarter: 16 of 64");
            Expect.Near(0.05f, fill.Width, "sized as before");
            bar.Value = 0.26f;
            Expect.Equal("w\\Bar.Green.Bar17.blp", fill.Texture, "the nearest step");
            bar.Value = 1.0f;
            Expect.Equal("w\\Bar.Green.Bar.blp", fill.Texture, "full: the look itself");
            bar.Value = 0.0f;
            Expect.Equal(Moonwell.Image.Blank, fill.Texture, "empty: nothing");
            bar.Texture = "w\\Plain.blp";
            bar.Value = 0.5f;
            Expect.Equal("w\\Plain.blp", fill.Texture, "any other texture whole");
        }

        /// <summary>A label's colour is a colour code around its text, as a label turned off (so clicks pass) draws the game's text colour grey (seen 11-07, I39); the label's own codes keep theirs, its machine's own text takes the colour, and a label given out again by the pool is white unless set.</summary>
        public static void LabelColour(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x", Color = Color.Red }, Game.Instance);
            var fake = frames.FrameOf(label.Handle!);
            Expect.Equal("|cffff0000x|r", fake.Text, "red, by a colour code");
            Expect.True(label.Color.Equals(Color.Red), "reads red");
            Expect.Equal("x", label.Text, "its text reads without the code");
            Expect.True(new Label().Color.Equals(Color.White), "white unless set");
            label.Text = "a |cff00ff00b|r c";
            Expect.Equal("|cffff0000a |cff00ff00b|r|cffff0000 c|r", fake.Text, "a code of its own draws over it, then its colour again");
            label.ShowLocal("mine");
            label.Color = Color.Blue;
            Expect.Equal("|cff0000ffmine|r", fake.Text, "this machine's own text keeps it, in the new colour");
            label.Text = "z";
            Expect.Equal("|cff0000ffz|r", fake.Text, "the shared text again");
            label.Color = Color.White;
            Expect.Equal("z", fake.Text, "white has no code");
            label.Color = Color.Red;
            label.End();
            var next = Screen.Add(new Label { Text = "y" }, Game.Instance);
            Expect.Same(label.Handle, next.Handle, "the same game frame");
            Expect.Equal("y", fake.Text, "white again");
        }

        /// <summary>A tooltip is a bordered panel with its text, made in the frame's slot on its layer for the frame's player, shown by the game while the mouse is on the frame, above it; the frame takes the mouse for it, and is registered for no event, as that makes the tooltip and the cursor flicker (seen).</summary>
        public static void Tooltip(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            label.Tooltip = new Tooltip { Text = "A hint" };
            var fake = frames.FrameOf(label.Handle!);
            var slot = fake.Tip;
            Expect.True(slot != null, "a slot given to the label");
            Expect.True(slot!.Type == "BACKDROP" && slot.Texture.EndsWith("blank-background.blp") && slot.Width == 0.0f && slot.Height == 0.0f, "a blank backdrop of no size");
            Expect.Equal("ConsoleUIBackdrop", slot.Parent!.Under, "on the screen layer, its frame's");
            var tip = WindowOf(frames, fake);
            Expect.True(tip != null, "the tooltip in it");
            Expect.Equal("EscMenuBackdrop", tip!.Template, "a bordered panel");
            Expect.True(frames.Frames.Exists(f => f.Parent == tip && f.Text == "A hint"), "its text");
            ExpectPoint(tip, 7, fake, 1, 0.0f, 0.01f, "above the label");
            Expect.Equal(true, fake.Enabled, "the label takes the mouse");
            Expect.Equal(0, fake.Listened.Count, "registered for no event");
            Expect.Equal("A hint", label.Tooltip!.Text, "reads its text");
        }

        /// <summary>A tooltip placed above its frame is moved, on the viewer's machine as the game shows it, to the side of the frame with room, judged from the cursor: below it near the top bar, above it lower down, and kept in at the screen's sides; one the map placed is left alone (I43).</summary>
        public static void TooltipOnScreen(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            frames.WindowWidth = 1600;
            frames.WindowHeight = 900;
            var button = Screen.Add(new IconButton { Icon = "x.blp", Width = 0.04f, Height = 0.04f }, Game.Instance);
            var tip = new Tooltip { Text = "hint", Width = 0.24f, Height = 0.12f };
            button.Tooltip = tip;
            var fake = frames.FrameOf(button.Handle!);
            var slot = fake.Tip!;
            var shown = frames.FrameOf(tip.Handle!);
            var other = Screen.Add(new IconButton { Icon = "y.blp" }, Game.Instance);
            other.Tooltip = new Tooltip { Text = "mine", Anchor = FramePoint.Bottom };
            var mine = frames.FrameOf(other.Tooltip.Handle!);
            var mineBefore = mine.Points.Count;
            slot.Shown = false;
            frames.FrameOf(other.Handle!).Tip!.Shown = false;
            world.Advance(0.1f);
            Hover(world, frames, slot, 800, 60);
            ExpectPoint(shown, 1, fake, 7, 0.0f, -0.01f, "below its frame near the top bar");
            Hover(world, frames, slot, 800, 600);
            ExpectPoint(shown, 7, fake, 1, 0.0f, 0.01f, "above it lower down");
            Hover(world, frames, slot, 1590, 600);
            ExpectPoint(shown, 8, fake, 2, 0.0f, 0.01f, "kept in at the right side");
            Hover(world, frames, slot, 10, 60);
            ExpectPoint(shown, 0, fake, 6, 0.0f, -0.01f, "kept in at the left side, below");
            Hover(world, frames, frames.FrameOf(other.Handle!).Tip!, 800, 60);
            Expect.Equal(mineBefore, mine.Points.Count, "the map's own place left alone");
        }

        // The game shows a slot while the cursor is on its frame: the cursor moves there, the slot shows, and a check passes.
        private static void Hover(FakeWorld world, FakeFramesWorld frames, FakeFrame slot, int x, int y)
        {
            foreach (var each in frames.Frames)
            {
                if (each.TipOf != null)
                {
                    each.Shown = false;
                }
            }

            world.Advance(0.1f);
            frames.MouseX = x;
            frames.MouseY = y;
            slot.Shown = true;
            world.Advance(0.1f);
        }

        /// <summary>An image's tooltip is shown by a catcher over it, as an image takes no mouse; a tooltip set before the frame is added is made with it.</summary>
        public static void TooltipOnImage(FakeWorld world)
        {
            var frames = Installed();
            var image = new Image { Texture = "x.blp", Tooltip = new Tooltip { Text = "An image" } };
            Screen.Add(image, Game.Instance);
            var fake = frames.FrameOf(image.Handle!);
            var catcher = frames.Frames.Find(f => f.Filled == fake);
            Expect.True(catcher != null, "a catcher over the image");
            Expect.True(WindowOf(frames, catcher!) != null, "the tooltip on the catcher");
            Expect.Equal(true, catcher!.Enabled, "taking the mouse");
        }

        /// <summary>A tooltip on a frame for one player is that player's: made on every machine, shown on none other (Review focus 4).</summary>
        public static void TooltipForOnePlayer(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance, shownTo: Player.Two);
            label.Tooltip = new Tooltip { Text = "Theirs" };
            Expect.True(label.Tooltip!.Player == Player.Two, "the frame's player's");
            Expect.Equal(false, frames.FrameOf(label.Tooltip.Handle!).Shown, "hidden on this machine");
        }

        /// <summary>A tooltip ends with its frame; an ended frame that had one is given out again, to a frame with no tooltip showing none, and to one with its own showing that (Review focus 4).</summary>
        public static void TooltipEndsWithItsFrame(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            var tip = new Tooltip { Text = "Old" };
            label.Tooltip = tip;
            label.End();
            Expect.True(tip.HasEnded, "the tooltip ended with it");
            Expect.Equal(false, frames.FrameOf(tip.Handle!).Shown, "and is hidden");
            var plain = Screen.Add(new Label { Text = "y" }, Game.Instance);
            Expect.Same(label.Handle, plain.Handle, "a label with no tooltip is given it");
            ExpectNoTip(frames, frames.FrameOf(plain.Handle!), "and shows none");
            plain.End();
            var tipped = Screen.Add(new Label { Text = "z", Tooltip = new Tooltip { Text = "New" } }, Game.Instance);
            Expect.Same(label.Handle, tipped.Handle, "one with a tooltip of its own is too");
            var newTip = WindowOf(frames, frames.FrameOf(tipped.Handle!))!;
            Expect.True(frames.Frames.Exists(f => f.Parent == newTip && f.Text == "New"), "showing its own text");
            Expect.True(!frames.Frames.Exists(f => f.Parent == newTip && f.Text == "Old"), "and none of the old");
        }

        /// <summary>The tooltip the game shows for a frame: the window shown in the slot the frame was given; null when there is none.</summary>
        internal static FakeFrame? WindowOf(FakeFramesWorld frames, FakeFrame frame)
        {
            var slot = frame.Tip;
            return slot == null ? null : frames.Frames.Find(f => f.Parent == slot && f.Shown);
        }

        /// <summary>Checks the game shows no tooltip for a frame: it has no slot, or its slot shows nothing.</summary>
        private static void ExpectNoTip(FakeFramesWorld frames, FakeFrame frame, string what)
        {
            Expect.True(WindowOf(frames, frame) == null, what);
        }

        /// <summary>A tooltip taken away, or replaced, leaves the game showing none, or the new one; the old one ends.</summary>
        public static void TooltipCleared(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            var first = new Tooltip { Text = "First" };
            label.Tooltip = first;
            var second = new Tooltip { Text = "Second" };
            label.Tooltip = second;
            var fake = frames.FrameOf(label.Handle!);
            Expect.True(first.HasEnded, "the first ended");
            Expect.Same(frames.FrameOf(second.Handle!), WindowOf(frames, fake), "the second shown");
            label.Tooltip = null;
            Expect.True(second.HasEnded, "the second ended");
            ExpectNoTip(frames, fake, "none shown once taken away");
        }

        /// <summary>A tooltip the map ends itself is taken from its frame, which shows none.</summary>
        public static void TooltipEndedByMap(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            var tip = new Tooltip { Text = "Gone" };
            label.Tooltip = tip;
            tip.End();
            Expect.True(label.Tooltip == null, "the frame has none");
            ExpectNoTip(frames, frames.FrameOf(label.Handle!), "the game shows none");
        }

        /// <summary>An empty panel listened to after frames were added to it uses the catcher it made first, beneath them, so they still get their clicks.</summary>
        public static void LateListeningKeepsCatcherBeneath(FakeWorld world)
        {
            var frames = Installed();
            var row = Screen.Add(new Row { Width = 0.3f, Height = 0.05f }, Game.Instance, shownTo: Player.One);
            var button = row.Add(new Button { Text = "x" });
            var made = frames.Frames.Count;
            row.Wheel += (who, n) => { };
            Expect.Equal(made, frames.Frames.Count, "nothing new made");
            var fr = frames.FrameOf(row.Handle!);
            var catcher = frames.Frames.Find(f => f.Filled == fr)!;
            Expect.True(frames.Frames.IndexOf(catcher) < frames.Frames.IndexOf(frames.FrameOf(button.Handle!)), "the catcher made before the button, beneath it");
            Expect.Equal(true, catcher.Enabled, "and turned on");
        }

        /// <summary>A slider's range keeps its value within it, so the value the game sends back for the clamp runs nothing.</summary>
        public static void SliderRangeKeepsValue(FakeWorld world)
        {
            var frames = Installed();
            var slider = Screen.Add(new Slider { Value = 50.0f }, Game.Instance, shownTo: Player.One);
            var runs = 0;
            slider.ValueChanged += (who, value) => runs++;
            slider.Max = 10.0f;
            Expect.Near(10.0f, slider.Value, "kept within the new range");
            Expect.Near(10.0f, frames.FrameOf(slider.Handle!).Value, "given to the game");
            frames.Fire(frames.FrameOf(slider.Handle!), SliderChanged, 0, 10.0f);
            Expect.Equal(0, runs, "the game's clamp runs nothing");
            slider.Max = 100.0f;
            slider.Min = 20.0f;
            Expect.Near(20.0f, slider.Value, "a new low end too");
        }

        /// <summary>A click on a frame the map has turned off gives no keyboard back, which would turn it on again on that machine.</summary>
        public static void ClickOnDisabledGivesNothingBack(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var button = Screen.Add(new Button { Text = "x" }, Game.Instance);
            var fake = frames.FrameOf(button.Handle!);
            button.Enabled = false;
            frames.Fire(fake, Click, 0);
            Expect.Equal(0, fake.KeyboardReleases, "nothing given back");
            Expect.Equal(false, fake.Enabled, "still off");
        }

        /// <summary>A frame set to keep the keyboard gives nothing back at a click, so its tooltip does not blink (Sven, 11-03); one not set gives it back.</summary>
        public static void KeptKeyboardGivesNothingBack(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = Installed();
            var kept = Screen.Add(new Button { Text = "x", KeepsKeyboard = true }, Game.Instance);
            var given = Screen.Add(new Button { Text = "y" }, Game.Instance);
            Expect.Equal(false, given.KeepsKeyboard, "false unless set");
            frames.Fire(frames.FrameOf(kept.Handle!), Click, 0);
            frames.Fire(frames.FrameOf(given.Handle!), Click, 0);
            Expect.Equal(0, frames.FrameOf(kept.Handle!).KeyboardReleases, "nothing given back");
            Expect.Equal(1, frames.FrameOf(given.Handle!).KeyboardReleases, "the other gives it back");
        }

        /// <summary>A tooltip goes on its frame's layer: one over the HUD for a frame added over it, also when the frame was put in a panel before the panel was added.</summary>
        public static void TooltipOverLayer(FakeWorld world)
        {
            var frames = Installed();
            var window = new Panel();
            var button = window.Add(new Button { Text = "x", Tooltip = new Tooltip { Text = "Over" } });
            Screen.AddOver(window, Game.Instance);
            var slot = frames.FrameOf(button.Handle!).Tip!;
            Expect.Equal("GameUI", slot.Parent!.Under, "its slot over the HUD, its frame's layer");
        }

        /// <summary>Check 2's crash, done again: a label's tooltip taken away and ended, then a button with a tooltip, which shows its own in its own slot; nothing the game was given as one frame's tooltip goes to another, which crashes the game (seen 11-03).</summary>
        public static void TooltipNeverGivenTwice(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x", Tooltip = new Tooltip { Text = "Taken" } }, Game.Instance);
            label.Tooltip = null;
            label.End();
            var button = Screen.Add(new Button { Text = "y", Tooltip = new Tooltip { Text = "Given" } }, Game.Instance);
            var window = WindowOf(frames, frames.FrameOf(button.Handle!));
            Expect.True(window != null && frames.Frames.Exists(f => f.Parent == window && f.Text == "Given"), "the button shows its own");
        }

        /// <summary>Probe 4's crash, done again: a button's tooltip ends with the button, then a label with a tooltip shows its own, with no frame moved out of the old tooltip (seen 11-03).</summary>
        public static void TooltipLeftByEndedFrame(FakeWorld world)
        {
            var frames = Installed();
            var button = Screen.Add(new Button { Text = "x", Tooltip = new Tooltip { Text = "Old" } }, Game.Instance);
            button.End();
            var label = Screen.Add(new Label { Text = "y", Tooltip = new Tooltip { Text = "New" } }, Game.Instance);
            var window = WindowOf(frames, frames.FrameOf(label.Handle!));
            Expect.True(window != null && frames.Frames.Exists(f => f.Parent == window && f.Text == "New"), "the label shows its own");
        }

        /// <summary>Two frames whose tooltips are taken away share nothing the game was given as a tooltip, and show none.</summary>
        public static void TakenAwayTooltipsShareNothing(FakeWorld world)
        {
            var frames = Installed();
            var first = Screen.Add(new Label { Text = "a", Tooltip = new Tooltip { Text = "1" } }, Game.Instance);
            var second = Screen.Add(new Label { Text = "b", Tooltip = new Tooltip { Text = "2" } }, Game.Instance);
            first.Tooltip = null;
            second.Tooltip = null;
            ExpectNoTip(frames, frames.FrameOf(first.Handle!), "the first shows none");
            ExpectNoTip(frames, frames.FrameOf(second.Handle!), "the second shows none");
        }

        /// <summary>A tooltip given to a second frame, or to itself, is refused and reported: the first keeps it, made once, and the other gets none.</summary>
        public static void TooltipGivenTwiceRefused(FakeWorld world)
        {
            var frames = Installed();
            var a = Screen.Add(new Button { Text = "a" }, Game.Instance);
            var b = Screen.Add(new Button { Text = "b" }, Game.Instance);
            var tip = new Tooltip { Text = "Buy" };
            a.Tooltip = tip;
            var window = tip.Handle;
            var made = frames.Frames.Count;
            b.Tooltip = tip;
            Expect.True(b.Tooltip == null, "the second gets none");
            Expect.Same(window, tip.Handle, "made once");
            Expect.Equal(made, frames.Frames.Count, "nothing new made");
            Expect.Same(tip, a.Tooltip, "the first keeps it");
            Expect.True(world.Logged.Exists(line => line.Contains("Tooltip")), "reported: " + string.Join(" | ", world.Logged));
            var loose = new Tooltip { Text = "Loose" };
            loose.Tooltip = loose;
            Expect.True(loose.Tooltip == null, "nor to itself");
        }

        /// <summary>A frame added to a second frame, or put on the screen again, is refused and reported: it stays where it was, made once.</summary>
        public static void FrameAddedTwiceRefused(FakeWorld world)
        {
            var frames = Installed();
            var first = Screen.Add(new Panel(), Game.Instance);
            var second = Screen.Add(new Panel(), Game.Instance);
            var label = first.Add(new Label { Text = "x" });
            var handle = label.Handle;
            var made = frames.Frames.Count;
            second.Add(label);
            Screen.Add(label, Game.Instance);
            Expect.Same(handle, label.Handle, "made once");
            Expect.Equal(made, frames.Frames.Count, "nothing new made");
            Expect.Same(first, label.Parent, "still in the first");
        }

        /// <summary>An ended frame is given out again only under the parent it was made under, as a frame moved to another still hides with its old one (seen 11-03): not on the other layer, nor out of the panel it was in.</summary>
        public static void PoolKeepsParents(FakeWorld world)
        {
            var frames = Installed();
            var low = Screen.Add(new Button { Text = "a" }, Game.Instance);
            low.End();
            var high = Screen.AddOver(new Button { Text = "b" }, Game.Instance);
            Expect.True(high.Handle != low.Handle, "a new game frame over the HUD");
            var again = Screen.Add(new Button { Text = "c" }, Game.Instance);
            Expect.Same(low.Handle, again.Handle, "the ended one given out on its own layer");
            var panel = Screen.Add(new Panel(), Game.Instance);
            var inner = panel.Add(new Label { Text = "in" });
            panel.End();
            var top = Screen.Add(new Label { Text = "top" }, Game.Instance);
            Expect.True(top.Handle != inner.Handle, "not the label from inside the panel");
            var refilled = Screen.Add(new Panel(), Game.Instance);
            Expect.Same(panel.Handle, refilled.Handle, "the panel given out again");
            var second = refilled.Add(new Label { Text = "in again" });
            Expect.Same(inner.Handle, second.Handle, "and its label inside it");
        }

        /// <summary>A grid given a new value after its frames were added lays them out with that value, not the one before (review G1: in Lua, a setter writing its field through ref laid out first).</summary>
        public static void GridSetAfterAdd(FakeWorld world)
        {
            var frames = Installed();
            var grid = Screen.Add(new Grid { CellWidth = 0.1f, CellHeight = 0.05f }, Game.Instance);
            grid.Add(new Image());
            var b = grid.Add(new Image());
            var fg = frames.FrameOf(grid.Handle!);
            grid.Columns = 2;
            ExpectPoint(frames.FrameOf(b.Handle!), 0, fg, 0, 0.1f, 0.0f, "the second cell, by the new columns");
            grid.Gap = 0.01f;
            ExpectPoint(frames.FrameOf(b.Handle!), 0, fg, 0, 0.11f, 0.0f, "moved by the new gap");
        }

        /// <summary>A progress bar sized by Fill fills its share of the width it covers, at the bar's height (review G3).</summary>
        public static void ProgressBarFilledOver(FakeWorld world)
        {
            var frames = Installed();
            var slot = Screen.Add(new Panel { Width = 0.2f, Height = 0.02f }, Game.Instance);
            var bar = Screen.Add(new ProgressBar { Texture = "x.blp", Value = 0.5f }, Game.Instance);
            bar.Fill(slot);
            var fb = frames.FrameOf(bar.Handle!);
            var fill = frames.Frames.Find(f => f.Parent == fb && f.Texture == "x.blp");
            Expect.True(fill != null, "the fill");
            Expect.Near(0.1f, fill!.Width, "half the width it covers");
            Expect.Near(0.02f, fill.Height, "its height");
            bar.Value = 0.25f;
            Expect.Near(0.05f, fill.Width, "a quarter of it");
        }

        /// <summary>The game's event for a value the map set runs nothing, even when a drag sent before the set arrives between them (review G4); Value then reads what the slider shows.</summary>
        public static void SliderEchoAfterDrag(FakeWorld world)
        {
            var frames = Installed();
            var slider = Screen.Add(new Slider { Max = 10.0f }, Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(slider.Handle!);
            var seen = new List<float>();
            slider.ValueChanged += (who, value) => seen.Add(value);
            slider.Value = 5.0f;
            frames.Fire(fake, SliderChanged, 0, 6.0f);
            frames.Fire(fake, SliderChanged, 0, 5.0f);
            Expect.Equal("6", string.Join(", ", seen), "the drag runs, the set's own event does not");
            Expect.Near(5.0f, slider.Value, "Value reads what the slider shows");
            frames.Fire(fake, SliderChanged, 0, 7.0f);
            Expect.Equal("6, 7", string.Join(", ", seen), "the next drag runs");
        }

        /// <summary>The game's event for a text the map set runs nothing, even when typing sent before the set arrives between them (review G4).</summary>
        public static void TextBoxEchoAfterTyping(FakeWorld world)
        {
            var frames = Installed();
            var box = Screen.Add(new TextBox(), Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(box.Handle!);
            var seen = new List<string>();
            box.TextChanged += (who, text) => seen.Add(text);
            box.Text = "AB";
            frames.Fire(fake, TextChanged, 0, 0.0f, "abc");
            frames.Fire(fake, TextChanged, 0, 0.0f, "AB");
            Expect.Equal("abc", string.Join(", ", seen), "the typing runs, the set's own event does not");
            Expect.Equal("AB", box.Text, "Text reads what the box shows");
        }

        /// <summary>A frame tied to a frame not made yet is placed when that frame is made (review G5).</summary>
        public static void AttachToUnmade(FakeWorld world)
        {
            var frames = Installed();
            var label = Screen.Add(new Label { Text = "x" }, Game.Instance);
            var box = new Panel();
            label.Attach(FramePoint.Left, box, FramePoint.Right);
            Screen.Add(box, Game.Instance);
            ExpectPoint(frames.FrameOf(label.Handle!), 3, frames.FrameOf(box.Handle!), 5, 0.0f, 0.0f, "tied once the other is made");
        }

        /// <summary>A frame set to cover a frame not made yet covers it when it is made, and keeps none of its old points meanwhile (review G5).</summary>
        public static void FillUnmade(FakeWorld world)
        {
            var frames = Installed();
            var cover = Screen.Add(new Panel(), Game.Instance);
            var box = new Panel();
            cover.Fill(box);
            var fake = frames.FrameOf(cover.Handle!);
            Expect.Equal(0, fake.Points.Count, "the old points cleared");
            Screen.Add(box, Game.Instance);
            Expect.Same(frames.FrameOf(box.Handle!), fake.Filled, "covers it once it is made");
        }

        /// <summary>An ended frame another frame is tied to is not given out again, so the tied frame does not follow the next frame of its kind (review G6).</summary>
        public static void TiedToEnded(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var notice = panel.Add(new Label { Text = "notice" });
            var icon = panel.Add(new Image());
            icon.Attach(FramePoint.Left, notice, FramePoint.Right);
            notice.End();
            var next = panel.Add(new Label { Text = "next", Anchor = FramePoint.TopLeft });
            Expect.True(next.Handle != notice.Handle, "the ended one kept from the next label");
            Expect.Same(frames.FrameOf(notice.Handle!), frames.FrameOf(icon.Handle!).Points[0].Other, "the icon still tied where it was");
        }

        /// <summary>A row's ended frame, and a tree ended whole with a frame tied to a later sibling, are still given out again.</summary>
        public static void TiedStillPooled(FakeWorld world)
        {
            Installed();
            var row = Screen.Add(new Row(), Game.Instance);
            row.Add(new Label { Text = "a" });
            var b = row.Add(new Label { Text = "b" });
            row.Add(new Label { Text = "c" });
            b.End();
            var again = row.Add(new Label { Text = "d" });
            Expect.Same(b.Handle, again.Handle, "the row's ended label given out again");
            var panel = new Panel();
            var first = panel.Add(new Label { Text = "first" });
            var second = panel.Add(new Label { Text = "second" });
            first.Attach(FramePoint.Top, second, FramePoint.Bottom);
            Screen.Add(panel, Game.Instance);
            panel.End();
            var refilled = Screen.Add(new Panel(), Game.Instance);
            var one = refilled.Add(new Label { Text = "one" });
            var two = refilled.Add(new Label { Text = "two" });
            Expect.True((one.Handle == first.Handle || one.Handle == second.Handle) && (two.Handle == first.Handle || two.Handle == second.Handle), "both labels of the ended panel given out again");
        }

        /// <summary>A frame put on the screen again in the same scope, or added again to the same frame, is refused and reported: it is made once, and a container lists it once (review lead 6, E10).</summary>
        public static void AddedAgainRefused(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var handle = panel.Handle;
            var made = frames.Frames.Count;
            Screen.Add(panel, Game.Instance);
            Expect.Same(handle, panel.Handle, "the screen's frame made once");
            Expect.Equal(made, frames.Frames.Count, "nothing new made");
            var row = Screen.Add(new Row(), Game.Instance);
            var label = row.Add(new Label { Text = "x" });
            var second = row.Add(new Label { Text = "y" });
            handle = label.Handle;
            made = frames.Frames.Count;
            row.Add(label);
            Expect.Same(handle, label.Handle, "the child made once");
            Expect.Equal(made, frames.Frames.Count, "nothing new made for it");
            Expect.Same(frames.FrameOf(row.Handle!), frames.FrameOf(label.Handle!).Points[0].Other, "still first in the row");
            Expect.True(world.Logged.Exists(line => line.Contains("already has a parent")), "reported: " + string.Join(" | ", world.Logged));
        }

        /// <summary>An empty panel and an image whose tooltip is taken away, or ended by the map, let clicks through again; a panel with a look still catches them (review lead 6).</summary>
        public static void TooltipGoneLetsClicksThrough(FakeWorld world)
        {
            var frames = Installed();
            var empty = Screen.Add(new Panel { Template = "" }, Game.Instance);
            var catcher = frames.Frames.Find(f => f.Filled == frames.FrameOf(empty.Handle!))!;
            empty.Tooltip = new Tooltip { Text = "tip" };
            Expect.Equal(true, catcher.Enabled, "on for the tooltip");
            empty.Tooltip = null;
            Expect.Equal(false, catcher.Enabled, "off once it is taken away");
            var image = Screen.Add(new Image { Texture = "x.blp" }, Game.Instance);
            var tip = new Tooltip { Text = "tip" };
            image.Tooltip = tip;
            var imageCatcher = frames.Frames.Find(f => f.Filled == frames.FrameOf(image.Handle!))!;
            Expect.Equal(true, imageCatcher.Enabled, "the image's on for the tooltip");
            tip.End();
            Expect.Equal(false, imageCatcher.Enabled, "off once the map ends it");
            var look = Screen.Add(new Panel(), Game.Instance);
            look.Tooltip = new Tooltip { Text = "tip" };
            look.Tooltip = null;
            Expect.Equal(true, frames.Frames.Find(f => f.Filled == frames.FrameOf(look.Handle!))!.Enabled, "a panel with a look still catches");
        }

        /// <summary>An image or an empty panel turned off and then listened to gets a catcher that stays off until it is turned on (review lead 6).</summary>
        public static void TurnedOffThenListened(FakeWorld world)
        {
            var frames = Installed();
            var image = Screen.Add(new Image { Texture = "x.blp" }, Game.Instance);
            image.Enabled = false;
            image.Wheel += (who, n) => { };
            var catcher = frames.Frames.Find(f => f.Filled == frames.FrameOf(image.Handle!))!;
            Expect.Equal(false, catcher.Enabled, "the image's catcher off");
            image.Enabled = true;
            Expect.Equal(true, catcher.Enabled, "on once turned on");
            var empty = Screen.Add(new Panel { Template = "" }, Game.Instance);
            empty.Enabled = false;
            empty.Tooltip = new Tooltip { Text = "tip" };
            Expect.Equal(false, frames.Frames.Find(f => f.Filled == frames.FrameOf(empty.Handle!))!.Enabled, "the empty panel's catcher off");
        }

        /// <summary>A frame given only a width keeps its template's height, and one given only a height its template's width; a label made by type is given 0, so it is as tall as its lines (review lead 6).</summary>
        public static void OneSideKeepsTemplate(FakeWorld world)
        {
            var frames = Installed();
            frames.TemplateSizes["ScriptDialogButton"] = new Vector2(0.228f, 0.035f);
            var button = Screen.Add(new Button { Text = "a", Width = 0.1f }, Game.Instance);
            var fake = frames.FrameOf(button.Handle!);
            Expect.Near(0.1f, fake.Width, "the width given");
            Expect.Near(0.035f, fake.Height, "the template's height kept");
            Expect.Near(0.0f, button.Height, "Height reads 0, as it was not set");
            button.Height = 0.05f;
            button.Width = 0.0f;
            Expect.Near(0.228f, fake.Width, "the template's width back");
            Expect.Near(0.05f, fake.Height, "the height given");
            button.End();
            var next = Screen.Add(new Button { Text = "b", Width = 0.2f }, Game.Instance);
            Expect.Same(fake, frames.FrameOf(next.Handle!), "the ended one given out again");
            Expect.Near(0.035f, fake.Height, "with the template's height, not the ended one's");
            var label = Screen.Add(new Label { Text = "x", Width = 0.1f }, Game.Instance);
            Expect.Near(0.0f, frames.FrameOf(label.Handle!).Height, "a label's height left to its lines");
        }

        /// <summary>A progress bar covering a frame follows that frame's new size, and one covering a frame that covers a sized one fills by that size; (review G3, lead 6).</summary>
        public static void ProgressBarFollowsResize(FakeWorld world)
        {
            var frames = Installed();
            var slot = Screen.Add(new Panel { Width = 0.2f, Height = 0.02f }, Game.Instance);
            var bar = Screen.Add(new ProgressBar { Texture = "x.blp", Value = 0.5f }, Game.Instance);
            bar.Fill(slot);
            var fb = frames.FrameOf(bar.Handle!);
            var fill = frames.Frames.Find(f => f.Parent == fb && f.Texture == "x.blp")!;
            slot.Width = 0.4f;
            Expect.Near(0.2f, fill.Width, "half the new width");
            var inner = Screen.Add(new Panel { Template = "" }, Game.Instance);
            inner.Fill(slot);
            var deep = Screen.Add(new ProgressBar { Texture = "y.blp", Value = 0.25f }, Game.Instance);
            deep.Fill(inner);
            var deepFill = frames.Frames.Find(f => f.Parent == frames.FrameOf(deep.Handle!) && f.Texture == "y.blp")!;
            Expect.Near(0.1f, deepFill.Width, "a quarter of the sized frame's width, through the one between");
            slot.Width = 0.8f;
            Expect.Near(0.2f, deepFill.Width, "following it");
        }

        /// <summary>A frame set to cover itself, or a frame that covers it, is refused and reported, and keeps what it covered.</summary>
        public static void FillLoopRefused(FakeWorld world)
        {
            var frames = Installed();
            var a = Screen.Add(new Panel(), Game.Instance);
            var b = Screen.Add(new Panel(), Game.Instance);
            b.Fill(a);
            a.Fill(b);
            a.Fill(a);
            Expect.True(frames.FrameOf(a.Handle!).Filled == null, "the first covers nothing");
            Expect.Same(frames.FrameOf(a.Handle!), frames.FrameOf(b.Handle!).Filled, "the second still covers the first");
            Expect.True(world.Logged.Exists(line => line.Contains("covers it")), "reported: " + string.Join(" | ", world.Logged));
        }

        /// <summary>A panel with a frame inside it that a frame outside is tied to is not given out again when it ends, so the tied frame does not follow the next panel (review G6).</summary>
        public static void TiedInsideEnded(FakeWorld world)
        {
            var frames = Installed();
            var panel = Screen.Add(new Panel(), Game.Instance);
            var notice = panel.Add(new Label { Text = "notice" });
            var icon = Screen.Add(new Image(), Game.Instance);
            icon.Attach(FramePoint.Left, notice, FramePoint.Right);
            panel.End();
            var next = Screen.Add(new Panel { Anchor = FramePoint.TopLeft }, Game.Instance);
            Expect.True(next.Handle != panel.Handle, "the ended panel kept from the next");
            Expect.Same(frames.FrameOf(notice.Handle!), frames.FrameOf(icon.Handle!).Points[0].Other, "the icon still tied where it was");
        }

        /// <summary>Attach or Fill to a frame that has ended is refused and reported, and the frame keeps its points (review G6).</summary>
        public static void TieToEndedRefused(FakeWorld world)
        {
            var frames = Installed();
            var gone = Screen.Add(new Label { Text = "gone" }, Game.Instance);
            gone.End();
            var icon = Screen.Add(new Image(), Game.Instance);
            var fake = frames.FrameOf(icon.Handle!);
            icon.Attach(FramePoint.Left, gone, FramePoint.Right);
            icon.Fill(gone);
            Expect.Equal(1, fake.Points.Count, "its anchor kept");
            Expect.True(fake.Filled == null, "covering nothing");
            Expect.True(world.Logged.Exists(line => line.Contains("has ended")), "reported: " + string.Join(" | ", world.Logged));
        }

        /// <summary>A frame not made yet that is tied to one that ends keeps that one out of the pool, and is tied to its game frame when it is made (review G6).</summary>
        public static void UnmadeTiedHolds(FakeWorld world)
        {
            var frames = Installed();
            var notice = Screen.Add(new Label { Text = "notice" }, Game.Instance);
            var icon = new Image();
            icon.Attach(FramePoint.Left, notice, FramePoint.Right);
            notice.End();
            var next = Screen.Add(new Label { Text = "next", Anchor = FramePoint.TopLeft }, Game.Instance);
            Expect.True(next.Handle != notice.Handle, "the ended one kept from the next label");
            Screen.Add(icon, Game.Instance);
            Expect.Same(frames.FrameOf(notice.Handle!), frames.FrameOf(icon.Handle!).Points[0].Other, "the icon tied where the notice was");
        }

        /// <summary>A slider set to the value it reads while a drag is on its way gives the game nothing, so the drag lands and runs once, and no echo comes back as the player's (review G4).</summary>
        public static void SliderSameValueAfterDrag(FakeWorld world)
        {
            var frames = Installed();
            var slider = Screen.Add(new Slider { Max = 10.0f, Value = 5.0f }, Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(slider.Handle!);
            var seen = new List<float>();
            slider.ValueChanged += (who, value) => seen.Add(value);
            fake.Value = 6.0f;
            var calls = fake.Calls;
            slider.Value = 5.0f;
            Expect.Equal(calls, fake.Calls, "nothing given to the game");
            frames.Fire(fake, SliderChanged, 0, 6.0f);
            Expect.Equal("6", string.Join(", ", seen), "the drag runs once");
            Expect.Near(6.0f, slider.Value, "Value reads the drag");
        }

        /// <summary>A text box set to the text it reads while typing is on its way gives the game nothing (review G4).</summary>
        public static void TextBoxSameTextAfterTyping(FakeWorld world)
        {
            var frames = Installed();
            var box = Screen.Add(new TextBox { Text = "a" }, Game.Instance, shownTo: Player.One);
            var fake = frames.FrameOf(box.Handle!);
            var seen = new List<string>();
            box.TextChanged += (who, text) => seen.Add(text);
            fake.Text = "ab";
            box.Text = "a";
            Expect.Equal("ab", fake.Text, "nothing given to the game");
            frames.Fire(fake, TextChanged, 0, 0.0f, "ab");
            Expect.Equal("ab", string.Join(", ", seen), "the typing runs once");
        }

        /// <summary>An ended slider goes only to one whose range holds its value, so the game sends back no clamp of it as a drag (review E08).</summary>
        public static void SliderPoolKeepsRange(FakeWorld world)
        {
            Installed();
            var high = Screen.Add(new Slider { Value = 90.0f }, Game.Instance, shownTo: Player.One);
            high.End();
            var low = Screen.Add(new Slider { Max = 50.0f, Value = 10.0f }, Game.Instance, shownTo: Player.One);
            Expect.True(low.Handle != high.Handle, "not to a range without its value");
            var wide = Screen.Add(new Slider { Value = 20.0f }, Game.Instance, shownTo: Player.One);
            Expect.Same(high.Handle, wide.Handle, "to one whose range holds it");
        }

        /// <summary>A text box given a limit refuses 0, reported, as the game's own limit cannot be given back, so an ended one never caps a box that sets none (review E09).</summary>
        public static void TextBoxLimitKept(FakeWorld world)
        {
            var frames = Installed();
            var box = Screen.Add(new TextBox(), Game.Instance, shownTo: Player.One);
            box.MaxLength = 5;
            box.MaxLength = 0;
            Expect.Equal(5, box.MaxLength, "the limit kept");
            Expect.Equal(5, frames.FrameOf(box.Handle!).TextLimit, "as the game has it");
            Expect.True(world.Logged.Exists(line => line.Contains("MaxLength")), "reported: " + string.Join(" | ", world.Logged));
            box.End();
            var free = Screen.Add(new TextBox(), Game.Instance, shownTo: Player.One);
            Expect.True(free.Handle != box.Handle, "not given to a box with no limit");
            var unmade = new TextBox { MaxLength = 5 };
            unmade.MaxLength = 0;
            Expect.Equal(0, unmade.MaxLength, "0 taken before the game has a limit");
        }
    }
}
