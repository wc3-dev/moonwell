using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The units of [Pooled] classes that have ended, kept hidden to be made again: making a unit costs the game about ten
    /// times what hiding, moving and showing one does (measured 11-08). Unit.Create takes a kept one of its type; the end of
    /// a living unit of a pooled type, not a hero or an illusion, keeps it (decision 220), unless it is in a transport or carries units (decision 219).
    /// </summary>
    internal static class UnitPool
    {
        /// <summary>The pooled types: their life and mana as the game makes them, read when the first is made.</summary>
        private static Dictionary<int, float[]>? types;

        /// <summary>The kept units of each pooled type, the last kept first out.</summary>
        private static Dictionary<int, List<unit>>? kept;

        /// <summary>Every kept unit, which nothing takes in while it is kept.</summary>
        private static HashSet<unit>? held;

        /// <summary>Marks a unit type pooled; the map's registry calls it for each [Pooled] class.</summary>
        internal static void Mark(int typeId)
        {
            if (types == null)
            {
                types = new Dictionary<int, float[]>();
                kept = new Dictionary<int, List<unit>>();
                held = new HashSet<unit>();
            }

            types[typeId] = new float[0];
        }

        /// <summary>A kept unit of the type, made ready as a new one: its owner, place and facing given, its life and mana as the game makes them, shown. Null when the type is not pooled or none is kept.</summary>
        internal static unit? Take(int typeId, Player owner, float x, float y, float facing)
        {
            if (kept == null || !kept.TryGetValue(typeId, out var units) || units.Count == 0)
            {
                return null;
            }

            var handle = units[units.Count - 1];
            units.RemoveAt(units.Count - 1);
            held!.Remove(handle);
            var world = Runtime.World;
            var made = types![typeId];
            world.SetOwner(handle, owner.Handle);
            world.MoveUnit(handle, x, y);
            world.FaceUnitNow(handle, facing);
            world.SetLife(handle, made[0]);
            world.SetMana(handle, made[1]);
            world.SetUnitShown(handle, true);
            return handle;
        }

        /// <summary>Notes the life and mana a pooled type is made with, from the first one the game makes.</summary>
        internal static void Made(int typeId, unit handle)
        {
            if (types != null && types.TryGetValue(typeId, out var made) && made.Length == 0)
            {
                types[typeId] = new[] { Runtime.World.LifeOf(handle), Runtime.World.ManaOf(handle) };
            }
        }

        /// <summary>Keeps an ending unit instead of removing it, when its type is pooled and it is alive, not a hero and not an illusion: its items are removed, its buffs taken off, and it is hidden and given to Neutral Passive, so it costs no food. False when it is not kept.</summary>
        internal static bool Keep(Unit unit)
        {
            if (kept == null || !types!.TryGetValue(unit.TypeId, out var made) || made.Length == 0 || unit is Hero || unit.IsIllusion)
            {
                return false;
            }

            var world = Runtime.World;
            var handle = unit.Handle;
            if (!world.IsAlive(handle) || world.IsLoaded(handle) || (unit.cargo != null && unit.cargo.Count > 0))
            {
                return false;
            }

            for (var slot = world.InventorySizeOf(handle) - 1; slot >= 0; slot--)
            {
                var carried = world.ItemInSlot(handle, slot);
                if (carried == null)
                {
                    continue;
                }

                var known = Item.Of(carried);
                if (known != null)
                {
                    known.Remove();
                }
                else
                {
                    world.DeleteItem(carried);
                }
            }

            world.PutAway(handle, Player.NeutralPassive.Handle);
            if (!kept!.TryGetValue(unit.TypeId, out var units))
            {
                units = new List<unit>();
                kept[unit.TypeId] = units;
            }

            units.Add(handle);
            held!.Add(handle);
            return true;
        }

        /// <summary>Whether the pool keeps the unit now, so that it is not taken in as a unit of the map.</summary>
        internal static bool Holds(unit handle)
        {
            return held != null && held.Contains(handle);
        }

        /// <summary>Forgets the pooled types and the kept units; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            types = null;
            kept = null;
            held = null;
        }
    }
}
