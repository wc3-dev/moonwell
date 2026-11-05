using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>
    /// A player's file as Preload writes it and Preloader reads it back (game facts, 11-03): the text escaped, cut into pieces,
    /// and each piece a line that puts it in a tooltip of Moonwell's store ability, 100 levels of tooltips and 100 of extended
    /// ones. Preload writes a quote as it is, which ends the JASS string, so the text's quotes, tildes and bytes below 32 are
    /// escaped; a backslash is written doubled and reads back as one.
    /// </summary>
    internal static class FileText
    {
        /// <summary>The store ability's id: Moonwell's own, declared by the build in a map that uses files.</summary>
        internal const string StoreId = "AMWf";

        /// <summary>The longest piece, in bytes: with its line's wrapping it keeps within the 259 bytes a Preload call keeps.</summary>
        internal const int PieceBytes = 200;

        /// <summary>How many pieces a file holds: a tooltip and an extended tooltip on each of the store's 100 levels.</summary>
        internal const int MostPieces = 200;

        private const string Hex = "0123456789ABCDEF";

        /// <summary>
        /// The text with <c>~~</c> for a tilde, <c>~q</c> for a quote and <c>~</c> and two hex digits for a byte below 32 and a
        /// backslash, which the game writes doubled, so that a piece's bytes are the bytes Preload keeps.
        /// </summary>
        internal static string Escape(string text)
        {
            var escaped = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '~')
                {
                    escaped.Append("~~");
                }
                else if (c == '"')
                {
                    escaped.Append("~q");
                }
                else if (c < 32 || c == '\\')
                {
                    escaped.Append('~').Append(Hex[c >> 4]).Append(Hex[c & 15]);
                }
                else
                {
                    escaped.Append(c);
                }
            }

            return escaped.ToString();
        }

        /// <summary>The text an escaped text stands for; an escape it does not know is kept as it is.</summary>
        internal static string Unescape(string text)
        {
            var plain = new StringBuilder();
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '~' && i + 1 < text.Length)
                {
                    var next = text[i + 1];
                    if (next == '~' || next == 'q')
                    {
                        plain.Append(next == '~' ? '~' : '"');
                        i += 2;
                        continue;
                    }

                    if (i + 2 < text.Length && Hex.IndexOf(next) >= 0 && Hex.IndexOf(text[i + 2]) >= 0)
                    {
                        plain.Append((char)(Hex.IndexOf(next) * 16 + Hex.IndexOf(text[i + 2])));
                        i += 3;
                        continue;
                    }
                }

                plain.Append(c);
                i++;
            }

            return plain.ToString();
        }

        /// <summary>The escaped text in pieces of at most <see cref="PieceBytes"/>, none ending inside a letter; the empty text is one empty piece.</summary>
        internal static List<string> Pieces(string escaped)
        {
            var pieces = new List<string>();
            var start = 0;
            while (escaped.Length - start > PieceBytes)
            {
                var end = start + PieceBytes;
                while (end > start && IsInsideLetter(escaped[end]))
                {
                    end--;
                }

                // No letter starts in the whole piece, which only text that is no UTF-8 gives.
                if (end == start)
                {
                    end = start + PieceBytes;
                }

                pieces.Add(escaped.Substring(start, end - start));
                start = end;
            }

            pieces.Add(escaped.Substring(start));
            return pieces;
        }

        /// <summary>The Preload text of a piece: it closes Preload's own call and adds one that puts the piece in its tooltip.</summary>
        internal static string Line(int index, string piece)
        {
            var call = index < 100 ? "BlzSetAbilityTooltip" : "BlzSetAbilityExtendedTooltip";
            return "\")\ncall " + call + "('" + StoreId + "', \"" + piece + "\", " + (index % 100) + ")\n//";
        }

        // In the game a string is its UTF-8 bytes, and a byte 10xxxxxx continues a letter; on .NET a low surrogate continues one.
        private static bool IsInsideLetter(char c)
        {
            return (c & 0xC0) == 0x80 || (c >= 0xDC00 && c <= 0xDFFF);
        }
    }
}
