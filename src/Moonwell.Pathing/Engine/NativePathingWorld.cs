using System;
using System.Collections.Generic;
using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>
    /// The game as the pathing package sees it, through the game's natives; the only class of the package that calls them.
    /// The helpers are hidden units of the types the build declares, made on first use, owned by Neutral Passive: a hidden
    /// unit is put by its own square against terrain, destructables, buildings and shown units, and blocks nothing itself
    /// (probe 11-09). It has no Locust, which would drop units from its square. A unit is left out of a try by Ghost (Visible),
    /// added for the moment of it: the helper then stands on the unit, and adding and removing it in one instant fires no
    /// order or selection event and keeps a walker's or attacker's order (probe 11-10). Turning its pathing off does not
    /// leave it out.
    /// </summary>
    internal sealed class NativePathingWorld : IPathingWorld
    {
        /// <summary>The helper types, cells 1 to 4 for each movement but flying (Ground, Float, Amphibious): index (cells - 1) * 3 + movement.</summary>
        private readonly int[] types;

        private static readonly int Ghost = ObjectTypes.FourCC("Aeth");

        /// <summary>The helpers made so far, by the same index.</summary>
        private readonly unit?[] helpers = new unit?[12];

        /// <summary>How far round a blocked cell's middle the destructables whose deaths are watched may stand: a texture up to 8 cells across has its place within it.</summary>
        // shortcut: a destructable larger than 8 cells (a bridge, a big gate) killed by the game is not seen; Moonwell's own changes to it are. Widen the rect if maps need it.
        private const float WatchReach = 128f;

        /// <summary>The destructables whose deaths are watched.</summary>
        private readonly HashSet<destructable> watched = new HashSet<destructable>();

        /// <summary>The trigger the watched deaths run, made by Listen.</summary>
        private trigger? deaths;

        /// <summary>The rect the destructables near a cell are found in, moved for each.</summary>
        private rect? near;

        internal NativePathingWorld(int[] typeIds)
        {
            types = typeIds;
        }

        public bool IsLiveBuilding(unit handle)
        {
            return Natives.IsUnitType(handle, Natives.UNIT_TYPE_STRUCTURE) && !Natives.IsUnitType(handle, Natives.UNIT_TYPE_DEAD) && Natives.GetUnitTypeId(handle) != 0;
        }

        public void PlaceOf(unit handle, out float x, out float y)
        {
            x = Natives.GetUnitX(handle);
            y = Natives.GetUnitY(handle);
        }

        public void Listen(Action<float, float> changed)
        {
            deaths = Natives.CreateTrigger();
            Natives.TriggerAddAction(deaths, () =>
            {
                var dead = Natives.GetTriggerDestructable();
                changed(Natives.GetDestructableX(dead), Natives.GetDestructableY(dead));
            });
            var buildings = Natives.CreateTrigger();
            for (var slot = 0; slot < Natives.bj_MAX_PLAYER_SLOTS; slot++)
            {
                Natives.TriggerRegisterPlayerUnitEvent(buildings, Natives.Player(slot), Natives.EVENT_PLAYER_UNIT_DEATH, null);
            }

            Natives.TriggerAddAction(buildings, () =>
            {
                var dying = Natives.GetTriggerUnit();
                if (Natives.IsUnitType(dying, Natives.UNIT_TYPE_STRUCTURE))
                {
                    changed(Natives.GetUnitX(dying), Natives.GetUnitY(dying));
                }
            });
        }

        public void WatchDeathsAround(float x, float y)
        {
            if (deaths == null)
            {
                return;
            }

            near ??= Natives.Rect(0f, 0f, 0f, 0f);
            Natives.SetRect(near, x - WatchReach, y - WatchReach, x + WatchReach, y + WatchReach);
            Natives.EnumDestructablesInRect(near, null, () =>
            {
                var found = Natives.GetEnumDestructable();
                if (watched.Add(found))
                {
                    Natives.TriggerRegisterDeathEvent(deaths, found);
                }
            });
        }

        public void Try(int cells, Movement movement, float x, float y, unit? leftOut, out float landedX, out float landedY)
        {
            var index = (cells - 1) * 3 + (int)movement;
            var helper = helpers[index];
            if (helper == null)
            {
                helper = Natives.CreateUnit(Natives.Player(Natives.PLAYER_NEUTRAL_PASSIVE), types[index], x, y, 0f);
                Natives.ShowUnit(helper, false);
                helpers[index] = helper;
            }

            // A unit that has Ghost (Visible) already is left out as it is, and keeps it.
            var ghosted = leftOut != null && Natives.UnitAddAbility(leftOut, Ghost);
            Natives.SetUnitPosition(helper, x, y);
            landedX = Natives.GetUnitX(helper);
            landedY = Natives.GetUnitY(helper);
            if (ghosted)
            {
                Natives.UnitRemoveAbility(leftOut!, Ghost);
            }
        }

        public float CollisionOf(unit handle)
        {
            return Natives.BlzGetUnitCollisionSize(handle);
        }

        // shortcut: the field's numbers (2 fly, 16 float, 32 amphibious) are the community's, not yet probed; the 0.10 gate checks them.
        public Movement MovementOf(unit handle)
        {
            var type = Natives.BlzGetUnitIntegerField(handle, Natives.UNIT_IF_MOVE_TYPE);
            if (type == 2)
            {
                return Movement.Fly;
            }

            if (type == 16)
            {
                return Movement.Float;
            }

            return type == 32 ? Movement.Amphibious : Movement.Ground;
        }
    }
}
