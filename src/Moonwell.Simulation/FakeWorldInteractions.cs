using Moonwell.Hosting;

namespace Moonwell
{
    /// <summary>
    /// The fake world as the interactions package's world, so that in a Simulation Find, interactions and gizmos answer from the
    /// fake's own units, items and destructables: a unit has a place while it is alive, shown and not in a transport, an item
    /// while it lies on the ground, a destructable while its life is above 0, and the world runs from -4096 to 4096 both ways.
    /// No destructable type is a tree, as in a map built without the build's table of them. Pathing and terrain stay the game's
    /// (decision 313).
    /// </summary>
    public sealed partial class FakeWorld : IInteractionWorld
    {
        /// <summary>Makes this world the game's own world of the interactions package, which installs itself on it at its first use as it does on the game's; Simulation.Start calls it after the runtime is made.</summary>
        internal void InstallInteractions()
        {
            Interactions.native = this;
        }

        /// <summary>Whether a unit is hidden.</summary>
        bool IInteractionWorld.IsHidden(unit target)
        {
            return IsHidden(target);
        }

        /// <summary>Whether a unit is in a transport.</summary>
        bool IInteractionWorld.IsLoaded(unit target)
        {
            return IsLoaded(target);
        }

        /// <summary>Whether an item lies on the ground: neither gone nor held in a unit's slots or bag.</summary>
        bool IInteractionWorld.IsVisible(item target)
        {
            return ItemTypeId(target) != 0 && !IsItemHeld(target);
        }

        /// <summary>Whether a destructable's life is above 0.</summary>
        bool IInteractionWorld.IsAlive(destructable target)
        {
            return DestructableLifeOf(target) > 0.0f;
        }

        /// <summary>No type is a tree: the fake has no table of them.</summary>
        bool IInteractionWorld.IsTreeType(int typeId)
        {
            return false;
        }

        /// <summary>The fake's world bounds.</summary>
        void IInteractionWorld.WorldBounds(out float minX, out float minY, out float maxX, out float maxY)
        {
            var bounds = WorldBounds();
            minX = bounds[0];
            minY = bounds[1];
            maxX = bounds[2];
            maxY = bounds[3];
        }
    }
}
