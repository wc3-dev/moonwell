namespace Moonwell
{
    /// <summary>The second word of reaction bits, for those added once Hook was full; a class's Mask2 holds them. A game integer is 32 bits, so a long would not hold more.</summary>
    internal static class Hook2
    {
        /// <summary>A research starts, is cancelled or finishes, or a shop sells: one bit for the upgrade classes', the units' and their components' reactions (decisions 213 and 214).</summary>
        internal const int OnResearchOrSale = 1;

        /// <summary>A stat's total changes on a unit or a player: one bit for the unit's and its components' OnStatChanged and the game and player components' OnUnitStatChanged and OnPlayerStatChanged.</summary>
        internal const int OnStatChanged = 2;
    }
}
