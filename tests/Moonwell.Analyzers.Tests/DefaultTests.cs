namespace Moonwell.Analyzers.Tests;

/// <summary>MW0037: Default&lt;T&gt;() is declared on Widget, so C# takes any widget class through Unit, Hero or Item.</summary>
public class DefaultTests
{
    private const string Classes = """
            using Moonwell;
            using Moonwell.Objects;

            namespace Map
            {
                public class Guard : Human.Units.Footman { }

                public class Champion : Human.Units.Paladin { }

                public class Sentry : Items.ClawsOfAttack15 { }

                public class Crate : Destructables.Barrel { }

                public static class Code
                {
                    public static void Run()
                    {
            """;

    private const string End = """
                    }
                }
            }
            """;

    [Fact]
    public void a_class_of_the_kind_the_call_names_is_fine()
    {
        Assert.Empty(MapCode.CheckFile(Classes + """
                        var guard = Unit.Default<Guard>();
                        var champion = Unit.Default<Champion>();
                        var hero = Hero.Default<Champion>();
                        var sentry = Item.Default<Sentry>();
                        var any = Widget.Default<Sentry>();
            """ + End));
    }

    [Fact]
    public void a_class_of_another_kind_is_an_error()
    {
        Assert.Equal(new[]
        {
            "MW0037: Item.Default<Guard>()",
            "MW0037: Unit.Default<Sentry>()",
            "MW0037: Hero.Default<Guard>()",
        }, MapCode.CheckFile(Classes + """
                        var a = Item.Default<Guard>();
                        var b = Unit.Default<Sentry>();
                        var c = Hero.Default<Guard>();
            """ + End));
    }

    [Fact]
    public void a_destructable_class_is_an_error_through_any_kind()
    {
        Assert.Equal(new[]
        {
            "MW0037: Widget.Default<Crate>()",
            "MW0037: Destructable.Default<Crate>()",
        }, MapCode.CheckFile(Classes + """
                        var a = Widget.Default<Crate>();
                        var b = Destructable.Default<Crate>();
            """ + End));
    }

    [Fact]
    public void the_message_names_the_kind_to_write()
    {
        Assert.Equal(new[]
        {
            "Guard is a unit class, not an item class: write Unit.Default<Guard>()",
            "Crate is a destructable class, which has no default object: only unit and item classes have one",
        }, MapCode.MessagesOfFile(Classes + """
                        var a = Item.Default<Guard>();
                        var b = Widget.Default<Crate>();
            """ + End));
    }

    [Fact]
    public void a_set_on_a_default_object_is_mw0054()
    {
        var found = MapCode.CheckFile(Classes + """
                        Unit.Default<Guard>().MaxLife = 500;
                        Item.Default<Sentry>().Charges += 1;
                        var champion = Unit.Default<Champion>();
                        champion.Default.MaxMana = 10;
                        var life = Unit.Default<Guard>().MaxLife;
                        (Unit.Default<Guard>().MaxMana, life) = (5, 6);
                        champion.MaxMana = 20;
            """ + End);

        Assert.Equal(new[]
        {
            "MW0054: Unit.Default<Guard>().MaxLife",
            "MW0054: Item.Default<Sentry>().Charges",
            "MW0054: champion.Default.MaxMana",
            "MW0054: Unit.Default<Guard>().MaxMana",
        }, found);
    }

    [Fact]
    public void a_set_on_its_own_default_in_a_declared_constructor_is_mw0012_alone()
    {
        var found = MapCode.CheckFile("""
            using Moonwell;
            using Moonwell.Objects;

            namespace Map
            {
                public class Guard : Human.Units.Footman
                {
                    public Guard()
                    {
                        Default.MaxLife = 5;
                        Unit.Default<Guard>().MaxMana = 5;
                    }
                }
            }
            """);

        Assert.All(found, line => Assert.StartsWith("MW0012", line));
        Assert.Equal(2, found.Count);
    }
}
