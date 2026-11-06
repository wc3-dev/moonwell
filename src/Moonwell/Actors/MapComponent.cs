namespace Moonwell
{
    /// <summary>A component that reacts to events of the map as a whole, such as the game's start.</summary>
    public abstract class MapComponent : Component
    {
        /// <summary>Internal, as only a game component or a player component can be added to anything.</summary>
        internal MapComponent()
        {
        }

        /// <summary>The table behind MapHooks, made on first use so that the class has no static initializer (see Unit.UnitHooks).</summary>
        private static HookTable? mapHooks;

        /// <summary>The reactions of map components; declared here, so that an override in a subclass is found.</summary>
        internal static HookTable MapHooks
        {
            get
            {
                if (mapHooks == null)
                {
                    mapHooks = new HookTable(typeof(MapComponent), new[] { "OnGameStart", "OnUnitCreated", "OnUnitDamaged", "OnUnitDied", "OnUnitCast", "OnLeave", "OnHeroLevelUp", "OnHeroSkillLearned", "OnUnitOrdered", "OnUnitSelected", "OnUnitDeselected" }, new[] { Hook.OnGameStart, Hook.OnUnitCreated, Hook.OnUnitDamaged, Hook.OnUnitDied, Hook.OnUnitCast, Hook.OnLeave, Hook.OnHeroLevelUp, Hook.OnHeroSkillLearned, Hook.OnUnitOrdered, Hook.OnSelection, Hook.OnSelection });
                }

                return mapHooks;
            }
        }

        /// <summary>Runs when the game starts.</summary>
        public virtual void OnGameStart()
        {
        }

        /// <summary>Runs once for each unit Moonwell takes in as it comes into the game: made by code, trained, summoned, or placed in World Editor. A unit that came in while nothing wanted OnUnitCreated, and that Moonwell takes in later (when it is first hit, cast at or found), runs none. For the game's components, then the owner's player components.</summary>
        /// <param name="createdUnit">The unit that came into the game.</param>
        public virtual void OnUnitCreated(Unit createdUnit)
        {
        }

        /// <summary>Runs when a unit in the map takes damage, for the game's components, then the components of the players involved.</summary>
        public virtual void OnUnitDamaged(Damage damage)
        {
        }

        /// <summary>Runs when a unit in the map dies, for the game's components, then the components of the players involved.</summary>
        /// <param name="dyingUnit">The unit that died.</param>
        /// <param name="killerUnit">The unit whose damage killed it; null when no unit did: Kill() or Life set to 0 from code, timed life running out, or its own damage (seen 10-21).</param>
        public virtual void OnUnitDied(Unit dyingUnit, Unit? killerUnit)
        {
        }

        /// <summary>Runs when a unit in the map is given an order, for the game's components, then the components of the unit's player, after the unit and its components heard it; <c>order.Cancel()</c> refuses it.</summary>
        public virtual void OnUnitOrdered(Order order)
        {
        }

        /// <summary>Runs when a player selects a unit, for the game's components, then the selecting player's, whoever owns the unit, after the unit and its components saw it (see Unit.OnSelected).</summary>
        /// <param name="selectedUnit">The unit selected.</param>
        /// <param name="selectingPlayer">The player who selected it.</param>
        public virtual void OnUnitSelected(Unit selectedUnit, Player selectingPlayer)
        {
        }

        /// <summary>Runs when a player deselects a unit, for the game's components, then the deselecting player's, after the unit and its components saw it (see Unit.OnDeselected).</summary>
        /// <param name="deselectedUnit">The unit deselected.</param>
        /// <param name="deselectingPlayer">The player who deselected it.</param>
        public virtual void OnUnitDeselected(Unit deselectedUnit, Player deselectingPlayer)
        {
        }

        /// <summary>Runs when a unit in the map begins a cast, for the game's components, then the components of the caster's player.</summary>
        public virtual void OnUnitCast(Cast cast)
        {
        }

        /// <summary>Runs when a player leaves the game, for the game's components, then the leaving player's.</summary>
        /// <param name="leavingPlayer">The player who left.</param>
        public virtual void OnLeave(Player leavingPlayer)
        {
        }

        /// <summary>Runs when a hero in the map gains a level, once for each level gained, for the game's components, then the components of the hero's player.</summary>
        /// <param name="leveledHero">The hero that gained the level.</param>
        /// <param name="reachedLevel">The level gained. In a jump of several levels (the game fires one event for it) the hero's Level already reads the last.</param>
        public virtual void OnHeroLevelUp(Hero leveledHero, int reachedLevel)
        {
        }

        /// <summary>Runs when a hero in the map learns a skill or raises its level, for the game's components, then the components of the hero's player. The skill point is not spent yet: the hero's SkillPoints reads one more than after (seen 11-02).</summary>
        /// <param name="learningHero">The hero that learned the skill.</param>
        /// <param name="learnedAbility">The ability learned; its Level reads the new level.</param>
        public virtual void OnHeroSkillLearned(Hero learningHero, Ability learnedAbility)
        {
        }

        /// <summary>The reactions this kind of component declares.</summary>
        internal override HookTable Table => MapHooks;
    }
}
