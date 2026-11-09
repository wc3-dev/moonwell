using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>
    /// The game as the pathing package sees it, through the game's natives; the only class of the package that calls them.
    /// The helpers are hidden units of the types the build declares, made on first use, owned by Neutral Passive: a hidden
    /// unit is put by its own square against terrain, destructables, buildings and shown units, and blocks nothing itself
    /// (probe 11-09). It has no Locust, which would drop units from its square.
    /// </summary>
    internal sealed class NativePathingWorld : IPathingWorld
    {
        /// <summary>The helper types, cells 1 to 4 for each movement but flying (Ground, Float, Amphibious): index (cells - 1) * 3 + movement.</summary>
        private readonly int[] types;

        /// <summary>The helpers made so far, by the same index.</summary>
        private readonly unit?[] helpers = new unit?[12];

        internal NativePathingWorld(int[] typeIds)
        {
            types = typeIds;
        }

        public void Try(int cells, Movement movement, float x, float y, out float landedX, out float landedY)
        {
            var index = (cells - 1) * 3 + (int)movement;
            var helper = helpers[index];
            if (helper == null)
            {
                helper = Natives.CreateUnit(Natives.Player(Natives.PLAYER_NEUTRAL_PASSIVE), types[index], x, y, 0f);
                Natives.ShowUnit(helper, false);
                helpers[index] = helper;
            }

            Natives.SetUnitPosition(helper, x, y);
            landedX = Natives.GetUnitX(helper);
            landedY = Natives.GetUnitY(helper);
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
