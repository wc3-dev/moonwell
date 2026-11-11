namespace Moonwell.Analyzers.Tests;

/// <summary>
/// MW0056: a new of a Dictionary or an OrderedMap keyed by a class of the map's with static setup, which the game's Lua runs
/// while the script loads (decision 271).
/// </summary>
public class LoadStopTests
{
    // The key classes: each of the first six has static setup of one kind, the rest have none.
    private const string Keys = """
        using System.Collections.Generic;
        using System.Linq;
        using Moonwell;

        namespace Map
        {
            public sealed class Quest { public static readonly List<Quest> All = new List<Quest>(); }

            public sealed class Wave
            {
                public static int Count;
                static Wave() { Count = 3; }
            }

            public class Zone { public static Zone? First { get; set; } = new Zone(); }

            public sealed class Camp : Zone { }

            public sealed class Spot { public static Color Tint; }

            public struct Cell
            {
                public int X;
                public static readonly Cell None = new Cell { X = -1 };
            }

            public sealed class Plain
            {
                public const int Max = 4;
                public static int Next = 1;
                public const string Kind = "plain";
                public static string Name = Kind + "!";
                public static Plain? Last;
                public static float? Rate;
                public static int Twice => Next * 2;
                static Plain() { }
                public static void Reset() { Next = 1; }
            }

            public sealed class Guard : Unit { }

            public enum Side { Left, Right }

        """;

    private static string Map(string code) => Keys + "    public static class Tables\n    {\n" + code + "\n    }\n}\n";

    [Fact]
    public void a_dictionary_keyed_by_a_class_with_static_setup_is_refused()
    {
        Assert.Equal(
            new[]
            {
                "MW0056: new Dictionary<Quest, int>()",
                "MW0056: new()",
                "MW0056: new Dictionary<Zone, string>()",
                "MW0056: new Dictionary<Camp, int>()",
                "MW0056: new Dictionary<Spot, int>()",
                "MW0056: new Dictionary<Cell, Unit>()",
            },
            MapCode.CheckFile(Map("""
                    public static readonly Dictionary<Quest, int> Progress = new Dictionary<Quest, int>();
                    public static readonly Dictionary<Wave, int> Sizes = new();
                    private static Dictionary<Zone, string> names = new Dictionary<Zone, string>();
                    public static Dictionary<Camp, int> Camps { get; } = new Dictionary<Camp, int>();
                    private static readonly Dictionary<Spot, int> spots = new Dictionary<Spot, int>();
                    private static readonly Dictionary<Cell, Unit> standing = new Dictionary<Cell, Unit>();
                """)));
    }

    [Fact]
    public void a_class_keying_its_own_static_dictionary_is_refused()
    {
        // The dictionary is itself the setup: its type is made while the script loads, and reading Champion runs the setup that needs it.
        const string source = """
            using System.Collections.Generic;

            namespace Map
            {
                public sealed class Champion
                {
                    private static readonly Dictionary<Champion, int> kills = new Dictionary<Champion, int>();
                    public static int Count => kills.Count;
                }
            }
            """;
        Assert.Equal(new[] { "MW0056: new Dictionary<Champion, int>()" }, MapCode.CheckFile(source));
        Assert.StartsWith("'Dictionary<Champion, int>' is keyed by Champion, and the value of Champion.kills is static setup", MapCode.MessagesOfFile(source)[0]);
    }

    [Fact]
    public void a_new_anywhere_in_the_file_is_refused_as_the_type_is_made_while_the_script_loads()
    {
        Assert.Equal(
            new[] { "MW0056: new Dictionary<Quest, float>()", "MW0056: new Dictionary<Quest, int> { [first] = 1 }", "MW0056: new List<Dictionary<Quest, int>>()" },
            MapCode.CheckFile(Map("""
                    public static float Run(Quest first)
                    {
                        var times = new Dictionary<Quest, float>();
                        var done = new Dictionary<Quest, int> { [first] = 1 };
                        var all = new List<Dictionary<Quest, int>>();
                        return times.Count + done.Count + all.Count;
                    }
                """)));
    }

