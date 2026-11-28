namespace Moonwell
{
    /// <summary>
    /// The game's names of the orders every unit or worker has, for <c>unit.Order(Orders.Stop)</c> and
    /// <c>order.Name == Orders.Move</c>; a typo in a constant fails the build, where one in a string never matches. A spell's
    /// order is the string of the ability it is based on (<c>"holybolt"</c>), as World Editor shows it.
    /// </summary>
    public static class Orders
    {
        /// <summary>A right-click: to a place it moves, on an enemy it attacks, on an item it picks the item up.</summary>
        public const string Smart = "smart";

        /// <summary>Stop.</summary>
        public const string Stop = "stop";

        /// <summary>Move, to a place or following a unit.</summary>
        public const string Move = "move";

        /// <summary>Attack: a unit, or a place to attack-move to.</summary>
        public const string Attack = "attack";

        /// <summary>Attack the ground at a place (a siege unit's).</summary>
        public const string AttackGround = "attackground";

        /// <summary>Attack once and stop.</summary>
        public const string AttackOnce = "attackonce";

        /// <summary>Patrol between where the unit is and a place.</summary>
        public const string Patrol = "patrol";

        /// <summary>Hold position.</summary>
        public const string HoldPosition = "holdposition";

        /// <summary>Set a building's rally point.</summary>
        public const string SetRally = "setrally";

        /// <summary>Pick up an item.</summary>
        public const string GetItem = "getitem";

        /// <summary>Drop an item.</summary>
        public const string DropItem = "dropitem";

        /// <summary>Harvest gold or lumber.</summary>
        public const string Harvest = "harvest";

        /// <summary>Bring what a worker carries back.</summary>
        public const string ReturnResources = "returnresources";

        /// <summary>Go back to harvesting.</summary>
        public const string ResumeHarvesting = "resumeharvesting";

        /// <summary>Repair a building or a mechanical unit.</summary>
        public const string Repair = "repair";

        /// <summary>Revive a dead hero at an altar.</summary>
        public const string Revive = "revive";

        /// <summary>Load a unit into a transport.</summary>
        public const string Load = "load";

        /// <summary>Unload a unit from a transport.</summary>
        public const string Unload = "unload";

        /// <summary>Unload every unit from a transport.</summary>
        public const string UnloadAll = "unloadall";

        /// <summary>Board a transport.</summary>
        public const string Board = "board";

        /// <summary>
        /// The name of an order by its id: the game's string, and for an order to build a type, which the game names with
        /// nothing (OrderId2String gives "", seen 11-28), the type's four-character id: a worker walking to lay a Farm down
        /// reads <c>"hhou"</c>.
        /// </summary>
        internal static string NameOf(int id)
        {
            var name = Runtime.World.OrderName(id);
            return name.Length == 0 && id >= FirstTypeId ? Names.IdText(id) : name;
        }

        // The orders' own ids are below a million (851968 and up); a type's id is four characters, the first of them not 0.
        private const int FirstTypeId = 0x01000000;
    }
}
