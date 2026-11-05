namespace Moonwell.Analyzers.Tests;

/// <summary>MW0003 to MW0005: handle ids, hash loops and number types, anywhere in map code.</summary>
public class AnywhereTests
{
    [Fact]
    public void get_handle_id_is_rejected()
    {
        Assert.Equal(new[] { "MW0003: Natives.GetHandleId(u)", "MW0003: Natives.GetHandleIdBJ(u)" },
            MapCode.Check("var a = Natives.GetHandleId(u); var b = Natives.GetHandleIdBJ(u);"));
    }

    [Fact]
    public void a_function_that_desyncs_on_its_own_is_rejected_inside_and_outside()
    {
        Assert.Equal(new[]
        {
            "MW0006: Natives.SelectGroupForPlayerBJ(null!, p.Handle)",
            "MW0006: Natives.SelectGroupForPlayerBJ(null!, p.Handle)",
        }, MapCode.Check("""
            Natives.SelectGroupForPlayerBJ(null!, p.Handle);
            Local.For(p, () => Natives.SelectGroupForPlayerBJ(null!, p.Handle));
            """));
    }

    [Fact]
    public void a_desync_message_names_the_function()
    {
        Assert.Equal(
            "'SelectGroupForPlayerBJ' changes the game inside its own GetLocalPlayer() block, so it desyncs wherever it runs",
            MapCode.Message("Natives.SelectGroupForPlayerBJ(null!, p.Handle);"));
    }

    [Fact]
    public void looping_over_a_dictionary_or_hash_set_is_rejected()
    {
        var found = MapCode.Check("""
            var map = new Dictionary<unit, int>();
            var set = new HashSet<int>();
            foreach (var pair in map) { }
            foreach (var key in map.Keys) { }
            foreach (var item in set) { }
            var first = set.First();
            var copy = new List<int>(set);
            var text = string.Join(",", map.Values);
            """);

        Assert.Equal(new[]
        {
            "MW0004: map",
            "MW0004: map.Keys",
            "MW0004: set",
            "MW0004: set",
            "MW0004: set",
            "MW0004: map.Values",
        }, found);
    }

    [Fact]
    public void spreading_a_dictionary_or_hash_set_is_rejected()
    {
        var found = MapCode.Check("""
            var seen = new HashSet<int>();
            var map = new Dictionary<int, int>();
            var ordered = new List<int>();
            List<int> order = [.. seen];
            int[] keys = [.. map.Keys, 1];
            List<int> fine = [.. ordered];
            """);

        Assert.Equal(new[] { "MW0004: seen", "MW0004: map.Keys" }, found);
    }

    [Fact]
    public void a_number_type_in_a_local_function_an_event_a_delegate_a_base_list_or_a_local_with_no_declarator_is_rejected()
    {
        var found = MapCode.CheckFile("""
            using System;
            using System.Collections.Generic;

            namespace Map
            {
                public delegate double Curve(double t);

                public sealed class Scores : List<long>
                {
                }

                public sealed class Score
                {
                    public event Action<long>? Scored;

                    public static int Run(int n, object stored)
                    {
                        long Fact(int k) => k <= 1 ? 1 : k * Fact(k - 1);
                        if (long.TryParse("12", out var code) && stored is double rate)
                        {
                            return (int)(code % 7) + (int)Fact(n);
                        }

                        var (q, r) = (1L, 2);
                        return stored is ulong and var big ? 1 : 0;
                    }
                }
            }
            """);

        Assert.Equal(new[]
        {
            "MW0005: Curve",
            "MW0005: Scores",
            "MW0005: Scored",
            "MW0005: Fact",
            "MW0005: code",
            "MW0005: rate",
            "MW0005: q",
            "MW0005: 1L",
            "MW0005: big",
        }, found);
    }

