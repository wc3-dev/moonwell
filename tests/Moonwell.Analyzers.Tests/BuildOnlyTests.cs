namespace Moonwell.Analyzers.Tests;

/// <summary>MW0045: a member only the code the build writes calls. MW0046: new of a class whose objects the game has.</summary>
public class BuildOnlyTests
{
    [Fact]
    public void a_call_the_build_writes_is_an_error_in_a_map()
    {
        Assert.Equal(new[]
        {
            "MW0045: Runtime.Start(map)",
            "MW0045: Runtime.Start(map, () => { })",
            "MW0045: ObjectTypes.Id<Guard>(1)",
            "MW0045: ObjectTypes.BuffDisplay<Burn>(1, 2)",
            "MW0045: ObjectTypes.Unit<Guard>(1, () => null!)",
            "MW0045: ObjectTypes.Item<Sword>(1, () => null!)",
            "MW0045: ObjectTypes.Destructable<Gate>(1, () => null!)",
            "MW0045: ObjectTypes.Ability<Bolt>(1, () => null!)",
            "MW0045: DamageSystem.Install()",
            "MW0045: Terrain.Install(1, 1, 0f, 0f, \"\")",
            "MW0045: AreaSystem.Install(names, numbers)",
            "MW0045: InputSystem.Install(false, false, names, numbers)",
            "MW0045: Physics.Install(\"\", \"\")",
        }, MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Settings : Map { }
                public class Guard : Unit { }
                public class Sword : Item { }
                public class Gate : Destructable { }
                public class Bolt : Ability { }
                public class Burn : Buff { }

                public static class Code
                {
                    public static void Run(Settings map, string[] names, float[] numbers)
                    {
                        Runtime.Start(map);
                        Runtime.Start(map, () => { });
                        ObjectTypes.Id<Guard>(1);
                        ObjectTypes.BuffDisplay<Burn>(1, 2);
                        ObjectTypes.Unit<Guard>(1, () => null!);
                        ObjectTypes.Item<Sword>(1, () => null!);
                        ObjectTypes.Destructable<Gate>(1, () => null!);
                        ObjectTypes.Ability<Bolt>(1, () => null!);
                        DamageSystem.Install();
                        Terrain.Install(1, 1, 0f, 0f, "");
                        AreaSystem.Install(names, numbers);
                        InputSystem.Install(false, false, names, numbers);
                        Physics.Install("", "");
                    }
                }
            }
            """));
    }

    [Fact]
    public void a_method_group_of_one_is_an_error()
    {
        Assert.Equal(new[] { "MW0045: Runtime.Start" }, MapCode.CheckFile("""
            using System;
            using Moonwell;

            namespace MyMap
            {
                public static class Code
                {
                    public static Action<Map> Run()
                    {
                        return Runtime.Start;
                    }
                }
            }
            """));
    }

    [Fact]
    public void what_the_declared_classes_hide_is_an_error_too()
    {
        Assert.Equal(new[] { "MW0045: Declare(\"uhpm\", 10)", "MW0045: DeclaredValue<int>(\"uhpm\")" }, MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit
                {
                    public int Run()
                    {
                        Declare("uhpm", 10);
                        return DeclaredValue<int>("uhpm");
                    }
                }
            }
            """));
    }

    [Fact]
    public void the_code_the_build_writes_is_left_alone()
    {
        Assert.Empty(MapCode.CheckFile("""
            namespace MyMap
            {
                public class Settings : Moonwell.Map { }
                public class Guard : Moonwell.Unit { }
            }

            namespace Moonwell.Generated
            {
                public static class ObjectRegistry
                {
                    public static void Register()
                    {
                        global::Moonwell.ObjectTypes.Id<global::MyMap.Guard>(1);
                        global::Moonwell.ObjectTypes.Unit<global::MyMap.Guard>(1, () => new global::MyMap.Guard());
                    }
                }

                public static class MapMain
                {
                    public static void Main()
                    {
                        global::Moonwell.Runtime.Start(new global::MyMap.Settings(), () => { global::Moonwell.DamageSystem.Install(); global::Moonwell.Generated.ObjectRegistry.Register(); });
                    }
                }
            }
            """));
    }

    [Fact]
    public void a_maps_ids_stay_open()
    {
        Assert.Empty(MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit { }

                public static class Code
                {
                    public static int Run()
                    {
                        return ObjectTypes.FourCC("hfoo") + ObjectTypes.IdOf<Guard>();
                    }
                }
            }
            """));
    }

    [Fact]
    public void new_of_a_class_the_game_has_is_an_error()
    {
        Assert.Equal(new[]
        {
            "MW0046: new Unit()",
            "MW0046: new Guard()",
            "MW0046: new()",
            "MW0046: new Sword()",
            "MW0046: new Gate()",
            "MW0046: new Bolt()",
            "MW0046: new Tree()",
            "MW0046: new Armor()",
            "MW0046: new T()",
        }, MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit { }
                public class Sword : Item { }
                public class Gate : Destructable { }
                public class Bolt : Ability { }
                public class Tree : Doodad { }
                public class Armor : Upgrade { }

                public static class Code
                {
                    public static void Run()
                    {
                        var plain = new Unit();
                        var guard = new Guard();
                        Guard second = new();
                        var sword = new Sword();
                        var gate = new Gate();
                        var bolt = new Bolt();
                        var tree = new Tree();
                        var armor = new Armor();
                    }

                    public static T Make<T>()
                        where T : Unit, new()
                    {
                        return new T();
                    }
                }
            }
            """));
    }

    [Fact]
    public void the_message_names_the_right_call()
    {
        var messages = MapCode.MessagesOfFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Guard : Unit { }
                public class Sword : Item { }

                public static class Code
                {
                    public static void Run()
                    {
                        var guard = new Guard();
                        var sword = new Sword();
                    }
                }
            }
            """);
        Assert.Contains("Unit.Create<Guard>(player, x, y, scope)", messages[0]);
        Assert.Contains("Unit.Default<Guard>()", messages[0]);
        Assert.Contains("Item.Create<Sword>(x, y, scope)", messages[1]);
    }

    [Fact]
    public void the_classes_a_map_makes_stay_open()
    {
        Assert.Empty(MapCode.CheckFile("""
            using Moonwell;

            namespace MyMap
            {
                public class Burn : Buff { }
                public class KillCounter : UnitComponent { }
                public class Rules : GameComponent { }
                public class ShopFloor : Area { }
                public class Well : Gizmo { }
                public class Firebolt : Missile { }

                public static class Code
                {
                    public static void Run(Unit unit)
                    {
                        unit.Apply(new Burn());
                        unit.Add(new KillCounter());
                        Game.Instance.Add(new Rules());
                        Game.Instance.Add(new ShopFloor());
                        var well = new Well();
                        var bolt = new Firebolt();
                    }
                }
            }
            """));
    }
}
