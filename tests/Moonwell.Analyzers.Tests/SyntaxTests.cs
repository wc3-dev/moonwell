namespace Moonwell.Analyzers.Tests;

/// <summary>
/// MW0023 to MW0032: C# syntax and library calls that CSharp.lua stops on, writes wrong, or the game's Lua core does not have.
/// Each was seen by running the code on .NET and in Lua and comparing what they printed. The forms the fork's 2.0.4-moonwell.15
/// writes right are accepted (the cases in FormCases).
/// </summary>
public class SyntaxTests
{
    private const string Two = "struct Two { public int A, B; public void Deconstruct(out int a, out int b) { a = A; b = B; } }";

    private const string Types = """
            enum Color { Red, Green = 5, Blue }
            [Flags] enum Mode { None = 0, A = 1, B = 2 }
            struct Pt { public int X; }
            class Box { public int X; public Box? Inner; }
            const int N = -5;
    """;

    [Theory]
    // MW0025: ref
    [InlineData("MW0025", "var a = new[] { 1, 2 }; ref int r = ref First(a);", "ref First(a)", "static ref int First(int[] a) => ref a[0];")]
    [InlineData("MW0025", "int x = 1, y = 2; ref int r = ref x; r = ref y;", "r = ref y")]
    // MW0028: char or enum given to an object parameter of a library method other than string's, Console's and StringBuilder's
    [InlineData("MW0028", "var w = new System.IO.StringWriter(); w.Write(Color.Green);", "Color.Green")]
    [InlineData("MW0028", "var s = string.Join(\",\", new[] { Color.Red, Color.Blue });", "new[] { Color.Red, Color.Blue }")]
    // MW0030: members the Lua core does not have (since CoreSystem 2.0.3-moonwell.9)
    [InlineData("MW0030", "var g = Guid.NewGuid();", "Guid")]
    [InlineData("MW0030", "var q = new[] { 1, 2 }.Shuffle();", "Shuffle")]
    [InlineData("MW0030", "var n = \"a\".Normalize();", "Normalize")]
    [InlineData("MW0030", "var ut = Enum.GetUnderlyingType(typeof(Color));", "GetUnderlyingType")]
    [InlineData("MW0030", "var c = int.Clamp(5, 0, 3);", "Clamp")]
    [InlineData("MW0030", "var m = int.Max(5, 3);", "Max")]
    [InlineData("MW0030", "var l = float.Lerp(0f, 1f, 0.5f);", "Lerp")]
    [InlineData("MW0030", "var a = float.Abs(-1f);", "Abs")]
    // MW0030: .NET's dates, whose 64-bit ticks the game's integers cannot hold (game facts, 11-03)
    [InlineData("MW0030", "var y = DateTime.UtcNow.Year;", "DateTime")]
    [InlineData("MW0030", "var t = new TimeSpan(1, 2, 3);", "TimeSpan")]
    [InlineData("MW0030", "var o = DateTimeOffset.UtcNow;", "DateTimeOffset")]
    [InlineData("MW0030", "Box? b = null; var later = b == null ? default(DateTime) : default;", "DateTime")]
    // MW0032: enum text
    [InlineData("MW0032", "var c = Enum.Parse(typeof(Color), \"Blue\");", "Enum.Parse(typeof(Color), \"Blue\")")]
    // MW0042: NaN and the infinities, which the game's own numbers do not have
    [InlineData("MW0042", "var x = float.NaN;", "NaN")]
    [InlineData("MW0042", "var x = double.NaN;", "NaN")]
    [InlineData("MW0042", "float y = 2f; var b = float.IsNaN(y);", "IsNaN")]
    [InlineData("MW0042", "var x = float.PositiveInfinity;", "PositiveInfinity")]
    [InlineData("MW0042", "var x = double.NegativeInfinity;", "NegativeInfinity")]
    [InlineData("MW0042", "float y = 2f; var b = float.IsInfinity(y);", "IsInfinity")]
    [InlineData("MW0042", "float y = 2f; var b = float.IsFinite(y);", "IsFinite")]
    [InlineData("MW0042", "float y = 2f; var b = y.Equals(float.NaN);", "NaN")]
    // MW0005: decimal
    [InlineData("MW0005", "decimal d = 1;", "d")]
    public void rejected(string id, string body, string pointsAt, string members = "")
    {
        var found = MapCode.Check(body, Types + "\n" + members);
        Assert.Contains(id + ": " + pointsAt, found.Where(f => f.StartsWith(id)).Select(f => f.Replace("\r", "")));
    }

    [Fact]
    public void a_DateTime_points_to_RealTime()
    {
        var messages = MapCode.MessagesOfFile("using System;\n\nclass Dates\n{\n    int M() { return new DateTime(2026, 11, 3).Year; }\n}\n");
        Assert.Contains(messages, message => message.Contains("RealTime"));
    }

