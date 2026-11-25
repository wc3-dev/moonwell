namespace Moonwell
{
    /// <summary>A kind of unit as the game's IsUnitType reads it, from the type's object data (a building, its classification) or added in play (a summon); Unit's Is... properties read them, and a test gives one to a fake world's unit with MakeKind.</summary>
    public enum UnitKind
    {
        /// <summary>A building (UNIT_TYPE_STRUCTURE): the object data's IsABuilding.</summary>
        Structure,

        /// <summary>A worker (UNIT_TYPE_PEON): the classification Worker.</summary>
        Worker,

        /// <summary>A town hall (UNIT_TYPE_TOWNHALL): the classification TownHall.</summary>
        TownHall,

        /// <summary>A summon (UNIT_TYPE_SUMMONED): the classification Summoned, or given in play by a summoning, Unit.Summon or a timed life.</summary>
        Summoned,

        /// <summary>A mechanical unit (UNIT_TYPE_MECHANICAL): the classification Mechanical.</summary>
        Mechanical,

        /// <summary>An undead unit (UNIT_TYPE_UNDEAD): the classification Undead.</summary>
        Undead,

        /// <summary>An ancient (UNIT_TYPE_ANCIENT): the classification Ancient.</summary>
        Ancient,

        /// <summary>A giant (UNIT_TYPE_GIANT): the classification Giant.</summary>
        Giant,

        /// <summary>A tauren (UNIT_TYPE_TAUREN): the classification Tauren.</summary>
        Tauren,

        /// <summary>A suicidal unit, a sapper (UNIT_TYPE_SAPPER): the classification Suicidal.</summary>
        Suicidal,

        /// <summary>A ranged attacker (UNIT_TYPE_RANGED_ATTACKER), as the game reads its attack; a simulation reads its type's first attack's weapon type, any but Normal.</summary>
        Ranged,
    }
}
