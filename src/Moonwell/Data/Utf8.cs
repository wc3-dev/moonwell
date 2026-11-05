using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>A text's UTF-8 bytes and back: in the game a string is its UTF-8 bytes already, while .NET's is UTF-16.</summary>
    internal static class Utf8
    {
        // "å" is one character on .NET and its two bytes in the game's Lua.
        private static readonly bool BytesAlready = "å".Length == 2;

        /// <summary>The text's UTF-8 bytes, each 0 to 255.</summary>
        internal static List<int> Encode(string text)
        {
            var bytes = new List<int>();
            if (BytesAlready)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    bytes.Add(text[i] & 255);
                }

                return bytes;
            }

            for (var i = 0; i < text.Length; i++)
            {
                int c = text[i];
                if (c >= 0xD800 && c <= 0xDBFF && i + 1 < text.Length && text[i + 1] >= 0xDC00 && text[i + 1] <= 0xDFFF)
                {
                    c = 0x10000 + ((c - 0xD800) << 10) + (text[i + 1] - 0xDC00);
                    i++;
                }

                AddBytes(bytes, c);
            }

            return bytes;
        }

        /// <summary>Appends a letter by its code point: as .NET's chars, or as its UTF-8 bytes in the game.</summary>
        internal static void Append(StringBuilder text, int letter)
        {
            if (!BytesAlready)
            {
                text.Append(char.ConvertFromUtf32(letter));
                return;
            }

            var bytes = new List<int>();
            AddBytes(bytes, letter);
            for (var i = 0; i < bytes.Count; i++)
            {
                text.Append((char)bytes[i]);
            }
        }

        private static void AddBytes(List<int> bytes, int c)
        {
            if (c < 0x80)
            {
                bytes.Add(c);
            }
            else if (c < 0x800)
            {
                bytes.Add(0xC0 | (c >> 6));
                bytes.Add(0x80 | (c & 63));
            }
            else if (c < 0x10000)
            {
                bytes.Add(0xE0 | (c >> 12));
                bytes.Add(0x80 | ((c >> 6) & 63));
                bytes.Add(0x80 | (c & 63));
            }
            else
            {
                bytes.Add(0xF0 | (c >> 18));
                bytes.Add(0x80 | ((c >> 12) & 63));
                bytes.Add(0x80 | ((c >> 6) & 63));
                bytes.Add(0x80 | (c & 63));
            }
        }

        /// <summary>The text of some UTF-8 bytes; a sequence that is no letter reads as U+FFFD on .NET.</summary>
        internal static string Decode(List<int> bytes, int start, int count)
        {
            var text = new StringBuilder();
            var end = start + count;
            if (BytesAlready)
            {
                for (var i = start; i < end; i++)
                {
                    text.Append((char)bytes[i]);
                }

                return text.ToString();
            }

            var at = start;
            while (at < end)
            {
                var first = bytes[at];
                int c;
                int extra;
                if (first < 0x80)
                {
                    c = first;
                    extra = 0;
                }
                else if (first >= 0xF0)
                {
                    c = first & 7;
                    extra = 3;
                }
                else if (first >= 0xE0)
                {
                    c = first & 15;
                    extra = 2;
                }
                else
                {
                    c = first & 31;
                    extra = 1;
                }

                // As .NET decodes: a byte that cannot start a letter, or a letter cut short or written too long, is one U+FFFD.
                var ok = first < 0x80 || (first >= 0xC2 && first <= 0xF4);
                var k = 1;
                for (; ok && k <= extra && at + k < end; k++)
                {
                    var next = bytes[at + k];
                    var low = k > 1 ? 0x80 : first == 0xE0 ? 0xA0 : first == 0xF0 ? 0x90 : 0x80;
                    var high = k > 1 ? 0xBF : first == 0xED ? 0x9F : first == 0xF4 ? 0x8F : 0xBF;
                    if (next < low || next > high)
                    {
                        break;
                    }

                    c = (c << 6) | (next & 63);
                }

                if (k <= extra)
                {
                    text.Append('\uFFFD');
                    at += k;
                    continue;
                }

                at += extra + 1;
                text.Append(char.ConvertFromUtf32(c));
            }

            return text.ToString();
        }
    }
}