    [Fact]
    public void RealTime_is_accepted()
    {
        Assert.Empty(MapCode.Check("var now = RealTime.Now; var later = now.AddDays(1); var y = later.Year + now.Month;"));
    }

    [Fact]
    public void the_forms_the_fork_writes_right_since_moonwell_15_are_accepted()
    {
        // The forms MW0017, MW0018, MW0020, MW0022, MW0024, MW0026, MW0027, MW0029 and MW0031 rejected, and those MW0023,
        // MW0025, MW0028, MW0030 and MW0032 no longer reject: each runs right in the game's Lua (the cases in FormCases and,
        // for MW0030 since CoreSystem 2.0.3-moonwell.9, LibraryCases).
        Assert.Empty(MapCode.Check("""
            int x = 1;
            var s1 = x switch { 1 => "a", > 10 => "b", _ => "c" };
            var s2 = x switch { var n when n > 3 => "a", _ => "c" };
            var s3 = x switch { not 1 => "a", _ => "c" };
            var s4 = x switch { (1 or 2) => "a", _ => "c" };
            var bx = new Box();
            var p1 = bx is { X: var y1 } && bx is { X: int y2 } && bx is { Inner.X: var y3 };
            int[] a = { 1, 2 };
            var p2 = a is [var first, .. var rest];
            var rc = new Rec(1, 2);
            var p3 = rc is (1, 2);
            Two? n2 = null;
            var p4 = n2 is (1, 2);
            object o = 1;
            var p5 = o is Two(1, _);
            var t = ((1, 2), 3);
            var p6 = t is ((1, _), 3);
            var hd = new Holder();
            var p7 = hd is { T: (1, _) };
            var named = (A: 1, B: 2);
            var p8 = named is (A: 1, B: 2);
            var arr = new Pt[1]; var c1 = arr[0]; c1.X = 6;
            var l = new List<Pt> { new Pt() }; var c2 = l[0]; c2.X++;
            var (d1, d2) = (1, 2);
            var xs = new[] { 1, 2 }; (xs[0], xs[1]) = (xs[1], xs[0]);
            var tt = (1, 2); var t1 = "t=" + tt; var t2 = $"{tt}"; Console.WriteLine(tt);
            var anon = new { A = 1 }; var w = anon with { A = 2 };
            var r1 = a[1..2]; var li = new List<int> { 1 }; var r2 = li[^1]; var r3 = "abc"[1..]; Index ix = 1;
            ref int re = ref a[1];
            Count(1, 2);
            var f = (params int[] ps) => ps.Length;
            var n3 = "a--b".Split("--").Length;
            var n4 = "a, b".Split(new[] { ", " }, StringSplitOptions.None).Length;
            var b1 = "abc".StartsWith('a') && "abc".EndsWith('c');
            var i1 = "abc".IndexOf("B", StringComparison.OrdinalIgnoreCase);
            var b2 = "Abc".StartsWith("ab", StringComparison.OrdinalIgnoreCase);
            var rv = new string("abc".Reverse().ToArray());
            char ch = 'a'; var e1 = Color.Green;
            var f1 = string.Format("{0}", ch); Console.WriteLine(e1); var j1 = string.Join(",", ch, e1);
            var sb = new System.Text.StringBuilder(); sb.Append((object)Color.Green);
            try { } catch (NullReferenceException) { } catch (IndexOutOfRangeException) { }
            try { } catch (Exception e) when (e is InvalidOperationException ioe) { }
            int m1 = 5, m2 = 0; try { var md = m1 % m2; } catch (DivideByZeroException) { }
            var ck = checked(x + 1); checked { x++; }
            var ep = Enum.Parse<Color>("Blue"); var ev = Enum.GetValues<Color>(); var ed = Color.Green.ToString("D");
            var mode = Mode.A | Mode.B; var ms = "m=" + mode; var cs = (Color)5 + " x";
            var gv = new Dictionary<int, int>().GetValueOrDefault(1); new Dictionary<int, int>().Remove(1, out var rv1);
            var ca = new List<int>().ConvertAll(i => i);
            new Queue<int>().TryPeek(out var qp); new Stack<int>().TryPop(out var sp); var se = new HashSet<int>().SetEquals(new[] { 1 });
            var sk = new[] { 2, 1 }; var sv = new[] { "b", "a" }; Array.Sort(sk, sv); var dr = Math.DivRem(7, 2, out var rem);
            var lq = new[] { 1 }.Append(2).Prepend(0).TakeLast(1).SkipLast(0).Zip(new[] { 2 }).Count() + new[] { 1 }.Chunk(1).Count();
            var hs = new[] { 1 }.ToHashSet(); var de = new int[0].DefaultIfEmpty(1); var db = new[] { 1 }.DistinctBy(i => i);
            var text = new System.Text.StringBuilder("a").Replace("a", "b").Insert(0, "c").AppendJoin(",", "d", "e").ToString();
            var nl = Environment.NewLine; var ic = char.IsAscii('a') && char.ToUpperInvariant('a') == 'A'; var itn = string.Intern("a");
            var hx = Convert.ToHexString(new byte[] { 1 }); var rl = int.RotateLeft(1, 3) + int.PopCount(7) + int.Log2(8);
            var hc = HashCode.Combine(1, 2); var cmp = StringComparer.Ordinal; var ba = new System.Collections.BitArray(3);
            var n0 = 1234.ToString("N0") + 0.5f.ToString("P0") + 3.5f.ToString("0.0") + int.Parse("1") + float.Parse("1");
            """, Types + "\n" + Two + "\n" + "record Rec(int X, int Y);" + "\n" + "class Holder { public (int, int) T; }" + "\n"
            + "static int Count(params List<int> xs) => xs.Count;"));
    }

