using System.Collections;
using System.Collections.Generic;

namespace Moonwell.Json
{
    /// <summary>A JSON array: <c>new JsonArray { 1, 2.5f, "three" }</c>.</summary>
    public sealed class JsonArray : JsonNode, IEnumerable<JsonNode?>
    {
        private readonly List<JsonNode?> items = new List<JsonNode?>();

        /// <summary>How many items the array has.</summary>
        public int Count => items.Count;

        /// <summary>Adds an item at the end.</summary>
        /// <exception cref="System.InvalidOperationException">The item is in a tree already, or holds this array.</exception>
        public void Add(JsonNode? item)
        {
            Adopt(item);
            items.Add(item);
        }

        /// <summary>Removes the item at an index from 0; the items after it move down one.</summary>
        public void RemoveAt(int index)
        {
            var old = items[index];
            items.RemoveAt(index);
            if (old != null)
            {
                old.Parent = null;
            }
        }

        /// <summary>The items, in order.</summary>
        public IEnumerator<JsonNode?> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal JsonNode? Get(int index)
        {
            return items[index];
        }

        internal void Set(int index, JsonNode? value)
        {
            var old = items[index];
            if (old == value)
            {
                return;
            }

            Adopt(value);
            if (old != null)
            {
                old.Parent = null;
            }

            items[index] = value;
        }
    }
}
