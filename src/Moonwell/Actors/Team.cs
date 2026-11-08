using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A team of players who are allies: declared on the map class, as a static field, and given its players by the map's
    /// Slots. In the game it is the game's team of the same number; a team the map does not declare is named "Team" and its
    /// number from 1 (decision 198).
    /// </summary>
    public sealed class Team
    {
        /// <summary>The team of each number asked for in this game, declared or not; made on first use, so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static Dictionary<int, Team>? byNumber;

        /// <summary>Declares a team; the map's Slots give it its players and its number.</summary>
        public Team(string name)
        {
            Name = name;
        }

        /// <summary>The team's name, as the lobby shows it.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>Whether each player of the team sees what the others see; read when the build writes the map, and when a player joins the team.</summary>
        [LocalSafe]
        public bool SharedVision { get; set; }

        /// <summary>Whether each player of the team can control the others' units; read when the build writes the map, and when a player joins the team.</summary>
        [LocalSafe]
        public bool SharedControl { get; set; }

        /// <summary>The game's number of the team, from 0, in the order the map's slots first name the teams; -1 for a team no slot names.</summary>
        [LocalSafe]
        public int Number { get; private set; } = -1;

        /// <summary>The players in the game on the team, people and computers, in slot order: a new list each time it is read.</summary>
        [LocalSafe]
        public List<Player> Players
        {
            get
            {
                var found = new List<Player>();
                if (Number < 0)
                {
                    return found;
                }

                var playing = Player.Playing;
                for (var i = 0; i < playing.Count; i++)
                {
                    if (Runtime.World.TeamOf(playing[i].Handle) == Number)
                    {
                        found.Add(playing[i]);
                    }
                }

                return found;
            }
        }

        /// <summary>Shows a line to each player of the team, for the seconds, or for the game's own time without them; it changes nothing in the game.</summary>
        [LocalSafe]
        public void Print(string text, float seconds = -1.0f)
        {
            foreach (var player in Players)
            {
                player.Print(text, seconds);
            }
        }

        /// <summary>Shows each player of the team a line like the game's errors, with the error sound (see Player.Error). Not inside Local.For, as the sound is made on every machine.</summary>
        public void Error(string text)
        {
            foreach (var player in Players)
            {
                player.Error(text);
            }
        }

        /// <summary>Shows each player of the team a hint as the game does, with its sound.</summary>
        [LocalSafe]
        public void Hint(string text)
        {
            foreach (var player in Players)
            {
                player.Hint(text);
            }
        }

        /// <summary>Shows each player of the team a warning as the game does, with its sound.</summary>
        [LocalSafe]
        public void Warning(string text)
        {
            foreach (var player in Players)
            {
                player.Warning(text);
            }
        }

        /// <summary>Clears the lines shown on the screens of the team's players.</summary>
        [LocalSafe]
        public void ClearMessages()
        {
            foreach (var player in Players)
            {
                player.ClearMessages();
            }
        }

        /// <summary>What each player of the team gives each of the others.</summary>
        internal Alliance Gives
        {
            get
            {
                var alliance = Alliance.Allied;
                if (SharedVision)
                {
                    alliance |= Alliance.SharedVision;
                }

                if (SharedControl)
                {
                    alliance |= Alliance.SharedControl;
                }

                return alliance;
            }
        }

        /// <summary>The team of a game's team number: the map's own, or one made for the number.</summary>
        internal static Team Of(int number)
        {
            if (byNumber == null)
            {
                byNumber = new Dictionary<int, Team>();
            }

            if (byNumber.TryGetValue(number, out var team))
            {
                return team;
            }

            var made = new Team(number < 0 ? "" : "Team " + (number + 1)) { Number = number };
            byNumber[number] = made;
            return made;
        }

        /// <summary>Numbers the map's teams in the order its slots first name them, as the build numbers the map's forces; Runtime.Install calls it.</summary>
        internal static void Install(List<Slot> slots)
        {
            byNumber = new Dictionary<int, Team>();
            for (var i = 0; i < slots.Count; i++)
            {
                slots[i].Team.Number = -1;
            }

            var next = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                var team = slots[i].Team;
                if (team.Number < 0)
                {
                    team.Number = next;
                    byNumber[next] = team;
                    next++;
                }
            }
        }

        /// <summary>Forgets every team of the game; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            byNumber = null;
        }
    }
}
