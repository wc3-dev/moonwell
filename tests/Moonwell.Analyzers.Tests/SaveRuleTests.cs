namespace Moonwell.Analyzers.Tests;

/// <summary>MW0052: a save the build can write and read (decisions 180 and 183).</summary>
public class SaveRuleTests
{
    private static List<string> Check(string types)
    {
        return MapCode.CheckFile("using System.Collections.Generic;\nusing Moonwell;\n\nnamespace Map\n{\n" + types + "\n}\n");
    }

    [Fact]
    public void every_type_a_save_holds_is_clean()
    {
        var found = Check("""
            public enum Kind { Warrior, Mage }

            public sealed class Gear
            {
                [Saved(1)] public int Id;
                [Saved(2)] public List<Gear> Inside = new List<Gear>();
            }

            public abstract class Base : SaveGame
            {
                [Saved(1)] public int Level;
            }

            public sealed class RpgSave : Base
            {
                [Saved(2)] public float Speed;
                [Saved(3)] public bool Alive;
                [Saved(4)] public string? Name;
                [Saved(5)] public Kind Kind;
                [Saved(6)] public RealTime Claimed;
                [Saved(7)] public List<int> Items = new List<int>();
                [Saved(8)] public string[] Titles = new string[0];
                [Saved(9)] public Gear? Weapon;
                [Saved(10)] public Gear[] Bag = new Gear[0];
                [Saved(11)] public List<Kind> Past = new List<Kind>();
                [Saved(12)] public List<RealTime> Days { get; set; } = new List<RealTime>();
                [Saved(9999)] public int Last;
            }

            [SaveId("settings")]
            public sealed class Settings : SaveGame
            {
                [Saved(1)] public bool Music;
            }
            """);

        Assert.Empty(found);
    }

    [Fact]
    public void a_number_used_twice_is_rejected_also_across_a_base_class()
    {
        Assert.Equal(new[] { "MW0052: Gold" }, Check("""
            public sealed class RpgSave : SaveGame
            {
                [Saved(3)] public int Level;
                [Saved(3)] public int Gold;
            }
            """));
        Assert.Equal(new[] { "MW0052: Gold" }, Check("""
            public abstract class Base : SaveGame
            {
                [Saved(3)] public int Level;
            }

            public sealed class RpgSave : Base
            {
                [Saved(3)] public int Gold;
            }
            """));
    }

    [Fact]
    public void a_number_below_1_or_past_9999_is_rejected()
    {
        Assert.Equal(new[] { "MW0052: Saved(0)", "MW0052: Saved(10000)" }, Check("""
            public sealed class RpgSave : SaveGame
            {
                [Saved(0)] public int Level;
                [Saved(10000)] public int Gold;
            }
            """));
    }

    [Fact]
    public void a_type_that_cannot_be_saved_is_rejected()
    {
        Assert.Equal(new[] { "MW0052: Gold", "MW0052: Hero", "MW0052: Maybe", "MW0052: Nested" }, Check("""
            public sealed class RpgSave : SaveGame
            {
                [Saved(1)] public Dictionary<int, int> Gold = new Dictionary<int, int>();
                [Saved(2)] public Unit? Hero;
                [Saved(3)] public int? Maybe;
                [Saved(4)] public List<List<int>> Nested = new List<List<int>>();
            }
            """));
        Assert.Contains("which cannot be saved", MapCode.MessagesOfFile("using System.Collections.Generic;\nusing Moonwell;\nnamespace Map\n{\npublic sealed class RpgSave : SaveGame\n{\n[Saved(1)] public Dictionary<int, int> Gold = new Dictionary<int, int>();\n}\n}\n")[0]);
    }

    [Fact]
    public void a_member_the_build_cannot_reach_is_rejected()
    {
        Assert.Equal(new[] { "MW0052: Gold", "MW0052: Level", "MW0052: Speed", "MW0052: Kept" }, Check("""
            public sealed class RpgSave : SaveGame
            {
                [Saved(1)] private int Gold;
                [Saved(2)] public readonly int Level;
                [Saved(3)] public float Speed { get; private set; }
                [Saved(4)] public int Kept { get; init; }

                public int Total => Gold + Level;
            }
            """));
    }

