namespace Moonwell.Analyzers.Tests;

/// <summary>MW0058: a map's build code (the files in its buildtime folder) and its game code naming each other's types.</summary>
public class BuildTimeTests
{
    private const string BuildCode = """
        using System.Linq;
        using Moonwell.BuildTime;

        namespace MyMap.BuildTime
        {
            public sealed class TreeCount : BuildStep
            {
                public const int Limit = 40;

                public override void Run()
                {
                    var water = Map.Cells.Where(cell => cell.IsWater);
                    Give("Trees", new { Count = Map.Doodads.Doodads.Count, Shore = water.Count });
                }
            }
        }
        """;

    private const string GameCode = """
        using Moonwell;

        namespace MyMap
        {
            public sealed class Settings : Map { }

            public static class Rules
            {
                public const int Lives = 20;
            }
        }
        """;

    [Fact]
    public void build_code_and_game_code_that_keep_apart_are_left_alone()
    {
        Assert.Empty(MapCode.CheckFiles((@"C:\Maps\MyMap\buildtime\TreeCount.cs", BuildCode), (@"C:\Maps\MyMap\Map.cs", GameCode)));
    }

    [Fact]
    public void game_code_that_names_build_code_is_an_error()
    {
        Assert.Equal(new[]
        {
            @"C:\Maps\MyMap\Start.cs: MW0058: TreeCount",
            @"C:\Maps\MyMap\Start.cs: MW0058: BuildStep",
            @"C:\Maps\MyMap\Start.cs: MW0058: MapReader",
            @"C:\Maps\MyMap\Start.cs: MW0058: MapPathingMap",
        }, MapCode.CheckFiles(
            (@"C:\Maps\MyMap\buildtime\TreeCount.cs", BuildCode),
            (@"C:\Maps\MyMap\Map.cs", GameCode),
            (@"C:\Maps\MyMap\Start.cs", """
                namespace MyMap
                {
                    public static class Start
                    {
                        public static int Limit() => BuildTime.TreeCount.Limit;

                        public static void Read(Moonwell.BuildTime.BuildStep? step, Moonwell.BuildTime.MapReader? map)
                        {
                            War3Net.Build.Environment.MapPathingMap? pathing = map?.PathingMap;
                            var cells = map?.Cells;
                        }
                    }
                }
                """)));
    }

    [Fact]
    public void build_code_that_names_game_code_is_an_error()
    {
        Assert.Equal(new[]
        {
            @"C:\Maps\MyMap\buildtime\Checks.cs: MW0058: Rules",
            @"C:\Maps\MyMap\buildtime\Checks.cs: MW0058: Unit",
            @"C:\Maps\MyMap\buildtime\Checks.cs: MW0058: Settings",
        }, MapCode.CheckFiles(
            (@"C:\Maps\MyMap\buildtime\TreeCount.cs", BuildCode),
            (@"C:\Maps\MyMap\Map.cs", GameCode),
            (@"C:\Maps\MyMap\buildtime\Checks.cs", """
                using Moonwell.BuildTime;

                namespace MyMap.BuildTime
                {
                    public sealed class Checks : BuildStep
                    {
                        public override void Run()
                        {
                            var lives = Rules.Lives + TreeCount.Limit;
                            Moonwell.Unit? unit = null;
                            var settings = new Settings();
                        }
                    }
                }
                """)));
    }

    [Fact]
    public void the_message_says_which_side_a_name_is_on_and_what_to_do()
    {
        Assert.Equal(new[]
        {
            "'BuildStep' is build code, which never reaches the game: game code reads what build code gives from Data",
            "'Settings' is game code, which is not compiled yet when build code runs: build code reads the map's files and gives its results",
        }, MapCode.MessagesOfFiles(
            (@"C:\Maps\MyMap\Map.cs", GameCode),
            (@"C:\Maps\MyMap\Heroes\Tools.cs", "namespace MyMap { public static class Tools { public static Moonwell.BuildTime.BuildStep? Step; } }"),
            (@"C:\Maps\MyMap\buildtime\Tools.cs", "namespace MyMap.BuildTime { public static class Tools { public static MyMap.Settings? Map; } }")));
    }

    [Fact]
    public void the_rules_about_the_game_and_its_lua_leave_build_code_alone()
    {
        // An unseeded Random (MW0038) and a namespace named as one of the game's Lua globals (MW0044) are wrong in game code only.
        const string Code = """
            using System;

            namespace print
            {
                public class Shape
                {
                    public static int Roll()
                    {
                        var dice = new Random();
                        return dice.Next(6);
                    }
                }
            }
            """;

        Assert.Empty(MapCode.CheckFiles((@"C:\Maps\MyMap\buildtime\Rect.cs", Code)));
        Assert.Empty(MapCode.CheckFiles((@"C:\Maps\MyMap\buildtime\Shapes\Rect.cs", Code)));
        Assert.Equal(new[]
        {
            @"C:\Maps\MyMap\Tools\Rect.cs: MW0044: print",
            @"C:\Maps\MyMap\Tools\Rect.cs: MW0038: new Random()",
        }, MapCode.CheckFiles((@"C:\Maps\MyMap\Tools\Rect.cs", Code)));
    }
}
