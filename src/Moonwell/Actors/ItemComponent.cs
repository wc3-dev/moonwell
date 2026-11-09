namespace Moonwell
{
    /// <summary>A component on an item, which reacts to the item's events.</summary>
    public abstract class ItemComponent : Component
    {
        /// <summary>The table behind ComponentHooks, made on first use so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static HookTable? componentHooks;

        /// <summary>The reactions of item components, named as Item's are.</summary>
        internal static HookTable ComponentHooks
        {
            get
            {
                if (componentHooks == null)
                {
                    componentHooks = new HookTable(typeof(ItemComponent), new string[] { "OnPickedUp", "OnDropped", "OnEquipped", "OnUnequipped" }, new int[] { Hook.OnPickedUp, Hook.OnDropped, Hook.OnPickedUp, Hook.OnDropped });
                }

                return componentHooks;
            }
        }

        /// <summary>The item this component is on; null before the component is added, though its type does not say so (read it from OnAdded on).</summary>
        [LocalSafe]
        public Item Item => (Item)Actor;

        /// <summary>The reactions this kind of component declares.</summary>
        internal override HookTable Table => ComponentHooks;

        /// <summary>Runs when the item is picked up by a unit.</summary>
        /// <param name="carrierUnit">The unit that picked the item up.</param>
        public virtual void OnPickedUp(Unit carrierUnit)
        {
        }

        /// <summary>Runs when the item is dropped by a unit.</summary>
        /// <param name="carrierUnit">The unit that dropped the item.</param>
        public virtual void OnDropped(Unit carrierUnit)
        {
        }

        /// <summary>Runs when the item goes into a unit's equipment slots, after the item's class has seen it (see Item.OnEquipped).</summary>
        /// <param name="carrierUnit">The unit that equipped it.</param>
        public virtual void OnEquipped(Unit carrierUnit)
        {
        }

        /// <summary>Runs when the item leaves a unit's equipment slots, after the item's class has seen it (see Item.OnUnequipped).</summary>
        /// <param name="carrierUnit">The unit that had it equipped.</param>
        public virtual void OnUnequipped(Unit carrierUnit)
        {
        }
    }
}
