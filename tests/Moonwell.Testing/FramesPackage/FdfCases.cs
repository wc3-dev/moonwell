using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check windows from the map's own .fdf: their parts found by name.</summary>
    public static class FdfCases
    {
        private const int Click = 1;

        private static FakeFramesWorld WithShop()
        {
            var frames = FrameCases.Installed();
            frames.TemplateParts["ShopWindow"] = new[] { "ShopTitle", "ShopBody" };
            frames.TemplateParts["ShopBody"] = new[] { "ShopBuy" };
            return frames;
        }

        /// <summary>A part is found by its name at any depth, as the kind asked for, the same object at each call, shown to the window's player, and its events and values reach the game's frame.</summary>
        public static void ChildFound(FakeWorld world)
        {
            var frames = WithShop();
            var window = Screen.Add(new Panel { Template = "ShopWindow" }, Game.Instance, shownTo: Player.One);
            var title = window.Child<Label>("ShopTitle");
            Expect.True(title != null, "the title found");
            title!.Text = "Shop";
            var fakeWindow = frames.FrameOf(window.Handle!);
            Expect.Equal("Shop", fakeWindow.Children.Find(f => f.Name == "ShopTitle")!.Text, "its text set on the template's frame");
            Expect.Equal(1, fakeWindow.Children.Find(f => f.Name == "ShopBody")!.Level, "the template's parts raised above the panel's catcher");
            var buy = window.Child<Button>("ShopBuy");
            Expect.True(buy != null, "a part inside a part found");
            Expect.Same(buy, window.Child<Button>("ShopBuy"), "the same object at the second call");
            Expect.Same(Player.One, buy!.Player, "the window's player");
            Expect.Same(window, buy.Parent, "in the window's scope");
            var clicks = new List<Player>();
            buy.Clicked += who => clicks.Add(who);
            frames.Fire(frames.FrameOf(buy.Handle!), Click, 0);
            Expect.Equal(1, clicks.Count, "its click heard");
        }

        /// <summary>A window whose parts were found ends with them and is not given out again, as what was set on them cannot be given back; one with none found is.</summary>
        public static void FoundNotPooled(FakeWorld world)
        {
            WithShop();
            var window = Screen.Add(new Panel { Template = "ShopWindow" }, Game.Instance);
            var title = window.Child<Label>("ShopTitle");
            window.End();
            Expect.Equal(true, title!.HasEnded, "the part ends with the window");
            var next = Screen.Add(new Panel { Template = "ShopWindow" }, Game.Instance);
            Expect.True(next.Handle != window.Handle, "not given out again");
            next.End();
            var third = Screen.Add(new Panel { Template = "ShopWindow" }, Game.Instance);
            Expect.Same(next.Handle, third.Handle, "one with no part found is");
        }

        /// <summary>Asked before the window is added, for a name it lacks, or as a kind drawn from Moonwell's own parts, Child gives null and is reported.</summary>
        public static void ChildRefused(FakeWorld world)
        {
            WithShop();
            var window = new Panel { Template = "ShopWindow" };
            Expect.True(window.Child<Label>("ShopTitle") == null, "not added yet");
            Screen.Add(window, Game.Instance);
            Expect.True(window.Child<Label>("Missing") == null, "no such name");
            Expect.True(window.Child<CheckBox>("ShopTitle") == null, "a kind drawn from parts");
            Expect.True(world.Logged.Exists(line => line.Contains("not added")), "reported: " + string.Join(" | ", world.Logged));
            Expect.True(world.Logged.Exists(line => line.Contains("Missing")), "reported: " + string.Join(" | ", world.Logged));
            Expect.True(world.Logged.Exists(line => line.Contains("parts of Moonwell's own")), "reported: " + string.Join(" | ", world.Logged));
        }

        /// <summary>A map's panel with a label of its own, added in its constructor.</summary>
        public sealed class NotedPanel : Panel
        {
            /// <summary>The label the constructor adds.</summary>
            public readonly Label Note;

            /// <summary>Adds the label.</summary>
            public NotedPanel()
            {
                Note = Add(new Label { Text = "note" });
            }
        }

        /// <summary>The frames a found kind's constructor adds are made in the part found, as a Tooltip's words are (review lead 5).</summary>
        public static void ChildMakesItsOwn(FakeWorld world)
        {
            var frames = WithShop();
            var window = Screen.Add(new Panel { Template = "ShopWindow" }, Game.Instance, shownTo: Player.One);
            var body = window.Child<NotedPanel>("ShopBody");
            Expect.True(body!.Note.Handle != null, "the constructor's label made");
            var note = frames.FrameOf(body.Note.Handle!);
            Expect.Same(frames.FrameOf(body.Handle!), note.Parent, "under the part found");
            Expect.Equal("note", note.Text, "with its text");
            Expect.Same(Player.One, body.Note.Player, "shown to the window's player");
        }

        /// <summary>A label found in a template lets clicks through, as a label made does (review lead 5).</summary>
        public static void FoundLabelLetsClicksThrough(FakeWorld world)
        {
            var frames = WithShop();
            var window = Screen.Add(new Panel { Template = "ShopWindow" }, Game.Instance);
            var title = window.Child<Label>("ShopTitle");
            Expect.Equal(false, frames.FrameOf(title!.Handle!).Enabled, "turned off");
        }

        /// <summary>A part that has ended, or asked for as another kind than it was found as, gives null and is reported, not the ended part or nothing silently (review lead 5).</summary>
        public static void EndedPartRefused(FakeWorld world)
        {
            WithShop();
            var window = Screen.Add(new Panel { Template = "ShopWindow" }, Game.Instance);
            var title = window.Child<Label>("ShopTitle");
            Expect.True(window.Child<Button>("ShopTitle") == null, "another kind");
            Expect.True(world.Logged.Exists(line => line.Contains("found as")), "reported: " + string.Join(" | ", world.Logged));
            title!.End();
            Expect.True(window.Child<Label>("ShopTitle") == null, "an ended part");
            Expect.True(world.Logged.Exists(line => line.Contains("has ended")), "reported: " + string.Join(" | ", world.Logged));
            var buy = window.Child<Button>("ShopBuy");
            window.End();
            Expect.True(window.Child<Button>("ShopBuy") == null, "a part of an ended window");
            Expect.True(buy!.HasEnded, "ended with it");
        }
    }
}
