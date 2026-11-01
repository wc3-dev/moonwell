using System.Text.RegularExpressions;

namespace Moonwell.Build.Tests
{
    /// <summary>Forms of code whose failing line has no statement of its own in a block: each error names the line that failed, built as a map is and run in Lua.</summary>
    public sealed class ErrorShapesTests : IDisposable
    {
        // Every class fails once; the comment on a line names the class that fails there.
        private const string Shapes = """
            using System;
            using System.Collections.Generic;
            using Moonwell;

            namespace Fails
            {
                public abstract class Shape : GameComponent
                {
                    protected readonly Holder? Missing = null;

                    protected static bool Yes() { return true; }

                    protected static bool No() { return false; }

                    protected static object Found() { return new Holder(); }

                    protected static void Use(int value) { }
                }

                public sealed class ElseIfCondition : Shape
                {
                    public override void OnGameStart()
                    {
                        if (No())
                        {
                            Use(1);
                        }
                        else if (Missing!.Value > 0) // fails: ElseIfCondition
                        {
                            Use(2);
                        }
                    }
                }

                public sealed class ThirdCondition : Shape
                {
                    public override void OnGameStart()
                    {
                        if (No())
                        {
                            Use(1);
                        }
                        else if (No())
                        {
                            Use(2);
                        }
                        else if (Missing!.Value > 0) // fails: ThirdCondition
                        {
                            Use(3);
                        }
                    }
                }

                public sealed class ElseIfAfterBareBody : Shape
                {
                    public override void OnGameStart()
                    {
                        if (No())
                            Use(1);
                        else if (Missing!.Value > 0) // fails: ElseIfAfterBareBody
                            Use(2);
                    }
                }

                public sealed class DoCondition : Shape
                {
                    public override void OnGameStart()
                    {
                        do
                        {
                            Use(1);
                        }
                        while (Missing!.Value > 0); // fails: DoCondition
                    }
                }

                public sealed class BareDoCondition : Shape
                {
                    public override void OnGameStart()
                    {
                        do
                            Use(1);
                        while (Missing!.Value > 0); // fails: BareDoCondition
                    }
                }

                public sealed class BareIfBody : Shape
                {
                    public override void OnGameStart()
                    {
                        if (Yes())
                            Use(Missing!.Value); // fails: BareIfBody
                    }
                }

                public sealed class BareElseBody : Shape
                {
                    public override void OnGameStart()
                    {
                        if (No())
                            Use(1);
                        else
                            Use(Missing!.Value); // fails: BareElseBody
                    }
                }

                public sealed class BareWhileBody : Shape
                {
                    public override void OnGameStart()
                    {
                        while (Yes())
                            Use(Missing!.Value); // fails: BareWhileBody
                    }
                }

                public sealed class BareForBody : Shape
                {
                    public override void OnGameStart()
                    {
                        for (var i = 0; i < 2; i++)
                            Use(Missing!.Value); // fails: BareForBody
                    }
                }

                public sealed class BareForeachBody : Shape
                {
                    public override void OnGameStart()
                    {
                        foreach (var value in new List<int> { 1 })
                            Use(Missing!.Value); // fails: BareForeachBody
                    }
                }

                public sealed class NestedBareBodies : Shape
                {
                    public override void OnGameStart()
                    {
                        if (Yes())
                            for (var i = 0; i < 2; i++)
                                if (Yes())
                                    Use(Missing!.Value); // fails: NestedBareBodies
                    }
                }

                public sealed class CaseAfterBreak : Shape
                {
                    public override void OnGameStart()
                    {
                        var thing = Found();
                        switch (thing)
                        {
                            case string text when text.Length > 5:
                                Use(1);
                                break;
                            case Holder found when Missing!.Value > 0: // fails: CaseAfterBreak
                                Use(2);
                                break;
                        }
                    }
                }

                public sealed class CaseAfterReturn : Shape
                {
                    public override void OnGameStart()
                    {
                        var thing = Found();
                        switch (thing)
                        {
                            case string text when text.Length > 5:
                                Use(1);
                                return;
                            case Holder found when Missing!.Value > 0: // fails: CaseAfterReturn
                                Use(2);
                                break;
                        }
                    }
                }

                public sealed class CaseAfterBlock : Shape
                {
                    public override void OnGameStart()
                    {
                        var thing = Found();
                        switch (thing)
                        {
                            case string text when text.Length > 5:
                            {
                                Use(1);
                                break;
                            }
                            case Holder found when Missing!.Value > 0: // fails: CaseAfterBlock
                                Use(2);
                                break;
                        }
                    }
                }

                public sealed class CaseAfterDeclaration : Shape
                {
                    public override void OnGameStart()
                    {
                        var thing = Found();
                        switch (thing)
                        {
                            case string text when text.Length > 5:
                                var length = text.Length;
                                Use(length);
                                break;
                            case Holder found when Missing!.Value > 0: // fails: CaseAfterDeclaration
                                Use(2);
                                break;
                        }
                    }
                }

                public sealed class CaseAfterDefault : Shape
                {
                    public override void OnGameStart()
                    {
                        var thing = Found();
                        switch (thing)
                        {
                            case string text when text.Length > 5:
                                Use(1);
                                break;
                            default:
                                Use(2);
                                break;
                            case Holder found when Missing!.Value > 0: // fails: CaseAfterDefault
                                Use(3);
                                break;
                        }
                    }
                }

                public sealed class FirstCase : Shape
                {
                    public override void OnGameStart()
                    {
                        var thing = Found();
                        switch (thing)
                        {
                            case Holder found when Missing!.Value > 0: // fails: FirstCase
                                Use(1);
                                break;
                        }
                    }
                }
            }
            """;

        private readonly TestFolders folders;

        public ErrorShapesTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void each_error_names_the_line_that_failed()
        {
            var lines = Shapes.ReplaceLineEndings("\n").Split('\n');
            var expected = new List<string>();
            for (var line = 0; line < lines.Length; line++)
            {
                var tag = Regex.Match(lines[line], @"// fails: (\w+)$");
                if (tag.Success)
                {
                    expected.Add($"{tag.Groups[1].Value}: Shapes.cs:{line + 1}");
                }
            }

            var folder = folders.TemporaryCopyOf("Fails");
            File.WriteAllText(Path.Combine(folder, "Shapes.cs"), Shapes);
            File.WriteAllText(
                Path.Combine(folder, "Map.cs"),
                "namespace Fails\n{\n    public sealed class FailsMap : Moonwell.Map\n    {\n        public FailsMap()\n        {\n            Name = \"Fails\";\n"
                + string.Concat(expected.Select(entry => "            Components.Add(new " + entry.Substring(0, entry.IndexOf(':')) + "());\n"))
                + "        }\n    }\n}\n");

            MapBuild.Run(folder, new BuildOptions(), TextWriter.Null);

            var script = File.ReadAllText(Path.Combine(MapProject.Find(folder).StagedFolder, "war3map.lua"));
            var named = ErrorLinesTests.FailsBuilds.Start(script, string.Empty).Log
                .Select(entry => Regex.Match(entry, @"^Moonwell: (\w+)\.OnGameStart failed: .* \((Shapes\.cs:\d+)"))
                .Select(match => $"{match.Groups[1].Value}: {match.Groups[2].Value}");
            Assert.Equal(expected.Order(StringComparer.Ordinal), named.Order(StringComparer.Ordinal));
        }
    }
}
