using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A leaderboard: a title and rows of a label and a whole number, sorted highest first, in the top right, in a scope, which
    /// ends it when it ends, as End does. A player sees one leaderboard, the newest shown to them; when it ends or is hidden, the
    /// one shown before comes back. It shows beside a multiboard. Given shownTo, it is made on every machine and shown on that
    /// player's only (decision 206). Every read gives what was set, the same on every machine.
    /// </summary>
    public abstract class Leaderboard : Scoped
    {
        /// <summary>The most rows a leaderboard shows: the game keys each row to a player slot, and two rows on one slot crash it (seen 11-07).</summary>
        public const int MaxRows = 24;

        private readonly Player? shownTo;

        private leaderboard? handle;

        private string title;

        private bool visible = true;

        internal Leaderboard(string title, Player? shownTo)
        {
            this.title = title ?? "";
            this.shownTo = shownTo;
        }

        /// <summary>The title, over the rows.</summary>
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
                    Write();
                }
            }
        }

        /// <summary>Whether the leaderboard shows; one set false is kept and set again, and set true it is the newest shown.</summary>
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
                    Leaderboards.Raise(this);
                }
                else
                {
                    Leaderboards.Drop(this);
                }
            }
        }

        /// <summary>The game's leaderboard, for anything Moonwell does not do; null for one that ended before it was made.</summary>
        [LocalSafe]
        public leaderboard? Handle => handle;

        /// <summary>
        /// Shows a leaderboard with a row for each of the rows, labelled and valued by Label and Value, in a scope, which ends it
        /// when it ends, and returns it. Refresh reads the rows again; it runs by itself in the instant after, so Label and Value
        /// given on the next lines show. Given shownTo, only that player sees it. In a scope that has ended it ends at once.
        /// </summary>
        public static Leaderboard<T> Create<T>(string title, IEnumerable<T> rows, Scope scope, Player? shownTo = null)
        {
            var made = new Leaderboard<T>(title, rows, shownTo);
            scope.Adopt(made);
            if (made.HasEnded)
            {
                return made;
            }

            made.handle = Runtime.World.MakeLeaderboard();
            made.Write();
            Leaderboards.Raise(made);
            Game.Instance.OwnAfter(0.0f, made.Refresh);
            return made;
        }

        /// <summary>Whether a player sees the leaderboard when it is the newest shown to them: the one it was given to, or everyone. The same on every machine.</summary>
        [LocalSafe]
        public bool IsShownTo(Player player)
        {
            return shownTo == null || shownTo == player;
        }

        /// <summary>Reads the rows again and writes them when they changed.</summary>
        public abstract void Refresh();

        /// <summary>Whether it is shown, to whoever it is shown to.</summary>
        internal bool Shown => visible && !HasEnded && handle != null;

        /// <summary>The labels and values last written, in the rows' order.</summary>
        internal List<string> Labels { get; } = new List<string>();

        internal List<int> Values { get; } = new List<int>();

        /// <summary>Writes the title and the rows to the game's leaderboard.</summary>
        internal void Write()
        {
            if (handle != null && !HasEnded)
            {
                Runtime.World.WriteLeaderboard(handle, title, Labels, Values);
            }
        }

        /// <summary>Takes the leaderboard off the screen, showing the one before, and destroys it, which is safe once hidden (seen 11-07).</summary>
        internal override void Finish()
        {
            Leaderboards.Drop(this);
            if (handle != null)
            {
                Runtime.World.EndLeaderboard(handle);
                handle = null;
            }
        }
    }

    /// <summary>A leaderboard with a row for each of its rows, read again by Refresh.</summary>
    /// <typeparam name="T">What a row is: a player, a team, a hero, anything.</typeparam>
    public sealed class Leaderboard<T> : Leaderboard
    {
        private readonly IEnumerable<T> rows;

        private Func<T, string>? label;

        private Func<T, int>? value;

        internal Leaderboard(string title, IEnumerable<T> rows, Player? shownTo)
            : base(title, shownTo)
        {
            this.rows = rows;
        }

        /// <summary>Sets a row's label: a player's name unless given, or the row as text. Returns the leaderboard.</summary>
        public Leaderboard<T> Label(Func<T, string> label)
        {
            this.label = label;
            return this;
        }

        /// <summary>Sets a row's value, which the rows are sorted by, highest first; 0 unless given. Returns the leaderboard.</summary>
        public Leaderboard<T> Value(Func<T, int> value)
        {
            this.value = value;
            return this;
        }

        /// <summary>Reads the rows again and writes them when they changed; the map calls it when its data changes, or on a timer: Every(1f, top.Refresh). Rows past MaxRows are left out and reported.</summary>
        public override void Refresh()
        {
            if (HasEnded)
            {
                return;
            }

            var labels = new List<string>();
            var values = new List<int>();
            foreach (var row in rows)
            {
                if (labels.Count == MaxRows)
                {
                    Errors.Refuse("Leaderboard.Refresh", "a leaderboard shows at most " + MaxRows + " rows; the rest are left out");
                    break;
                }

                labels.Add(label != null ? label(row) ?? "" : row is Player player ? player.Name : row?.ToString() ?? "");
                values.Add(value != null ? value(row) : 0);
            }

            if (Same(labels, Labels) && Same(values, Values))
            {
                return;
            }

            Labels.Clear();
            Labels.AddRange(labels);
            Values.Clear();
            Values.AddRange(values);
            Write();
        }

        private static bool Same(List<string> a, List<string> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Same(List<int> a, List<int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>The leaderboards shown, in the order shown, the same on every machine: each player is given the newest shown to them, and this machine displays its player's.</summary>
    internal static class Leaderboards
    {
        private static readonly List<Leaderboard> order = new List<Leaderboard>();

        /// <summary>What each slot was given last, the same on every machine.</summary>
        private static readonly Leaderboard?[] given = new Leaderboard?[Leaderboard.MaxRows];

        /// <summary>The leaderboard this machine displays: its own.</summary>
        private static Leaderboard? here;

        internal static void Reset()
        {
            order.Clear();
            for (var slot = 0; slot < given.Length; slot++)
            {
                given[slot] = null;
            }

            here = null;
        }

        internal static void Raise(Leaderboard board)
        {
            order.Remove(board);
            order.Add(board);
            Update();
        }

        internal static void Drop(Leaderboard board)
        {
            order.Remove(board);
            Update();
        }

        private static void Update()
        {
            Leaderboard? local = null;
            for (var slot = 0; slot < given.Length; slot++)
            {
                var player = Player.Get(slot);
                Leaderboard? top = null;
                for (var i = order.Count - 1; i >= 0 && top == null; i--)
                {
                    if (order[i].Shown && order[i].IsShownTo(player))
                    {
                        top = order[i];
                    }
                }

                if (top != null && top != given[slot] && top.Handle != null)
                {
                    Runtime.World.GiveLeaderboard(player.Handle, top.Handle);
                }

                given[slot] = top;
                if (Runtime.World.IsLocal(player.Handle))
                {
                    local = top;
                }
            }

            if (local == here)
            {
                return;
            }

            if (here?.Handle != null)
            {
                Runtime.World.DisplayLeaderboard(here.Handle, false);
            }

            here = local;
            if (here?.Handle != null)
            {
                Runtime.World.DisplayLeaderboard(here.Handle, true);
            }
        }
    }
}
