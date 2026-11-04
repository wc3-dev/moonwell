using System;
using System.Text;

namespace Moonwell.Json
{
    /// <summary>
    /// A JSON value: an object, an array, a number, a bool or a string, with System.Text.Json.Nodes' names (decision 178).
    /// A JSON null is a null node. A number is an int when it is whole and within int, else a float.
    /// </summary>
    public abstract class JsonNode
    {
        internal JsonNode()
        {
        }

        /// <summary>The object or array that holds this node; null for a node in none. A node is in one place at most.</summary>
        public JsonNode? Parent { get; internal set; }

        /// <summary>An object's value for a key, null when it has none; setting it adds the key or replaces its value.</summary>
        /// <exception cref="InvalidOperationException">The node is not an object.</exception>
        public JsonNode? this[string key]
        {
            get => AsObject().Get(key);
            set => AsObject().Set(key, value);
        }

        /// <summary>An array's item at an index from 0.</summary>
        /// <exception cref="InvalidOperationException">The node is not an array.</exception>
        public JsonNode? this[int index]
        {
            get => AsArray().Get(index);
            set => AsArray().Set(index, value);
        }

        /// <summary>Reads JSON text: <c>JsonNode.Parse(text)!["gold"]</c>. Null for the text <c>null</c>.</summary>
        /// <exception cref="JsonException">The text is no JSON, or nests deeper than 64.</exception>
        public static JsonNode? Parse(string text)
        {
            return JsonReader.Parse(text);
        }

        /// <summary>The node as compact JSON text, an object's keys in their order.</summary>
        public string ToJsonString()
        {
            var text = new StringBuilder();
            JsonText.Write(this, text);
            return text.ToString();
        }

        /// <summary>The node as compact JSON text, as <see cref="ToJsonString"/>.</summary>
        public override string ToString()
        {
            return ToJsonString();
        }

        /// <summary>The node as an object.</summary>
        /// <exception cref="InvalidOperationException">It is an array or a value.</exception>
        public JsonObject AsObject()
        {
            if (this is JsonObject json)
            {
                return json;
            }

            throw new InvalidOperationException("a JSON " + KindName() + " is not an object");
        }

        /// <summary>The node as an array.</summary>
        /// <exception cref="InvalidOperationException">It is an object or a value.</exception>
        public JsonArray AsArray()
        {
            if (this is JsonArray json)
            {
                return json;
            }

            throw new InvalidOperationException("a JSON " + KindName() + " is not an array");
        }

        /// <summary>The node as a value: a number, a bool or a string.</summary>
        /// <exception cref="InvalidOperationException">It is an object or an array.</exception>
        public JsonValue AsValue()
        {
            if (this is JsonValue json)
            {
                return json;
            }

            throw new InvalidOperationException("a JSON " + KindName() + " is not a value");
        }

        /// <summary>The value as an <c>int</c>, a <c>float</c>, a <c>bool</c> or a <c>string</c>: <c>node["gold"]!.GetValue&lt;int&gt;()</c>.</summary>
        /// <exception cref="InvalidOperationException">The node is not a value, or a value of another kind.</exception>
        /// <exception cref="FormatException">An int is asked of a number that is not whole or not within int.</exception>
        public T GetValue<T>()
        {
            return AsValue().Get<T>();
        }

        /// <summary>A number as JSON.</summary>
        public static implicit operator JsonNode(int value) => JsonValue.Create(value);

        /// <summary>A number as JSON.</summary>
        /// <exception cref="ArgumentException">It is infinite or not a number, which JSON has not.</exception>
        public static implicit operator JsonNode(float value) => JsonValue.Create(value);

        /// <summary>A bool as JSON.</summary>
        public static implicit operator JsonNode(bool value) => JsonValue.Create(value);

        /// <summary>A string as JSON; null for null.</summary>
        public static implicit operator JsonNode?(string? value) => JsonValue.Create(value);

        /// <summary>A number as an int: <c>(int)node["gold"]</c>.</summary>
        /// <exception cref="InvalidOperationException">The node is null, or not a number.</exception>
        /// <exception cref="FormatException">The number is not whole or not within int.</exception>
        public static explicit operator int(JsonNode? node) => Value(node).Get<int>();

        /// <summary>A number as a float.</summary>
        /// <exception cref="InvalidOperationException">The node is null, or not a number.</exception>
        public static explicit operator float(JsonNode? node) => Value(node).Get<float>();

        /// <summary>A bool.</summary>
        /// <exception cref="InvalidOperationException">The node is null, or not a bool.</exception>
        public static explicit operator bool(JsonNode? node) => Value(node).Get<bool>();

        /// <summary>A string; null for null.</summary>
        /// <exception cref="InvalidOperationException">The node is not a string.</exception>
        public static explicit operator string?(JsonNode? node) => node == null ? null : node.AsValue().Get<string>();

        /// <summary>Takes a node in as this node's child.</summary>
        /// <exception cref="InvalidOperationException">It is in a tree already, or it holds this node.</exception>
        internal void Adopt(JsonNode? child)
        {
            if (child == null)
            {
                return;
            }

            if (child.Parent != null)
            {
                throw new InvalidOperationException("the node is in a tree already: remove it from there first");
            }

            for (JsonNode? up = this; up != null; up = up.Parent)
            {
                if (up == child)
                {
                    throw new InvalidOperationException("a node cannot hold itself");
                }
            }

            child.Parent = this;
        }

        private static JsonValue Value(JsonNode? node)
        {
            if (node == null)
            {
                throw new InvalidOperationException("the JSON is null");
            }

            return node.AsValue();
        }

        private string KindName()
        {
            return this is JsonObject ? "object" : this is JsonArray ? "array" : "value";
        }
    }
}
