using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// Deliveries (J109). The game has no delivery event (probe, 11-28): when a worker brings in a load, in one instant the
    /// player's gold or lumber gathered rises (its state event names no unit), then the game orders the worker back to harvest
    /// (852018, on the mine or tree). Its way back to the building was an order too, 852017 on that building. So a rise of a
    /// player's gathered gold or lumber is held for the instant and given to the next worker of that player ordered back to
    /// harvest, at the building its last way back went to.
    /// </summary>
    internal static class Delivering
    {
        /// <summary>The game's order that sends a loaded worker back to a building (the game's name for it is resumeharvesting), on the building.</summary>
        internal const int ReturnOrder = 852017;

        /// <summary>The harvest order, which the game gives a worker right after its delivery, on the mine or tree.</summary>
        internal const int HarvestOrder = 852018;

        /// <summary>The right-click order: on a building of its own player's, a loaded worker takes its load there.</summary>
        private const int SmartOrder = 851971;

        /// <summary>How far from the worker, at its delivery, a building of its player's is looked for when its way back was not seen.</summary>
        private const float NearBuilding = 400.0f;

        /// <summary>Whether deliveries are listened for: the order events then go here first.</summary>
        internal static bool Active;

        /// <summary>The building each worker's last way back went to, by the worker's handle; forgotten at its delivery, and once the worker is gone.</summary>
        private static OrderedMap<unit, unit> returns = new OrderedMap<unit, unit>();

        /// <summary>The gold and lumber each player gathered in this instant that no worker has taken yet, by slot.</summary>
        private static int[] gold = new int[Player.MaxSlots];

        private static int[] lumber = new int[Player.MaxSlots];

        /// <summary>The timer of 0 s that lets go, after the instant, of what no worker took.</summary>
        private static Timer? clear;

        /// <summary>Forgets the game before's workers and amounts.</summary>
        internal static void Reset()
        {
            Active = false;
            returns = new OrderedMap<unit, unit>();
            gold = new int[Player.MaxSlots];
            lumber = new int[Player.MaxSlots];
            clear = null;
        }

        /// <summary>A player's gathered gold or lumber rose (its state events): held for the instant, for the worker the game orders back to harvest next.</summary>
        internal static void Gathered(int slot, int goldAdded, int lumberAdded)
        {
            if (slot < 0 || slot >= Player.MaxSlots || (goldAdded <= 0 && lumberAdded <= 0))
            {
                return;
            }

            gold[slot] += Math.Max(goldAdded, 0);
            lumber[slot] += Math.Max(lumberAdded, 0);
            clear ??= Game.Instance.OwnAfter(0.0f, Clear);
        }

        /// <summary>A unit is given an order: a way back to a building is remembered, and the harvest order that follows a delivery runs the delivery reactions.</summary>
        internal static void Ordered(unit worker, int id, unit? target)
        {
            if (id == ReturnOrder && target != null)
            {
                returns[worker] = target;
                return;
            }

            if (id == SmartOrder && target != null && Runtime.World.OwnerOf(target) == Runtime.World.OwnerOf(worker) && Runtime.World.IsStructure(target))
            {
                returns[worker] = target;
                return;
            }

            if (id != HarvestOrder)
            {
                return;
            }

            var slot = Runtime.World.SlotOf(Runtime.World.OwnerOf(worker));
            if (slot < 0 || slot >= Player.MaxSlots || (gold[slot] == 0 && lumber[slot] == 0))
            {
                return;
            }

            var goldIn = gold[slot];
            var lumberIn = lumber[slot];
            gold[slot] = 0;
            lumber[slot] = 0;
            returns.TryGetValue(worker, out var went);
            returns.Remove(worker);
            var building = Unit.Of(went != null && Runtime.World.UnitTypeId(went) != 0 ? went : Nearest(worker));
            var carrier = Unit.Of(worker);
            if (building == null || carrier == null || building.HasEnded || carrier.HasEnded)
            {
                return;
            }

            Delivered(carrier, building, slot, goldIn, lumberIn);
        }

        /// <summary>The nearest structure of the worker's player within NearBuilding of it; null when there is none.</summary>
        private static unit? Nearest(unit worker)
        {
            var owner = Runtime.World.OwnerOf(worker);
            var x = Runtime.World.UnitX(worker);
            var y = Runtime.World.UnitY(worker);
            unit? best = null;
            var bestDistance = float.MaxValue;
            var near = Runtime.World.UnitsInRange(x, y, NearBuilding);
            for (var i = 0; i < near.Count; i++)
            {
                var candidate = near[i];
                if (candidate == worker || Runtime.World.OwnerOf(candidate) != owner || !Runtime.World.IsStructure(candidate))
                {
                    continue;
                }

                var dx = Runtime.World.UnitX(candidate) - x;
                var dy = Runtime.World.UnitY(candidate) - y;
                var distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Runs OnDelivered on the building, its components, then the game's components and the worker's player's.</summary>
        private static void Delivered(Unit worker, Unit building, int slot, int goldIn, int lumberIn)
        {
            if ((building.Mask2 & Hook2.OnDelivered) != 0)
            {
                try
                {
                    building.OnDelivered(worker, goldIn, lumberIn);
                }
                catch (Exception error)
                {
                    Errors.Report(building, ".OnDelivered", error);
                }
            }

            var count = building.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = building.ComponentAt(i) as UnitComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnDelivered) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnDelivered(worker, goldIn, lumberIn);
                }
                catch (Exception error)
                {
                    Errors.Report(component, ".OnDelivered", error);
                }
            }

            building.EndDispatch();
            Map(Game.Instance, worker, building, goldIn, lumberIn);
            var player = Player.Existing(slot);
            if (player != null)
            {
                Map(player, worker, building, goldIn, lumberIn);
            }
        }

        /// <summary>Runs OnDelivered on the map components of an actor, in add order.</summary>
        private static void Map(Actor actor, Unit worker, Unit building, int goldIn, int lumberIn)
        {
            var count = actor.BeginDispatch();
            for (var i = 0; i < count; i++)
            {
                var component = actor.ComponentAt(i) as MapComponent;
                if (component == null || component.HasEnded || (component.Mask2 & Hook2.OnDelivered) == 0)
                {
                    continue;
                }

                try
                {
                    component.OnDelivered(worker, building, goldIn, lumberIn);
                }
                catch (Exception error)
                {
                    Errors.Report(component, ".OnDelivered", error);
                }
            }

            actor.EndDispatch();
        }

        /// <summary>After the instant: what no worker took (a wisp's lumber, an acolyte's gold, a code change) is let go, and the ways back of workers that are gone are forgotten.</summary>
        private static void Clear()
        {
            clear = null;
            for (var i = 0; i < gold.Length; i++)
            {
                gold[i] = 0;
                lumber[i] = 0;
            }

            var gone = new List<unit>();
            for (var i = 0; i < returns.Count; i++)
            {
                if (Runtime.World.UnitTypeId(returns.KeyAt(i)) == 0)
                {
                    gone.Add(returns.KeyAt(i));
                }
            }

            for (var i = 0; i < gone.Count; i++)
            {
                returns.Remove(gone[i]);
            }
        }
    }
}
