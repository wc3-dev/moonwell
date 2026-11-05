using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>Reads back what a <see cref="ByteWriter"/> wrote, in the same order; a read past the end throws FormatException.</summary>
    public sealed class ByteReader
    {
        private readonly List<int> bytes;
        private readonly int end;
        private int at;

        /// <summary>Reads these bytes.</summary>
        public ByteReader(byte[] bytes)
        {
            this.bytes = new List<int>();
            for (var i = 0; i < bytes.Length; i++)
            {
                this.bytes.Add(bytes[i]);
            }

            end = this.bytes.Count;
        }

        /// <summary>Reads a stretch of bytes, each 0 to 255, from the start up to the end.</summary>
        internal ByteReader(List<int> bytes, int start, int end)
        {
            this.bytes = bytes;
            at = start;
            this.end = end;
        }

        /// <summary>How many bytes are left to read.</summary>
        [LocalSafe]
        public int Remaining => end - at;

        /// <summary>Reads the bytes of a code's letters; null when the text is no code.</summary>
        public static ByteReader? FromCode(string text)
        {
            var bytes = CodeLetters.Decode(text);
            return bytes == null ? null : new ByteReader(bytes, 0, bytes.Count);
        }

        /// <summary>Reads a whole number.</summary>
        public int ReadInt()
        {
            var read = Next();
            var negative = (read & 1) == 1;
            var size = (read >> 1) & 63;
            var shift = 6;
            while ((read & 128) != 0)
            {
                if (shift > 27)
                {
                    throw new FormatException("ByteReader: a whole number longer than five bytes");
                }

                read = Next();
                // The fifth byte holds the last 4 of the 31 bits.
                size |= (shift == 27 ? read & 15 : read & 127) << shift;
                shift += 7;
            }

            return negative ? -size - 1 : size;
        }

        /// <summary>Reads a float.</summary>
        public float ReadFloat()
        {
            var four = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                four[i] = (byte)Next();
            }

            return BitConverter.ToSingle(four, 0);
        }

        /// <summary>Reads a bool.</summary>
        public bool ReadBool()
        {
            return Next() != 0;
        }

        /// <summary>Reads a text.</summary>
        public string ReadString()
        {
            var length = ReadInt();
            if (length < 0 || length > Remaining)
            {
                throw new FormatException("ByteReader: a text of " + length + " bytes with " + Remaining + " left");
            }

            var text = Utf8.Decode(bytes, at, length);
            at += length;
            return text;
        }

        /// <summary>Reads one byte, 0 to 255.</summary>
        internal int ReadByte()
        {
            return Next();
        }

        /// <summary>A reader of the next bytes, which this reader then passes over.</summary>
        internal ByteReader Take(int count)
        {
            if (count < 0 || count > Remaining)
            {
                throw new FormatException("ByteReader: a stretch of " + count + " bytes with " + Remaining + " left");
            }

            var taken = new ByteReader(bytes, at, at + count);
            at += count;
            return taken;
        }

        private int Next()
        {
            if (at >= end)
            {
                throw new FormatException("ByteReader: a read past the end of the bytes");
            }

            return bytes[at++];
        }
    }
}
