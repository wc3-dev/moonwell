using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>
    /// Bytes as letters easy to type (decision 181): Crockford's Base32, digits and capitals without I, L, O and U, five bits
    /// a letter, high bits first, in groups of four. Read back in either case, with O as 0 and I or L as 1, skipping dashes
    /// and spaces. The letters hold no quote or backslash, so they are safe in a Preload file's JASS too.
    /// </summary>
    internal static class CodeLetters
    {
        private const string Letters = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        /// <summary>The letters of the bytes, each 0 to 255, with a dash after every fourth letter.</summary>
        internal static string Encode(IReadOnlyList<int> bytes)
        {
            return Encode(bytes, true);
        }

        /// <summary>The letters of the bytes, each 0 to 255, with or without a dash after every fourth letter.</summary>
        internal static string Encode(IReadOnlyList<int> bytes, bool dashes)
        {
            var text = new StringBuilder();
            var written = 0;
            var held = 0;
            var bits = 0;
            for (var i = 0; i < bytes.Count; i++)
            {
                held = (held << 8) | (bytes[i] & 255);
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    Add(text, ref written, dashes, (held >> bits) & 31);
                    held &= (1 << bits) - 1;
                }
            }

            if (bits > 0)
            {
                Add(text, ref written, dashes, (held << (5 - bits)) & 31);
            }

            return text.ToString();
        }

        /// <summary>The bytes of typed letters; null for a letter the code has not, or letters that end off a byte.</summary>
        internal static List<int>? Decode(string text)
        {
            var bytes = new List<int>();
            var held = 0;
            var bits = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '-' || c == ' ')
                {
                    continue;
                }

                var value = ValueOf(c);
                if (value < 0)
                {
                    return null;
                }

                held = (held << 5) | value;
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    bytes.Add((held >> bits) & 255);
                    held &= (1 << bits) - 1;
                }
            }

            // Encode pads the last letter with zeros and never writes a letter more than the bytes need.
            return bits >= 5 || held != 0 ? null : bytes;
        }

        private static void Add(StringBuilder text, ref int written, bool dashes, int value)
        {
            if (dashes && written > 0 && written % 4 == 0)
            {
                text.Append('-');
            }

            text.Append(Letters[value]);
            written++;
        }

        private static int ValueOf(char c)
        {
            if (c >= 'a' && c <= 'z')
            {
                c = (char)(c - 32);
            }

            if (c == 'O')
            {
                return 0;
            }

            if (c == 'I' || c == 'L')
            {
                return 1;
            }

            return Letters.IndexOf(c);
        }
    }
}
