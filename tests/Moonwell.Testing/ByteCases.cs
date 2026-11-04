using System;

namespace Moonwell.Testing
{
    /// <summary>Cases for ByteWriter and ByteReader: compact numbers, floats as their four bytes, strings as UTF-8 (decision 177).</summary>
    public static class ByteCases
    {
        /// <summary>A whole number takes its sign and 7 bits a byte: 63 and -64 one byte, 64 and -65 two, the ends of int five.</summary>
        public static void IntSizes(FakeWorld world)
        {
            Expect.Equal(1, SizeOf(0), "0");
            Expect.Equal(1, SizeOf(63), "63");
            Expect.Equal(1, SizeOf(-64), "-64");
            Expect.Equal(2, SizeOf(64), "64");
            Expect.Equal(2, SizeOf(-65), "-65");
            Expect.Equal(5, SizeOf(int.MaxValue), "int.MaxValue");
            Expect.Equal(5, SizeOf(int.MinValue), "int.MinValue");
        }

        /// <summary>Whole numbers, floats, bools and strings come back as written, in order.</summary>
        public static void RoundTrip(FakeWorld world)
        {
            var ints = new[] { 0, 1, -1, 63, 64, -64, -65, 1000000, -1000000, 1093677104, int.MaxValue, int.MinValue };
            // Floats a single holds exactly: the cases' desktop Lua has doubles, where 0.1f is not one (the game's are singles,
            // and 0.1f came back the same there, game facts 11-03); 0.1f is checked as near below.
            var floats = new[] { 0.0f, -0.5f, 1.5f, 0.25f, 1048576.0f, -3.75f };
            var writer = new ByteWriter();
            for (var i = 0; i < ints.Length; i++)
            {
                writer.WriteInt(ints[i]);
            }

            for (var i = 0; i < floats.Length; i++)
            {
                writer.WriteFloat(floats[i]);
            }

            writer.WriteFloat(0.1f);
            writer.WriteBool(true);
            writer.WriteBool(false);
            writer.WriteString("Åsa ö é € 中 😀 \"q\" \\ |n");
            writer.WriteString("");
            writer.WriteString(null);

            var reader = new ByteReader(writer.ToArray());
            for (var i = 0; i < ints.Length; i++)
            {
                Expect.Equal(ints[i], reader.ReadInt(), "int " + ints[i]);
            }

            for (var i = 0; i < floats.Length; i++)
            {
                Expect.True(reader.ReadFloat() == floats[i], "float " + i);
            }

            Expect.Near(0.1f, reader.ReadFloat(), "0.1f");
            Expect.True(reader.ReadBool(), "true");
            Expect.True(!reader.ReadBool(), "false");
            Expect.Equal("Åsa ö é € 中 😀 \"q\" \\ |n", reader.ReadString(), "letters of every length");
            Expect.Equal("", reader.ReadString(), "the empty text");
            Expect.Equal("", reader.ReadString(), "null reads as the empty text");
            Expect.Equal(0, reader.Remaining, "nothing left");
        }

        /// <summary>A string is its length, then its UTF-8 bytes; a float its four bytes, as .NET's BitConverter writes them.</summary>
        public static void Layout(FakeWorld world)
        {
            var text = new ByteWriter();
            text.WriteString("å");
            var bytes = text.ToArray();
            Expect.Equal(3, bytes.Length, "å: a length and two bytes");
            Expect.Equal(4, bytes[0], "the length 2, its sign bit 0");
            Expect.Equal(0xC3, bytes[1], "å's first byte");
            Expect.Equal(0xA5, bytes[2], "å's second byte");

            var number = new ByteWriter();
            number.WriteFloat(1.5f);
            var four = number.ToArray();
            Expect.Equal(4, four.Length, "a float's four bytes");
            Expect.Equal(192, four[2], "1.5f's third byte");
            Expect.Equal(63, four[3], "1.5f's fourth byte");
        }

        /// <summary>The bytes as the code's letters, and back.</summary>
        public static void Code(FakeWorld world)
        {
            var writer = new ByteWriter();
            writer.WriteInt(-1234);
            writer.WriteString("gold");
            var reader = ByteReader.FromCode(writer.ToCode());
            Expect.True(reader != null, "the code reads");
            Expect.Equal(-1234, reader!.ReadInt(), "the number");
            Expect.Equal("gold", reader.ReadString(), "the text");
            Expect.True(ByteReader.FromCode("not a code!") == null, "a bad code gives null");
        }

        /// <summary>A read past the end throws FormatException.</summary>
        public static void PastTheEnd(FakeWorld world)
        {
            Expect.Throws<FormatException>(() => new ByteReader(new byte[0]).ReadInt(), "an int from nothing");
            var writer = new ByteWriter();
            writer.WriteInt(5);
            Expect.Throws<FormatException>(() => new ByteReader(writer.ToArray()).ReadString(), "a string longer than the bytes");
        }

        private static int SizeOf(int value)
        {
            var writer = new ByteWriter();
            writer.WriteInt(value);
            return writer.Count;
        }
    }
}
