using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>The fake world's multiboards, leaderboards and dialogs.</summary>
    public sealed partial class FakeWorld
    {
        /// <summary>The multiboards the runtime made, in the order made.</summary>
        public List<FakeBoard> Boards = new List<FakeBoard>();

        /// <summary>The leaderboards the runtime made, in the order made.</summary>
        public List<FakeLeaderboard> Leaderboards = new List<FakeLeaderboard>();

        /// <summary>The dialogs the runtime made, in the order made.</summary>
        public List<FakeDialog> Dialogs = new List<FakeDialog>();

        /// <summary>Whether the game's tooltip shows on this machine.</summary>
        public bool TooltipUp;

        private readonly Dictionary<multiboard, FakeBoard> boards = new Dictionary<multiboard, FakeBoard>();

        private readonly Dictionary<leaderboard, FakeLeaderboard> leaderboards = new Dictionary<leaderboard, FakeLeaderboard>();

        private readonly Dictionary<dialog, FakeDialog> dialogs = new Dictionary<dialog, FakeDialog>();

        /// <summary>The board this machine displays, as the game shows one at a time; null for none.</summary>
        public FakeBoard? DisplayedBoard
        {
            get
            {
                foreach (var board in Boards)
                {
                    if (board.Displayed && !board.Ended)
                    {
                        return board;
                    }
                }

                return null;
            }
        }

        /// <summary>Clicks a button of a dialog for a player, as the game does: it closes the dialog for them, then fires the button's event.</summary>
        public void ClickDialog(FakeDialog dialog, int button, int slot)
        {
            if (!dialog.ShownTo.Contains(slot) || dialog.Ended)
            {
                throw new ExpectationFailed("a click on a dialog that is not on the player's screen");
            }

            dialog.ShownTo.Remove(slot);
            dialog.Buttons[button].Clicked(Player.Get(slot).Handle);
        }

        /// <summary>Makes a multiboard.</summary>
        public multiboard MakeBoard()
        {
            var made = new multiboard();
            var fake = new FakeBoard();
            boards[made] = fake;
            Boards.Add(fake);
            return made;
        }

        /// <summary>Sizes a multiboard.</summary>
        public void SizeBoard(multiboard board, int rows, int columns)
        {
            var fake = LiveBoard(board);
            fake.Rows = rows;
            fake.Columns = columns;
        }

        /// <summary>Titles a multiboard.</summary>
        public void TitleBoard(multiboard board, string title)
        {
            LiveBoard(board).Title = title;
        }

        /// <summary>Writes a cell; one outside the board fails the case.</summary>
        public void WriteCell(multiboard board, int row, int column, string text, string icon, Color color, float width)
        {
            var fake = LiveBoard(board);
            if (row >= fake.Rows || column >= fake.Columns)
            {
                throw new ExpectationFailed("a cell written outside the board");
            }

            fake.Cells[row + "," + column] = new FakeCell { Text = text, Icon = icon, Color = color, Width = width };
            fake.Writes++;
        }

        /// <summary>Displays or hides a multiboard on this machine; displaying one hides the rest, as the game does (seen 11-07).</summary>
        public void DisplayBoard(multiboard board, bool shown)
        {
            var fake = LiveBoard(board);
            if (shown)
            {
                foreach (var other in Boards)
                {
                    other.Displayed = false;
                }
            }

            fake.Displayed = shown;
            if (shown)
            {
                fake.Displays++;
            }
        }

        /// <summary>Destroys a multiboard; a second end fails the case.</summary>
        public void EndBoard(multiboard board)
        {
            var fake = LiveBoard(board);
            fake.Ended = true;
            fake.Displayed = false;
        }

        /// <summary>Whether the tooltip shows.</summary>
        public bool TooltipShown()
        {
            return TooltipUp;
        }

        /// <summary>Makes a leaderboard.</summary>
        public leaderboard MakeLeaderboard()
        {
            var made = new leaderboard();
            var fake = new FakeLeaderboard();
            leaderboards[made] = fake;
            Leaderboards.Add(fake);
            return made;
        }

        /// <summary>Writes a leaderboard: its rows sorted highest first, as the game shows them.</summary>
        public void WriteLeaderboard(leaderboard board, string title, List<string> labels, List<int> values)
        {
            var fake = LiveLeaderboard(board);
            fake.Title = title;
            fake.Rows.Clear();
            for (var i = 0; i < labels.Count; i++)
            {
                fake.Rows.Add(labels[i] + " " + values[i]);
            }

            var sorted = new List<int>();
            for (var i = 0; i < values.Count; i++)
            {
                sorted.Add(i);
            }

            sorted.Sort((a, b) => values[b] != values[a] ? values[b].CompareTo(values[a]) : a.CompareTo(b));
            fake.Shown.Clear();
            foreach (var i in sorted)
            {
                fake.Shown.Add(labels[i] + " " + values[i]);
            }

            fake.Writes++;
        }

        /// <summary>Gives a leaderboard to a player.</summary>
        public void GiveLeaderboard(player player, leaderboard board)
        {
            LiveLeaderboard(board).GivenTo.Add(SlotOf(player));
        }

        /// <summary>Displays or hides a leaderboard on this machine.</summary>
        public void DisplayLeaderboard(leaderboard board, bool shown)
        {
            LiveLeaderboard(board).Displayed = shown;
        }

        /// <summary>Destroys a leaderboard; it must be hidden first (Moonwell's rule, from the crash probes of 11-07).</summary>
        public void EndLeaderboard(leaderboard board)
        {
            var fake = LiveLeaderboard(board);
            if (fake.Displayed)
            {
                throw new ExpectationFailed("a leaderboard destroyed while displayed");
            }

            fake.Ended = true;
        }

        /// <summary>Makes a dialog.</summary>
        public dialog MakeDialog(string message)
        {
            var made = new dialog();
            var fake = new FakeDialog { Message = message };
            dialogs[made] = fake;
            Dialogs.Add(fake);
            return made;
        }

        /// <summary>Adds a button.</summary>
        public void AddDialogButton(dialog dialog, string text, int hotkey, Action<player> clicked)
        {
            LiveDialog(dialog).Buttons.Add(new FakeButton { Text = text, Hotkey = hotkey, Clicked = clicked });
        }

        /// <summary>Shows a dialog to a player or hides it; showing it hides the player's other dialog, as the game does (seen 11-07).</summary>
        public void DisplayDialog(player player, dialog dialog, bool shown)
        {
            var slot = SlotOf(player);
            var fake = LiveDialog(dialog);
            if (shown)
            {
                foreach (var other in Dialogs)
                {
                    other.ShownTo.Remove(slot);
                }

                fake.ShownTo.Add(slot);
            }
            else
            {
                fake.ShownTo.Remove(slot);
            }
        }

        /// <summary>Destroys a dialog; a second end fails the case.</summary>
        public void EndDialog(dialog dialog)
        {
            LiveDialog(dialog).Ended = true;
        }

        private FakeBoard LiveBoard(multiboard board)
        {
            var fake = boards[board];
            if (fake.Ended)
            {
                throw new ExpectationFailed("the runtime asked the game about a multiboard it had ended");
            }

            return fake;
        }

        private FakeLeaderboard LiveLeaderboard(leaderboard board)
        {
            var fake = leaderboards[board];
            if (fake.Ended)
            {
                throw new ExpectationFailed("the runtime asked the game about a leaderboard it had ended");
            }

            return fake;
        }

        private FakeDialog LiveDialog(dialog dialog)
        {
            var fake = dialogs[dialog];
            if (fake.Ended)
            {
                throw new ExpectationFailed("the runtime asked the game about a dialog it had ended");
            }

            return fake;
        }
    }

    /// <summary>A multiboard of the fake world.</summary>
    public sealed class FakeBoard
    {
        /// <summary>Its title and size.</summary>
        public string Title = "";

        public int Rows, Columns;

        /// <summary>Its cells by "row,column", and how many writes it had.</summary>
        public Dictionary<string, FakeCell> Cells = new Dictionary<string, FakeCell>();

        public int Writes;

        /// <summary>Whether this machine displays it, whether it ended, and how often it was displayed.</summary>
        public bool Displayed, Ended;

        public int Displays;

        /// <summary>A cell's text.</summary>
        public string Text(int row, int column)
        {
            return Cells[row + "," + column].Text;
        }
    }

    /// <summary>A cell of a fake multiboard.</summary>
    public sealed class FakeCell
    {
        public string Text = "", Icon = "";

        public Color Color;

        public float Width;
    }

    /// <summary>A leaderboard of the fake world.</summary>
    public sealed class FakeLeaderboard
    {
        public string Title = "";

        /// <summary>Its rows as "label value", in the order written, and in the order shown, highest first.</summary>
        public List<string> Rows = new List<string>(), Shown = new List<string>();

        /// <summary>The slots it was given to, in order.</summary>
        public List<int> GivenTo = new List<int>();

        public int Writes;

        public bool Displayed, Ended;
    }

    /// <summary>A dialog of the fake world.</summary>
    public sealed class FakeDialog
    {
        public string Message = "";

        public List<FakeButton> Buttons = new List<FakeButton>();

        /// <summary>The slots whose screen shows it.</summary>
        public HashSet<int> ShownTo = new HashSet<int>();

        public bool Ended;
    }

    /// <summary>A button of a fake dialog.</summary>
    public sealed class FakeButton
    {
        public string Text = "";

        public int Hotkey;

        public Action<player> Clicked = p => { };
    }
}
