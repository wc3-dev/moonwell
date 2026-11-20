using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>
    /// The fake world as the damage package's world, so that a Simulation runs the package's stages over the fake's own units,
    /// life and armor. A hit is played as the game plays it: the before-armor event, then the armor, then the after-armor
    /// event unless spell immunity stops it, then the life drops by what the reactions left. Armor cuts only damage type normal,
    /// as the game's does a hit from code (seen 11-06), so a before-armor reaction's changes to the armor or the damage type
    /// count. An attack type, a defense type and a weapon type are carried through the events and change no amount: the fake
    /// has no table of them.
    /// </summary>
    public sealed partial class FakeWorld : IDamageWorld
    {
        /// <summary>The attack type of a swing, as the game numbers it: normal.</summary>
        private const int SwingAttackType = 1;

        /// <summary>The damage type normal, which a swing deals and the only one armor cuts.</summary>
        private const int NormalDamageType = 4;

        /// <summary>The longest attack range of a unit that counts as a melee attacker; a played unit with more is a ranged one.</summary>
        private const float MeleeRange = 128.0f;

        /// <summary>Whether the damage package runs on this world: Simulation.Start installs it.</summary>
        private bool damageInstalled;

        /// <summary>Whether the package asked to hear the damage events, which it does once a reaction or a code hit needs them.</summary>
        private bool damageListening;

        /// <summary>The package's before-armor action.</summary>
        private Action damaging = () => { };

        /// <summary>The package's after-armor action.</summary>
        private Action damaged = () => { };

        /// <summary>The hits being played, innermost last.</summary>
        private readonly List<DamageEvent> damageEvents = new List<DamageEvent>();

        /// <summary>The action the package wants run after a 0 s timer; null when none is waiting.</summary>
        private Action? damageZero;

        /// <summary>The defense types set on units, as the game numbers them; a unit not in it is normal, 4.</summary>
        private readonly Dictionary<unit, int> defenseTypes = new Dictionary<unit, int>();

        /// <summary>Starts the damage package on this world; Simulation.Start calls it after the runtime is made.</summary>
        internal void InstallDamage()
        {
            damageInstalled = true;
            DamageSystem.Install(this);
        }

        /// <summary>Plays a hit as the game does, whether or not the package listens: the events only when it does.</summary>
        private void PlayHit(unit source, unit target, float amount, bool isAttack, int attackType, int damageType, int weaponType)
        {
            var current = new DamageEvent { Source = source, Target = target, Amount = amount, AttackType = attackType, DamageType = damageType, WeaponType = weaponType, IsAttack = isAttack };
            damageEvents.Add(current);
            try
            {
                if (damageListening)
                {
                    damaging();
                }

                // Spell immunity: the damaging event came, and neither the armor nor the damaged event nor the life loss do (seen 10-31).
                if (SpellImmune.Contains(target) && (current.AttackType == 4 || IsMagicDamage(current.DamageType)))
                {
                    return;
                }

                if (current.DamageType == NormalDamageType)
                {
                    current.Amount *= ArmorFactor(ArmorAt(target));
                }

                if (damageListening)
                {
                    damaged();
                }

                LoseLife(source, target, current.Amount);
            }
            finally
            {
                damageEvents.RemoveAt(damageEvents.Count - 1);
            }
        }

        /// <summary>Runs the action the package asked for after a 0 s timer, once; one it asks for while that runs waits for the next call. Called after each unit's act, after each wake of Moonwell's timers, and when time moves.</summary>
        private void RunDamageZero()
        {
            var action = damageZero;
            damageZero = null;
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception error)
            {
                Errors.Report("Moonwell.Damage", error);
            }
        }

        /// <summary>The current event.</summary>
        private DamageEvent Current()
        {
            return damageEvents[damageEvents.Count - 1];
        }

        /// <summary>Remembers the package's actions.</summary>
        void IDamageWorld.Listen(Action onDamaging, Action onDamaged)
        {
            damageListening = true;
            damaging = onDamaging;
            damaged = onDamaged;
        }

        unit? IDamageWorld.EventSource() => Current().Source;

        unit IDamageWorld.EventTarget() => Current().Target;

        float IDamageWorld.EventAmount() => Current().Amount;

        int IDamageWorld.EventAttackType() => Current().AttackType;

        int IDamageWorld.EventDamageType() => Current().DamageType;

        int IDamageWorld.EventWeaponType() => Current().WeaponType;

        bool IDamageWorld.EventIsAttack() => Current().IsAttack;

        void IDamageWorld.SetEventAmount(float amount) => Current().Amount = amount;

        void IDamageWorld.SetEventAttackType(int type) => Current().AttackType = type;

        void IDamageWorld.SetEventDamageType(int type) => Current().DamageType = type;

        void IDamageWorld.SetEventWeaponType(int type) => Current().WeaponType = type;

        float IDamageWorld.Life(unit target) => lives.TryGetValue(target, out var life) ? life : 0.0f;

        bool IDamageWorld.IsDead(unit target) => Gone(target);

        bool IDamageWorld.IsMagicImmune(unit target) => SpellImmune.Contains(target);

        bool IDamageWorld.IsMelee(unit target) => played.TryGetValue(target, out var unitPlay) && unitPlay.AttackRange <= MeleeRange;

        bool IDamageWorld.IsRanged(unit target) => played.TryGetValue(target, out var unitPlay) && unitPlay.AttackRange > MeleeRange;

        float IDamageWorld.Armor(unit target) => ArmorAt(target);

        void IDamageWorld.SetArmor(unit target, float armor) => armors[target] = armor;

        int IDamageWorld.DefenseType(unit target) => defenseTypes.TryGetValue(target, out var type) ? type : 4;

        void IDamageWorld.SetDefenseType(unit target, int type) => defenseTypes[target] = type;

        /// <summary>Deals as the game's UnitDamageTarget does, which the call records and may refuse.</summary>
        void IDamageWorld.Deal(unit source, unit target, float amount, bool attack, bool ranged, int attackType, int damageType, int weaponType)
        {
            DealDamage(source, target, amount, attack, ranged, attackType, damageType, weaponType);
        }

        void IDamageWorld.AfterZero(Action action) => damageZero = action;

        /// <summary>One damage event the fake is playing.</summary>
        private sealed class DamageEvent
        {
            public unit? Source;

            public unit Target = null!;

            /// <summary>The amount: before armor, then after.</summary>
            public float Amount;

            public int AttackType;

            public int DamageType;

            public int WeaponType;

            public bool IsAttack;
        }
    }
}
