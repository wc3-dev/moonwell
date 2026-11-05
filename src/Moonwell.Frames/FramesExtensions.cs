namespace Moonwell
{
    /// <summary>What the frames package adds to a player.</summary>
    public static class FramesExtensions
    {
        /// <summary>The player's own view of the game's HUD: what is set on it changes only that player's screen.</summary>
        [LocalSafe]
        public static GameUi GameUi(this Player player)
        {
            return FramesSystem.GameUiOf(player);
        }
    }
}
