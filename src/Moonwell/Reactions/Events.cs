using System;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>Registers each game event with the world once, when a reaction first needs it.</summary>
    internal static class Events
    {
        /// <summary>Whether the world already listens for each game event, indexed by GameEvent.</summary>
        private static bool[] listening = new bool[EventCount];

        /// <summary>How many GameEvents there are.</summary>
        private const int EventCount = 19;

        /// <summary>The reaction bits that some class overrides, once any has been needed.</summary>
        private static int wanted;

        /// <summary>Set by a damage package: called in place of listening for the game's damage event; null without one.</summary>
        internal static Action? DamageListener;

        /// <summary>Set by the frames package: makes its frames again when a saved game loads, before the map's OnGameLoaded; null without one.</summary>
        internal static Action? LoadListener;

        /// <summary>Set by the terrain package: makes its raises and painted tiles again when a saved game loads, before the map's OnGameLoaded; null without one.</summary>
        internal static Action? TerrainLoadListener;

        /// <summary>Set by the terrain package: takes its craters out of the game when the game is about to write a saved game; null without one.</summary>
        internal static Action? TerrainSaveListener;

        /// <summary>Set by the AI package: hears a unit whose acquire event is watched pick a target by itself; null without one.</summary>
        internal static Action<Unit>? AcquireListener;

        /// <summary>Set by the AI package: hears each order given to a known unit, before its reactions, with whether it came from code; Moonwell's own orders, undefend and the stunned order are not heard. Null without one.</summary>
        internal static Action<Unit, bool>? OrderListener;

        /// <summary>A damage package's own Unit.Damage, which deals through its stages and returns its hit; null without one.</summary>
        internal static Func<Unit, Unit, float, DamageKind, AttackType, bool, bool, WeaponSound, object?, Damage?>? DamageDealer;

        /// <summary>Adds the reaction bits to the wanted ones, and listens for the event each bit, of Hook and of Hook2 (mask2), needs.</summary>
        internal static void Need(int mask, int mask2 = 0)
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

            // The equip event too: an equip or unequip by hand in the 3.0 panel fires neither a pick-up nor a drop (I54).
            if ((mask & (Hook.OnItemPickedUp | Hook.OnPickedUp)) != 0)
            {
                Listen(GameEvent.PickUp);
                Listen(GameEvent.Equip);
            }

            if ((mask & (Hook.OnItemDropped | Hook.OnDropped)) != 0)
            {
                Listen(GameEvent.Drop);
                Listen(GameEvent.Equip);
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

            if ((mask & Hook.OnGameLoaded) != 0)
            {
                Listen(GameEvent.Loaded);
            }

            if ((mask & Hook.OnSelection) != 0)
            {
                Listen(GameEvent.Selection);
            }

            if ((mask & Hook.OnTransport) != 0)
            {
                // The game has no unload event: the order it gives the cargo on every way out tells it (seen 11-08).
                Listen(GameEvent.Cargo);
                Listen(GameEvent.Order);
            }

            if ((mask & Hook.OnSummon) != 0)
            {
                Listen(GameEvent.Summon);
            }

            if ((mask2 & Hook2.OnResearchOrSale) != 0)
            {
                Listen(GameEvent.Research);
                Listen(GameEvent.Sale);
            }

            if ((mask & Hook.OnAllianceChanged) != 0)
            {
                if (!listening[(int)GameEvent.Alliance])
                {
                    Inbound.WatchAlliances();
                }

                Listen(GameEvent.Alliance);
            }
        }

        /// <summary>Needs the reactions obj's class overrides, in both words.</summary>
        internal static void NeedOf(object obj, HookTable table)
        {
            var mask = Hooks.MaskOf(obj, table, out var mask2);
            Need(mask, mask2);
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
            listening = new bool[EventCount];
            wanted = 0;
            DamageListener = null;
            LoadListener = null;
            TerrainLoadListener = null;
            TerrainSaveListener = null;
            AcquireListener = null;
            OrderListener = null;
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
