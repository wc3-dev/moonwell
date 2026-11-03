namespace Moonwell
{
    /// <summary>
    /// An order a unit was given, by a player or by code: what the order reactions get, and what <c>unit.LastOrder</c> keeps.
    /// It does not change once made, but for <c>Previous</c>, which reaches back at most to the third order kept, and
    /// <c>IsCancelled</c>. Compare its name with the constants: <c>order.Name == Orders.Move</c>.
    /// </summary>
    public sealed class Order
    {
        /// <summary>Makes an order as the game's event gave it.</summary>
        internal Order(Unit unit, int id, string name, OrderKind kind, float x, float y, Widget? target, bool queued)
        {
            Unit = unit;
            Id = id;
            Name = name;
            Kind = kind;
            X = x;
            Y = y;
            Target = target;
            IsQueued = queued;
        }

        /// <summary>The unit ordered.</summary>
        [LocalSafe]
        public Unit Unit { get; }

        /// <summary>The game's number of the order; the only thing the few orders with no name have (using an item's slot).</summary>
        [LocalSafe]
        public int Id { get; }

        /// <summary>The game's string of the order (<c>"move"</c>, <c>"holybolt"</c>), "" for the few with none.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>Whether it is given to nothing, a place or a target.</summary>
        [LocalSafe]
        public OrderKind Kind { get; }

        /// <summary>The x of a place order; of a target order, the target's when ordered; of one with neither, the unit's.</summary>
        [LocalSafe]
        public float X { get; }

        /// <summary>The y, as X.</summary>
        [LocalSafe]
        public float Y { get; }

        /// <summary>The unit, item or destructable a target order is given to; null for the others.</summary>
        [LocalSafe]
        public Widget? Target { get; }

        /// <summary>Whether the unit had orders queued after this one when it started (Shift-queued); the last of a queue starts as a fresh order does.</summary>
        [LocalSafe]
        public bool IsQueued { get; }

        /// <summary>Whether a reaction cancelled it.</summary>
        [LocalSafe]
        public bool IsCancelled { get; private set; }

        /// <summary>The unit's last order before this one, as <c>unit.LastOrder</c> kept it; null when none was kept.</summary>
        [LocalSafe]
        public Order? Previous { get; internal set; }

        /// <summary>Whether the order is done and is not given again: a spell order once the spell was cast.</summary>
        internal bool Done;

        /// <summary>
        /// Refuses the order, inside its reaction: the unit carries on with its last order, which Moonwell gives back, or stops
        /// when it has none. The order's other reactions still run, and read <c>IsCancelled</c>. A refused queued order ends the
        /// queue, as the game can't skip one.
        /// </summary>
        public void Cancel()
        {
            IsCancelled = true;
        }
    }
}