    [Fact]
    public void an_ordered_map_holds_a_dictionary_so_it_is_refused_too()
    {
        Assert.Equal(
            new[] { "MW0056: new OrderedMap<Quest, int>()", "MW0056: new Dictionary<string, OrderedMap<Wave, int>>()" },
            MapCode.CheckFile(Map("""
                    public static readonly OrderedMap<Quest, int> Progress = new OrderedMap<Quest, int>();
                    public static readonly Dictionary<string, OrderedMap<Wave, int>> Named = new Dictionary<string, OrderedMap<Wave, int>>();
                """)));
    }

    [Fact]
    public void a_key_class_without_static_setup_is_allowed()
    {
        Assert.Empty(MapCode.CheckFile(Map("""
                    public static readonly Dictionary<Plain, int> Plains = new Dictionary<Plain, int>();
                    public static readonly OrderedMap<Plain, int> Ordered = new OrderedMap<Plain, int>();
                    public static readonly Dictionary<Guard, int> Guards = new Dictionary<Guard, int>();
                    public static readonly Dictionary<Side, int> Sides = new Dictionary<Side, int>();
                    public static readonly Dictionary<string, int> Names = new Dictionary<string, int>();
                    public static readonly Dictionary<int, List<int>> Lists = new Dictionary<int, List<int>>();
                """)));
    }

    [Fact]
    public void the_classes_of_moonwell_are_allowed()
    {
        Assert.Empty(MapCode.CheckFile(Map("""
                    public static readonly Dictionary<Unit, int> Kills = new Dictionary<Unit, int>();
                    public static readonly Dictionary<Player, int> Gold = new Dictionary<Player, int>();
                    public static readonly Dictionary<Item, Unit> Carriers = new Dictionary<Item, Unit>();
                    public static readonly OrderedMap<Unit, float> Since = new OrderedMap<Unit, float>();
                    public static readonly Dictionary<Color, int> Counts = new Dictionary<Color, int>();
                    public static readonly Dictionary<unit, int> Handles = new Dictionary<unit, int>();
                """)));
    }

    [Fact]
    public void what_does_not_read_the_key_class_is_allowed()
    {
        Assert.Empty(MapCode.CheckFile(Map("""
                    public static readonly HashSet<Quest> Open = new HashSet<Quest>();
                    public static readonly OrderedSet<Quest> Ordered = new OrderedSet<Quest>();
                    public static readonly List<Quest> Listed = new List<Quest>();
                    public static readonly Dictionary<int, Quest> ById = new Dictionary<int, Quest>();
                    public static readonly Dictionary<string, List<Wave>> Waves = new Dictionary<string, List<Wave>>();

                    // No new is written, so no type is made while the script loads: LINQ makes its dictionary when it runs.
                    public static Dictionary<Quest, int> Count(List<Quest> quests) => quests.ToDictionary(quest => quest, quest => 0);
                """)));
    }

    [Fact]
    public void the_message_names_the_key_its_setup_and_the_ways_out()
    {
        var messages = MapCode.MessagesOfFile(Map("""
                    public static readonly Dictionary<Quest, int> Progress = new Dictionary<Quest, int>();
                    public static readonly Dictionary<Wave, int> Sizes = new();
                    public static readonly OrderedMap<Camp, int> Camps = new OrderedMap<Camp, int>();
                    private static readonly Dictionary<Spot, int> spots = new Dictionary<Spot, int>();
                """));
        Assert.Equal(4, messages.Count);
        Assert.StartsWith("'Dictionary<Quest, int>' is keyed by Quest, and the value of Quest.All is static setup", messages[0]);
        Assert.Contains("while the script loads", messages[0]);
        Assert.Contains("move it to another class, do it on first use, or key by something else", messages[0]);
        Assert.StartsWith("'Dictionary<Wave, int>' is keyed by Wave, and Wave's static constructor is static setup", messages[1]);
        // The setup is in the key's base class, which the read goes up to.
        Assert.StartsWith("'OrderedMap<Camp, int>' is keyed by Camp, and the value of Zone.First is static setup", messages[2]);
        Assert.StartsWith("'Dictionary<Spot, int>' is keyed by Spot, and the default value of Spot.Tint is static setup", messages[3]);
    }
}