    [Fact]
    public void lookups_and_ordered_loops_are_allowed()
    {
        Assert.Empty(MapCode.Check("""
            var map = new Dictionary<unit, int>();
            map[u] = 1;
            var has = map.ContainsKey(u) && map.TryGetValue(u, out var value) && new HashSet<int>().Contains(value);
            var list = new List<int> { 1, 2 };
            foreach (var item in list) { }
            var ordered = new OrderedSet<int>();
            foreach (var item in ordered.Snapshot()) { }
            """));
    }

    [Fact]
    public void number_types_the_game_cannot_carry_are_rejected()
    {
        var found = MapCode.Check(
            """
            long a = 1;
            var b = 1.5;
            var c = new List<double>();
            var d = (long)a;
            float e = (float)Math.Sqrt(4f);
            var f = 2u;
            """,
            """
                private static uint count;
                private static ulong Big(int x) => 0;
                private static void Take(double x) { }
                private enum Wide : long { One }
            """);

        Assert.Equal(new[]
        {
            "MW0005: a",
            "MW0005: b",
            "MW0005: 1.5",
            "MW0005: c",
            "MW0005: d",
            "MW0005: (long)a",
            "MW0005: f",
            "MW0005: 2u",
            "MW0005: count",
            "MW0005: Big",
            "MW0005: Take",
            "MW0005: Wide",
        }, found);
    }

    [Fact]
    public void a_number_type_message_says_what_to_use()
    {
        Assert.Equal("'uint' arithmetic becomes a float in the game's Lua; use int", MapCode.Message("", "private static uint count;"));
        Assert.Equal("'double' is a single-precision float in the game's Lua; use float", MapCode.Message("", "private static double rate;"));
        Assert.Equal("'long' is a 32-bit integer in the game's Lua; use int", MapCode.Message("", "private static long total;"));
    }

    [Theory]
    [InlineData("Hud.g.cs", "")]
    [InlineData("Hud.Designer.cs", "")]
    [InlineData("Hud.cs", "// <auto-generated/>\n")]
    public void a_map_file_that_looks_generated_is_checked(string path, string header)
    {
        // CSharp.lua compiles such a file like any other, and the game runs it.
        var found = MapCode.CheckFile(header + """
            using Moonwell;

            namespace Map
            {
                public static class Hud
                {
                    public static void Run(Player p, unit u)
                    {
                        var me = Natives.GetLocalPlayer();
                        Local.For(p, () => Natives.KillUnit(u));
                        var id = Natives.GetHandleId(u);
                        var day = System.Enum.Parse(typeof(System.DayOfWeek), "Monday");
                    }
                }
            }
            """, path);

        Assert.Equal(new[]
        {
            "MW0002: Natives.GetLocalPlayer()",
            "MW0001: Natives.KillUnit(u)",
            "MW0003: Natives.GetHandleId(u)",
            "MW0032: System.Enum.Parse(typeof(System.DayOfWeek), \"Monday\")",
        }, found);
    }

    [Fact]
    public void handles_joined_as_a_list_or_an_array_are_mw0002()
    {
        var found = MapCode.Check("""
            var list = new List<unit>();
            var units = new unit[1];
            var names = new List<string>();
            var a = string.Join(",", list);
            var b = string.Join(",", (object[])units);
            var c = string.Concat(units.AsEnumerable());
            var d = new System.Text.StringBuilder().AppendJoin(",", list);
            var e = string.Join(",", names);
            Local.For(p, () => p.Print(string.Join(",", list)));
            """);

        Assert.Equal(new[] { "MW0002: list", "MW0002: units", "MW0002: units.AsEnumerable()", "MW0002: list" }, found);
    }

    [Fact]
    public void the_keys_of_a_dictionary_interface_kept_in_a_variable_are_mw0004()
    {
        var found = MapCode.Check("""
            IDictionary<int, int> map = new SortedDictionary<int, int>();
            var keys = map.Keys;
            ICollection<int> values;
            values = map.Values;
            foreach (var key in keys) { }
            var dictionary = new Dictionary<int, int>();
            var own = dictionary.Keys;
            foreach (var key in own) { }
            """);

        Assert.Equal(new[] { "MW0004: map.Keys", "MW0004: map.Values", "MW0004: own" }, found);
    }
}
