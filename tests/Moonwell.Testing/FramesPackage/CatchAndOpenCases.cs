using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for P11's small additions: an empty panel or an image catching clicks (J79), an icon slot a map places alone (J80), and a player's top bar dialogs opened from code (J162).</summary>
    public static class CatchAndOpenCases
    {
        private const int Click = 1;

        private static void ClickOn(FakeFramesWorld frames, Frame frame, int slot)
        {
            frames.Fire(frames.FrameOf(frame.MouseHandle!), Click, slot);
        }

        /// <summary>An empty panel set to catch clicks has its catcher on, set before it is made or after; set back, the catcher is off again. A panel with a look catches them anyway.</summary>
        public static void PanelCatchesClicks(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var before = Screen.Add(new Panel { Template = "", CatchesClicks = true }, Game.Instance);
            var catcher = frames.Frames.Find(f => f.Filled == frames.FrameOf(before.Handle!))!;
            Expect.Equal("TEXT", catcher.Type, "an empty text over it");
            Expect.True(catcher.Enabled, "on, set before it was made");
            var after = Screen.Add(new Panel { Template = "" }, Game.Instance);
            var later = frames.Frames.Find(f => f.Filled == frames.FrameOf(after.Handle!))!;
            Expect.True(!later.Enabled, "off while it is not set");
            after.CatchesClicks = true;
            Expect.True(later.Enabled, "on once set");
            Expect.True(after.CatchesClicks, "reads set");
            after.CatchesClicks = false;
            Expect.True(!later.Enabled, "off again");
            var listened = Screen.Add(new Panel { Template = "", CatchesClicks = true }, Game.Instance);
            listened.Wheel += (who, notches) => { };
            listened.CatchesClicks = false;
            Expect.True(frames.Frames.Find(f => f.Filled == frames.FrameOf(listened.Handle!))!.Enabled, "a panel listened to keeps taking the mouse");
        }

        /// <summary>An image set to catch clicks gets a catcher over it, on; set back, it is off; an image not set gets none.</summary>
        public static void ImageCatchesClicks(FakeWorld world)
        {
            var frames = FrameCases.Installed();
            var plain = Screen.Add(new Image { Texture = "art.blp" }, Game.Instance);
            Expect.True(plain.catcher == null, "no catcher unless set");
            var art = Screen.Add(new Image { Texture = "art.blp", CatchesClicks = true }, Game.Instance);
            var catcher = frames.FrameOf(art.catcher!);
            Expect.Equal("TEXT", catcher.Type, "an empty text over it");
            Expect.Same(frames.FrameOf(art.Handle!), catcher.Filled, "covering it");
            Expect.True(catcher.Enabled, "on");
            art.CatchesClicks = false;
            Expect.True(!catcher.Enabled, "off once set back");
            plain.CatchesClicks = true;
            Expect.True(plain.catcher != null && frames.FrameOf(plain.catcher).Enabled, "made and on when set after");
        }

        /// <summary>A slot made alone stands in no grid, 0.04 square, draws its own empty picture, and runs its own Moved when a slot is dropped on it; dropped on a grid's slot it runs the grid's Moved; SwapWith works between them.</summary>
        public static void IconSlotAlone(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var head = Screen.Add(new IconSlot { EmptyIcon = "head-outline.blp" }, Game.Instance);
            var chest = Screen.Add(new IconSlot(), Game.Instance);
            var bag = Screen.Add(new SlotGrid { Columns = 2 }, Game.Instance);
            Expect.True(head.Grid == null, "in no grid");
            Expect.Equal(-1, head.Index, "no number");
            Expect.Near(0.04f, frames.FrameOf(head.Handle!).Width, "0.04 square");
            var look = frames.FrameOf(head.iconFrame!);
            Expect.Equal("head-outline.blp", look.Texture, "its own empty picture");
            Expect.Equal(IconSlot.Empty, frames.FrameOf(chest.iconFrame!).Texture, "the game's black unless set");
            head.Icon = "helm.blp";
            Expect.Equal("helm.blp", look.Texture, "its icon");
            var heard = new List<string>();
            chest.Moved += (who, from, to) => heard.Add("chest " + (from == head ? "head" : "bag" + from.Index) + ">" + (to == chest ? "chest" : "?"));
            bag.Moved += (who, from, to) => heard.Add("bag " + (from == chest ? "chest" : from == head ? "head" : "bag" + from.Index) + ">" + to.Index);
            ClickOn(frames, head, 0);
            ClickOn(frames, chest, 0);
            bag[1].Icon = "ring.blp";
            ClickOn(frames, bag[1], 0);
            ClickOn(frames, chest, 0);
            ClickOn(frames, head, 0);
            ClickOn(frames, bag[0], 0);
            Expect.Equal("chest head>chest, chest bag1>chest, bag head>0", string.Join(", ", heard), "a lone slot's own Moved, and the grid's for its slot");
            head.SwapWith(chest);
            Expect.Equal("|helm.blp", head.Icon + "|" + chest.Icon, "swapped");
            Expect.Equal("head-outline.blp", look.Texture, "emptied, its own empty picture again");
            head.EmptyIcon = "other.blp";
            Expect.Equal("other.blp", look.Texture, "a new empty picture drawn at once");
        }

        /// <summary>OpenMenu, OpenAllies, OpenQuests and OpenLog click the top bar's buttons on that player's machine only, even with the HUD hidden.</summary>
        public static void OpenTopBarDialogs(FakeWorld world)
        {
            world.LocalSlot = 0;
            var frames = FrameCases.Installed();
            var ui = Player.One.GameUi();
            ui.Visible = false;
            ui.OpenMenu();
            ui.OpenAllies();
            ui.OpenQuests();
            ui.OpenLog();
            Expect.Equal("UpperButtonBarMenuButton, UpperButtonBarAlliesButton, UpperButtonBarQuestsButton, UpperButtonBarChatButton", string.Join(", ", frames.Clicks), "each button clicked here, the HUD hidden");
            frames.Clicks.Clear();
            Player.Two.GameUi().OpenMenu();
            Expect.Equal(0, frames.Clicks.Count, "another player's menu: nothing clicked on this machine");
        }
    }
}
