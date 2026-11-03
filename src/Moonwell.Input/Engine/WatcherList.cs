using System;
using System.Collections.Generic;

namespace Moonwell.Engine
{
    /// <summary>
    /// One actor's map components that write one of a package's reactions, with their masks, read again only when the actor's
    /// components change, so an event does not look at every component again (a map with many areas has many game
    /// components).
    /// </summary>
    internal sealed class WatcherList
    {
        /// <summary>The actor they were read from; null before the first read.</summary>
        private Actor? actor;

        /// <summary>The actor's list of components they were read from: core makes a new list when it drops a removed component.</summary>
        private List<Component?>? source;

        /// <summary>How many components that list held then: an added one makes it more.</summary>
        private int count;

        /// <summary>The watchers, in add order; a new list at each read, so a loop over the old one is not changed under it.</summary>
        internal List<MapComponent> Found { get; private set; } = new List<MapComponent>();

        /// <summary>The mask of each watcher, in the same order.</summary>
        internal List<int> Masks { get; private set; } = new List<int>();

        /// <summary>Reads the actor's watchers again when its components changed since the last read: the map components that have not ended and whose mask is not 0.</summary>
        internal void Refresh(Actor of, Func<object, int> maskOf)
        {
            if (actor == of && source == of.components && count == of.components.Count)
            {
                return;
            }

            var components = of.components;
            var found = new List<MapComponent>();
            var masks = new List<int>();
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i] as MapComponent;
                if (component != null && !component.HasEnded)
                {
                    var mask = maskOf(component);
                    if (mask != 0)
                    {
                        found.Add(component);
                        masks.Add(mask);
                    }
                }
            }

            actor = of;
            source = components;
            count = components.Count;
            Found = found;
            Masks = masks;
        }
    }
}
