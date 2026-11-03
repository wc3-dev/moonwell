using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for the C# the fork's CSharp.lua wrote wrong until 2.0.4-moonwell.15 (backlog F1, F3, F5, F10), whose analyzer
    /// rules are gone or narrowed with it: patterns (MW0017, MW0018), a struct read from a collection (MW0020), tuples
    /// (MW0022), user-defined operators, derived records and with on an anonymous type (MW0023), ranges and indexes
    /// (MW0024), ref locals of elements (MW0025), params collections (MW0026), string calls (MW0027), a char or an enum
    /// given as an object (MW0028), catches (MW0029), checked (MW0031) and enums as text (MW0032); and the forms no rule
    /// could see: small integers that wrap, a method group in a conditional, a long constant string local, a local function
    /// of another switch section, break and continue after a using declaration, a private indexer and LINQ that stops
    /// early.
    /// </summary>
    public static class FormCases
    {
        /// <summary>Positional patterns on a value that can be null, inside another pattern and with names; and switch arms of every kind (MW0017, MW0018).</summary>
        public static void Patterns(FakeWorld world)
        {
            Expect.Equal("one-two", IsOneTwo(new Pair { X = 1, Y = 2 }), "a class");
            Expect.Equal("not", IsOneTwo(null), "a class, null");
            Expect.Equal("y 5", RecordY(new Point(0, 5)), "a record");
            Expect.Equal("not", RecordY(null), "a record, null");
            Expect.Equal("pair", ObjectPair(new Pair { X = 3, Y = 4 }), "object");
            Expect.Equal("not", ObjectPair("x"), "object, another type");
            Expect.Equal("nested", Nested(new Holder { Pair = new Pair { X = 5, Y = 6 } }), "inside a property pattern");
            Expect.Equal("not", Nested(new Holder()), "inside a property pattern, null");
            (int A, int B) named = (1, 2);
            Expect.True(named is (A: 1, B: 2), "with names on a tuple");
            Expect.True(new Point(3, 9) is (X: 3, Y: _), "with names on a record");
            Expect.Equal("null", ArmOnNull(null), "a positional arm given null");

            Expect.Equal("big", Size(20), "a relational arm");
            Expect.Equal("negative", Size(-1), "a relational arm, the second");
            Expect.Equal("zero", Size(0), "a parenthesized arm");
            Expect.Equal("other", Size(3), "a not arm");
            Expect.Equal("var 7", VarArm(7), "a var arm");
            Expect.True(new Holder { Count = 4 } is { Count: var count } && count == 4, "var in a property pattern");
            Expect.True(new Holder { Count = 4 } is { Count: int typed } && typed == 4, "a name in a property pattern");
            var numbers = new[] { 1, 2, 3 };
            Expect.True(numbers is [var first, .. var rest] && first == 1 && rest.Length == 2, "a list pattern's slice with a name");
        }

        /// <summary>A struct read from an array, a List or a Dictionary is a copy (MW0020).</summary>
        public static void StructCopies(FakeWorld world)
        {
            var spots = new Spot[1];
            var fromArray = spots[0];
            fromArray.X = 6;
            Expect.Equal(0, spots[0].X, "an array's element");
            var list = new List<Spot> { new Spot() };
            var fromList = list[0];
            fromList.X = 6;
            Expect.Equal(0, list[0].X, "a List's element");
            var map = new Dictionary<string, Spot> { ["a"] = new Spot() };
            var fromMap = map["a"];
            fromMap.X = 6;
            Expect.Equal(0, map["a"].X, "a Dictionary's value");
        }

        /// <summary>A deconstruction into new names, elements as targets, and a tuple as text (MW0022).</summary>
        public static void Tuples(FakeWorld world)
        {
            var (a, b) = (1, 2);
            Expect.Equal(1, a, "var (a, b) = (1, 2), a");
            Expect.Equal(2, b, "var (a, b) = (1, 2), b");
            var swapped = new[] { 1, 2 };
            (swapped[0], swapped[1]) = (swapped[1], swapped[0]);
            Expect.Equal(2, swapped[0], "elements as targets");
            var tuple = (1, "x");
            Expect.Equal("(1, x)", tuple.ToString(), "ToString");
            Expect.Equal("t=(1, x)", "t=" + tuple, "joined");
            Expect.Equal("(1, x)", $"{tuple}", "interpolated");
        }

        /// <summary>A user-defined ++, --, true and false, a record derived from a record, and with on an anonymous type (MW0023).</summary>
        public static void Declarations(FakeWorld world)
        {
            var counter = new Counter { Value = 1 };
            counter++;
            ++counter;
            counter--;
            Expect.Equal(2, counter.Value, "++ and --");
            var zero = new Counter();
            Expect.Equal("f", zero ? "t" : "f", "operator true");
            Expect.Equal(0, (zero && counter).Value, "&& stops at false");
            Expect.Equal(2, (zero || counter).Value, "|| goes on at false");
            var steps = 0;
            for (var left = new Counter { Value = 3 }; left; left--)
            {
                steps++;
            }

            Expect.Equal(3, steps, "a loop on operator true");
            Expect.True(new Derived(1, "q") == new Derived(1, "q"), "a derived record's ==");
            Expect.True(!new Derived(1, "q").Equals(new Base(1)), "Equals of another record type");
            Expect.Equal("Derived { A = 1, B = q }", new Derived(1, "q").ToString(), "a derived record's text");
            var anonymous = new { X = 1, Y = "a" };
            var changed = anonymous with { X = 2 };
            Expect.Equal(2, changed.X, "with on an anonymous type");
            Expect.Equal("a", changed.Y, "with keeps the others");
            Expect.Equal(1, anonymous.X, "with leaves the first");
        }

        /// <summary>Ranges, indexes from the end, and the types Range and Index (MW0024, MW0030).</summary>
        public static void Ranges(FakeWorld world)
        {
            var numbers = new[] { 1, 2, 3, 4 };
            var middle = numbers[1..3];
            Expect.Equal(2, middle.Length, "an array's range");
            Expect.Equal(2, middle[0], "an array's range, its first");
            Expect.Equal("el", "hello"[1..3], "a string's range");
            var list = new List<int> { 5, 6, 7 };
            Expect.Equal(7, list[^1], "a List's ^1");
            Expect.Equal(2, list[1..].Count, "a List's range");
            Expect.Equal('o', "hello"[^1], "a string's ^1");
            Index last = ^1;
            Expect.Equal(4, numbers[last], "an Index");
            Expect.Equal(1, last.Value, "Index.Value");
            Range tail = 2..;
            Expect.Equal(2, numbers[tail].Length, "a Range");
        }

        /// <summary>A ref local of an array's element and of a Span's (MW0025).</summary>
        public static void RefElements(FakeWorld world)
        {
            var numbers = new[] { 1, 2, 3 };
            ref int second = ref numbers[1];
            second = 5;
            second += 1;
            second++;
            Expect.Equal(7, numbers[1], "an array's element");
            var index = 2;
            ref int third = ref numbers[index];
            index = 0;
            third = 9;
            Expect.Equal(9, numbers[2], "the index taken where the ref is made");
            Span<int> span = numbers;
            ref int head = ref span[0];
            head = 4;
            Expect.Equal(4, numbers[0], "a Span's element");
            Expect.Throws<IndexOutOfRangeException>(() =>
            {
                ref int outside = ref numbers[5];
            }, "an index out of range throws where the ref is made");
        }

        /// <summary>params of a collection type, and of a lambda (MW0026).</summary>
        public static void Params(FakeWorld world)
        {
            Expect.Equal(6, Sum(1, 2, 3), "params List<int>");
            Expect.Equal(0, Sum(), "params List<int>, none");
            var count = (params int[] items) => items.Length;
            Expect.Equal(3, count(1, 2, 3), "params in a lambda");
        }

        /// <summary>Split on strings, StartsWith and EndsWith with a char, ignore-case comparisons and LINQ over a string (MW0027).</summary>
        public static void Strings(FakeWorld world)
        {
            Expect.Equal("a|b|c", string.Join("|", "a--b--c".Split("--")), "Split(string)");
            Expect.Equal("a|b,|c", string.Join("|", "a, b,, c".Split(new[] { ", " }, StringSplitOptions.None)), "Split(string[], options)");
            Expect.True("abc".StartsWith('a') && !"abc".StartsWith('b'), "StartsWith(char)");
            Expect.True("abc".EndsWith('c') && !"".EndsWith('c'), "EndsWith(char)");
            Expect.Equal(2, "Hello".IndexOf("LL", StringComparison.OrdinalIgnoreCase), "IndexOf, ignoring case");
            Expect.True("Hello".StartsWith("he", StringComparison.OrdinalIgnoreCase), "StartsWith, ignoring case");
            Expect.True("Hello".EndsWith("LO", StringComparison.OrdinalIgnoreCase), "EndsWith, ignoring case");
            Expect.Equal("cba", new string("abc".Reverse().ToArray()), "Reverse() on a string");
        }

        /// <summary>A char or an enum given as an object to a method that writes text (MW0028).</summary>
        public static void BoxedText(FakeWorld world)
        {
            Expect.Equal("z Blue", string.Format("{0} {1}", 'z', Shade.Blue), "string.Format");
            Expect.Equal("zBlue", string.Concat('z', Shade.Blue), "string.Concat");
            Expect.Equal("z,Blue,3", string.Join(",", 'z', Shade.Blue, 3), "string.Join");
            Expect.Equal("z Blue", new StringBuilder().AppendFormat("{0} {1}", 'z', Shade.Blue).ToString(), "StringBuilder.AppendFormat");
            Expect.Equal("Blue", new StringBuilder().Append((object)Shade.Blue).ToString(), "StringBuilder.Append(object)");
        }

        /// <summary>Each Lua error caught by its .NET type, and a catch filter's pattern variable (MW0029).</summary>
        public static void Catches(FakeWorld world)
        {
            string? none = null;
            Expect.Throws<NullReferenceException>(() => _ = none!.Length, "the length of null");
            Action? nothing = null;
            Expect.Throws<NullReferenceException>(() => nothing!(), "a call of null");
            var numbers = new int[1];
            Expect.Throws<IndexOutOfRangeException>(() => _ = numbers[3], "an array's index");
            var zero = 0;
            Expect.Throws<DivideByZeroException>(() => _ = 5 % zero, "% by zero");
            string message = "";
            try
            {
                throw new InvalidOperationException("filtered");
            }
            catch (Exception e) when (e is InvalidOperationException invalid)
            {
                message = invalid.Message;
            }

            Expect.Equal("filtered", message, "a catch filter's pattern variable");
        }

        /// <summary>checked int arithmetic throws on an overflow (MW0031).</summary>
        public static void Checked(FakeWorld world)
        {
            var big = int.MaxValue;
            Expect.Throws<OverflowException>(() => _ = checked(big + 1), "checked +");
            var small = int.MinValue;
            Expect.Throws<OverflowException>(() => _ = checked(small - 1), "checked -");
            Expect.Throws<OverflowException>(() =>
            {
                checked
                {
                    big *= 2;
                }
            }, "a checked block's *=");
            Expect.Equal(int.MaxValue - 1, checked(big - 1), "checked, no overflow");
        }

        /// <summary>An enum as text: a [Flags] combination, the order of values and names, formats, a constant joined to text, and Parse (MW0032).</summary>
        public static void EnumText(FakeWorld world)
        {
            Expect.Equal("Read, Write", (Access.Read | Access.Write).ToString(), "a [Flags] combination");
            Expect.Equal("None", Access.None.ToString(), "a [Flags] zero");
            Expect.Equal("Read, Write", $"{Access.Read | Access.Write}", "a [Flags] combination interpolated");
            Expect.Equal("Red,Green,Blue", string.Join(",", Enum.GetNames<Shade>()), "GetNames in order");
            Expect.Equal(3, Enum.GetValues<Shade>().Length, "GetValues");
            Expect.True(Enum.GetValues<Shade>()[0] == Shade.Red && Enum.GetValues<Shade>()[2] == Shade.Blue, "GetValues in order");
            Expect.Equal("2", Shade.Blue.ToString("D"), "ToString(\"D\")");
            Expect.Equal("00000002", Shade.Blue.ToString("X"), "ToString(\"X\")");
            Expect.Equal("Green x", (Shade)1 + " x", "a constant joined to text");
            Expect.True(Enum.Parse<Shade>("Blue") == Shade.Blue, "Parse<T>");
            Expect.True(Enum.Parse<Shade>("2") == Shade.Blue, "Parse<T> of a number");
            Expect.Equal("  Red", $"{Shade.Red,5}", "an interpolation's width");
        }

        /// <summary>
        /// A cast to a handle type, also the one a foreach makes, which the fork writes with no check (MW0035). The fake world's
        /// handles are CSharp.lua objects, so a check would pass here too: LuaRuntimeTests looks for it in the compiled script.
        /// </summary>
        public static void HandleCasts(FakeWorld world)
        {
            widget held = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            var back = (unit)held;
            Expect.Same(held, back, "a cast to a handle type");
            var widgets = new List<widget> { held };
            var count = 0;
            foreach (unit each in widgets)
            {
                count++;
            }

            Expect.Equal(1, count, "a foreach's cast");
        }

        /// <summary>
        /// The forms no rule could see: small integers that wrap, a method group in a conditional, a long constant string local, a
        /// local function of another switch section, break and continue after a using declaration, a private indexer and LINQ that
        /// stops early.
        /// </summary>
        public static void Silent(FakeWorld world)
        {
            byte b = 255;
            b++;
            Expect.Equal(0, b, "byte ++");
            short s = 32767;
            s += 1;
            Expect.Equal(-32768, s, "short +=");
            ushort u = 0;
            u--;
            Expect.Equal(65535, u, "ushort --");
            sbyte sb = -128;
            sb--;
            Expect.Equal(127, sb, "sbyte --");

            Func<int, int>? twice = b == 0 ? Doubler.Twice : null;
            Expect.Equal(8, twice!(4), "a method group in a conditional");

            const string Long = Prefix + " that is longer than twenty-seven characters";
            Expect.Equal("A constant that is longer than twenty-seven characters", Long, "a long constant string local");

            Expect.Equal(2, Section(1), "a local function of another switch section");
            Expect.Equal(5, Section(2), "a local function of its own switch section");

            var log = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                using var scope = new Logged(log, i);
                if (i == 1)
                {
                    continue;
                }

                if (i == 3)
                {
                    break;
                }

                log.Add("body " + i);
            }

            Expect.Equal("body 0,end 0,end 1,body 2,end 2,end 3", string.Join(",", log), "break and continue after a using declaration");

            Expect.Equal(9, new Grid().Read(3), "a private indexer");

            var disposed = new List<string>();
            Expect.Equal(1, Numbers(disposed).First(), "LINQ that stops early");
            Expect.Equal(1, disposed.Count, "LINQ that stops early disposes the iterator");
        }

        private const string Prefix = "A constant";

        private static string IsOneTwo(Pair? p) => p is (1, 2) ? "one-two" : "not";

        private static string RecordY(Point? p) => p is (0, var y) ? "y " + y : "not";

        private static string ObjectPair(object o) => o is Pair (3, 4) ? "pair" : "not";

        private static string Nested(Holder h) => h is { Pair: (5, 6) } ? "nested" : "not";

        private static string ArmOnNull(Pair? p) => p switch
        {
            (1, 2) => "one-two",
            null => "null",
            _ => "other",
        };

        private static string Size(int n) => n switch
        {
            > 10 => "big",
            < 0 => "negative",
            (0) => "zero",
            not 0 => "other",
        };

        private static string VarArm(int n) => n switch
        {
            var v when v > 5 => "var " + v,
            _ => "small",
        };

        private static int Sum(params List<int> items) => items.Sum();

        private static int Section(int n)
        {
            switch (n)
            {
                case 1:
                    return Twice(n);
                case 2:
                    int Twice(int x) => x * 2;
                    return Twice(n) + 1;
                default:
                    return 0;
            }
        }

        private static IEnumerable<int> Numbers(List<string> disposed)
        {
            try
            {
                yield return 1;
                yield return 2;
            }
            finally
            {
                disposed.Add("disposed");
            }
        }

        private class Pair
        {
            public int X;
            public int Y;

            public void Deconstruct(out int x, out int y)
            {
                x = X;
                y = Y;
            }
        }

        private record Point(int X, int Y);

        private class Holder
        {
            public Pair? Pair;
            public int Count;
        }

        private struct Spot
        {
            public int X;
        }

        private class Counter
        {
            public int Value;

            public static Counter operator ++(Counter c) => new Counter { Value = c.Value + 1 };
            public static Counter operator --(Counter c) => new Counter { Value = c.Value - 1 };
            public static bool operator true(Counter c) => c.Value != 0;
            public static bool operator false(Counter c) => c.Value == 0;
            public static Counter operator &(Counter a, Counter b) => new Counter { Value = a.Value & b.Value };
            public static Counter operator |(Counter a, Counter b) => new Counter { Value = a.Value | b.Value };
        }

        private record Base(int A);

        private record Derived(int A, string B) : Base(A);

        private enum Shade
        {
            Red,
            Green,
            Blue,
        }

        [Flags]
        private enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
        }

        private static class Doubler
        {
            public static int Twice(int x) => x * 2;
        }

        private sealed class Logged : IDisposable
        {
            private readonly List<string> log;
            private readonly int number;

            public Logged(List<string> log, int number)
            {
                this.log = log;
                this.number = number;
            }

            public void Dispose() => log.Add("end " + number);
        }

        private class Grid
        {
            private int this[int i] => i * 3;

            public int Read(int i) => this[i];
        }
    }
}
