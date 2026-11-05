using System;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>Registers each game event with the world once, when a reaction first needs it.</summary>
    internal static class Events
    {
        /// <summary>Whether the world already listens for each game event, indexed by GameEvent.</summary>
        private static bool[] listening = new bool[11];

        /// <summary>The reaction bits that some class overrides, once any has been needed.</summary>
        private static int wanted;

        /// <summary>Set by a damage package: called in place of listening for the game's damage event; null without one.</summary>
        internal static Action? DamageListener;

        /// <summary>A damage package's own Unit.Damage, which deals through its stages and returns its hit; null without one.</summary>
        internal static Func<Unit, Unit, float, DamageKind, AttackType, bool, bool, WeaponSound, object?, Damage?>? DamageDealer;

        /// <summary>Adds the reaction bits to the wanted ones, and listens for the event each bit needs.</summary>
        internal static void Need(int mask)
        {
            wanted |= mask;
            if ((mask & (Hook.OnDamaged | Hook.OnDealtDamage | Hook.OnUnitDamaged)) != 0)
            {
                if (DamageListener != null)
                {
                    DamageListener();
                }
                else
                {
                    Listen(GameEvent.Damage);
                }
            }

            if ((mask & (Hook.OnDeath | Hook.OnKill | Hook.OnUnitDied)) != 0)
            {
                Listen(GameEvent.Death);
            }

            if ((mask & (Hook.OnCast | Hook.OnUnitCast)) != 0)
            {
                Listen(GameEvent.Spell);
            }

            if ((mask & (Hook.OnItemPickedUp | Hook.OnPickedUp)) != 0)
            {
                Listen(GameEvent.PickUp);
            }

            if ((mask & (Hook.OnItemDropped | Hook.OnDropped)) != 0)
            {
                Listen(GameEvent.Drop);
            }

            if ((mask & Hook.OnLeave) != 0)
            {
                Listen(GameEvent.Leave);
            }

            if ((mask & (Hook.OnLevelUp | Hook.OnHeroLevelUp)) != 0)
            {
                if (!listening[(int)GameEvent.HeroLevel])
                {
                    KnownUnits.RememberLevels();
                }

                Listen(GameEvent.HeroLevel);
            }

            if ((mask & (Hook.OnSkillLearned | Hook.OnHeroSkillLearned)) != 0)
            {
                Listen(GameEvent.HeroSkill);
            }

            if ((mask & (Hook.OnOrdered | Hook.OnUnitOrdered)) != 0)
            {
                Listen(GameEvent.Order);
                Listen(GameEvent.Spell);
            }

            if ((mask & (Hook.OnDawn | Hook.OnDusk)) != 0)
            {
                DayChange.Start();
            }
        }

        /// <summary>Whether the world delivers a game event.</summary>
        internal static bool Listens(GameEvent gameEvent)
        {
            return listening[(int)gameEvent];
        }

        /// <summary>Whether a reaction bit has been needed.</summary>
        internal static bool Wants(int bit)
        {
            return (wanted & bit) != 0;
        }

        /// <summary>Forgets every registration and need; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            listening = new bool[11];
            wanted = 0;
            DamageListener = null;
            DamageDealer = null;
        }

        /// <summary>Asks the world to deliver an event, once.</summary>
        internal static void Listen(GameEvent gameEvent)
        {
            var index = (int)gameEvent;
            if (listening[index])
            {
                return;
            }

            listening[index] = true;
            Runtime.World.Listen(gameEvent);
        }
    }
}
