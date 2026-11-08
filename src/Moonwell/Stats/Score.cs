using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A whole number for each player that the map counts (decision 222), as kills in its own death reaction: made once, as a
    /// static readonly field, public static readonly Score Kills = new Score("Kills"). Every player starts at 0. Moonwell
    /// counts nothing itself and saves nothing: a save class copies what it keeps.
    /// </summary>
    public sealed class Score
    {
        private readonly Dictionary<Player, int> values = new Dictionary<Player, int>();

        /// <summary>Run with the player whose score changed, as the kit's Scoreboard follows a score.</summary>
        internal List<Action<Player>>? changed;

        /// <summary>Makes a score, best kept in a static readonly field.</summary>
        public Score(string name)
        {
            Name = name;
        }

        /// <summary>The name, for a board's column.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>A player's score; 0 until counted.</summary>
        [LocalSafe]
        public int this[Player player]
        {
            get { return values.TryGetValue(player, out var value) ? value : 0; }
        }

        /// <summary>Adds to a player's score; a negative amount takes away.</summary>
        public void Add(Player player, int amount)
        {
            Set(player, this[player] + amount);
        }

        /// <summary>Sets a player's score.</summary>
        public void Set(Player player, int value)
        {
            if (this[player] == value)
            {
                return;
            }

            values[player] = value;
            if (changed == null)
            {
                return;
            }

            var now = changed.ToArray();
            for (var i = 0; i < now.Length; i++)
            {
                try
                {
                    now[i](player);
                }
                catch (Exception error)
                {
                    Errors.Report("Score " + Name, error);
                }
            }
        }
    }
}