    [Fact]
    public void saved_outside_a_save_or_its_parts_is_rejected()
    {
        Assert.Equal(new[] { "MW0052: Level" }, Check("""
            public sealed class Loose
            {
                [Saved(1)] public int Level;
            }
            """));
    }

    [Fact]
    public void a_save_or_a_part_without_a_constructor_without_parameters_is_rejected()
    {
        Assert.Equal(new[] { "MW0052: Gear" }, Check("""
            public sealed class Gear
            {
                public Gear(int id) { Id = id; }

                [Saved(1)] public int Id;
            }

            public sealed class RpgSave : SaveGame
            {
                [Saved(1)] public Gear? Weapon;
            }
            """));
    }

    [Fact]
    public void two_save_classes_without_an_id_are_rejected()
    {
        Assert.Equal(new[] { "MW0052: Settings" }, Check("""
            public sealed class RpgSave : SaveGame
            {
                [Saved(1)] public int Level;
            }

            public sealed class Settings : SaveGame
            {
                [Saved(1)] public bool Music;
            }
            """));
    }

    [Fact]
    public void two_classes_with_one_id_or_a_bad_id_are_rejected()
    {
        Assert.Equal(new[] { "MW0052: SaveId(\"a\")", "MW0052: SaveId(\"b c\")" }, Check("""
            [SaveId("a")]
            public sealed class First : SaveGame
            {
                [Saved(1)] public int Level;
            }

            [SaveId("a")]
            public sealed class Second : SaveGame
            {
                [Saved(1)] public int Level;
            }

            [SaveId("b c")]
            public sealed class Third : SaveGame
            {
                [Saved(1)] public int Level;
            }
            """));
    }
    [Fact]
    public void a_save_or_a_part_the_build_cannot_name_is_rejected()
    {
        Assert.Equal(new[] { "MW0052: Hidden", "MW0052: Kept", "MW0052: Many", "MW0052: Inner" }, Check("""
            public sealed class Outer
            {
                private sealed class Hidden : SaveGame
                {
                    [Saved(1)] public Kept? Part;
                }

                protected sealed class Kept
                {
                    [Saved(1)] public int Id;
                }
            }

            [SaveId("many")]
            public sealed class Many<T> : SaveGame
            {
                [Saved(1)] public int Level;
            }

            public sealed class Generic<T>
            {
                [SaveId("inner")]
                public sealed class Inner : SaveGame
                {
                    [Saved(1)] public int Level;
                }
            }
            """));
        Assert.Equal(new[] { "MW0052: Local" }, MapCode.CheckFile("using Moonwell;\nnamespace Map\n{\nfile sealed class Local : SaveGame\n{\n[Saved(1)] public int Level;\n}\n}\n", "Saves.cs"));
    }

    [Fact]
    public void a_member_the_build_cannot_write_is_rejected()
    {
        Assert.Equal(new[] { "MW0052: this", "MW0052: Level", "MW0052: Gold", "MW0052: Name" }, Check("""
            public abstract class Base : SaveGame
            {
                [Saved(1)] public int Level;
            }

            public sealed class RpgSave : Base
            {
                [Saved(2)] public int this[int slot] { get => slot; set { } }
                [Saved(3)] public new int Level;
                [Saved(4)] public required int Gold;
            }

            [SaveId("named")]
            public sealed class Named : SaveGame
            {
                public required string Name { get; set; }
            }
            """));
    }

    [Fact]
    public void an_override_keeps_its_bases_saved_number()
    {
        Assert.Empty(Check("""
            public abstract class Base : SaveGame
            {
                [Saved(1)] public abstract int Level { get; set; }
                [Saved(2)] public virtual int Gold { get; set; }
            }

            public sealed class RpgSave : Base
            {
                public override int Level { get; set; }
                [Saved(2)] public override int Gold { get; set; }
                [Saved(3)] public int @class;
            }
            """));
    }
}
