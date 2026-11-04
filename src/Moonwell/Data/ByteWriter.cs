using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// Writes numbers, bools and text as compact bytes (decision 177), to keep in a file or share as a code: a whole number
    /// takes its sign and 7 bits a byte (one byte from -64 to 63), a float its four bytes, a bool one, a text its length and
    /// its UTF-8 bytes. <see cref="ByteReader"/> reads them back in the same order.
    /// </summary>
    public sealed class ByteWriter
    {
        /// <summary>The bytes written, each 0 to 255.</summary>
        internal List<int> Bytes { get; } = new List<int>();

        /// <summary>How many bytes are written.</summary>
        [LocalSafe]
        public int Count => Bytes.Count;

        /// <summary>Writes a whole number: one byte from -64 to 63, two to ±8192, five at most.</summary>
        public void WriteInt(int value)
        {
            // A negative number's size is -(value + 1), so int.MinValue fits and no step passes 2^31.
            var size = value < 0 ? -(value + 1) : value;
            var first = (value < 0 ? 1 : 0) | ((size & 63) << 1);
            size >>= 6;
            if (size == 0)
            {
                Bytes.Add(first);
                return;
            }

            Bytes.Add(first | 128);
            while (size > 127)
            {
                Bytes.Add((size & 127) | 128);
                size >>= 7;
            }

            Bytes.Add(size);
        }

        /// <summary>Writes a float as its four bytes, as .NET's BitConverter gives them; it reads back exactly (game facts, 11-03).</summary>
        public void WriteFloat(float value)
        {
            var four = BitConverter.GetBytes(value);
            for (var i = 0; i < 4; i++)
            {
                Bytes.Add(four[i]);
            }
        }

        /// <summary>Writes a bool as one byte.</summary>
        public void WriteBool(bool value)
        {
            Bytes.Add(value ? 1 : 0);
        }

        /// <summary>Writes a text as its length and its UTF-8 bytes; null is written as the empty text.</summary>
        public void WriteString(string? text)
        {
            var bytes = Utf8.Encode(text ?? "");
            WriteInt(bytes.Count);
            Bytes.AddRange(bytes);
        }

        /// <summary>The bytes as the save code's letters, easy to type: <c>X7KQ-M3PA-...</c>.</summary>
        public string ToCode()
        {
            return CodeLetters.Encode(Bytes);
        }

        /// <summary>The bytes, for .NET's Base64 or a ByteReader.</summary>
        public byte[] ToArray()
        {
            var array = new byte[Bytes.Count];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = (byte)Bytes[i];
            }

            return array;
        }
    }
}
