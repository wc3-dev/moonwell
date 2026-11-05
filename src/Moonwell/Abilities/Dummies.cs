using System.Collections.Generic;
using System.Diagnostics;
using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>
    /// The dummies that cast spells for <c>CastOn</c> and <c>CastAt</c> (B13): hidden units of the type the build declares, kept in
    /// a pool that grows as needed and never removed. A cast takes a free dummy, gives it the player it casts for, stands it where
    /// the spell starts, gives it the spell with range, mana and cooldown taken off its copy, and orders it; the spell takes effect
    /// inside the order (seen 11-03), and an order taken without is stopped, no cast. A target the player does not see is seen for
    /// the instant of the order (decision 186). A dummy
    /// whose spell the game took stays busy, keeping its player, until its channel has ended and its spell's duration, its
    /// missile's flight and a second have passed: a hit names its source when it lands (seen 11-03). A channel stops when its
    /// scope ends.
    /// </summary>
    internal static class Dummies
    {
        /// <summary>How deep dummy casts may nest, one inside another's effect, before the next is stopped (the design's call 14).</summary>
        private const int MostNested = 16;

        /// <summary>The seconds a dummy stays busy past its spell's duration and missile.</summary>
        private const float Margin = 1.0f;

        /// <summary>Every dummy, in the order made.</summary>
        private static List<Dummy> pool = new List<Dummy>();

        /// <summary>Each dummy by its handle.</summary>
        private static Dictionary<unit, Dummy> byHandle = new Dictionary<unit, Dummy>();

        /// <summary>How many dummy casts are running, one inside another.</summary>
        private static int depth;

        /// <summary>
        /// A dummy: its handle, the unit it casts for, the player whose it is, the spell it was given last and its level, the
        /// seconds its missile flies, whether it is busy, whether its spell has ended, and while it channels, what ties the
        /// channel to its scope.
        /// </summary>
        private sealed class Dummy
        {
            internal unit Handle = null!;
            internal Unit? For;
            internal Player Player = null!;
            internal int Ability;
            internal int Level;
            internal float Flight;
            internal bool Busy;
            internal bool Took;
            internal bool Ended;
            internal Channel? Channel;
        }

        /// <summary>A dummy's channel in the scope of its cast: the scope's end stops the channel, unless it ended already.</summary>
        private sealed class Channel : Scoped
        {
            internal Dummy Dummy = null!;

            /// <summary>Stops the channel when it is still on.</summary>
            internal override void Finish()
            {
                Dummy.Channel = null;
                if (!Dummy.Ended)
                {
                    Stop(Dummy);
                }
            }
        }

        /// <summary>Casts a spell through a dummy for a unit, or for a player when the unit is null, at a target or else at a place, in a scope, at a level; the game's answer.</summary>
        internal static bool Cast(Unit? caster, Player player, int abilityId, Widget? target, float x, float y, Scope scope, int level)
        {
            var label = (caster == null ? "Player" : Names.NameOf(caster)) + (target == null ? ".CastAt" : ".CastOn");
            if (scope.HasEnded)
            {
                Dev.Report(label, Names.NameOf(scope) + " has ended, so nothing was cast");
                return false;
            }

            if ((caster != null && caster.IsGone) || (target != null && target.IsGone))
            {
                return false;
            }

            if (ObjectTypes.DummyTypeId == 0)
            {
                Dev.Report(label, "no dummy type is registered: the build declares one when the map uses Moonwell.Objects and its code names CastOn or CastAt");
                return false;
            }

            var order = ObjectTypes.OrderOf(abilityId);
            if (order == null)
            {
                Dev.Report(label, "no order is known for " + ObjectTypes.NameOfId(abilityId) + ": the build registers the map's own abilities, declared or from World Editor, and the standard ones its code names as classes, so name its class (Human.Abilities.StormBolt)");
                return false;
            }

            if (depth >= MostNested)
            {
                Dev.Report(label, "dummy casts nested deeper than " + MostNested + ", each inside another's effect, were stopped");
                return false;
            }

            if (ObjectTypes.SelfOnly(abilityId))
            {
                Dev.Report(label, ObjectTypes.NameOfId(abilityId) + " targets only its caster, so it lands on the dummy that casts it");
            }

            // A dummy is free again from its spell's end-cast, so the spell events are delivered.
            Events.Need(Hook.OnCast);
            var startX = x;
            var startY = y;
            if (target != null)
            {
                startX = XOf(target);
                startY = YOf(target);
            }

            if (caster != null)
            {
                startX = caster.X;
                startY = caster.Y;
            }

            var dummy = Take(player, startX, startY);
            if (dummy == null)
            {
                return false;
            }

            dummy.Ability = abilityId;
            Runtime.World.AddAbility(dummy.Handle, abilityId);
            var given = Runtime.World.ChangeAbilityLevel(dummy.Handle, abilityId, level);
            if (given != level)
            {
                Dev.Report(label, "level " + level + " of " + ObjectTypes.NameOfId(abilityId) + " was asked for, and the game gave " + given + ": an ability's level goes from 1 to its last");
            }

            Runtime.World.FreeCasting(dummy.Handle, abilityId, given);
            dummy.Level = given;
            dummy.Ended = false;
            var speed = Runtime.World.ProjectileSpeedOf(dummy.Handle, abilityId);
            var dx = (target != null ? XOf(target) : x) - startX;
            var dy = (target != null ? YOf(target) : y) - startY;
            dummy.Flight = speed > 0 ? System.MathF.Sqrt(dx * dx + dy * dy) / speed : 0.0f;

            // The spell's hits come inside the order (seen 11-03), so the dummy names its unit before it.
            dummy.For = caster;
            dummy.Took = false;
            var taken = Order(dummy, player, order, target, x, y);
            if (!taken)
            {
                Free(dummy);
                ReportResearch(label, abilityId, player);
                return false;
            }

            // An order taken with no effect inside it is held, not cast (seen 11-03), and would cast whenever the game let it.
            if (!dummy.Took)
            {
                Stop(dummy);
                Free(dummy);
                return false;
            }

            // An instant spell's end-cast came inside the order; a channel goes on, and ends with its scope.
            if (!dummy.Ended)
            {
                if (scope.HasEnded)
                {
                    Stop(dummy);
                }
                else
                {
                    var channel = new Channel { Dummy = dummy };
                    dummy.Channel = channel;
                    scope.Adopt(channel);
                }
            }

            return true;
        }

        /// <summary>A dummy's spell has taken effect.</summary>
        internal static void TookEffect(unit handle)
        {
            if (byHandle.TryGetValue(handle, out var dummy))
            {
                dummy.Took = true;
            }
        }

        /// <summary>A dummy's spell has ended: it is free again once the spell's duration, its missile's flight and the margin have passed.</summary>
        internal static void SpellEnded(unit handle)
        {
            if (!byHandle.TryGetValue(handle, out var dummy) || !dummy.Busy || dummy.Ended)
            {
                return;
            }

            dummy.Ended = true;
            dummy.Channel?.End();
            var lasts = Runtime.World.SpellLasts(dummy.Handle, dummy.Ability, dummy.Level);
            Game.Instance.OwnAfter(lasts + dummy.Flight + Margin, () => Free(dummy));
        }

        /// <summary>A unit changed hands: each busy dummy casting for it is given its new player (the design's call 11), so the game's bounty and Moonwell's reactions credit the same one. The game stops a unit it gives away (seen 11-03), so a channel ends, as the unit's own would.</summary>
        internal static void OwnerChanged(Unit unit)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var dummy = pool[i];
                if (!dummy.Busy || dummy.For != unit)
                {
                    continue;
                }

                var owner = unit.Owner;
                if (dummy.Player != owner)
                {
                    dummy.Player = owner;
                    Runtime.World.SetOwner(dummy.Handle, owner.Handle);
                }
            }
        }

        /// <summary>Whether a unit is a dummy: one of the pool's, or one of the dummy's type, for an event before the pool knows it.</summary>
        internal static bool IsDummy(unit handle)
        {
            return byHandle.ContainsKey(handle) || (ObjectTypes.DummyTypeId != 0 && Runtime.World.UnitTypeId(handle) == ObjectTypes.DummyTypeId);
        }

        /// <summary>The unit a dummy casts for; null for a player's cast and for a unit that is no dummy.</summary>
        internal static Unit? CastFor(unit handle)
        {
            return byHandle.TryGetValue(handle, out var dummy) ? dummy.For : null;
        }

        /// <summary>Forgets every dummy; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            pool = new List<Dummy>();
            byHandle = new Dictionary<unit, Dummy>();
            depth = 0;
        }

        /// <summary>
        /// Orders a dummy: at a target, or at a place; a spell with no target, which refuses those, is cast with the dummy standing
        /// at the target or the place. A target the player does not see is seen for the instant; the game tells no invisible
        /// target from one in fog (seen 11-03), and refuses an invisible one still.
        /// </summary>
        private static bool Order(Dummy dummy, Player player, string order, Widget? target, float x, float y)
        {
            var aimed = target as Unit;
            fogmodifier? patch = null;
            if (aimed != null && !Runtime.World.Sees(player.Handle, aimed.Handle))
            {
                patch = Runtime.World.SeeAround(player.Handle, aimed.X, aimed.Y);
            }

            // The dummy is busy from the order, so a cast inside this one's effect takes another.
            dummy.Busy = true;
            depth++;
            try
            {
                if (target != null ? Runtime.World.OrderUnitOn(dummy.Handle, order, HandleOf(target)) : Runtime.World.OrderUnitAt(dummy.Handle, order, x, y))
                {
                    return true;
                }

                Runtime.World.PlaceUnit(dummy.Handle, target != null ? XOf(target) : x, target != null ? YOf(target) : y);
                return Runtime.World.OrderUnit(dummy.Handle, order);
            }
            finally
            {
                depth--;
                if (patch != null)
                {
                    Runtime.World.StopSeeing(patch);
                }
            }
        }

        /// <summary>A free dummy given the player and stood at a place, made when none is free; null when the game makes none.</summary>
        private static Dummy? Take(Player player, float x, float y)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var free = pool[i];
                if (!free.Busy)
                {
                    if (free.Player != player)
                    {
                        Runtime.World.SetOwner(free.Handle, player.Handle);
                        free.Player = player;
                    }

                    Runtime.World.PlaceUnit(free.Handle, x, y);
                    return free;
                }
            }

            var handle = Runtime.World.SpawnUnit(player.Handle, ObjectTypes.DummyTypeId, x, y, 0.0f);
            if (handle == null)
            {
                Dev.Report("Dummies", "the game made no dummy of type " + Names.IdText(ObjectTypes.DummyTypeId));
                return null;
            }

            var made = new Dummy { Handle = handle, Player = player };
            pool.Add(made);
            byHandle[handle] = made;
            Events.Listen(GameEvent.OwnerChange);
            return made;
        }

        /// <summary>Stops a dummy's channel; its end-cast comes inside the order (seen 11-03).</summary>
        private static void Stop(Dummy dummy)
        {
            Runtime.World.OrderUnit(dummy.Handle, "stop");
        }

        /// <summary>Frees a dummy for the next cast: its spell is taken off and it casts for no one.</summary>
        private static void Free(Dummy dummy)
        {
            dummy.Busy = false;
            dummy.For = null;
            if (dummy.Ability != 0)
            {
                Runtime.World.RemoveAbility(dummy.Handle, dummy.Ability);
                dummy.Ability = 0;
            }
        }

        /// <summary>Reports a refused cast whose player lacks a research the spell requires (decision 185), in a development build.</summary>
        [Conditional("DEBUG")]
        private static void ReportResearch(string label, int abilityId, Player player)
        {
            var required = ObjectTypes.RequirementsOf(abilityId);
            for (var i = 0; i < required.Count; i++)
            {
                if (Runtime.World.TechCount(player.Handle, required[i].Tech) < required[i].Level)
                {
                    Dev.Report(label, ObjectTypes.NameOfId(abilityId) + " needs " + Names.IdText(required[i].Tech) + " at level " + required[i].Level + ", which " + Names.NameOf(player) + " lacks, so the game refused it: for a dummy to cast it, declare a copy with Requires.Remove<...>()");
                    return;
                }
            }
        }

        private static widget HandleOf(Widget target)
        {
            var unit = target as Unit;
            if (unit != null)
            {
                return unit.Handle;
            }

            var item = target as Item;
            if (item != null)
            {
                return item.Handle;
            }

            return ((Destructable)target).Handle;
        }

        private static float XOf(Widget target)
        {
            var unit = target as Unit;
            if (unit != null)
            {
                return unit.X;
            }

            var item = target as Item;
            return item != null ? item.X : ((Destructable)target).X;
        }

        private static float YOf(Widget target)
        {
            var unit = target as Unit;
            if (unit != null)
            {
                return unit.Y;
            }

            var item = target as Item;
            return item != null ? item.Y : ((Destructable)target).Y;
        }
    }
}
