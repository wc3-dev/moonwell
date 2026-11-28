using Moonwell;

namespace InTheGame
{
    /// <summary>A print-only map the launch tests play in the game with nobody at the keyboard (GameRunTests).</summary>
    public sealed class GameMap : Map
    {
        public GameMap()
        {
            Name = "In the game";
            Author = "Moonwell";
            Description = "Played by the launch tests.";
            RecommendedPlayers = "1";
            Components.Add(new Report());
        }
    }
}
