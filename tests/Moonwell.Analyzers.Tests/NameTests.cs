namespace Moonwell.Analyzers.Tests;

/// <summary>MW0044: a name of the map's that the game's Lua takes for a native's, or for one of Moonwell's.</summary>
public class NameTests
{
    [Theory]
    [InlineData("namespace Rect { public class Shape { } }")]
    [InlineData("namespace Rect.Shapes { public class Shape { } }")]
    [InlineData("namespace Rect;\npublic class Shape { }")]
    public void a_namespace_whose_first_name_is_a_native_is_an_error(string source)
    {
        Assert.Equal(new[] { "MW0044: Rect" }, MapCode.CheckFile(source));
    }

    [Fact]
    public void a_namespace_whose_first_name_is_a_function_of_lua_is_an_error()
    {
        Assert.Equal(new[] { "MW0044: print" }, MapCode.CheckFile("namespace print { public class Shape { } }"));
    }

    [Theory]
    [InlineData("namespace Map.Rect { public class Shape { } }")]
    [InlineData("namespace Map { namespace Rect { public class Shape { } } }")]
    [InlineData("namespace math { public class Shape { } }")]
    public void a_namespace_the_lua_can_hold_passes(string source)
    {
        Assert.Empty(MapCode.CheckFile(source));
    }

    [Theory]
    [InlineData("public class Rect { }", "Rect")]
    [InlineData("public struct Location { }", "Location")]
    [InlineData("public enum Player { One }", "Player")]
    [InlineData("public interface Filter { }", "Filter")]
    [InlineData("public class print { }", "print")]
    [InlineData("public class math { }", "math")]
    public void a_type_in_no_namespace_named_like_a_global_is_an_error(string source, string name)
    {
        Assert.Equal(new[] { "MW0044: " + name }, MapCode.CheckFile(source));
    }

    [Fact]
    public void a_type_in_a_namespace_named_like_a_native_passes()
    {
        Assert.Empty(MapCode.CheckFile("""
            namespace Map
            {
                public class Rect { }

                public class Location
                {
                    public class Filter { }
                }
            }
            """));
    }

    [Fact]
    public void a_type_with_the_full_name_of_a_moonwell_type_is_an_error()
    {
        Assert.Equal(new[] { "MW0044: Hero" }, MapCode.CheckFile("namespace Moonwell { public class Hero { } }"));
    }

    [Fact]
    public void an_internal_type_of_moonwell_counts()
    {
        Assert.Equal(new[] { "MW0044: Sweep" }, MapCode.CheckFile("namespace Moonwell { public class Sweep { } }"));
    }

    [Fact]
    public void a_new_name_in_moonwells_namespace_passes()
    {
        Assert.Empty(MapCode.CheckFile("namespace Moonwell { public class MyOwnThing { } }"));
    }

    [Theory]
    [InlineData("public static float Cos(float x) { return 0f; }")]
    [InlineData("private static void Cos(int a, int b) { }")]
    public void a_native_called_in_a_class_with_a_method_of_its_name_is_an_error(string method)
    {
        Assert.Equal(new[] { "MW0044: Natives.Cos(1f)" }, MapCode.Check("var a = Natives.Cos(1f);", method));
    }

    [Fact]
    public void an_instance_method_counts()
    {
        Assert.Equal(new[] { "MW0044: Natives.Cos(1f)" }, MapCode.CheckFile("""
            using Moonwell;

            namespace Map
            {
                public class Reader
                {
                    public float Cos() { return 0f; }

                    public float Read() { return Natives.Cos(1f); }
                }
            }
            """));
    }

    [Fact]
    public void a_method_is_seen_from_a_lambda()
    {
        Assert.Equal(new[] { "MW0044: Natives.Cos(x)" }, MapCode.Check(
            "Func<float, float> run = x => Natives.Cos(x);",
            "private static float Cos(float x) { return 0f; }"));
    }

