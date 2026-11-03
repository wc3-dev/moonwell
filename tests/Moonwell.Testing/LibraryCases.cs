using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for the library members the fork's Lua core lacked or answered wrongly until CoreSystem 2.0.3-moonwell.8
    /// (backlog F2), which MW0030 rejected, and for the forms the compiler 2.0.4-moonwell.16 writes right (F12, F14).
    /// </summary>
    public static class LibraryCases
    {
        /// <summary>Members of Dictionary, List, Queue, Stack, HashSet and Array (F2).</summary>
        public static void Collections(FakeWorld world)
        {
            var gold = new Dictionary<string, int> { ["a"] = 1 };
            Expect.Equal(1, gold.GetValueOrDefault("a"), "GetValueOrDefault");
            Expect.Equal(7, gold.GetValueOrDefault("b", 7), "GetValueOrDefault with a default");
            Expect.True(gold.Remove("a", out var removed) && removed == 1 && gold.Count == 0, "Remove with its value");
            var pairs = new Dictionary<int, string> { [1] = "x" }.ToList();
            Expect.Equal(1, pairs[0].Key, "a Dictionary's pairs keep their Key");
            Expect.Equal("2,4", string.Join(",", new List<int> { 1, 2 }.ConvertAll(i => i * 2)), "ConvertAll");
            Expect.True(!new Queue<int>().TryPeek(out var none) && none == 0, "TryPeek of an empty Queue");
            var stack = new Stack<int>();
            stack.Push(3);
            Expect.True(stack.TryPop(out var top) && top == 3 && stack.Count == 0, "TryPop");
            Expect.True(new HashSet<int> { 1, 2 }.SetEquals(new[] { 2, 1 }), "SetEquals");
            var keys = new[] { 2, 1 };
            var items = new[] { "b", "a" };
            Array.Sort(keys, items);
            Expect.Equal("a,b", string.Join(",", items), "Sort of keys and items");
        }

        /// <summary>LINQ members the core lacked, and a Take that runs twice (F2).</summary>
        public static void Linq(FakeWorld world)
        {
            var numbers = new List<int> { 1, 2, 3, 4 };
            Expect.Equal("0,1,2,3,4,5", string.Join(",", numbers.Prepend(0).Append(5)), "Prepend and Append");
            Expect.Equal("3,4 1,2", string.Join(",", numbers.TakeLast(2)) + " " + string.Join(",", numbers.SkipLast(2)), "TakeLast and SkipLast");
            Expect.Equal(2, numbers.Chunk(3).Count(), "Chunk");
            Expect.Equal("1a 2b", string.Join(" ", numbers.Zip(new[] { "a", "b" }).Select(p => p.First + p.Second)), "Zip into tuples");
            Expect.Equal("1,2", string.Join(",", numbers.DistinctBy(i => i % 2)), "DistinctBy");
            Expect.Equal("1=2 0=2", string.Join(" ", numbers.CountBy(i => i % 2).Select(p => p.Key + "=" + p.Value)), "CountBy");
            Expect.Equal(2, numbers.ToLookup(i => i % 2)[0].Count(), "ToLookup");
            Expect.Equal("9", string.Join(",", new List<int>().DefaultIfEmpty(9)), "DefaultIfEmpty with a value");
            var first = numbers.Take(2);
            Expect.Equal(4, first.Count() + first.Count(), "Take enumerated twice");
        }

        /// <summary>StringBuilder, char and string members the core lacked, and a % given to Replace (F2).</summary>
        public static void Text(FakeWorld world)
        {
            var sb = new StringBuilder("banana");
            sb.Replace("an", "AN").Insert(0, '>').Remove(1, 1);
            Expect.Equal(">ANANa", sb.ToString(), "Replace, Insert of a char and Remove");
            Expect.Equal("AN", sb.ToString(1, 2), "ToString of a range");
            Expect.Equal("a, b", new StringBuilder().AppendJoin(", ", "a", "b").ToString(), "AppendJoin");
            Expect.True(char.IsAsciiDigit('7') && !char.IsAscii((char)233) && char.IsBetween('m', 'a', 'z'), "char's ASCII helpers");
            Expect.Equal("a%b", "a-b".Replace("-", "%"), "Replace with a %");
            Expect.Equal("a|b", "a\r\nb".ReplaceLineEndings("|"), "ReplaceLineEndings");
            Expect.Equal(2, "hé".EnumerateRunes().Count(), "EnumerateRunes");
        }

        /// <summary>Number formats as .NET writes them (decision 120), and the number members the core lacked (F2).</summary>
        public static void Numbers(FakeWorld world)
        {
            Expect.Equal("1,234,567", 1234567.ToString("N0"), "N0");
            Expect.Equal("-005", (-5).ToString("D3"), "D3 of a negative");
            Expect.Equal("1.5000000000", 1.5f.ToString("F10"), "F10");
            Expect.Equal("1,235", 1234.5f.ToString("#,##0"), "a custom format");
            Expect.Equal("07", 7.ToString("00"), "a custom format of an int");
            Expect.Equal("2.50E+001", 25.ToString("E2"), "E2");
            Expect.Equal("3 -2", Math.DivRem(17, 5, out var rest) + " " + Math.DivRem(-17, 5).Remainder, "DivRem toward zero");
            Expect.Equal(int.MinValue, int.RotateLeft(1, 31), "RotateLeft");
            Expect.Equal(8, int.PopCount(255), "PopCount");
            Expect.Equal("000AFF", Convert.ToHexString(new byte[] { 0, 10, 255 }), "ToHexString");
            Expect.True(HashCode.Combine(1, 2) == HashCode.Combine(1, 2), "HashCode.Combine");
        }

        /// <summary>StringComparer, BitArray and Span members the core lacked (F2).</summary>
        public static void Types(FakeWorld world)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Footman"] = 1 };
            Expect.Equal(1, counts["FOOTMAN"], "a Dictionary with OrdinalIgnoreCase");
            var bits = new BitArray(4);
            bits[2] = true;
            Expect.True(bits.Get(2) && !bits[1] && bits.HasAnySet(), "BitArray");
            var items = new[] { 1, 2, 3 };
            items.AsSpan(1).Fill(9);
            Expect.Equal("1,9,9", string.Join(",", items), "Span.Fill");
        }

        /// <summary>A local of one switch section used in another, an out struct from a Dictionary, and an out call in a conditional's branch (F12, F14).</summary>
        public static void Forms(FakeWorld world)
        {
            Expect.Equal(14, SectionLocal(1), "a local of another switch section");
            var spots = new Dictionary<int, Spot> { [1] = new Spot() };
            spots.TryGetValue(1, out var spot);
            spot.X = 9;
            Expect.Equal(0, spots[1].X, "an out struct is a copy");
            string? key = null;
            var found = new Dictionary<string, int> { ["a"] = 1 };
            Expect.Equal(5, key == null ? 5 : found.TryGetValue(key, out var value) ? value : 7, "an out call runs only in its branch");
        }

        private static int SectionLocal(int k)
        {
            switch (k)
            {
                case 0:
                    int y = 5;
                    return y;
                default:
                    y = 7;
                    return y * 2;
            }
        }

        private struct Spot
        {
            public int X;
        }
    }
}
