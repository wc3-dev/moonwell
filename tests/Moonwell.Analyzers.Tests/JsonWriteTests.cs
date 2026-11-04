namespace Moonwell.Analyzers.Tests;

/// <summary>MW0048 for a JSON tree every machine shares, changed inside Local.For, as for OrderedMap.</summary>
public class JsonWriteTests
{
    private const string Shared = """
            public static Moonwell.Json.JsonObject Settings = new Moonwell.Json.JsonObject();
            public static Moonwell.Json.JsonArray Items = new Moonwell.Json.JsonArray();
        """;

    [Fact]
    public void a_shared_tree_changed_inside_is_rejected()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                Settings["volume"] = 1;
                Settings.Add("name", "x");
                Settings.Remove("name");
                Items.Add(1);
                Items.RemoveAt(0);
                Items[0] = 2;
            });
            """, Shared);

        Assert.Equal(new[]
        {
            "MW0048: Settings[\"volume\"]",
            "MW0048: Settings",
            "MW0048: Settings",
            "MW0048: Items",
            "MW0048: Items",
            "MW0048: Items[0]",
        }, found);
    }

    [Fact]
    public void reading_a_shared_tree_or_changing_one_made_inside_is_allowed()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var volume = (int)Settings["volume"] + Items.Count;
                var text = Settings.ToJsonString();
                var made = new Moonwell.Json.JsonObject { ["a"] = 1 };
                made["b"] = 2;
                made.Add("c", 3);
            });
            """, Shared));
    }
}