    [Fact]
    public void a_method_of_an_outer_class_shadows_too()
    {
        Assert.Equal(new[] { "MW0044: Natives.Cos(1f)" }, MapCode.CheckFile("""
            using Moonwell;

            namespace Map
            {
                public class Outer
                {
                    public float Cos(float x) { return x; }

                    public class Inner
                    {
                        public float Read() { return Natives.Cos(1f); }
                    }
                }
            }
            """));
    }

    [Theory]
    [InlineData("var Cos = 1f; var a = Natives.Cos(Cos);", "Natives.Cos(Cos)")]
    [InlineData("float Cos(float x) => x; var a = Natives.Cos(1f) + Cos(1f);", "Natives.Cos(1f)")]
    public void a_local_or_a_local_function_of_a_natives_name_is_an_error(string body, string call)
    {
        Assert.Equal(new[] { "MW0044: " + call }, MapCode.Check(body));
    }

    [Fact]
    public void a_parameter_of_a_natives_name_is_an_error()
    {
        Assert.Equal(new[] { "MW0044: Natives.Cos(Cos)" }, MapCode.Check("", "private static float Read(float Cos) { return Natives.Cos(Cos); }"));
    }

    [Fact]
    public void a_local_made_after_the_call_passes()
    {
        Assert.Empty(MapCode.Check("var a = Natives.Cos(1f); var Cos = 2f; var b = Cos;"));
    }

    [Fact]
    public void a_method_of_a_base_class_shadows_nothing()
    {
        Assert.Empty(MapCode.CheckFile("""
            using Moonwell;

            namespace Map
            {
                public class Base
                {
                    public float Cos(float x) { return x; }
                }

                public class Derived : Base
                {
                    public float Read() { return Natives.Cos(1f) + Cos(1f); }
                }
            }
            """));
    }

    [Fact]
    public void a_field_a_property_or_another_class_shadows_nothing()
    {
        Assert.Empty(MapCode.CheckFile("""
            using Moonwell;

            namespace Map
            {
                public class Reader
                {
                    private static float Cos = 1f;

                    private static float Sin => 2f;

                    public float Read() { return Natives.Cos(1f) + Natives.Sin(1f) + Cos + Sin + Other.Tan(1f) + Natives.Tan(1f); }
                }

                public static class Other
                {
                    public static float Tan(float x) { return x; }
                }
            }
            """));
    }

    [Fact]
    public void the_messages_say_what_happens_and_what_to_do()
    {
        Assert.Equal(new[]
        {
            "'Rect' is a function of the game's Lua, and a namespace's first name is a global there, so the script stops while it loads: rename the namespace",
        }, MapCode.MessagesOfFile("namespace Rect { public class Shape { } }"));
        Assert.Equal(new[]
        {
            "'Rect' is a global of the game's Lua, and a type in no namespace is a global there too, so the script stops while it loads: put the type in a namespace",
        }, MapCode.MessagesOfFile("public class Rect { }"));
        Assert.Equal(new[]
        {
            "'Moonwell.Hero' is a type of Moonwell, and the game's Lua cannot hold two of one full name: rename it or use another namespace",
        }, MapCode.MessagesOfFile("namespace Moonwell { public class Hero { } }"));
        Assert.Equal(
            "'Natives.Cos' runs the method Cos in the game's Lua, not the native, as the two share a name there: rename the method",
            MapCode.Message("var a = Natives.Cos(1f);", "private static float Cos(float x) { return 0f; }"));
        Assert.Equal(
            "'Natives.Cos' runs the local Cos in the game's Lua, not the native, as the two share a name there: rename the local",
            MapCode.Message("var Cos = 1f; var a = Natives.Cos(Cos);"));
    }

    [Theory]
    [InlineData("Probe.g.cs", "")]
    [InlineData("Probe.cs", "// <auto-generated/>\n")]
    public void a_map_file_that_looks_generated_is_checked(string path, string header)
    {
        Assert.Equal(new[] { "MW0044: Rect" }, MapCode.CheckFile(header + "namespace Rect { public class Shape { } }", path));
    }
}
