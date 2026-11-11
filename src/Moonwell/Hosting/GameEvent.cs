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

        /// <summary>A player selects or deselects a unit.</summary>
        Selection,

        /// <summary>A player changes what it gives another.</summary>
        Alliance,

        /// <summary>A unit is loaded into a transport.</summary>
        Cargo,

        /// <summary>A unit summons another with the game's spell.</summary>
        Summon,

        /// <summary>A building starts, cancels or finishes a research.</summary>
        Research,

        /// <summary>A shop sells an item or a unit, or a unit sells an item to a shop.</summary>
        Sale,

        /// <summary>An item goes into or comes out of a unit's 3.0 equipment slots.</summary>
        Equip,

        /// <summary>The game is about to write a saved game.</summary>
        Saving,
    }
}
