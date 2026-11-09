using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A ready scoreboard of the UI kit (decision 223): a window with a row for each player in the game, in their colour, and a
    /// column for each Score or value given. When a team has more than one player, the players go under a line for each
    /// team with its totals. Rows are sorted by a number column, highest first: the first unless SortBy names another. A
    /// Score column follows its score by itself; call Refresh after what a value column reads has changed. It shows and
    /// hides with Visible, as any window.
    /// </summary>
    public class Scoreboard : Window
    {
        /// <summary>A row's height, the names' width and a value's width.</summary>
        internal const float RowHeight = 0.018f, NameWidth = 0.12f, ValueWidth = 0.05f;

        /// <summary>The body's top below the window's, under the title bar.</summary>
        internal const float Top = BarTop + BarHeight + 0.006f;

        internal readonly List<BoardColumn> columns = new List<BoardColumn>();

        /// <summary>The labels, a row of 1 + columns at a time: the header, then the team lines and the players' rows.</summary>
        internal readonly List<Label> cells = new List<Label>();

        internal string? sortBy;

        internal readonly Action<Player> follow;

        /// <summary>Makes a scoreboard titled Scores with no columns, to be given columns and added to the screen: Screen.Add(board, Game.Instance).</summary>
        public Scoreboard()
        {
            Title = "Scores";
            follow = who => Refresh();
            Refresh();
        }

        /// <summary>Adds a column of a score, which follows it by itself; its total shows on a team's line.</summary>
        public void Column(Score score)
        {
            if (HasEnded)
            {
                return;
            }

            columns.Add(new BoardColumn(score.Name, who => score[who], null));
            (score.changed ??= new List<Action<Player>>()).Add(follow);
            scores.Add(score);
            Refresh();
        }

        /// <summary>Adds a column of a number each player has, read again at Refresh; its total shows on a team's line.</summary>
        public void Column(string name, Func<Player, int> value)
        {
            columns.Add(new BoardColumn(name, value, null));
            Refresh();
        }

        /// <summary>Adds a column of a text each player has, read again at Refresh; it has no total and does not sort.</summary>
        public void Column(string name, Func<Player, string> text)
        {
            columns.Add(new BoardColumn(name, null, text));
            Refresh();
        }

        /// <summary>Sorts the rows by a number column, by its name, highest first; the first number column unless set.</summary>
        public void SortBy(string column)
        {
            sortBy = column;
            Refresh();
        }

        internal readonly List<Score> scores = new List<Score>();

        /// <summary>Writes every row again: the players in the game, their teams, the values and the order.</summary>
        public void Refresh()
        {
            if (HasEnded)
            {
                return;
            }

            var sort = SortColumn();
            var lines = new List<string[]>();
            var header = new string[columns.Count + 1];
            header[0] = "Player";
            for (var c = 0; c < columns.Count; c++)
            {
                header[c + 1] = columns[c].Name;
            }

            lines.Add(header);
            var teams = new List<List<Player>>();
            var playing = Player.Playing;
            var grouped = false;
            for (var i = 0; i < playing.Count; i++)
            {
                var team = TeamIn(teams, playing[i].Team);
                team.Add(playing[i]);
                grouped |= team.Count > 1;
            }

            if (!grouped)
            {
                teams = new List<List<Player>> { playing };
            }

            for (var t = 0; t < teams.Count; t++)
            {
                SortPlayers(teams[t], sort);
            }

            if (sort != null)
            {
                teams.Sort((a, b) =>
                {
                    var order = Total(b, sort).CompareTo(Total(a, sort));
                    return order != 0 ? order : a[0].Slot.CompareTo(b[0].Slot);
                });
            }

            var teamLines = new List<int>();
            for (var t = 0; t < teams.Count; t++)
            {
                if (grouped)
                {
                    teamLines.Add(lines.Count);
                    lines.Add(TeamLine(teams[t]));
                }

                for (var p = 0; p < teams[t].Count; p++)
                {
                    lines.Add(PlayerLine(teams[t][p]));
                }
            }

            Write(lines, teamLines);
        }

        /// <summary>Takes the scoreboard off the scores it follows.</summary>
        protected override void OnEnd()
        {
            for (var i = 0; i < scores.Count; i++)
            {
                scores[i].changed?.Remove(follow);
            }
        }

        private BoardColumn? SortColumn()
        {
            for (var c = 0; c < columns.Count; c++)
            {
                if (columns[c].Value != null && (sortBy == null || columns[c].Name == sortBy))
                {
                    return columns[c];
                }
            }

            return null;
        }

        private static List<Player> TeamIn(List<List<Player>> teams, Team team)
        {
            for (var t = 0; t < teams.Count; t++)
            {
                if (teams[t][0].Team == team)
                {
                    return teams[t];
                }
            }

            var made = new List<Player>();
            teams.Add(made);
            return made;
        }

        // Highest first, then in slot order, so rows keep their order while scores tie.
        private static void SortPlayers(List<Player> players, BoardColumn? sort)
        {
            if (sort == null)
            {
                return;
            }

            players.Sort((a, b) =>
            {
                var order = sort.Value!(b).CompareTo(sort.Value!(a));
                return order != 0 ? order : a.Slot.CompareTo(b.Slot);
            });
        }

        private static int Total(List<Player> players, BoardColumn column)
        {
            var total = 0;
            for (var i = 0; i < players.Count; i++)
            {
                total += column.Value!(players[i]);
            }

            return total;
        }

        private string[] TeamLine(List<Player> players)
        {
            var line = new string[columns.Count + 1];
            line[0] = players[0].Team.Name;
            for (var c = 0; c < columns.Count; c++)
            {
                line[c + 1] = columns[c].Value != null ? Total(players, columns[c]).ToString() : "";
            }

            return line;
        }

        private string[] PlayerLine(Player player)
        {
            var line = new string[columns.Count + 1];
            line[0] = player.ColoredName;
            for (var c = 0; c < columns.Count; c++)
            {
                line[c + 1] = columns[c].Value != null ? columns[c].Value!(player).ToString() : columns[c].Text!(player);
            }

            return line;
        }

        // Keeps a label for each cell, placed again by row and column on every write, as a column added moves the cells after it, and sizes the window to the rows.
        private void Write(List<string[]> lines, List<int> teamLines)
        {
            var width = columns.Count + 1;
            var count = lines.Count * width;
            while (cells.Count < count)
            {
                cells.Add(Add(new Label { Anchor = FramePoint.TopLeft }));
            }

            while (cells.Count > count)
            {
                cells[cells.Count - 1].End();
                cells.RemoveAt(cells.Count - 1);
            }

            for (var row = 0; row < lines.Count; row++)
            {
                var title = row == 0 || teamLines.Contains(row);
                for (var column = 0; column < width; column++)
                {
                    var cell = cells[row * width + column];
                    cell.Offset = new Vector2(Inset + (column == 0 ? 0.0f : NameWidth + (column - 1) * ValueWidth), -Top - row * RowHeight);
                    cell.Text = lines[row][column];
                    cell.Color = title ? theme.Title : theme.Text;
                }
            }

            Width = 2.0f * Inset + NameWidth + columns.Count * ValueWidth;
            Height = Top + lines.Count * RowHeight + Inset;
        }

        /// <summary>A column: its name, and the number or the text it reads for a player.</summary>
        internal sealed class BoardColumn
        {
            internal BoardColumn(string name, Func<Player, int>? value, Func<Player, string>? text)
            {
                Name = name;
                Value = value;
                Text = text;
            }

            internal string Name { get; }

            internal Func<Player, int>? Value { get; }

            internal Func<Player, string>? Text { get; }
        }
    }
}
