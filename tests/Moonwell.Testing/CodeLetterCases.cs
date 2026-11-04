using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for the code's letters: bytes as Crockford's Base32 in groups of four, easy to type (decision 181).</summary>
    public static class CodeLetterCases
    {
        /// <summary>No bytes, one byte, and five bytes in two groups.</summary>
        public static void Encode(FakeWorld world)
        {
            Expect.Equal("", CodeLetters.Encode(new List<int>()), "no bytes");
            Expect.Equal("00", CodeLetters.Encode(Bytes(0)), "a zero byte");
            Expect.Equal("ZW", CodeLetters.Encode(Bytes(255)), "255: 11111, then 111 and two zero bits");
            Expect.Equal("0410-6105", CodeLetters.Encode(Bytes(1, 2, 3, 4, 5)), "five bytes, a dash after four letters");
            Expect.Equal("04106105", CodeLetters.Encode(Bytes(1, 2, 3, 4, 5), false), "without dashes");
        }

        /// <summary>A typed code reads the same in lower case, with spaces, without dashes, with O for 0 and I or L for 1.</summary>
        public static void DecodeTolerant(FakeWorld world)
        {
            ExpectBytes(Bytes(255), CodeLetters.Decode("zw"), "lower case");
            ExpectBytes(Bytes(1, 2, 3, 4, 5), CodeLetters.Decode("O41O 61O5"), "O for 0, and a space");
            ExpectBytes(Bytes(1, 2, 3, 4, 5), CodeLetters.Decode("04106105"), "no dashes");
            ExpectBytes(Bytes(0, 66), CodeLetters.Decode("0IL0"), "I and L for 1, as 0110");
        }

        /// <summary>A letter that is not one, padding bits that are not zero, and a letter too many give null.</summary>
        public static void DecodeBad(FakeWorld world)
        {
            Expect.True(CodeLetters.Decode("0410-61U5") == null, "U is no letter of the code");
            Expect.True(CodeLetters.Decode("0410-61!5") == null, "nor is !");
            Expect.True(CodeLetters.Decode("ZX") == null, "padding bits not zero");
            Expect.True(CodeLetters.Decode("ZW0") == null, "a letter past the last byte");
            ExpectBytes(new List<int>(), CodeLetters.Decode(""), "no letters, no bytes");
        }

        /// <summary>300 bytes from a seeded Random come back as they went.</summary>
        public static void RoundTrip(FakeWorld world)
        {
            var random = new Random(7);
            var bytes = new List<int>();
            for (var i = 0; i < 300; i++)
            {
                bytes.Add(random.Next(0, 256));
            }

            ExpectBytes(bytes, CodeLetters.Decode(CodeLetters.Encode(bytes)), "with dashes");
            ExpectBytes(bytes, CodeLetters.Decode(CodeLetters.Encode(bytes, false)), "without");
        }

        private static List<int> Bytes(params int[] values)
        {
            return new List<int>(values);
        }

        private static void ExpectBytes(List<int> expected, List<int>? actual, string what)
        {
            Expect.True(actual != null, what + ": read");
            Expect.Equal(expected.Count, actual!.Count, what + ": the count");
            for (var i = 0; i < expected.Count; i++)
            {
                Expect.Equal(expected[i], actual[i], what + ": byte " + i);
            }
        }
    }
}
