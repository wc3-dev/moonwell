using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A multiboard: a table of cells in the top right, in a scope, which ends it when it ends, as End does. A player sees one
    /// multiboard, the newest shown to them; when it ends or is hidden, the one shown before comes back (seen 11-07). It hides
    /// while the game's tooltip for a button shows, which it would cover, unless HidesForTooltips is false. Given shownTo, it is
    /// made on every machine and shown on that player's only (decision 206). Every read gives what was set, the same on every
    /// machine.
    /// </summary>
    public class Board : Scoped
    {
        /// <summary>The width a cell has unless given one, in the game's screen units (the screen is 0.8 wide).</summary>
        public const float DefaultWidth = 0.06f;

        private readonly Player? shownTo;

        private readonly List<List<BoardCell>> cells = new List<List<BoardCell>>();

        private multiboard? handle;

        private string title;

        private int columns;

        private bool visible = true;

        private bool hidesForTooltips = true;

        private bool relayoutDue;

        internal Board(string title, Player? shownTo)
        {
            this.title = title ?? "";
            this.shownTo = shownTo;
        }

        /// <summary>The title, over the cells.</summary>
        [LocalSafe]
        public string Title
        {
            get
            {
                return title;
            }

            set
            {
                if (!HasEnded)
                {
                    title = value ?? "";
                    if (handle != null)
                    {
                        Runtime.World.TitleBoard(handle, title);
                    }
                }
            }
        }

        /// <summary>The rows, read and set; cells a new row brings are empty.</summary>
        [LocalSafe]
        public int Rows
        {
            get
            {
                return cells.Count;
            }

            set
            {
                Resize(value, columns);
            }
        }

        /// <summary>The columns, read and set; cells a new column brings are empty.</summary>
        [LocalSafe]
        public int Columns
        {
            get
            {
                return columns;
            }

            set
            {
                Resize(cells.Count, value);
            }
        }

        /// <summary>Whether the board shows; one set false is kept and set again, and set true it is the newest shown.</summary>
        [LocalSafe]
        public bool Visible
        {
            get
            {
                return visible;
            }

            set
            {
                if (HasEnded || handle == null)
                {
                    return;
                }

                visible = value;
                if (value)
                {
                    Boards.Raise(this);
                }
                else
                {
                    Boards.Drop(this);
                }
            }
        }

        /// <summary>Whether the board hides while the game's tooltip for a button shows; true unless set.</summary>
        [LocalSafe]
        public bool HidesForTooltips
        {
            get
            {
                return hidesForTooltips;
            }

            set
            {
                hidesForTooltips = value;
                Boards.Changed(this);
            }
        }

        /// <summary>The game's multiboard, for anything Moonwell does not do; null for one that ended before it was made.</summary>
        [LocalSafe]
        public multiboard? Handle => handle;

        /// <summary>A cell, by row and column from 0. One outside the board is reported, and setting it does nothing.</summary>
        [LocalSafe]
        public BoardCell this[int row, int column]
        {
            get
            {
                if (row < 0 || row >= cells.Count || column < 0 || column >= columns)
                {
                    Errors.Refuse("Board[" + row + ", " + column + "]", "the board has " + cells.Count + " rows and " + columns + " columns");
                    return new BoardCell(null, 0, 0);
                }

                return cells[row][column];
            }
        }

        /// <summary>
        /// Shows a board of empty cells, in a scope, which ends it when it ends, and returns it. Given shownTo, only that player
        /// sees it. In a scope that has ended it ends at once.
        /// </summary>
        public static Board Create(string title, int columns, int rows, Scope scope, Player? shownTo = null)
        {
            var made = new Board(title, shownTo);
            made.Open(scope, rows, columns);
            return made;
        }

        /// <summary>
        /// Shows a board with a row for each of the rows, whose columns are added with Column, in a scope, which ends it when it
        /// ends, and returns it. Refresh reads the rows again; it runs by itself in the instant after, so the columns added on the
        /// next lines show. Given shownTo, only that player sees it. In a scope that has ended it ends at once.
        /// </summary>
        public static Board<T> Create<T>(string title, IEnumerable<T> rows, Scope scope, Player? shownTo = null)
        {
            var made = new Board<T>(title, rows, shownTo);
            made.Open(scope, 0, 0);
            if (!made.HasEnded)
            {
                Game.Instance.OwnAfter(0.0f, made.Refresh);
            }

            return made;
        }

        /// <summary>Whether a player sees the board when it is the newest shown to them: the one it was given to, or everyone. The same on every machine.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return shownTo == null || shownTo == player;
        }

        /// <summary>Whether this machine's player is to see it: shown, and to them.</summary>
        internal bool ShownHere => visible && !HasEnded && handle != null && (shownTo == null || Runtime.World.IsLocal(shownTo.Handle));

        /// <summary>Makes the game's board in the scope, sized, and shows it as the newest.</summary>
        internal void Open(Scope scope, int rows, int columns)
        {
            scope.Adopt(this);
            if (HasEnded)
            {
                return;
            }

            handle = Runtime.World.MakeBoard();
            Runtime.World.TitleBoard(handle, title);
            Resize(rows, columns);
            Boards.Raise(this);
        }

        /// <summary>Sets the rows and columns, writing every cell they bring, as the game shows an eye icon in a cell not written.</summary>
        internal void Resize(int rows, int columns)
        {
            if (HasEnded || handle == null)
            {
                return;
            }

            rows = Math.Max(0, rows);
            columns = Math.Max(0, columns);
            if (rows == cells.Count && columns == this.columns)
            {
                return;
            }

            while (cells.Count > rows)
            {
                cells.RemoveAt(cells.Count - 1);
            }

            foreach (var line in cells)
            {
                while (line.Count > columns)
                {
                    line.RemoveAt(line.Count - 1);
                }
            }

            while (cells.Count < rows)
            {
                cells.Add(new List<BoardCell>());
            }

            this.columns = columns;
            Runtime.World.SizeBoard(handle, rows, columns);
            for (var row = 0; row < rows; row++)
            {
                var line = cells[row];
                while (line.Count < columns)
                {
                    var cell = new BoardCell(this, row, line.Count);
                    line.Add(cell);
                    Write(cell, false);
                }
            }

            Relayout();
        }

        /// <summary>Writes a cell to the game's board; a new width or icon also lays the board out again.</summary>
        internal void Write(BoardCell cell, bool layout)
        {
            if (handle != null && !HasEnded)
            {
                Runtime.World.WriteCell(handle, cell.Row, cell.Column, cell.Text, cell.Icon, cell.Color, cell.Width);
                if (layout)
                {
                    Relayout();
                }
            }
        }

        // A displayed board keeps the layout it was displayed with: a new column, or a width, shows only once it is displayed
        // again (seen 11-07). So it is displayed again once, in the instant after its changes.
        private void Relayout()
        {
            if (relayoutDue)
            {
                return;
            }

            relayoutDue = true;
            Game.Instance.OwnAfter(0.0f, () =>
            {
                relayoutDue = false;
                if (!HasEnded)
                {
                    Boards.Relayout(this);
                }
            });
        }

        /// <summary>Takes the board off the screen, showing the one before, and destroys it.</summary>
        internal override void Finish()
        {
            Boards.Drop(this);
            if (handle != null)
            {
                Runtime.World.EndBoard(handle);
                handle = null;
            }
        }
    }

    /// <summary>A cell of a board: a text, an icon on its left, the text's colour and the cell's width. It lasts as long as its board has its row and column.</summary>
    public sealed class BoardCell
    {
        private readonly Board? board;

        private string text = "";

        private string icon = "";

        private Color color = Color.White;

        private float width = Board.DefaultWidth;

        internal BoardCell(Board? board, int row, int column)
        {
            this.board = board;
            Row = row;
            Column = column;
        }

        /// <summary>The row, from 0.</summary>
        [LocalSafe]
        public int Row { get; }

        /// <summary>The column, from 0.</summary>
        [LocalSafe]
        public int Column { get; }

        /// <summary>The text; colour codes work in it.</summary>
        [LocalSafe]
        public string Text
        {
            get
            {
                return text;
            }

            set
            {
                if ((value ?? "") != text)
                {
                    text = value ?? "";
                    board?.Write(this, false);
                }
            }
        }

        /// <summary>An icon's path, shown left of the text; empty for none, the default.</summary>
        [LocalSafe]
        public string Icon
        {
            get
            {
                return icon;
            }

            set
            {
                if ((value ?? "") != icon)
                {
                    icon = value ?? "";
                    board?.Write(this, true);
                }
            }
        }

        /// <summary>The text's colour; white unless set.</summary>
        [LocalSafe]
        public Color Color
        {
            get
            {
                return color;
            }

            set
            {
                if (value != color)
                {
                    color = value;
                    board?.Write(this, false);
                }
            }
        }

        /// <summary>The cell's width in the game's screen units; Board.DefaultWidth unless set.</summary>
        [LocalSafe]
        public float Width
        {
            get
            {
                return width;
            }

            set
            {
                if (value != width)
                {
                    width = value;
                    board?.Write(this, true);
                }
            }
        }
    }

    /// <summary>
    /// A board with a row for each of its rows, read again by Refresh, and a column for each Column added: a header row of the
    /// columns' names first, in the game's gold, when any column has a name.
    /// </summary>
    /// <typeparam name="T">What a row is: a player, a team, a hero, anything.</typeparam>
    public sealed class Board<T> : Board
    {
        private readonly IEnumerable<T> rows;

        private readonly List<BoardColumn> columnList = new List<BoardColumn>();

        internal Board(string title, IEnumerable<T> rows, Player? shownTo)
            : base(title, shownTo)
        {
            this.rows = rows;
        }

        /// <summary>Adds a column: its name in the header, its text for a row, its width, and an icon for a row; returns the board.</summary>
        public Board<T> Column(string name, Func<T, string> text, float width = DefaultWidth, Func<T, string>? icon = null)
        {
            columnList.Add(new BoardColumn(name ?? "", text, width, icon));
            return this;
        }

        /// <summary>Reads the rows again and writes the cells that changed; the map calls it when its data changes, or on a timer: Every(0.5f, board.Refresh).</summary>
        public void Refresh()
        {
            if (HasEnded)
            {
                return;
            }

            var list = new List<T>();
            foreach (var row in rows)
            {
                list.Add(row);
            }

            var header = false;
            foreach (var column in columnList)
            {
                header = header || column.Name != "";
            }

            var top = header ? 1 : 0;
            Resize(list.Count + top, columnList.Count);
            for (var c = 0; c < columnList.Count; c++)
            {
                var column = columnList[c];
                if (header)
                {
                    var name = this[0, c];
                    name.Text = column.Name;
                    name.Color = Color.TooltipGold;
                    name.Width = column.Width;
                }

                for (var r = 0; r < list.Count; r++)
                {
                    var cell = this[r + top, c];
                    cell.Text = column.Text(list[r]);
                    cell.Width = column.Width;
                    if (column.IconOf != null)
                    {
                        cell.Icon = column.IconOf(list[r]);
                    }
                }
            }
        }

        private sealed class BoardColumn
        {
            public readonly string Name;

            public readonly Func<T, string> Text;

            public readonly float Width;

            public readonly Func<T, string>? IconOf;

            public BoardColumn(string name, Func<T, string> text, float width, Func<T, string>? icon)
            {
                Name = name;
                Text = text;
                Width = width;
                IconOf = icon;
            }
        }
    }

    /// <summary>The boards shown, in the order shown, the same on every machine; this machine displays the newest its player is to see.</summary>
    internal static class Boards
    {
        /// <summary>How often this machine checks the game's tooltip: 0.03 s showed no flicker (seen 11-06).</summary>
        private const float TooltipCheck = 0.03f;

        private static readonly List<Board> order = new List<Board>();

        /// <summary>The board this machine displays: its own, as is the tooltip's state.</summary>
        private static Board? here;

        private static bool tooltip;

        private static Timer? check;

        internal static void Reset()
        {
            order.Clear();
            here = null;
            tooltip = false;
            check = null;
        }

        /// <summary>Puts a board on top, the newest shown.</summary>
        internal static void Raise(Board board)
        {
            order.Remove(board);
            order.Add(board);
            Update();
        }

        /// <summary>Takes a board out of the order; the one before it shows again.</summary>
        internal static void Drop(Board board)
        {
            order.Remove(board);
            Update();
        }

        /// <summary>Displays a board again on this machine, if it is the one displayed here, so the game lays it out again.</summary>
        internal static void Relayout(Board board)
        {
            if (board == here && board.Handle != null)
            {
                Runtime.World.DisplayBoard(board.Handle, false);
                Display();
            }
        }

        /// <summary>A board's tooltip setting changed.</summary>
        internal static void Changed(Board board)
        {
            if (board == here)
            {
                Display();
            }
        }

        private static void Update()
        {
            Board? top = null;
            for (var i = order.Count - 1; i >= 0 && top == null; i--)
            {
                if (order[i].ShownHere)
                {
                    top = order[i];
                }
            }

            if (top != here)
            {
                if (here?.Handle != null)
                {
                    Runtime.World.DisplayBoard(here.Handle, false);
                }

                here = top;
                Display();
            }

            if (order.Count > 0 && check == null)
            {
                check = Game.Instance.OwnEvery(TooltipCheck, Check);
            }
            else if (order.Count == 0 && check != null)
            {
                check.End();
                check = null;
            }
        }

        private static void Display()
        {
            if (here?.Handle != null)
            {
                Runtime.World.DisplayBoard(here.Handle, !(tooltip && here.HidesForTooltips));
            }
        }

        // Runs on every machine; what it reads and displays is this machine's own.
        private static void Check()
        {
            var shown = Runtime.World.TooltipShown();
            if (shown != tooltip)
            {
                tooltip = shown;
                if (here != null && here.HidesForTooltips)
                {
                    Display();
                }
            }
        }
    }
}
