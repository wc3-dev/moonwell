using System.Reflection;

namespace Moonwell.Analyzers.Tests;

/// <summary>MW0049: Set("id", value) with a value its field does not hold, or an id its class does not have, as the build reports it.</summary>
public class SetByIdTests
{
    private const string Footman = "Moonwell.Objects.Human.Units.Footman";
    private const string StormBolt = "Moonwell.Objects.Human.Abilities.StormBolt";
    private const string Paladin = "Moonwell.Objects.Human.Units.Paladin";

    public static IEnumerable<object[]> Wrong()
    {
        yield return new object[] { "a float on an int", Footman, "Set(\"ua1b\", 1.5f);" };
        yield return new object[] { "an int on a float", Footman, "Set(\"uacq\", 5);" };
        yield return new object[] { "text on a shared field", Footman, "Set(\"uhpm\", \"many\");" };
        yield return new object[] { "a float on a shared int", Footman, "Set(\"uhpm\", 5f);" };
        yield return new object[] { "null", Footman, "Set(\"unam\", null);" };
        yield return new object[] { "a number on a choice", Footman, "Set(\"ua1w\", 4);" };
        yield return new object[] { "the number of a choice", Footman, "Set(\"ua1w\", (int)WeaponType.Missile);" };
        yield return new object[] { "another choice", Footman, "Set(\"ua1w\", MoveType.Foot);" };
        yield return new object[] { "one value on a field of levels", StormBolt, "Set(\"acdn\", 3f);" };
        yield return new object[] { "levels of another type", StormBolt, "Set(\"acdn\", Levels(1, 2));" };
        yield return new object[] { "levels on a field without", Footman, "Set(\"ua1b\", Levels(1, 2));" };
        yield return new object[] { "a level on a field without", Footman, "Set(\"ua1b\", 2, 5);" };
        yield return new object[] { "level 0", StormBolt, "Set(\"acdn\", 0, 1f);" };
        yield return new object[] { "the wrong type at a level", StormBolt, "Set(\"acdn\", 2, 1);" };
        yield return new object[] { "null at a level", StormBolt, "Set(\"acdn\", 2, null);" };
        yield return new object[] { "an unknown id", Footman, "Set(\"zzzz\", 1);" };
        yield return new object[] { "a hero's field on a unit", Footman, "Set(\"ustr\", 1);" };
        yield return new object[] { "an unknown id on an ability", StormBolt, "Set(\"zzzz\", 1);" };
        yield return new object[] { "a unit's id on an ability", StormBolt, "Set(\"uhpm\", 1);" };
        yield return new object[] { "another ability's field", StormBolt, "Set(\"Hhb1\", 1, 5f);" };
    }

    public static IEnumerable<object[]> Right()
    {
        yield return new object[] { "an int", Footman, "Set(\"ua1b\", 5);" };
        yield return new object[] { "a float", Footman, "Set(\"uacq\", 5f);" };
        yield return new object[] { "a shared field", Footman, "Set(\"uhpm\", 500);" };
        yield return new object[] { "a hero's field on a hero", Paladin, "Set(\"ustr\", 20);" };
        yield return new object[] { "a choice", Footman, "Set(\"ua1w\", WeaponType.Missile);" };
        yield return new object[] { "a core choice", Footman, "Set(\"ua1t\", AttackType.Magic);" };
        yield return new object[] { "flags", Footman, "Set(\"ua1g\", Targets.Air | Targets.Ground);" };
        yield return new object[] { "a choice at a level", StormBolt, "Set(\"atar\", 2, Targets.Air);" };
        yield return new object[] { "levels", StormBolt, "Set(\"acdn\", Levels(1f, 2f));" };
        yield return new object[] { "a value at a level", StormBolt, "Set(\"acdn\", 2, 1f);" };
        yield return new object[] { "a value typed object", Footman, "object value = 5; Set(\"ua1b\", value);" };
        yield return new object[] { "an ability's own field", StormBolt, "Set(\"Htb1\", 2, 250f);" };
    }

    [Theory]
    [MemberData(nameof(Wrong))]
    public void the_analyzer_gives_the_text_the_build_gives(string name, string @base, string statements)
    {
        var source = FieldRulesTests.File(FieldRulesTests.Class("Subject", @base, statements));

        var analyzer = Assert.Single(MapCode.MessagesOfFile(source));
        var thrown = Record.Exception(() => Construct(MapCode.Load(source)));

        Assert.True(thrown != null, name + ": the build allows it, the analyzer says " + analyzer);
        Assert.StartsWith(analyzer, thrown!.Message);
        Assert.Equal(new[] { "MW0049" }, MapCode.CheckFile(source).Select(found => found.Substring(0, 6)));
    }

    [Theory]
    [MemberData(nameof(Right))]
    public void what_the_build_allows_gives_nothing(string name, string @base, string statements)
    {
        var source = FieldRulesTests.File(FieldRulesTests.Class("Subject", @base, statements));

        Assert.Empty(MapCode.CheckFile(source));
        var thrown = Record.Exception(() => Construct(MapCode.Load(source)));
        Assert.True(thrown == null, name + ": the build says " + thrown?.Message);
    }

    [Fact]
    public void the_value_is_what_is_pointed_at()
    {
        var source = FieldRulesTests.File(FieldRulesTests.Class("Subject", Footman, "Set(\"ua1b\", 1.5f);"));

        Assert.Equal(new[] { "MW0049: 1.5f" }, MapCode.CheckFile(source));
    }

    [Fact]
    public void a_computed_id_or_a_value_of_no_type_known_here_is_left_to_the_build()
    {
        Assert.Empty(MapCode.CheckFile(FieldRulesTests.File("""
            public class Computed : Moonwell.Objects.Human.Units.Footman
            {
                private static string Id() => "ua1b";

                private static object Value() => 1.5f;

                public Computed(int? number)
                {
                    Set(Id(), 1.5f);
                    Set("ua1b", Value());
                    Set("ua1b", number!);
                }
            }
            """)));
    }

    [Fact]
    public void a_set_that_is_not_the_objects_set_is_left_alone()
    {
        Assert.Empty(MapCode.CheckFile(FieldRulesTests.File("""
            public class Plain
            {
                public Plain()
                {
                    Set("ua1b", 1.5f);
                    Set("ua1w", WeaponType.Missile, 1);
                }

                private static void Set(string field, object value) { }

                private static void Set(string field, WeaponType type, int level) { }
            }
            """)));
    }

    private static void Construct(Assembly assembly)
    {
        try
        {
            Activator.CreateInstance(assembly.GetType("Map.Subject")!);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }
}
