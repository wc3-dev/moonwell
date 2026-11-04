using System.Text;

namespace Moonwell.Build.Tests
{
    [Collection("Sample")]
    public sealed class LuaLoadTests
    {
        private static readonly string[] Files = { "Probe.cs" };

        [Fact]
        public void a_script_lua_loads_passes()
        {
            LuaLoad.Check("--@mw 1:3\nlocal a = 1\n", Files);
        }

        [Fact]
        public void too_many_locals_names_the_line_of_the_marker_above()
        {
            var script = new StringBuilder("--@mw 1:5\nlocal function f()\n");
            for (var i = 0; i < 201; i++)
            {
                script.Append("--@mw 1:").Append(10 + i).Append("\nlocal v").Append(i).Append(" = 1\n");
            }

            script.Append("end\n");

            var error = Assert.Throws<BuildException>(() => LuaLoad.Check(script.ToString(), Files));

            Assert.Equal(
                "Probe.cs:210: Lua allows 200 locals in a function, and the method around this line has more: each parameter, local variable and object or collection initializer is one, and so are some values the compiler keeps. Split the method",
                error.Message);
        }

        [Fact]
        public void another_load_error_names_its_line_and_luas_message()
        {
            var error = Assert.Throws<BuildException>(() => LuaLoad.Check("--@mw 1:7\nbreak\n", Files));

            Assert.StartsWith("Probe.cs:7: the game's Lua could not load the script CSharp.lua wrote for this line (", error.Message);
            Assert.Contains("break outside loop", error.Message);
        }

        [Fact]
        public void a_break_outside_a_loop_names_its_own_line_not_the_functions_end()
        {
            var error = Assert.Throws<BuildException>(() => LuaLoad.Check("--@mw 1:7\nbreak\n--@mw 1:9\nlocal a = 1\n", Files));

            Assert.StartsWith("Probe.cs:7: ", error.Message);
        }

        [Fact]
        public void an_error_with_no_marker_above_names_the_scripts_line()
        {
            var error = Assert.Throws<BuildException>(() => LuaLoad.Check("local a = 1\nlocal = 2\n", Files));

            Assert.StartsWith("war3map.lua:2: the game's Lua could not load the script", error.Message);
        }

        [Fact]
        public void a_marker_of_no_line_counts_as_none()
        {
            var error = Assert.Throws<BuildException>(() => LuaLoad.Check("--@mw 1:4\nlocal a = 1\n--@mw -\nlocal = 2\n", Files));

            Assert.StartsWith("war3map.lua:2: ", error.Message);
        }

        [Fact]
        public void a_map_method_past_200_locals_fails_the_build_at_its_line()
        {
            using var folders = new TestFolders();
            var error = Assert.Throws<BuildException>(() => Build(folders, 200));

            Assert.StartsWith("Probe.cs:" + (FirstAddLine + 199) + ": Lua allows 200 locals in a function", error.Message);
        }

        [Fact]
        public void a_map_method_of_200_locals_builds()
        {
            using var folders = new TestFolders();
            Build(folders, 199);
        }

        private const int FirstAddLine = 17;

        // The NameClash fixture with a Probe whose static Fill takes one parameter and adds that many boxes, each an object
        // initializer, so a local of its own in the Lua.
        private static void Build(TestFolders folders, int boxes)
        {
            var source = new StringBuilder("""
                using System.Collections.Generic;
                using Moonwell;

                namespace NameClash
                {
                    public sealed class Box
                    {
                        public int X;
                    }

                    public sealed class Probe : GameComponent
                    {
                        public override void OnGameStart() => Fill(new List<Box>());

                        private static void Fill(List<Box> list)
                        {

                """);
            for (var i = 0; i < boxes; i++)
            {
                source.Append("            list.Add(new Box { X = ").Append(i).Append(" });\n");
            }

            source.Append("        }\n    }\n}\n");
            var folder = folders.TemporaryCopyOf("NameClash");
            File.WriteAllText(Path.Combine(folder, "Probe.cs"), source.ToString());
            MapBuild.Run(folder, new BuildOptions(), TextWriter.Null);
        }
    }
}