    [Fact]
    public void the_supported_forms_are_accepted()
    {
        Assert.Empty(MapCode.Check("""
            int x = 5;
            var a = x switch { 1 => "one", 2 or 3 => "few", _ => "many" };
            object o = x;
            var b = o switch { int i when i > 3 => "big", int i => "int" + i, string s => s, null => "null", _ => "?" };
            var c = o is int n && n > 3;
            var d = x is > 1 and < 9;
            var e = o is not null;
            var f = o is not string;
            var box = new Box { X = 3 };
            var g = box is { X: 3 } && box is { X: > 2 } && box is { Inner: null };
            (int, int) t = (1, 2);
            var h = t switch { (1, 2) => "a", _ => "b" };
            int[] arr = { 1, 2, 3 };
            var i1 = arr[^1];
            var j = arr is [1, 2, 3] && arr is [1, ..] && arr is [.., 3];
            (int q, int r) = (1, 2);
            var (first, second) = Pair();
            """, Types + "\n" + "static (int, int) Pair() => (1, 2);"));
    }

    [Fact]
    public void a_type_name_in_an_arm_and_several_usings_are_accepted()
    {
        // MW0017's type pattern and MW0034 went with the fork's 2.0.4-moonwell.14 and CoreSystem 2.0.3-moonwell.6 (C43, C44).
        Assert.Empty(MapCode.Check("""
            object o = 1;
            var a = o switch { int => "a", string => "b", Box => "c", _ => "e" };
            var b = o switch { int or string => "d", _ => "e" };
            using var first = new Res();
            using var second = new Res();
            using Res third = new Res(), fourth = new Res();
            """, Types + "\n" + "class Res : IDisposable { public void Dispose() { } }"));
    }

    [Fact]
    public void the_patterns_the_fork_writes_right_are_accepted()
    {
        // Decision 80: each of these runs right in the game's Lua (the cases in PatternCases).
        Assert.Empty(MapCode.Check("""
            int x = 5;
            var a = x is (1 or 5) && x is not (1 or 2) && x is (> 0 and (< 3 or > 4));
            var bx = new Box { X = 3 };
            var b = bx is { X: (1 or 3) } && bx is { Inner: not null } && bx is { X: not 3 } && bx is { Inner: not { X: 1 } };
            var c = bx is { Inner.X: 9 } && bx is { Inner.Inner.X: > 2 };
            var d = bx switch { { Inner.X: 9 } => "nine", { Inner: not null } => "inner", { X: (1 or 3) } => "odd", _ => "other" };
            (int, int) t = (1, 2);
            var e = t is (1, _) && t is (> 0, >= 2) && t is (not 3, _) && (t is (2, _) or (1, _)) && t is not (3, _);
            var f = t is (var m, var n) ? m + n : 0;
            var two = new Two { A = 1, B = 2 };
            var g = two is (1, 2) && two is Two(1, var h) && (bx, 1) is ({ X: 3 }, 1);
            """, Types + "\n" + Two));
    }

