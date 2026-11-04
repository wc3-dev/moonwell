using System.Globalization;
using System.Text;

namespace Moonwell.Json
{
    /// <summary>Reads JSON text into a tree, as RFC 8259 writes it, nested 64 deep at most, as .NET's reader.</summary>
    internal sealed class JsonReader
    {
        private const int MostDepth = 64;

        private readonly string text;
        private int at;

        private JsonReader(string text)
        {
            this.text = text;
        }

        internal static JsonNode? Parse(string text)
        {
            var reader = new JsonReader(text);
            reader.SkipSpace();
            var node = reader.Value(0);
            reader.SkipSpace();
            if (reader.at < text.Length)
            {
                throw reader.Error("more follows the JSON");
            }

            return node;
        }

        private JsonNode? Value(int depth)
        {
            if (at >= text.Length)
            {
                throw Error("the text ends where a value was expected");
            }

            var c = text[at];
            if (c == '{')
            {
                return Object(depth + 1);
            }

            if (c == '[')
            {
                return Array(depth + 1);
            }

            if (c == '"')
            {
                return JsonValue.Create(String());
            }

            if (c == '-' || (c >= '0' && c <= '9'))
            {
                return Number();
            }

            if (Word("true"))
            {
                return JsonValue.Create(true);
            }

            if (Word("false"))
            {
                return JsonValue.Create(false);
            }

            if (Word("null"))
            {
                return null;
            }

            throw Error("no JSON value starts here");
        }

        private JsonObject Object(int depth)
        {
            Deepen(depth);
            var json = new JsonObject();
            at++;
            SkipSpace();
            if (Next('}'))
            {
                return json;
            }

            while (true)
            {
                SkipSpace();
                if (at >= text.Length || text[at] != '"')
                {
                    throw Error("a key must be a string");
                }

                var key = String();
                SkipSpace();
                if (!Next(':'))
                {
                    throw Error("a ':' must follow a key");
                }

                SkipSpace();
                json.Set(key, Value(depth));
                SkipSpace();
                if (Next('}'))
                {
                    return json;
                }

                if (!Next(','))
                {
                    throw Error("a ',' or a '}' must follow a value in an object");
                }
            }
        }

        private JsonArray Array(int depth)
        {
            Deepen(depth);
            var json = new JsonArray();
            at++;
            SkipSpace();
            if (Next(']'))
            {
                return json;
            }

            while (true)
            {
                SkipSpace();
                json.Add(Value(depth));
                SkipSpace();
                if (Next(']'))
                {
                    return json;
                }

                if (!Next(','))
                {
                    throw Error("a ',' or a ']' must follow a value in an array");
                }
            }
        }

        private string String()
        {
            at++;
            var value = new StringBuilder();
            var from = at;
            while (true)
            {
                if (at >= text.Length)
                {
                    throw Error("the text ends inside a string");
                }

                var c = text[at];
                if (c == '"')
                {
                    value.Append(text.Substring(from, at - from));
                    at++;
                    return value.ToString();
                }

                if (c < ' ')
                {
                    throw Error("a control character must be written as an escape");
                }

                if (c != '\\')
                {
                    at++;
                    continue;
                }

                value.Append(text.Substring(from, at - from));
                at++;
                if (at >= text.Length)
                {
                    throw Error("the text ends inside an escape");
                }

                var escape = text[at];
                at++;
                switch (escape)
                {
                    case '"':
                        value.Append('"');
                        break;
                    case '\\':
                        value.Append('\\');
                        break;
                    case '/':
                        value.Append('/');
                        break;
                    case 'b':
                        value.Append('\b');
                        break;
                    case 'f':
                        value.Append('\f');
                        break;
                    case 'n':
                        value.Append('\n');
                        break;
                    case 'r':
                        value.Append('\r');
                        break;
                    case 't':
                        value.Append('\t');
                        break;
                    case 'u':
                        Utf8.Append(value, Letter());
                        break;
                    default:
                        at -= 2;
                        throw Error("no such escape");
                }

                from = at;
            }
        }

