using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>Adds several starting components, or the map's slots, in one call: <c>Components.Add(new Start(), new Rules());</c>.</summary>
    public static class ComponentLists
    {
        /// <summary>Adds the components to the end of the list, in the order given.</summary>
        public static void Add<T>(this List<T> list, T first, T second, params T[] more)
            where T : Component
        {
            list.Add(first);
            list.Add(second);
            for (var i = 0; i < more.Length; i++)
            {
                list.Add(more[i]);
            }
        }

        /// <summary>Adds the slots to the end of the map's list, in the order given: <c>Slots.Add(new Slot(0, West, Controller.User), new Slot(1, East, Controller.User));</c>.</summary>
        public static void Add(this List<Slot> list, Slot first, Slot second, params Slot[] more)
        {
            list.Add(first);
            list.Add(second);
            for (var i = 0; i < more.Length; i++)
            {
                list.Add(more[i]);
            }
        }

        /// <summary>Adds the makers of player components to the end of the list, in the order given: <c>PlayerComponents.Add(() => new Aim(), () => new Score());</c>.</summary>
        public static void Add(this List<Func<PlayerComponent>> list, Func<PlayerComponent> first, Func<PlayerComponent> second, params Func<PlayerComponent>[] more)
        {
            list.Add(first);
            list.Add(second);
            for (var i = 0; i < more.Length; i++)
            {
                list.Add(more[i]);
            }
        }
    }
}
