namespace Moonwell
{
    /// <summary>What an order is given to: nothing, a place, or a unit, an item or a destructable.</summary>
    public enum OrderKind
    {
        /// <summary>No place and no target: <c>stop</c>, <c>holdposition</c>.</summary>
        None,

        /// <summary>A place: <c>move</c> to x, y.</summary>
        Place,

        /// <summary>A unit, an item or a destructable: <c>attack</c> on a unit.</summary>
        Target,
    }
}
