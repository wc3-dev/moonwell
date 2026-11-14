using System;

namespace Moonwell.AI.Combat
{
    /// <summary>
    /// A spell a fighter casts while it fights, for its <c>Spells</c>: the ability's id or class, its order string, its aim
    /// and a condition. At each pass of the fight the first spell in the list whose aim has someone, whose condition holds
    /// and that is ready is cast. Ready: the unit has the ability, its cooldown is over and the unit's mana is at least its
    /// mana cost now, as the game reads them. A spell never changes once made, so one is shared freely.
    /// </summary>
    [LocalSafe]
    public sealed class Spell
    {
        /// <summary>The aim of a spell on the fighter's target.</summary>
        internal const int OnTarget = 0;

        /// <summary>The aim of a spell with no target.</summary>
        internal const int NoTarget = 1;

        /// <summary>The aim of a spell on a hurt ally.</summary>
        internal const int OnAlly = 2;

        /// <summary>The aim of a spell at the target's place.</summary>
        internal const int AtPlace = 3;

        /// <summary>The aim of a spell on the fighter's own unit.</summary>
        internal const int OnItself = 4;

        /// <summary>Its aim.</summary>
        internal readonly int aim;

        /// <summary>The condition on a unit, for a spell aimed at a unit or its place; null holds always.</summary>
        internal readonly Func<Unit, bool>? whenUnit;

        /// <summary>The condition on the fighter, for a spell with no target or on the fighter's own unit; null holds always.</summary>
        internal readonly Func<Fighter, bool>? whenFighter;

        /// <summary>Makes a spell.</summary>
        private Spell(int abilityId, string order, int aim, Func<Unit, bool>? whenUnit, Func<Fighter, bool>? whenFighter)
        {
            AbilityId = abilityId;
            Order = order;
            this.aim = aim;
            this.whenUnit = whenUnit;
            this.whenFighter = whenFighter;
        }

        /// <summary>The ability's id.</summary>
        public int AbilityId { get; }

        /// <summary>The order string it is cast with, as <c>"thunderbolt"</c>.</summary>
        public string Order { get; }

        /// <summary>A spell cast on the fighter's target, when the condition holds for the target: <c>Spell.On(StormBoltId, "thunderbolt", when: t =&gt; t is Hero)</c>.</summary>
        public static Spell On(int abilityId, string order, Func<Unit, bool>? when = null)
        {
            return new Spell(abilityId, order, OnTarget, when, null);
        }

        /// <summary>A spell of a registered ability class cast on the fighter's target, as <c>On</c> with its id.</summary>
        public static Spell On<T>(string order, Func<Unit, bool>? when = null)
            where T : Ability
        {
            return On(ObjectTypes.IdOf<T>(), order, when);
        }

        /// <summary>A spell with no target, when the condition holds for the fighter: <c>Spell.Self(ThunderClapId, "thunderclap", when: f =&gt; f.EnemiesNear &gt;= 3)</c>.</summary>
        public static Spell Self(int abilityId, string order, Func<Fighter, bool>? when = null)
        {
            return new Spell(abilityId, order, NoTarget, null, when);
        }

        /// <summary>A spell of a registered ability class with no target, as <c>Self</c> with its id.</summary>
        public static Spell Self<T>(string order, Func<Fighter, bool>? when = null)
            where T : Ability
        {
            return Self(ObjectTypes.IdOf<T>(), order, when);
        }

        /// <summary>A spell cast on the fighter's own unit, as a spell on a unit, when the condition holds for the fighter: <c>Spell.OnSelf(FrostArmorId, "frostarmor", when: f =&gt; f.EnemiesNear &gt;= 2)</c>.</summary>
        public static Spell OnSelf(int abilityId, string order, Func<Fighter, bool>? when = null)
        {
            return new Spell(abilityId, order, OnItself, null, when);
        }

        /// <summary>A spell of a registered ability class cast on the fighter's own unit, as <c>OnSelf</c> with its id.</summary>
        public static Spell OnSelf<T>(string order, Func<Fighter, bool>? when = null)
            where T : Ability
        {
            return OnSelf(ObjectTypes.IdOf<T>(), order, when);
        }

        /// <summary>A spell cast on an ally in meet range, the fighter's unit counted: of those for whom the condition holds, the one with the least share of its most life, the condition checked from the most hurt. Without a condition, an ally whose life is below its most.</summary>
        public static Spell Ally(int abilityId, string order, Func<Unit, bool>? when = null)
        {
            return new Spell(abilityId, order, OnAlly, when, null);
        }

        /// <summary>A spell of a registered ability class cast on an ally, as <c>Ally</c> with its id.</summary>
        public static Spell Ally<T>(string order, Func<Unit, bool>? when = null)
            where T : Ability
        {
            return Ally(ObjectTypes.IdOf<T>(), order, when);
        }

        /// <summary>A spell cast at the place where the fighter's target stands, when the condition holds for the target: <c>Spell.At(BlizzardId, "blizzard")</c>.</summary>
        public static Spell At(int abilityId, string order, Func<Unit, bool>? when = null)
        {
            return new Spell(abilityId, order, AtPlace, when, null);
        }

        /// <summary>A spell of a registered ability class cast at the target's place, as <c>At</c> with its id.</summary>
        public static Spell At<T>(string order, Func<Unit, bool>? when = null)
            where T : Ability
        {
            return At(ObjectTypes.IdOf<T>(), order, when);
        }

        /// <summary>Whether the unit can cast it now: it has the ability, the ability's cooldown is over, and its mana is at least the ability's mana cost now.</summary>
        internal bool Ready(Unit unit)
        {
            if (!unit.HasAbility(AbilityId))
            {
                return false;
            }

            var ability = unit.AbilityOf(AbilityId);
            return ability.CooldownLeft <= 0f && unit.Mana >= ability.LiveManaCost;
        }
    }
}
