namespace Moonwell
{
    /// <summary>A player slot the map sets up in its Slots: the build writes them into the map's war3map.w3i, in place of World Editor's players and forces (decision 198).</summary>
    public sealed class Slot
    {
        /// <summary>Sets up a slot, numbered from 0 as Player.Get takes it, on a team, played by a person or a computer, with a race, or the player's pick without one.</summary>
        public Slot(int number, Team team, Controller controller, Race? race = null)
        {
            Number = number;
            Team = team;
            Controller = controller;
            Race = race;
        }

        /// <summary>The slot's number, from 0: 0 is Player.One.</summary>
        public int Number { get; }

        /// <summary>The team the slot plays on.</summary>
        public Team Team { get; }

        /// <summary>Who plays the slot: User, Computer, Rescuable or Neutral.</summary>
        public Controller Controller { get; }

        /// <summary>The slot's race: Human, Orc, Undead or NightElf; null lets the player pick it in the lobby.</summary>
        public Race? Race { get; }

        /// <summary>The slot's name, which a computer's player has in the game; empty is "Player" and the slot's number from 1.</summary>
        public string Name { get; set; } = "";
    }
}