        // The letter of a \u escape, read past its "\u": a surrogate pair is one letter, and a lone surrogate U+FFFD.
        private int Letter()
        {
            var letter = Hex4();
            if (letter < 0xD800 || letter > 0xDFFF)
            {
                return letter;
            }

            if (letter <= 0xDBFF && at + 1 < text.Length && text[at] == '\\' && text[at + 1] == 'u')
            {
                var pair = at;
                at += 2;
                var low = Hex4();
                if (low >= 0xDC00 && low <= 0xDFFF)
                {
                    return 0x10000 + ((letter - 0xD800) << 10) + (low - 0xDC00);
                }

                at = pair;
            }

            return 0xFFFD;
        }

        private int Hex4()
        {
            var value = 0;
            for (var i = 0; i < 4; i++)
            {
                var digit = at < text.Length ? "0123456789abcdef".IndexOf(char.ToLowerInvariant(text[at])) : -1;
                if (digit < 0)
                {
                    throw Error("\\u must be followed by four hex digits");
                }

                value = value * 16 + digit;
                at++;
            }

            return value;
        }

        private JsonNode Number()
        {
            var start = at;
            Next('-');
            if (!Next('0') && !Digits())
            {
                throw Error("a number must start with a digit");
            }

            var whole = true;
            if (Next('.'))
            {
                whole = false;
                if (!Digits())
                {
                    throw Error("a digit must follow the point");
                }
            }

            if (Next('e') || Next('E'))
            {
                whole = false;
                if (!Next('+'))
                {
                    Next('-');
                }

                if (!Digits())
                {
                    throw Error("a digit must follow the exponent");
                }
            }

            var token = text.Substring(start, at - start);
            if (whole && WithinInt(token, out var value))
            {
                return JsonValue.Create(value);
            }

            // With an exponent Lua's tonumber, which float.Parse is in the game, gives a float; for digits alone an integer.
            if (!float.TryParse(whole ? token + "e0" : token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                || !(number >= -float.MaxValue && number <= float.MaxValue))
            {
                throw Error(start, "the number is too large for a float");
            }

            return JsonValue.Create(number);
        }

        // Each digit is taken off a negative sum, so int.MinValue fits and no step passes 2^31.
        private static bool WithinInt(string token, out int value)
        {
            var negative = token[0] == '-';
            var sum = 0;
            for (var i = negative ? 1 : 0; i < token.Length; i++)
            {
                var digit = token[i] - '0';
                if (sum < -214748364 || (sum == -214748364 && digit > 8))
                {
                    value = 0;
                    return false;
                }

                sum = sum * 10 - digit;
            }

            if (!negative && sum == int.MinValue)
            {
                value = 0;
                return false;
            }

            value = negative ? sum : -sum;
            return true;
        }

        private bool Digits()
        {
            var start = at;
            while (at < text.Length && text[at] >= '0' && text[at] <= '9')
            {
                at++;
            }

            return at > start;
        }

        private bool Word(string word)
        {
            if (text.Length - at < word.Length || text.Substring(at, word.Length) != word)
            {
                return false;
            }

            at += word.Length;
            return true;
        }

        private bool Next(char c)
        {
            if (at < text.Length && text[at] == c)
            {
                at++;
                return true;
            }

            return false;
        }

        private void SkipSpace()
        {
            while (at < text.Length && (text[at] == ' ' || text[at] == '\t' || text[at] == '\n' || text[at] == '\r'))
            {
                at++;
            }
        }

        private void Deepen(int depth)
        {
            if (depth > MostDepth)
            {
                throw Error("the JSON nests deeper than " + MostDepth);
            }
        }

        private JsonException Error(string problem)
        {
            return Error(at, problem);
        }

        private static JsonException Error(int position, string problem)
        {
            return new JsonException(problem, position);
        }
    }
}
