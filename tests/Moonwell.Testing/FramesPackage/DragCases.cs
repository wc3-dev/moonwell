using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check drag and drop: a click picks a draggable frame up, a click on a frame listened to for Dropped drops it (decision 210).</summary>
    public static class DragCases
    {
        private const int Click = 1;

        private static void ClickOn(FakeFramesWorld frames, Frame frame, int slot)
        {
            frames.Fire(frames.FrameOf(frame.MouseHandle!), Click, slot);
        }

        /// <summary>The icon that follows a player's cursor: the newest BACKDROP over the HUD painted with the texture.</summary>
        private static FakeFrame? IconPainted(FakeFramesWorld frames, string texture)
        {
            FakeFrame? found = null;
            foreach (var frame in frames.Frames)
            {
                if (frame.Type == "BACKDROP" && frame.Texture == texture && frame.Parent != null && frame.Parent.Under == "GameUI")
                {
                    found = frame;
                }
            }

            return found;
        }

        private sealed class Slots
        {
            public IconButton Bag = null!;
            public IconButton Hand = null!;
            public List<string> Heard = new List<string>();
        }

        private static Slots Made()
        {
            var made = new Slots();
            var panel = Screen.Add(new Panel(), Game.Instance);
            made.Bag = panel.Add(new IconButton { Icon = "sword.blp", Width = 0.05f, Height = 0.05f, Draggable = true });
            made.Hand = panel.Add(new IconButton { Icon = "", Width = 0.05f, Height = 0.05f });
            made.Bag.Clicked += who => made.Heard.Add("bag clicked");
            made.Hand.Clicked += who => made.Heard.Add("hand clicked");
            made.Hand.Dropped += (who, dragged) => made.Heard.Add("dropped " + (dragged == made.Bag ? "bag" : "other") + " by " + who.Slot);
            return made;
        }

        /// <summary>A click picks the frame up, its icon showing on the dragger's machine; a click on a target drops it there, runs Dropped with the player and the frame, hides the icon and gives the keyboard back. Neither click runs Clicked.</summary>
        public static void PickAndDrop(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slots = Made();
            ClickOn(frames, slots.Bag, 0);
            var icon = IconPainted(frames, "sword.blp");
            Expect.True(icon != null, "the icon made, painted with the button's icon");
            Expect.Equal(true, icon!.Shown, "shown on the dragger's machine");
            Expect.Same(slots.Bag, FramesSystem.Drags.HeldBy(Player.Get(0)), "held");
            ClickOn(frames, slots.Hand, 0);
            Expect.Equal("dropped bag by 0", string.Join(", ", slots.Heard), "Dropped ran, and no Clicked");
            Expect.Equal(false, icon.Shown, "the icon hidden");
            Expect.True(FramesSystem.Drags.HeldBy(Player.Get(0)) == null, "nothing held");
            Expect.True(frames.FrameOf(slots.Hand.MouseHandle!).KeyboardReleases > 0, "the keyboard given back from the target");
            ClickOn(frames, slots.Hand, 0);
            Expect.Equal("dropped bag by 0, hand clicked", string.Join(", ", slots.Heard), "a click with nothing held is a click");
        }

        /// <summary>The icon sits under this machine's cursor, the frame's size, and follows it.</summary>
        public static void FollowsCursor(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slots = Made();
            frames.WindowWidth = 800;
            frames.WindowHeight = 600;
            frames.MouseX = 400;
            frames.MouseY = 300;
            ClickOn(frames, slots.Bag, 0);
            var icon = IconPainted(frames, "sword.blp")!;
            Expect.Near(0.375f, icon.Left, "centred on the cursor, the button's width");
            Expect.Near(0.325f, icon.Top, "the cursor's y from the window's top");
            frames.MouseX = 600;
            frames.MouseY = 150;
            world.Advance(0.1f);
            Expect.Near(0.575f, icon.Left, "followed to the right");
            Expect.Near(0.425f, icon.Bottom, "followed up");
        }

        /// <summary>Another machine makes the icon too, so handles stay in step, but neither shows nor moves it; the drop still runs Dropped there.</summary>
        public static void OtherMachine(FakeWorld world)
        {
            world.LocalSlot = 1;
            var frames = FrameCases.Installed();
            var slots = Made();
            ClickOn(frames, slots.Bag, 0);
            var icon = IconPainted(frames, "sword.blp");
            Expect.True(icon != null, "made on every machine");
            Expect.Equal(false, icon!.Shown, "not shown here");
            Expect.Near(0.0f, icon.Left, "not moved here");
            ClickOn(frames, slots.Hand, 0);
            Expect.Equal("dropped bag by 0", string.Join(", ", slots.Heard), "Dropped ran on this machine too");
        }

        /// <summary>Right-click, Esc, a click on the frame held, and a left click that no frame click follows each put it back; a click with a frame click in the same instant drops.</summary>
        public static void PutBack(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slots = Made();
            var who = Player.Get(0);

            ClickOn(frames, slots.Bag, 0);
            frames.ReleaseMouse(0, left: false);
            Expect.True(FramesSystem.Drags.HeldBy(who) == null, "right-click puts it back");

            ClickOn(frames, slots.Bag, 0);
            frames.PressEscape(0);
            Expect.True(FramesSystem.Drags.HeldBy(who) == null, "Esc puts it back");

            ClickOn(frames, slots.Bag, 0);
            ClickOn(frames, slots.Bag, 0);
            Expect.True(FramesSystem.Drags.HeldBy(who) == null, "a click on it again puts it back");

            ClickOn(frames, slots.Bag, 0);
            frames.ReleaseMouse(0);
            Expect.Same(slots.Bag, FramesSystem.Drags.HeldBy(who), "still held just after a left release");
            world.Advance(0.2f);
            Expect.True(FramesSystem.Drags.HeldBy(who) == null, "a left click on the world puts it back");

            ClickOn(frames, slots.Bag, 0);
            frames.ReleaseMouse(0);
            ClickOn(frames, slots.Hand, 0);
            world.Advance(0.2f);
            Expect.Equal("dropped bag by 0", string.Join(", ", slots.Heard), "a release with a click on a target drops; nothing else ran");
        }

        /// <summary>While one is held, a click on another draggable frame picks that one up, and a click on a frame neither draggable nor a target puts it back and is a click.</summary>
        public static void OtherFrames(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slots = Made();
            var other = Screen.Add(new IconButton { Icon = "shield.blp", Draggable = true }, Game.Instance);
            var plain = Screen.Add(new Button { Text = "Buy" }, Game.Instance);
            var bought = 0;
            plain.Clicked += who => bought++;
            ClickOn(frames, slots.Bag, 0);
            ClickOn(frames, other, 0);
            Expect.Same(other, FramesSystem.Drags.HeldBy(Player.Get(0)), "the other picked up");
            Expect.True(IconPainted(frames, "shield.blp") != null, "the icon painted with the other's");
            ClickOn(frames, plain, 0);
            Expect.True(FramesSystem.Drags.HeldBy(Player.Get(0)) == null, "put back");
            Expect.Equal(1, bought, "the button's click ran");
        }

        /// <summary>Two players each hold their own frame, with their own icon; one's drop leaves the other's held.</summary>
        public static void TwoPlayers(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slots = Made();
            var second = Screen.Add(new IconButton { Icon = "axe.blp", Draggable = true }, Game.Instance);
            ClickOn(frames, slots.Bag, 0);
            ClickOn(frames, second, 1);
            Expect.Equal(true, IconPainted(frames, "sword.blp")!.Shown, "player 1's icon shown here");
            Expect.Equal(false, IconPainted(frames, "axe.blp")!.Shown, "player 2's icon not shown here");
            ClickOn(frames, slots.Hand, 1);
            Expect.Equal("dropped other by 1", string.Join(", ", slots.Heard), "player 2 dropped theirs");
            Expect.Same(slots.Bag, FramesSystem.Drags.HeldBy(Player.Get(0)), "player 1 still holds theirs");
        }

        /// <summary>A held frame that ends is put back at the next step, with nothing reported; a load puts every drag back, and the next pick makes a new icon.</summary>
        public static void EndedAndLoaded(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slots = Made();
            var printed = world.Printed.Count;
            ClickOn(frames, slots.Bag, 0);
            slots.Bag.End();
            world.Advance(0.1f);
            Expect.True(FramesSystem.Drags.HeldBy(Player.Get(0)) == null, "put back once ended");
            Expect.Equal(printed, world.Printed.Count, "nothing reported");

            var axe = Screen.Add(new IconButton { Icon = "axe.blp", Draggable = true }, Game.Instance);
            ClickOn(frames, axe, 0);
            frames.Load();
            Inbound.GameLoaded();
            Expect.True(FramesSystem.Drags.HeldBy(Player.Get(0)) == null, "a load puts it back");
            var count = frames.Frames.Count;
            ClickOn(frames, axe, 0);
            Expect.Equal(count + 1, frames.Frames.Count, "a new icon made at the next pick");
            Expect.Equal(false, IconPainted(frames, "axe.blp")!.Lost, "the new icon is not one the load took");
            world.Advance(0.1f);
            Expect.Equal(printed, world.Printed.Count, "nothing reported after the load");
        }

        /// <summary>DragIcon replaces the button's icon; an image held shows its texture; a label made draggable takes the mouse.</summary>
        public static void Icons(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var button = Screen.Add(new IconButton { Icon = "sword.blp", DragIcon = "glow.blp", Draggable = true }, Game.Instance);
            var image = Screen.Add(new Image { Texture = "gem.blp", Draggable = true }, Game.Instance);
            var label = Screen.Add(new Label { Text = "Drag me", Draggable = true, DragIcon = "note.blp" }, Game.Instance);
            ClickOn(frames, button, 0);
            Expect.True(IconPainted(frames, "glow.blp") != null, "DragIcon drawn");
            frames.PressEscape(0);
            ClickOn(frames, image, 0);
            Expect.Same(image, FramesSystem.Drags.HeldBy(Player.Get(0)), "an image picked up");
            Expect.True(IconPainted(frames, "gem.blp") != null, "its texture drawn");
            frames.PressEscape(0);
            Expect.Equal(true, label.Enabled, "the label takes the mouse");
            ClickOn(frames, label, 0);
            Expect.Same(label, FramesSystem.Drags.HeldBy(Player.Get(0)), "a label picked up");
        }

        /// <summary>A draggable frame with no icon to draw is not picked up, and its click is a click; it still takes drops.</summary>
        public static void EmptyNotPicked(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slots = Made();
            slots.Hand.Draggable = true;
            ClickOn(frames, slots.Hand, 0);
            Expect.True(FramesSystem.Drags.HeldBy(Player.Get(0)) == null, "the empty slot not picked up");
            Expect.Equal("hand clicked", string.Join(", ", slots.Heard), "its click a click");
            ClickOn(frames, slots.Bag, 0);
            ClickOn(frames, slots.Hand, 0);
            Expect.Equal("hand clicked, dropped bag by 0", string.Join(", ", slots.Heard), "it still takes a drop");
        }

        /// <summary>A Dropped handler that throws is reported with the frame, and the next still runs.</summary>
        public static void DroppedThrows(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var slots = Made();
            var second = false;
            slots.Hand.Dropped += (who, dragged) => { throw new System.InvalidOperationException("boom"); };
            slots.Hand.Dropped += (who, dragged) => second = true;
            var printed = world.Printed.Count;
            ClickOn(frames, slots.Bag, 0);
            ClickOn(frames, slots.Hand, 0);
            Expect.True(world.Printed.Count > printed && world.Printed[printed].Contains("Dropped"), "reported with the event");
            Expect.Equal(true, second, "the next handler ran");
        }
    }
}
