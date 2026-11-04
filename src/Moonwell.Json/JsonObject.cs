using System;
using System.Collections;
using System.Collections.Generic;

namespace Moonwell.Json
{
    /// <summary>
    /// A JSON object: <c>new JsonObject { ["gold"] = 25 }</c>. Its keys keep the order they were added or parsed in, so a
    /// loop over one goes the same way on every machine.
    /// </summary>
    public sealed class JsonObject : JsonNode, IEnumerable<KeyValuePair<string, JsonNode?>>
    {
        private readonly OrderedMap<string, JsonNode?> values = new OrderedMap<string, JsonNode?>();

        /// <summary>How many keys the object has.</summary>
        public int Count => values.Count;

        /// <summary>Adds a key and its value.</summary>
        /// <exception cref="ArgumentException">The object has the key already.</exception>
        /// <exception cref="InvalidOperationException">The value is in a tree already, or holds this object.</exception>
        public void Add(string key, JsonNode? value)
        {
            if (values.ContainsKey(key))
            {
                throw new ArgumentException("the JSON object has the key \"" + key + "\" already", nameof(key));
            }

            Set(key, value);
        }

        /// <summary>Whether the object has a key, whatever its value, null too.</summary>
        public bool ContainsKey(string key)
        {
            return values.ContainsKey(key);
        }

        /// <summary>Removes a key and its value; false when the object has no such key.</summary>
        public bool Remove(string key)
        {
            if (!values.TryGetValue(key, out var old))
            {
                return false;
            }

            values.Remove(key);
            if (old != null)
            {
                old.Parent = null;
            }

            return true;
        }

        /// <summary>The keys and their values, in their order.</summary>
        public IEnumerator<KeyValuePair<string, JsonNode?>> GetEnumerator()
        {
            return values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal JsonNode? Get(string key)
        {
            return values.TryGetValue(key, out var value) ? value : null;
        }

        internal void Set(string key, JsonNode? value)
        {
            var had = values.TryGetValue(key, out var old);
            if (had && old == value)
            {
                return;
            }

            Adopt(value);
            if (old != null)
            {
                old.Parent = null;
            }

            values[key] = value;
        }
    }
}
