namespace Moonwell
{
    /// <summary>Where an item is (decision 215): on the ground, in a unit's six inventory slots, in its 3.0 bag, or in its equipment slots.</summary>
    public enum ItemPlace
    {
        /// <summary>On the ground, or gone.</summary>
        Ground,

        /// <summary>In a unit's inventory, its six slots.</summary>
        Inventory,

        /// <summary>In a unit's 3.0 bag, where its abilities do nothing (seen 11-03).</summary>
        Bag,

        /// <summary>In a unit's 3.0 equipment slots.</summary>
        Equipped,
    }
}
