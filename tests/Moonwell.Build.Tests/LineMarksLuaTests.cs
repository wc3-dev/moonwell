using CSharpLua;

namespace Moonwell.Build.Tests
{
    /// <summary>The marks only add lines: a program's Lua, with the marker lines taken out, is the Lua it has without marks.</summary>
    public sealed class LineMarksLuaTests
    {
        private static readonly string[] Symbols = { "DEBUG", "__CSharpLua__" };

        [Theory]
        [InlineData("if (A()) { G(); } else if (B()) { H(); } else { G(); }")]
        [InlineData("if (A()) G(); else if (B()) H(); else G();")]
        [InlineData("if (A()) return 1;")]
        [InlineData("if (A()) if (B()) G(); else H();")]
        [InlineData("if (A()) ; else G();")]
        [InlineData("while (A()) G();")]
        [InlineData("while (A()) ;")]
        [InlineData("while (A()) if (B()) break; else continue;")]
        [InlineData("for (var i = 0; i < 3; i++) G();")]
        [InlineData("for (var i = 0; i < n; i++) if (B()) continue;")]
        [InlineData("for (var i = 0; i < n; i++) if (B()) return i;")]
        [InlineData("foreach (var item in items) G();")]
        [InlineData("foreach (var item in items) if (item > 1) return item;")]
        [InlineData("do G(); while (A());")]
        [InlineData("do { G(); } while (A());")]
        [InlineData("do if (B()) continue; while (A());")]
        [InlineData("using (var a = new Res()) G();")]
        [InlineData("using (var a = new Res()) using (var b = new Res()) return 1;")]
        [InlineData("lock (items) G();")]
        [InlineData("try { if (A()) return 1; } catch { if (B()) G(); } finally { if (B()) H(); }")]
        [InlineData("if (A()) goto done; G(); done: H();")]
        [InlineData("switch (n) { case 1: G(); break; case 2: H(); break; default: G(); break; }")]
        [InlineData("switch (n) { case 1 when A(): G(); break; case 2 when B(): H(); break; }")]
        [InlineData("switch (n) { case 1 when A(): return 1; case 2 when B(): throw new System.Exception(); default: return 3; }")]
        [InlineData("switch (n) { case 1 when A(): G(); return 1; case 2 when B(): H(); return 2; }")]
        [InlineData("switch (n) { case 1 when A(): { G(); break; } case 2 when B(): { H(); break; } }")]
        [InlineData("switch (n) { case 1 when A(): { G(); return 1; } case 2 when B(): return 2; }")]
        [InlineData("switch (n) { case 1 when A(): var x = 1; G(); return x; case 2 when B(): x = 2; return x; }")]
        [InlineData("switch (n) { case 1 when A(): G(); break; default: H(); break; case 2 when B(): G(); break; }")]
        [InlineData("switch (n) { case 1 when A(): if (B()) G(); else H(); break; case 2 when B(): if (A()) break; G(); break; }")]
        [InlineData("foreach (var item in items) { switch (item) { case 1 when A(): continue; case 2 when B(): G(); break; } H(); }")]
        [InlineData("switch (n) { case 1 when A(): goto default; case 3 when B(): G(); break; default: H(); break; }")]
        [InlineData("switch (n) { case 1 when A(): if (B()) G(); return 1; case 2 when B(): for (;;) G(); }")]
        [InlineData("switch (Name()) { case \"a\": G(); break; case \"b\" when B(): H(); break; }")]
        public void marks_change_only_their_own_lines(string body)
        {
            var program = Program(body);

            var marked = Compiler.CompileSingleCodeForGame(LineMarks.Mark(program, 1, Symbols));

            Assert.Contains("--@mw 1:", marked);
            Assert.Equal(Compiler.CompileSingleCodeForGame(program), WithoutMarkerLines(marked));
        }

        [Fact]
        public void marks_part_two_using_declarations_and_nothing_else()
        {
            // BL6: the fork joins two using var next to each other into one call, and a marker between them keeps them apart. Both
            // dispose last first since CoreSystem 2.0.3-moonwell.6, so the Lua differs and what it does does not (compiler.two-using-declarations).
            var program = Program("using var a = new Res(); using var b = new Res(); G(); return 1;");

            Assert.Contains("System.usingX(", Compiler.CompileSingleCodeForGame(program));
            Assert.DoesNotContain("System.usingX(", Compiler.CompileSingleCodeForGame(LineMarks.Mark(program, 1, Symbols)));
        }

        private static string Program(string body)
        {
            return "class Res : System.IDisposable { public void Dispose() { } }\n"
                + "class Case\n{\n"
                + "    static bool A() { return true; }\n    static bool B() { return false; }\n    static void G() { }\n    static void H() { }\n    static string Name() { return \"a\"; }\n"
                + "    static int F(int n, System.Collections.Generic.List<int> items)\n    {\n"
                + "        " + body + "\n"
                + "        return 0;\n    }\n}\n";
        }

        private static string WithoutMarkerLines(string lua)
        {
            return string.Concat(lua.Split('\n').Where(line => !line.TrimStart().StartsWith("--@mw ", StringComparison.Ordinal)).Select(line => line + "\n"))[..^1];
        }
    }
}
