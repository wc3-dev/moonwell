namespace Moonwell
{
    /// <summary>How a minimap ping looks.</summary>
    public enum PingStyle
    {
        /// <summary>A plain ping in its colour.</summary>
        Simple,

        /// <summary>A ping in its colour with extra rings.</summary>
        Flashy,

        /// <summary>The game's red warning ping, as when a player's units are attacked; its colour is always red.</summary>
        Attack,
    }
}
