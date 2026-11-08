namespace Moonwell
{
    /// <summary>A component on a unit, which reacts to the unit's events.</summary>
    public abstract class UnitComponent : Component
    {
        /// <summary>The table behind ComponentHooks, made on first use so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static HookTable? componentHooks;

        /// <summary>The reactions of unit components, named as Unit's are.</summary>
        internal static HookTable ComponentHooks
        {
            get
            {
                if (componentHooks == null)
                {
                    componentHooks = new HookTable(typeof(UnitComponent), new string[] { "OnItemPickedUp", "OnItemDropped", "OnDamaged", "OnDealtDamage", "OnDeath", "OnKill", "OnCast", "OnLevelUp", "OnSkillLearned", "OnOrdered", "OnSelected", "OnDeselected", "OnSummon", "OnLoaded", "OnUnloaded", "OnCargoLoaded", "OnCargoUnloaded" }, new int[] { Hook.OnItemPickedUp, Hook.OnItemDropped, Hook.OnDamaged, Hook.OnDealtDamage, Hook.OnDeath, Hook.OnKill, Hook.OnCast, Hook.OnLevelUp, Hook.OnSkillLearned, Hook.OnOrdered, Hook.OnSelection, Hook.OnSelection, Hook.OnSummon, Hook.OnTransport, Hook.OnTransport, Hook.OnTransport, Hook.OnTransport });
                }

                return componentHooks;
            }
        }

        /// <summary>The unit this component is on; null before the component is added, though its type does not say so (read it from OnAdded on).</summary>
        [LocalSafe]
        public Unit Unit => (Unit)Actor;

        /// <summary>The reactions this kind of component declares.</summary>
        internal override HookTable Table => ComponentHooks;

        /// <summary>Runs when the unit picks up an item.</summary>
        /// <param name="pickedItem">The item the unit picked up.</param>
        public virtual void OnItemPickedUp(Item pickedItem)
        {
        }

        /// <summary>Runs when the unit drops an item.</summary>
        /// <param name="droppedItem">The item the unit dropped.</param>
        public virtual void OnItemDropped(Item droppedItem)
        {
        }

        /// <summary>Runs when the unit takes damage.</summary>
        public virtual void OnDamaged(Damage damage)
        {
        }

        /// <summary>Runs when the unit deals damage.</summary>
        public virtual void OnDealtDamage(Damage damage)
        {
        }

        /// <summary>Runs when the unit dies; the killer is null when no unit killed it. A timer made here with After is the component's, which ends when the unit leaves the game, as its corpse goes; a respawn timer is made on the game: Game.Instance.After(60f, Respawn).</summary>
        /// <param name="killerUnit">The unit whose damage killed it; null when no unit did: Kill() or Life set to 0 from code, timed life running out, or its own damage (seen 10-21).</param>
        public virtual void OnDeath(Unit? killerUnit)
        {
        }

        /// <summary>Runs when the unit kills another.</summary>
        /// <param name="killedUnit">The unit it killed.</param>
        public virtual void OnKill(Unit killedUnit)
        {
        }

        /// <summary>Runs when the unit begins a cast.</summary>
        public virtual void OnCast(Cast cast)
        {
        }

        /// <summary>Runs when the unit is given an order, after its class has heard it; <c>order.Cancel()</c> refuses it, and an order given here replaces it.</summary>
        public virtual void OnOrdered(Order order)
        {
        }

        /// <summary>Runs when the unit summons another, after its class has seen it (see Unit.OnSummon).</summary>
        /// <param name="summonedUnit">The unit summoned.</param>
        public virtual void OnSummon(Unit summonedUnit)
        {
        }

        /// <summary>Runs when a player selects the unit, after its class has seen it (see Unit.OnSelected).</summary>
        /// <param name="selectingPlayer">The player who selected it.</param>
        public virtual void OnSelected(Player selectingPlayer)
        {
        }

        /// <summary>Runs when a player deselects the unit, after its class has seen it (see Unit.OnDeselected).</summary>
        /// <param name="deselectingPlayer">The player who deselected it.</param>
        public virtual void OnDeselected(Player deselectingPlayer)
        {
        }

        /// <summary>Runs when the unit is loaded into a transport, after its class has seen it (see Unit.OnLoaded).</summary>
        /// <param name="carryingTransport">The transport it is in.</param>
        public virtual void OnLoaded(Unit carryingTransport)
        {
        }

        /// <summary>Runs when the unit comes out of its transport, after its class has seen it (see Unit.OnUnloaded).</summary>
        /// <param name="carryingTransport">The transport it was in.</param>
        public virtual void OnUnloaded(Unit carryingTransport)
        {
        }

        /// <summary>Runs when a unit is loaded into this transport, after the transport's class has seen it (see Unit.OnCargoLoaded).</summary>
        /// <param name="loadedUnit">The unit loaded.</param>
        public virtual void OnCargoLoaded(Unit loadedUnit)
        {
        }

        /// <summary>Runs when a unit comes out of this transport, after the transport's class has seen it (see Unit.OnCargoUnloaded).</summary>
        /// <param name="unloadedUnit">The unit that came out.</param>
        public virtual void OnCargoUnloaded(Unit unloadedUnit)
        {
        }

        /// <summary>Runs when the unit, a hero, gains a level, once for each level gained, after the hero's own OnLevelUp; never on a unit that is not a hero.</summary>
        /// <param name="reachedLevel">The level gained. In a jump of several levels (the game fires one event for it) Level already reads the last.</param>
        public virtual void OnLevelUp(int reachedLevel)
        {
        }

        /// <summary>Runs when the unit, a hero, learns a skill or raises its level, after the hero's own OnSkillLearned; never on a unit that is not a hero. The skill point is not spent yet: the hero's SkillPoints reads one more than after (seen 11-02).</summary>
        /// <param name="learnedAbility">The ability learned; its Level reads the new level.</param>
        public virtual void OnSkillLearned(Ability learnedAbility)
        {
        }
    }
}
