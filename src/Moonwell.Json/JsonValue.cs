using System;
using System.Globalization;
using System.Text;

namespace Moonwell.Json
{
    /// <summary>A JSON number, bool or string. A number is an int when it is whole and within int, else a float.</summary>
    public sealed class JsonValue : JsonNode
    {
        private readonly Kind kind;
        private readonly int whole;
        private readonly float number;
        private readonly bool truth;
        private readonly string text;

        private JsonValue(Kind kind, int whole, float number, bool truth, string text)
        {
            this.kind = kind;
            this.whole = whole;
            this.number = number;
            this.truth = truth;
            this.text = text;
        }

        private enum Kind
        {
            Int,
            Float,
            Bool,
            String,
        }

        /// <summary>Whether the value is a number that is an int; the cases read it.</summary>
        internal bool IsInt => kind == Kind.Int;

        /// <summary>A whole number.</summary>
        public static JsonValue Create(int value)
        {
            return new JsonValue(Kind.Int, value, 0.0f, false, "");
        }

        /// <summary>A number.</summary>
        /// <exception cref="ArgumentException">It is infinite or not a number, which JSON has not.</exception>
        public static JsonValue Create(float value)
        {
            if (!(value >= -float.MaxValue && value <= float.MaxValue))
            {
                throw new ArgumentException("JSON has only finite numbers", nameof(value));
            }

            return new JsonValue(Kind.Float, 0, value, false, "");
        }

        /// <summary>A bool.</summary>
        public static JsonValue Create(bool value)
        {
            return new JsonValue(Kind.Bool, 0, 0.0f, value, "");
        }

        /// <summary>A string; null for null, as JSON's null is a null node.</summary>
        public static JsonValue? Create(string? value)
        {
            return value == null ? null : new JsonValue(Kind.String, 0, 0.0f, false, value);
        }

        /// <summary>
        /// Gives the value as an <c>int</c>, a <c>float</c>, a <c>bool</c> or a <c>string</c> when it is one: a number as either
        /// number type, an int only when it is whole and within int. False, and the type's default, when it is not.
        /// </summary>
        public bool TryGetValue<T>(out T value)
        {
            value = default!;
            if (typeof(T) == typeof(int))
            {
                if (kind == Kind.Int)
                {
                    value = (T)(object)whole;
                    return true;
                }

                if (kind == Kind.Float && number >= -2147483648.0f && number < 2147483648.0f && (int)number == number)
                {
                    value = (T)(object)(int)number;
                    return true;
                }

                return false;
            }

            if (typeof(T) == typeof(float))
            {
                if (kind == Kind.Int || kind == Kind.Float)
                {
                    value = (T)(object)(kind == Kind.Int ? (float)whole : number);
                    return true;
                }

                return false;
            }

            if (typeof(T) == typeof(bool) && kind == Kind.Bool)
            {
                value = (T)(object)truth;
                return true;
            }

            if (typeof(T) == typeof(string) && kind == Kind.String)
            {
                value = (T)(object)text;
                return true;
            }

            return false;
        }

        /// <summary>The value as T, or the exception that says why it is not one.</summary>
        internal T Get<T>()
        {
            if (TryGetValue<T>(out var value))
            {
                return value;
            }

            if (typeof(T) == typeof(int) && kind == Kind.Float)
            {
                throw new FormatException("the JSON number " + Written() + " is not a whole number within int");
            }

            throw new InvalidOperationException("a JSON " + KindName() + " is not " + TypeName<T>());
        }

        /// <summary>Writes the value as JSON.</summary>
        internal void WriteTo(StringBuilder json)
        {
            if (kind == Kind.String)
            {
                JsonText.WriteString(text, json);
            }
            else
            {
                json.Append(Written());
            }
        }

        private string Written()
        {
            switch (kind)
            {
                case Kind.Int:
                    return whole.ToString(CultureInfo.InvariantCulture);
                case Kind.Float:
                    return number.ToString("R", CultureInfo.InvariantCulture);
                case Kind.Bool:
                    return truth ? "true" : "false";
                default:
                    return text;
            }
        }

        private string KindName()
        {
            return kind == Kind.Bool ? "bool" : kind == Kind.String ? "string" : "number";
        }

        private static string TypeName<T>()
        {
            return typeof(T) == typeof(int) ? "an int" : typeof(T) == typeof(float) ? "a float" : typeof(T) == typeof(bool) ? "a bool" : typeof(T) == typeof(string) ? "a string" : "a type JSON has";
        }
    }
}
