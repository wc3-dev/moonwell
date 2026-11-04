using System.Text;

namespace Moonwell.Json
{
    /// <summary>Writes a tree as compact JSON: a string's quote, backslash and control characters escaped, its other letters as they are.</summary>
    internal static class JsonText
    {
        private const string Hex = "0123456789ABCDEF";

        internal static void Write(JsonNode? node, StringBuilder json)
        {
            if (node == null)
            {
                json.Append("null");
            }
            else if (node is JsonObject values)
            {
                json.Append('{');
                var first = true;
                foreach (var pair in values)
                {
                    json.Append(first ? "" : ",");
                    first = false;
                    WriteString(pair.Key, json);
                    json.Append(':');
                    Write(pair.Value, json);
                }

                json.Append('}');
            }
            else if (node is JsonArray items)
            {
                json.Append('[');
                var first = true;
                foreach (var item in items)
                {
                    json.Append(first ? "" : ",");
                    first = false;
                    Write(item, json);
                }

                json.Append(']');
            }
            else
            {
                node.AsValue().WriteTo(json);
            }
        }

        internal static void WriteString(string text, StringBuilder json)
        {
            json.Append('"');
            var from = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c != '"' && c != '\\' && c >= ' ')
                {
                    continue;
                }

                json.Append(text.Substring(from, i - from));
                from = i + 1;
                switch (c)
                {
                    case '"':
                        json.Append("\\\"");
                        break;
                    case '\\':
                        json.Append("\\\\");
                        break;
                    case '\b':
                        json.Append("\\b");
                        break;
                    case '\f':
                        json.Append("\\f");
                        break;
                    case '\n':
                        json.Append("\\n");
                        break;
                    case '\r':
                        json.Append("\\r");
                        break;
                    case '\t':
                        json.Append("\\t");
                        break;
                    default:
                        json.Append("\\u00").Append(Hex[c >> 4]).Append(Hex[c & 15]);
                        break;
                }
            }

            json.Append(text.Substring(from, text.Length - from));
            json.Append('"');
        }
    }
}
