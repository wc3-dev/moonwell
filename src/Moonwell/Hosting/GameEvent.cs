namespace Moonwell.Hosting
{
    /// <summary>The game events the runtime can ask the world to deliver.</summary>
    internal enum GameEvent
    {
        /// <summary>A unit takes damage.</summary>
        Damage,

        /// <summary>A unit dies.</summary>
        Death,

        /// <summary>A unit casts a spell.</summary>
        Spell,

        /// <summary>A unit picks up an item.</summary>
        PickUp,

        /// <summary>A unit drops an item.</summary>
        Drop,

        /// <summary>A player leaves the game.</summary>
        Leave,

        /// <summary>A hero gains one level or more.</summary>
        HeroLevel,

        /// <summary>A hero learns a skill.</summary>
        HeroSkill,

        /// <summary>A unit is given an order: none, to a place, or on a target.</summary>
        Order,

        /// <summary>A unit changes hands: given away by code, charmed or possessed.</summary>
        OwnerChange,

        /// <summary>A game saved with the game's own Save Game is loaded.</summary>
        Loaded,
    }
}
