namespace Moonwell.AI
{
    /// <summary>The blackboards of the players and the game, made on first use: <c>player.Blackboard()</c> and <c>Game.Instance.Blackboard()</c>.</summary>
    public static class Blackboards
    {
        /// <summary>The player's blackboard: its player brains and its units' brains read it after their own, and the game's after it.</summary>
        public static Blackboard Blackboard(this Player player)
        {
            return Minds.Current().BoardOf(player);
        }

        /// <summary>The game's blackboard, the last of every chain.</summary>
        public static Blackboard Blackboard(this Game game)
        {
            return Minds.Current().GameBoard();
        }
    }
}
