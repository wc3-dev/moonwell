namespace Moonwell.Testing
{
    /// <summary>An item that keeps the slot of the player it belongs to when it is picked up.</summary>
    public class OwnerWatch : Item
    {
        /// <summary>The slot of the item's player inside OnPickedUp; -1 before.</summary>
        public int OwnerAtPickup = -1;

        /// <summary>Keeps the player seen.</summary>
        public override void OnPickedUp(Unit by)
        {
            OwnerAtPickup = Owner.Slot;
        }
    }
}
