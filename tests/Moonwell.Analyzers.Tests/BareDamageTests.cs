namespace Moonwell.Analyzers.Tests;

/// <summary>MW0048: a bare Damage(...) in a unit class, where the name is the declared field.</summary>
public class BareDamageTests
{
    [Fact]
    public void a_bare_damage_in_a_unit_class_is_an_error_beside_the_compilers()
    {
        Assert.Equal(new[] { "MW0048: Damage(target, 10f)", "MW0048: Damage(target, 10f)" }, MapCode.CheckBrokenFile("""
            using Moonwell;
            using Moonwell.Objects;

            namespace Map
            {
                public class Guard : Human.Units.Footman
                {
                    public void Hit(Unit target)
                    {
                        Damage(target, 10f);
                    }
                }

                public class Champion : Human.Units.Paladin
                {
                    public void Hit(Unit target)
                    {
                        Damage(target, 10f);
                    }
                }
            }
            """));
    }

    [Fact]
    public void the_message_names_the_fix()
    {
        var message = Assert.Single(MapCode.MessagesOfBrokenFile("""
            using Moonwell;
            using Moonwell.Objects;

            namespace Map
            {
                public class Guard : Human.Units.Footman
                {
                    public void Hit(Unit target)
                    {
                        Damage(target, 10f);
                    }
                }
            }
            """));

        Assert.Equal("'Damage' here is the unit's field, attack 1's base damage: write this.Damage(...) to deal damage", message);
    }

    [Fact]
    public void other_calls_are_left_alone()
    {
        Assert.Empty(MapCode.CheckFile("""
            using Moonwell;
            using Moonwell.Objects;

            namespace Map
            {
                public class Guard : Human.Units.Footman
                {
                    public void Hit(Unit target)
                    {
                        this.Damage(target, 10f);
                        target.Damage(this, 10f);
                    }
                }

                public class Tracked : Human.Units.Footman
                {
                    public new void Damage(Unit target, float amount)
                    {
                    }

                    public void Hit(Unit target)
                    {
                        Damage(target, 10f);
                    }
                }

                public static class Code
                {
                    private static void Damage(Unit target, float amount)
                    {
                    }

                    public static void Run(Unit target)
                    {
                        Damage(target, 10f);
                    }
                }
            }
            """));
    }

    [Fact]
    public void an_ability_field_is_not_the_method()
    {
        Assert.Empty(MapCode.CheckBrokenFile("""
            using Moonwell.Objects;

            namespace Map
            {
                public class Smite : Human.Abilities.StormBolt
                {
                    public void Run()
                    {
                        Damage(1);
                    }
                }
            }
            """));
    }
}
