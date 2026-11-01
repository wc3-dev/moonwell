namespace Moonwell.Build.Tests
{
    public sealed class LineMarksTests : IDisposable
    {
        private static readonly string[] Debug = { "DEBUG" };

        private readonly TestFolders folders;

        public LineMarksTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        [Fact]
        public void marks_each_statement()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "        G();",
                "        H();",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("        /*[[--@mw 1:5]]*/ G();", marked);
            Assert.Contains("        /*[[--@mw 1:6]]*/ H();", marked);
            Assert.Equal(source.Split('\n').Length, marked.Split('\n').Length);
        }

        [Fact]
        public void marks_body_end()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "        G();",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 2, Debug);

            Assert.Contains("    /*[[--@mw -]]*/ }", marked);
        }

        [Fact]
        public void lambda_end_marks_enclosing()
        {
            var source = Lines(
                "using System;",
                "class A",
                "{",
                "    void F()",
                "    {",
                "        G();",
                "        Action act = () =>",
                "        {",
                "            H();",
                "        };",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("            /*[[--@mw 1:9]]*/ H();", marked);
            Assert.Contains("        /*[[--@mw 1:7]]*/ };", marked);
            Assert.Contains("    /*[[--@mw -]]*/ }", marked);
        }

        [Fact]
        public void two_statements_one_line()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "        G(); H();",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("        /*[[--@mw 1:5]]*/ G(); /*[[--@mw 1:5]]*/ H();", marked);
        }

        [Fact]
        public void switch_section()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F(int x)",
                "    {",
                "        switch (x)",
                "        {",
                "            case 1:",
                "                G();",
                "                break;",
                "        }",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("/*[[--@mw 1:8]]*/ G();", marked);
            Assert.Contains("/*[[--@mw 1:9]]*/ break;", marked);
        }

        [Fact]
        public void a_bare_body_gets_braces_and_markers()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "        if (B())",
                "            G();",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("            { /*[[--@mw 1:6]]*/ G(); /*[[--@mw 1:5]]*/ }\n", marked);
            Assert.Equal(source.Split('\n').Length, marked.Split('\n').Length);
        }

        [Fact]
        public void an_else_if_stays_bare_and_the_block_before_it_ends_on_its_condition()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "        if (B())",
                "        {",
                "            G();",
                "        }",
                "        else",
                "            if (",
                "                C())",
                "        {",
                "            H();",
                "        }",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("        /*[[--@mw 1:11]]*/ }\n        else\n            if (\n", marked);
        }

        [Fact]
        public void two_bare_bodies_end_inside_out()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "        if (B()) while (C()) G();H();",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("if (B()) { /*[[--@mw 1:5]]*/ while (C()) { /*[[--@mw 1:5]]*/ G(); /*[[--@mw 1:5]]*/ } /*[[--@mw 1:5]]*/ }/*[[--@mw 1:5]]*/ H();", marked);
        }

        [Fact]
        public void a_do_body_ends_on_its_condition()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "        do",
                "        {",
                "            G();",
                "        }",
                "        while (B());",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("        /*[[--@mw 1:9]]*/ }\n        while (B());", marked);
        }

        [Fact]
        public void a_section_before_a_case_that_can_fail_ends_on_that_case()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F(int x)",
                "    {",
                "        switch (x + 1)",
                "        {",
                "            case 1:",
                "                G();",
                "                break;",
                "            case 2 when B():",
                "                return;",
                "            case 3 when C():",
                "                G();",
                "                break;",
                "            case 4:",
                "                break;",
                "        }",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("/*[[--@mw 1:10]]*/ break;", marked);
            Assert.Contains("{ /*[[--@mw 1:11]]*/ return; /*[[--@mw 1:12]]*/ }", marked);
            Assert.Contains("/*[[--@mw 1:14]]*/ break;", marked);
            Assert.Contains("/*[[--@mw 1:5]]*/ switch (x + 1)", marked);
        }

        [Fact]
        public void a_section_with_a_declaration_keeps_its_last_statement_and_stays_without_braces()
        {
            // x belongs to the whole switch, so braces around the first section would hide it from the second.
            var source = Lines(
                "class A",
                "{",
                "    int F(int n)",
                "    {",
                "        switch (n)",
                "        {",
                "            case 1 when B():",
                "                var x = 1;",
                "                return x;",
                "            case 2 when C():",
                "                x = 2;",
                "                return x;",
                "        }",
                "        return 0;",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("                /*[[--@mw 1:9]]*/ return x;\n            case 2", marked);
        }

        [Fact]
        public void a_switch_on_a_name_names_its_first_case()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F(object x)",
                "    {",
                "        switch (x)",
                "        {",
                "            case string text when text.Length > 1:",
                "                break;",
                "        }",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug);

            Assert.Contains("/*[[--@mw 1:7]]*/ switch (x)", marked);
        }

        [Fact]
        public void columns_read_as_the_files_own()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "        if (B()) G(); H();",
                "    }",
                "}");

            var marked = LineMarks.Mark(source, 1, Debug, out var columns).Split('\n');

            var line = source.Split('\n')[4];
            foreach (var code in new[] { "if (", "B(", "G(", "H(" })
            {
                Assert.Equal(line.IndexOf(code, StringComparison.Ordinal) + 1, columns.Original(5, marked[4].IndexOf(code, StringComparison.Ordinal) + 1));
            }

            // Inside a marker, the column it was put before; on a line with no marker, the column itself.
            Assert.Equal(line.IndexOf("if (", StringComparison.Ordinal) + 1, columns.Original(5, marked[4].IndexOf("@mw", StringComparison.Ordinal) + 1));
            Assert.Equal(5, columns.Original(3, 5));
        }

        [Fact]
        public void inactive_branch()
        {
            var source = Lines(
                "class A",
                "{",
                "    void F()",
                "    {",
                "#if DEBUG",
                "        G();",
                "#else",
                "        H();",
                "#endif",
                "    }",
                "}");

            var development = LineMarks.Mark(source, 1, Debug);
            var release = LineMarks.Mark(source, 1, Array.Empty<string>());

            Assert.Contains("/*[[--@mw 1:6]]*/ G();", development);
            Assert.DoesNotContain("/*[[--@mw 1:8]]*/", development);
            Assert.Contains("/*[[--@mw 1:8]]*/ H();", release);
            Assert.DoesNotContain("/*[[--@mw 1:6]]*/", release);
        }

        [Fact]
        public void stage_copies_and_marks()
        {
            var folder = folders.TemporaryCopyOfSample();
            var generated = Path.Combine(folder, "build", "generated");
            Directory.CreateDirectory(generated);
            File.WriteAllText(Path.Combine(generated, "MapMain.g.cs"), Lines("class M", "{", "    static void Main()", "    {", "        N();", "    }", "}"));
            var project = MapProject.Find(folder);

            var marked = MarkedProject.Stage(project, Debug);

            Assert.Equal(Path.Combine(folder, "build", "marked"), marked.Folder);
            Assert.Equal(new[] { "Hello.cs", "Map.cs", "Objects.cs" }, marked.Files);
            Assert.Equal(Path.Combine(marked.Folder, "Sample.csproj"), marked.ProjectFile);
            var csproj = File.ReadAllText(marked.ProjectFile);
            var moonwell = Path.GetFullPath(Path.Combine(folder, "..", "..", "..", "..", "src", "Moonwell", "Moonwell.csproj"));
            Assert.Contains($"Include=\"{moonwell}\"", csproj);
            Assert.Contains("/*[[--@mw 1:", File.ReadAllText(Path.Combine(marked.Folder, "Hello.cs")));
            Assert.DoesNotContain("@mw", File.ReadAllText(Path.Combine(marked.Folder, "build", "generated", "MapMain.g.cs")));
            Assert.Equal(
                "error at " + Path.Combine(folder, "Hello.cs"),
                marked.Unstage("error at " + Path.Combine(marked.Folder, "Hello.cs")));
            Assert.Equal(
                "(" + Path.Combine(folder, "Bad.cs") + "@7:42)",
                marked.Unstage("(" + marked.Folder.Replace('\\', '/') + "\\Bad.cs@7:42)"));
        }

        [Fact]
        public void unstage_reads_a_marked_lines_column_as_the_files_own()
        {
            var folder = folders.TemporaryCopyOfSample();
            File.WriteAllText(Path.Combine(folder, "Bad.cs"), Lines("class Bad", "{", "    void F()", "    {", "        G();", "    }", "}"));
            var marked = MarkedProject.Stage(MapProject.Find(folder), Debug);
            var bad = Path.Combine(marked.Folder, "Bad.cs");
            var column = File.ReadAllLines(bad)[4].IndexOf("G()", StringComparison.Ordinal) + 1;

            // Roslyn's form, and CSharp.lua's own.
            Assert.Equal(Path.Combine(folder, "Bad.cs") + "(5,9): error CS0103: no", marked.Unstage(bad + $"(5,{column}): error CS0103: no"));
            Assert.Equal("SourceLocation(" + Path.Combine(folder, "Bad.cs") + "@5:9): no", marked.Unstage("SourceLocation(" + bad + $"@5:{column}): no"));
            Assert.Equal(Path.Combine(folder, "Bad.cs") + "(3,5): no", marked.Unstage(bad + "(3,5): no"));
        }

        [Fact]
        public void stage_empties_the_marked_folder()
        {
            var folder = folders.TemporaryCopyOfSample();
            var stale = Path.Combine(folder, "build", "marked", "Stale.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
            File.WriteAllText(stale, "class Stale { }");

            MarkedProject.Stage(MapProject.Find(folder), Debug);

            Assert.False(File.Exists(stale));
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        private static string Lines(params string[] lines)
        {
            return string.Join("\n", lines) + "\n";
        }
    }
}