    [Fact]
    public void library_calls_that_work_are_accepted()
    {
        Assert.Empty(MapCode.Check("""
            var s = "a b c";
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var more = s.Split(',');
            var has = s.Contains('b') && s.StartsWith("a") && s.EndsWith("c") && s.IndexOf("b", StringComparison.Ordinal) >= 0;
            var eq = s.Equals("A", StringComparison.OrdinalIgnoreCase);
            var sb = new System.Text.StringBuilder().Append('x').Append("y").Append(1).AppendLine().Append(Color.Green.ToString());
            Console.WriteLine('c' + "d");
            Console.WriteLine($"{Color.Green} {'c'}");
            var text = "x" + Color.Red + 'c';
            var list = new List<int> { 3, 1 }; list.Sort(); list.RemoveAll(x => x > 5);
            var dict = new Dictionary<string, int>(); dict["a"] = 1; dict.TryGetValue("a", out var one); dict.Remove("a");
            var q = new Queue<int>(); q.TryDequeue(out var first);
            var linq = list.Where(x => x > 1).Select(x => x * 2).OrderBy(x => x).Concat(list).Distinct().ToList();
            try { } catch (InvalidOperationException) { } catch (ArgumentException e) when (e.Message == "x") { } catch (Exception) { }
            try { var share = 6 / list.Count; var part = 5f % 0f; } catch (DivideByZeroException) { }
            var map = new OrderedMap<string, int>(); var pairs = map.ToList(); var all = Enumerable.ToArray(map);
            var un = unchecked(1 + 2);
            var big = 5; var cast = checked((byte)big);
            var mode = Mode.A | Mode.B; var has2 = mode.HasFlag(Mode.A) && (mode & Mode.B) != 0;
            var named = Color.Green; var back = (Color)5; var ok = back == named;
            var most = float.MaxValue; var least = float.MinValue; var root = MathF.Sqrt(2f); var near = MathF.Abs(root - 1.414f) < 0.001f;
            """, Types));
    }

    [Fact]
    public void an_infinity_in_an_attribute_is_accepted()
    {
        var found = MapCode.CheckFile("""
            class MaxAttribute : System.Attribute { public MaxAttribute(float most) { } }
            [Max(float.PositiveInfinity)]
            class Limited { }
            """);
        Assert.Empty(found);
    }

    [Fact]
    public void primary_constructor_parameters_are_allowed_in_initializers_and_base()
    {
        var found = MapCode.CheckFile("""
            class Base(string n) { public string N = n; }
            class Der(string n, int k) : Base(n) { public int K = k; public string M() => n + k; public int Z { get; } = k; }
            class Plain(int x) { public int Get() => x; }
            record Rec(int A) { public int B = A; }
            """);
        Assert.Empty(found);
    }

    [Fact]
    public void a_minus_before_a_negative_constant_is_allowed()
    {
        var found = MapCode.Check("""
            var a = -N;
            var b = unchecked(-int.MinValue);
            var c = -(N);
            """, Types);
        Assert.Empty(found);
    }

    [Fact]
    public void initializers_read_in_one_statement_are_allowed()
    {
        var found = MapCode.Check("""
            var s = new Box { X = 1 }.X + " " + new Box { X = 2 }.X;
            var e = new Box { X = 1 }.Equals(new Box { X = 1 }) + " " + object.Equals(new Box { X = 2 }, new Box { X = 2 });
            """, Types);
        Assert.Empty(found);
    }

    [Fact]
    public void user_defined_increment_and_truth_operators_are_accepted()
    {
        var found = MapCode.CheckFile("""
            struct V
            {
                public int X;
                public static V operator ++(V a) => new V { X = a.X + 1 };
                public static V operator -(V a) => new V { X = -a.X };
                public static bool operator true(V a) => a.X != 0;
                public static bool operator false(V a) => a.X == 0;
            }
            """);
        Assert.Empty(found);
    }

    [Fact]
    public void a_class_with_two_constructions_of_one_generic_interface_is_rejected()
    {
        var found = MapCode.CheckFile("""
            interface IG<T> { T Get(); }
            class E : IG<int>, IG<string> { int IG<int>.Get() => 1; string IG<string>.Get() => "s"; }
            class F : IG<int> { public int Get() => 1; }
            """);
        Assert.Equal(new[] { "MW0023: E" }, found);
    }

    [Fact]
    public void a_static_abstract_member_is_rejected_and_a_derived_record_accepted()
    {
        var found = MapCode.CheckFile("""
            record R(int A);
            record D(int A, int B) : R(A);
            interface IMake<T> { static abstract T Make(); }
            interface IPlain { static int Z() => 1; int V(); }
            """);
        Assert.Equal(new[] { "MW0023: static abstract T Make();" }, found);
    }

    [Fact]
    public void two_initializers_beside_each_other_are_accepted()
    {
        Assert.Empty(MapCode.Check("""
            var s = new Box { X = 1 }.Equals(new Box { X = 1 });
            var t = Take(new Box { X = 1 }, new Box { X = 2 });
            """, Types + "\nstatic int Take(Box a, Box b) => a.X + b.X;"));
    }

    [Fact]
    public void the_message_says_what_to_write_instead()
    {
        Assert.Equal(
            "A ref reassignment writes the value into what the ref local named before in the game's Lua; make a new ref local",
            MapCode.Message("int x = 1, y = 2; ref int r = ref x; r = ref y;"));
    }
}
