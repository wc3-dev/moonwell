using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases that check multiboards, leaderboards and dialogs: their cells and rows, who sees them, their order and their end.</summary>
    public static class BoardCases
    {
        /// <summary>A game component that holds boards and dialogs, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        /// <summary>A board of cells writes every cell when made, so none shows the game's eye icon, a cell writes again only when it changes, and a new width or size lays the board out again.</summary>
        public static void Cells(FakeWorld world)
        {
            var info = Board.Create("Round 3", columns: 2, rows: 2, Game.Instance);
            var made = world.Boards[0];
            Expect.Equal("Round 3", made.Title, "the title");
            Expect.Equal(2, made.Rows, "rows");
            Expect.Equal(2, made.Columns, "columns");
            Expect.Equal(4, made.Cells.Count, "every cell written");
            Expect.Equal(true, made.Displayed, "displayed");
            info[0, 0].Text = "Wave";
            info[1, 0].Icon = @"ReplaceableTextures\CommandButtons\BTNFootman.blp";
            info[1, 1].Color = Color.TooltipGold;
            info[0, 1].Width = 0.1f;
            Expect.Equal("Wave", made.Text(0, 0), "a text");
            Expect.Equal(@"ReplaceableTextures\CommandButtons\BTNFootman.blp", made.Cells["1,0"].Icon, "an icon");
            Expect.True(made.Cells["1,1"].Color == Color.TooltipGold, "a colour");
            Expect.True(made.Cells["0,1"].Width == 0.1f, "a width");
            var writes = made.Writes;
            info[0, 0].Text = "Wave";
            Expect.Equal(writes, made.Writes, "the same text writes nothing");
            world.Advance(0.01f);
            var displays = made.Displays;
            info[0, 0].Text = "Waves";
            world.Advance(0.01f);
            Expect.Equal(displays, made.Displays, "a new text needs no new layout");
            info[0, 0].Width = 0.08f;
            info[1, 1].Width = 0.08f;
            world.Advance(0.01f);
            Expect.Equal(displays + 1, made.Displays, "new widths display the board again once, so the game lays it out");
            Expect.Equal(true, made.Displayed, "displayed after");
            info[0, 0].Text = "Wave";
            info.Rows = 3;
            Expect.Equal(6, made.Cells.Count, "a new row's cells written");
            Expect.Equal("Wave", made.Text(0, 0), "the old cells kept");
            var before = world.Printed.Count;
            info[5, 0].Text = "Outside";
            Expect.True(world.Printed.Count > before, "a cell outside the board is reported");
            info.Title = "Round 4";
            Expect.Equal("Round 4", made.Title, "retitled");
        }

        /// <summary>A board of rows fills itself in the instant after it is made, a header row of the columns' names first, and Refresh writes what changed.</summary>
        public static void Rows(FakeWorld world)
        {
            var kills = new Dictionary<string, int> { ["Arthas"] = 3, ["Jaina"] = 5 };
            var names = new List<string> { "Arthas", "Jaina" };
            var board = Board.Create("Kills", names, Game.Instance);
            board.Column("Hero", n => n);
            board.Column("Kills", n => kills[n].ToString(), width: 0.03f);
            var made = world.Boards[0];
            world.Advance(0.01f);
            Expect.Equal(3, made.Rows, "a header and two rows");
            Expect.Equal("Hero", made.Text(0, 0), "the header");
            Expect.True(made.Cells["0,0"].Color == Color.TooltipGold, "the header in gold");
            Expect.Equal("Jaina", made.Text(2, 0), "a row");
            Expect.Equal("5", made.Text(2, 1), "a value");
            Expect.True(made.Cells["2,1"].Width == 0.03f, "the column's width");
            kills["Arthas"] = 4;
            names.Add("Thrall");
            kills["Thrall"] = 0;
            var writes = made.Writes;
            board.Refresh();
            Expect.Equal(4, made.Rows, "a row added");
            Expect.Equal("4", made.Text(1, 1), "a value changed");
            Expect.Equal("Thrall", made.Text(3, 0), "the new row");
            board.Refresh();
            Expect.True(made.Writes > writes, "the changes written");
            var after = made.Writes;
            board.Refresh();
            Expect.Equal(after, made.Writes, "nothing changed, nothing written");
        }

        /// <summary>A player sees the newest board shown to them; when it ends or hides, the one before shows again.</summary>
        public static void Newest(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var a = Board.Create("A", 1, 1, Game.Instance);
            var b = Board.Create("B", 1, 1, holder);
            Expect.Same(world.Boards[1], world.DisplayedBoard, "the newest shows");
            b.Visible = false;
            Expect.Same(world.Boards[0], world.DisplayedBoard, "the one before, when the newest hides");
            b.Visible = true;
            Expect.Same(world.Boards[1], world.DisplayedBoard, "shown again, it is the newest");
            holder.End();
            Expect.Equal(true, world.Boards[1].Ended, "ended with its scope");
            Expect.Same(world.Boards[0], world.DisplayedBoard, "the one before, when the newest ends");
            a.End();
            Expect.Same(null, world.DisplayedBoard, "none");
        }

        /// <summary>A board for one player is made on every machine and displayed on theirs; the newest board shown to another player does not hide this one's.</summary>
        public static void ShownTo(FakeWorld world)
        {
            world.LocalSlot = 0;
            var mine = Board.Create("Mine", 1, 1, Game.Instance, shownTo: Player.One);
            var theirs = Board.Create("Theirs", 1, 1, Game.Instance, shownTo: Player.Two);
            Expect.Equal(2, world.Boards.Count, "both made here");
            Expect.Same(world.Boards[0], world.DisplayedBoard, "this machine's player's board");
            Expect.Equal(true, mine.IsShownTo(Player.One) && !theirs.IsShownTo(Player.One), "who sees each");
        }

        /// <summary>A board hides while the tooltip shows, and shows again after, unless it does not hide for tooltips.</summary>
        public static void Tooltips(FakeWorld world)
        {
            var board = Board.Create("Kills", 1, 1, Game.Instance);
            var made = world.Boards[0];
            world.TooltipUp = true;
            world.Advance(0.05f);
            Expect.Equal(false, made.Displayed, "hidden under a tooltip");
            world.TooltipUp = false;
            world.Advance(0.05f);
            Expect.Equal(true, made.Displayed, "back after");
            board.HidesForTooltips = false;
            world.TooltipUp = true;
            world.Advance(0.05f);
            Expect.Equal(true, made.Displayed, "kept when it does not hide for tooltips");
        }

        /// <summary>A leaderboard's rows come from any data, each keyed to its own slot, sorted highest first; it is given to every player and displayed here, and the newest wins.</summary>
        public static void Leaderboards(FakeWorld world)
        {
            world.LocalSlot = 0;
            var gold = new Dictionary<string, int> { ["West"] = 10, ["East"] = 30 };
            var teams = new List<string> { "West", "East" };
            var top = Leaderboard.Create("Team gold", teams, Game.Instance);
            top.Label(t => t).Value(t => gold[t]);
            var made = world.Leaderboards[0];
            world.Advance(0.01f);
            Expect.Equal("Team gold", made.Title, "the title");
            Expect.Equal("East 30", made.Shown[0], "highest first");
            Expect.Equal(true, made.Displayed, "displayed");
            Expect.True(made.GivenTo.Contains(0) && made.GivenTo.Contains(1), "given to every player");
            var writes = made.Writes;
            top.Refresh();
            Expect.Equal(writes, made.Writes, "nothing changed, nothing written");
            gold["West"] = 50;
            top.Refresh();
            Expect.Equal("West 50", made.Shown[0], "sorted again");
            var holder = Game.Instance.Add(new Holder());
            var mine = Leaderboard.Create("Mine", new List<Player> { Player.One }, holder, shownTo: Player.One);
            Expect.Equal(false, made.Displayed, "the newest shows instead");
            holder.End();
            Expect.Equal(true, world.Leaderboards[1].Ended, "ended with its scope, hidden first");
            Expect.Equal(true, made.Displayed, "the one before shows again");
            Expect.Equal(false, mine.Visible && !mine.HasEnded, "ended");
        }

        /// <summary>A leaderboard shows at most MaxRows rows, as the game keys each to a slot; the rest are left out and reported.</summary>
        public static void LeaderboardLimit(FakeWorld world)
        {
            var rows = new List<int>();
            for (var i = 0; i < Leaderboard.MaxRows + 2; i++)
            {
                rows.Add(i);
            }

            var top = Leaderboard.Create("Many", rows, Game.Instance);
            top.Value(i => i);
            var before = world.Printed.Count;
            world.Advance(0.01f);
            Expect.Equal(Leaderboard.MaxRows, world.Leaderboards[0].Rows.Count, "at most MaxRows rows");
            Expect.True(world.Printed.Count > before, "the rest reported");
        }

        /// <summary>A click runs its button's action with the clicking player and closes the dialog for them; the others still see it.</summary>
        public static void Dialogs(FakeWorld world)
        {
            var votes = new List<string>();
            var mode = Dialog.Create("Pick a mode", Game.Instance);
            mode.AddButton("Easy", who => votes.Add(who.Slot + " easy"), hotkey: 'E');
            mode.AddButton("Hard", who => votes.Add(who.Slot + " hard"));
            mode.ShowTo(new List<Player> { Player.One, Player.Two });
            var made = world.Dialogs[0];
            Expect.Equal("Pick a mode", made.Message, "the message");
            Expect.Equal('E', made.Buttons[0].Hotkey, "the hotkey");
            Expect.Equal(true, made.ShownTo.Contains(0) && made.ShownTo.Contains(1), "shown to both");
            world.ClickDialog(made, 1, 1);
            Expect.Equal("1 hard", votes[0], "the action with the clicking player");
            Expect.Equal(false, mode.IsShownTo(Player.Two), "closed for the clicker");
            Expect.Equal(true, mode.IsShownTo(Player.One), "still up for the other");
            mode.ShowTo(Player.Two);
            Expect.Equal(true, made.ShownTo.Contains(1), "shown again");
            mode.End();
            Expect.Equal(true, made.Ended && made.ShownTo.Count == 0, "taken off every screen and destroyed");
        }

        /// <summary>A second dialog for a player waits until the first is answered, or ends, then shows; one shown from inside a click waits behind those already waiting.</summary>
        public static void DialogQueue(FakeWorld world)
        {
            var order = new List<string>();
            var first = Dialog.Create("First", Game.Instance);
            var second = Dialog.Create("Second", Game.Instance);
            var third = Dialog.Create("Third", Game.Instance);
            first.AddButton("One", who =>
            {
                order.Add("one");
                third.ShowTo(who);
            });
            second.AddButton("Two", who => order.Add("two"));
            third.AddButton("Three", who => order.Add("three"));
            first.ShowTo(Player.One);
            second.ShowTo(Player.One);
            first.ShowTo(Player.One);
            Expect.Equal(true, first.IsShownTo(Player.One) && !second.IsShownTo(Player.One), "the second waits");
            world.ClickDialog(world.Dialogs[0], 0, 0);
            Expect.Equal(true, second.IsShownTo(Player.One), "the second shows once the first is answered");
            Expect.Equal(true, world.Dialogs[1].ShownTo.Contains(0), "on the screen");
            second.End();
            Expect.Equal(true, third.IsShownTo(Player.One), "the third, shown from the click, shows once the second ends");
            world.ClickDialog(world.Dialogs[2], 0, 0);
            Expect.Equal("one,three", string.Join(",", order), "the actions in order");
        }

        /// <summary>A dialog made in a scope that has ended is never made, and a button's action that throws is reported.</summary>
        public static void DialogEnds(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            holder.End();
            var late = Dialog.Create("Late", holder);
            late.AddButton("No", who => { });
            late.ShowTo(Player.One);
            Expect.Equal(0, world.Dialogs.Count, "never made");
            var broken = Dialog.Create("Broken", Game.Instance);
            broken.AddButton("Throw", who => throw new System.InvalidOperationException("broken"));
            broken.ShowTo(Player.One);
            var before = world.Printed.Count;
            world.ClickDialog(world.Dialogs[0], 0, 0);
            Expect.True(world.Printed.Count > before, "reported");
        }
    }
}
