namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001 for a Lua library's function inside Local.For, which the analyzer cannot see into (decision 256).</summary>
public class LuaLibraryTests
{
    private const string Bindings = """
            public static class Knockback
            {
                [LuaLibrary]
                public static void push(float distance) => push__lua(distance);

                public static float strength(float distance) => strength__lua(distance);

                private static void push__lua(float distance) { }

                private static float strength__lua(float distance) => distance;
            }
        """;

    [Fact]
    public void a_library_function_inside_local_for_is_reported()
    {
        var found = MapCode.Check("Local.For(p, () => Knockback.push(1f));", Bindings);

        Assert.Equal(new[] { "MW0001: Knockback.push(1f)" }, found);
    }

    [Fact]
    public void a_function_its_lua_marks_local_safe_is_not()
    {
        Assert.Empty(MapCode.Check("Local.For(p, () => Knockback.strength(1f));", Bindings));
    }

    [Fact]
    public void a_library_function_outside_local_for_is_not()
    {
        Assert.Empty(MapCode.Check("Knockback.push(1f);", Bindings));
    }
}
