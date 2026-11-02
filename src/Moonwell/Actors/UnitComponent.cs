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
                    componentHooks = new HookTable(typeof(UnitComponent), new string[] { "OnItemPickedUp", "OnItemDropped", "OnDamaged", "OnDealtDamage", "OnDeath", "OnKill", "OnCast", "OnLevelUp", "OnSkillLearned" }, new int[] { Hook.OnItemPickedUp, Hook.OnItemDropped, Hook.OnDamaged, Hook.OnDealtDamage, Hook.OnDeath, Hook.OnKill, Hook.OnCast, Hook.OnLevelUp, Hook.OnSkillLearned });
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
