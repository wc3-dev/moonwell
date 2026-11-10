using Microsoft.CodeAnalysis.CSharp;
using Moonwell.Tool;

namespace Moonwell.Build.Tests
{
    /// <summary>The map's own Lua libraries: linked before the map's code, and called through bindings their comments type (decisions 255, 256).</summary>
    [Collection("Sample")]
    public sealed class LuaLibrariesTests : IDisposable
    {
        private const string Library = """
            ---Pushes a unit away.
            ---@param target unit The unit pushed
            ---@param distance number
            ---@param angle? number Degrees; away from the centre when nil
            ---@return boolean
            function Knockback.push(target, distance, angle)
                return true
            end

            ---@localsafe
            ---@param distance number
            ---@return number
            function Knockback.strength(distance)
                return distance / 2
            end

            ---@param onLand fun(u: unit, x: number): boolean
            function Knockback.onLand(onLand)
            end

            function SpawnShadow(x, y)
            end

            local function hidden() end

            function Knockback:method() end
            """;

        private readonly TestFolders folders;

        public LuaLibrariesTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_typed_function_turns_moonwells_objects_into_handles_and_back()
        {
            var text = Text();

            Assert.Contains("/// <summary>Pushes a unit away.</summary>\n        [global::Moonwell.LuaLibrary]\n        public static bool push(global::Moonwell.Unit target, float distance, float? angle = null) => push__lua(target.Handle, distance, angle);", text);
            Assert.Contains("/// @CSharpLua.Template = \"Knockback.push({0}, {1}, {2})\"\n        private static bool push__lua(global::Moonwell.unit target, float distance, float? angle)", text);
        }

        [Fact]
        public void a_function_marked_localsafe_is_written_without_the_mark()
        {
            var text = Text();

            Assert.Contains("/// <summary>Knockback.strength of lua/knockback.lua.</summary>\n        public static float strength(float distance)", text);
        }

        [Fact]
        public void a_callback_is_a_lambda_given_moonwells_objects()
        {
            var text = Text();

            Assert.Contains("public static void onLand(global::System.Func<global::Moonwell.Unit, float, bool> onLand) => onLand__lua((global::System.Func<global::Moonwell.unit?, float, bool>)((global::Moonwell.unit? a0, float a1) => onLand(global::Moonwell.Unit.Of(a0)!, a1)));", text);
        }

        [Fact]
        public void a_global_function_goes_under_its_files_class_untyped_and_methods_and_locals_are_left_out()
        {
            var text = Text();

            Assert.Contains("public static partial class KnockBack", text);
            Assert.Contains("public static void SpawnShadow(object x, object y) => SpawnShadow__lua(x, y);", text);
            Assert.DoesNotContain("hidden", text);
            Assert.DoesNotContain("method", text);
            Assert.Empty(CSharpSyntaxTree.ParseText(text).GetDiagnostics());
        }

        [Fact]
        public void a_map_calling_a_library_builds_with_the_library_before_its_code()
        {
            var project = folders.TemporaryCopyOf("Sample");
            Directory.CreateDirectory(Path.Combine(project, "lua"));
            File.WriteAllText(Path.Combine(project, "lua", "knock_back.lua"), "Knockback = {}\n" + Library);
            File.WriteAllText(
                Path.Combine(project, "Settings.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Settings : GameComponent\n    {\n        public override void OnGameStart()\n        {\n            var hero = Placed.Units.Hpal_0002!;\n            Lua.Knockback.push(hero, Lua.Knockback.strength(300f));\n            Lua.Knockback.onLand((u, x) => u.IsAlive);\n        }\n    }\n}\n");

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 0, error.ToString() + output);
            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            var library = script.IndexOf("function Knockback.push(target, distance, angle)", StringComparison.Ordinal);
            Assert.True(library > 0, "the library is in the script");
            Assert.True(library < script.IndexOf("Knockback.push(", library + 10, StringComparison.Ordinal), "and before the call");
            Assert.Contains("Knockback.strength(300", script);
        }

        private static string Text()
        {
            var classes = LuaLibraries.Bindings(Library.Split('\n'), "KnockBack", "lua/knockback.lua").ToList();
            return string.Join("\n", classes.Select(one => one.Table + ":\n" + one.Method)) + "\n" + Wrapped(classes);
        }

        // The methods in classes, as the written file has them, for the syntax check.
        private static string Wrapped(IEnumerable<(string Table, string Method)> classes)
        {
            return "public static partial class KnockBack {}\n" + string.Join("\n", classes.GroupBy(one => one.Table).Select(group => "public static partial class " + group.Key + " {\n" + string.Concat(group.Select(one => one.Method)) + "}"));
        }
    }
}
