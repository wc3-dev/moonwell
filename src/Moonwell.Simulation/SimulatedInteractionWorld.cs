using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>
    /// The interactions package's world in a Simulation, so that Find, interactions and gizmos answer from the fake's own units,
    /// items and destructables: a unit has a place while it is alive, shown and not in a transport, an item while it lies on
    /// the ground, a destructable while its life is above 0, and the world runs from -4096 to 4096 both ways. No destructable
    /// type is a tree, as in a map built without the build's table of them. Pathing and terrain stay the game's (decision 313).
    /// </summary>
    internal sealed class SimulatedInteractionWorld : IInteractionWorld
    {
        private readonly FakeWorld world;

        private SimulatedInteractionWorld(FakeWorld world)
        {
            this.world = world;
        }

        /// <summary>Makes a fake world the game's own world of the interactions package, which installs itself on it at its first use as it does on the game's; Simulation.Start calls it after the runtime is made, and FakeWorld.Install lets it go.</summary>
        internal static void Install(FakeWorld world)
        {
            Interactions.native = new SimulatedInteractionWorld(world);
        }

        /// <summary>Whether a unit is hidden.</summary>
        public bool IsHidden(unit target)
        {
            return world.IsHidden(target);
        }

        /// <summary>Whether a unit is in a transport.</summary>
        public bool IsLoaded(unit target)
        {
            return world.IsLoaded(target);
        }

        /// <summary>Whether an item lies on the ground: neither gone nor held in a unit's slots or bag.</summary>
        public bool IsVisible(item target)
        {
            return world.ItemTypeId(target) != 0 && !world.IsItemHeld(target);
        }

        /// <summary>Whether a destructable's life is above 0.</summary>
        public bool IsAlive(destructable target)
        {
            return world.DestructableLifeOf(target) > 0.0f;
        }

        /// <summary>No type is a tree: the fake has no table of them.</summary>
        public bool IsTreeType(int typeId)
        {
            return false;
        }

        /// <summary>The fake's world bounds.</summary>
        public void WorldBounds(out float minX, out float minY, out float maxX, out float maxY)
        {
            var bounds = world.WorldBounds();
            minX = bounds[0];
            minY = bounds[1];
            maxX = bounds[2];
            maxY = bounds[3];
        }
    }
}
