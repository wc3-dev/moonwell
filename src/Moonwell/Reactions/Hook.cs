namespace Moonwell
{
    /// <summary>The bit of each reaction a class can override; a class's mask holds the bits of the ones it overrides.</summary>
    internal static class Hook
    {
        /// <summary>A unit takes damage.</summary>
        internal const int OnDamaged = 1;

        /// <summary>A unit deals damage.</summary>
        internal const int OnDealtDamage = 2;

        /// <summary>A unit dies.</summary>
        internal const int OnDeath = 4;

        /// <summary>A unit kills another.</summary>
        internal const int OnKill = 8;

        /// <summary>A unit casts a spell.</summary>
        internal const int OnCast = 16;

        /// <summary>A unit picks up an item.</summary>
        internal const int OnItemPickedUp = 32;

        /// <summary>A unit drops an item.</summary>
        internal const int OnItemDropped = 64;

        /// <summary>An item is picked up.</summary>
        internal const int OnPickedUp = 128;

        /// <summary>An item is dropped.</summary>
        internal const int OnDropped = 256;

        /// <summary>A unit enters the map.</summary>
        internal const int OnUnitCreated = 512;

        /// <summary>A unit in the map takes damage.</summary>
        internal const int OnUnitDamaged = 1024;

        /// <summary>A unit in the map dies.</summary>
        internal const int OnUnitDied = 2048;

        /// <summary>A unit in the map casts a spell.</summary>
        internal const int OnUnitCast = 4096;

        /// <summary>The game starts.</summary>
        internal const int OnGameStart = 8192;

        /// <summary>A player leaves the game.</summary>
        internal const int OnLeave = 16384;

        /// <summary>A hero gains a level.</summary>
        internal const int OnLevelUp = 32768;

        /// <summary>A hero learns a skill, or an ability is learned.</summary>
        internal const int OnSkillLearned = 65536;

        /// <summary>A hero in the map gains a level.</summary>
        internal const int OnHeroLevelUp = 131072;

        /// <summary>A hero in the map learns a skill.</summary>
        internal const int OnHeroSkillLearned = 262144;

        /// <summary>A destructable is brought back to life by Restore.</summary>
        internal const int OnRestored = 524288;

        /// <summary>A hero's skill is taken away by RemoveAbility; no game event.</summary>
        internal const int OnLost = 1048576;

        /// <summary>A unit is given an order.</summary>
        internal const int OnOrdered = 2097152;

        /// <summary>A unit in the map is given an order.</summary>
        internal const int OnUnitOrdered = 4194304;

        /// <summary>Night turns to day.</summary>
        internal const int OnDawn = 8388608;

        /// <summary>Day turns to night.</summary>
        internal const int OnDusk = 16777216;

        /// <summary>A saved game is loaded.</summary>
        internal const int OnGameLoaded = 33554432;

        /// <summary>A player selects or deselects a unit: one bit for the unit's and the map's reactions (decision 203).</summary>
        internal const int OnSelection = 134217728;

        /// <summary>A player changes what it gives another: the map components' OnAllianceChanged (decision 198).</summary>
        internal const int OnAllianceChanged = 67108864;
    }
}
