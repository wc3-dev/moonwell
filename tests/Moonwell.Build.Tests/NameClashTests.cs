namespace Moonwell.Build.Tests
{
    /// <summary>
    /// What MW0044 keeps a map from: each probe silences the rule, is built as a map and is run in Lua, with the natives it
    /// meets as functions, as the game has them. If the fork stops any of these, that part of the rule can go.
    /// </summary>
    [Collection("Sample")]
    public sealed class NameClashTests : IDisposable
    {
        private const string Start = """
            #pragma warning disable MW0044
            using Moonwell;

            namespace NameClash
            {
                public sealed class Probe : GameComponent
                {
                    public override void OnGameStart()
                    {
                        Natives.BJDebugMsg(Text());
                    }

            """;

        private const string End = """
                }
            }
            """;

        private readonly TestFolders folders;

        public NameClashTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void the_rule_stops_such_a_map_before_the_lua_step()
        {
            var error = Assert.Throws<BuildException>(() => Run("""
                using Moonwell;

                namespace NameClash
                {
                    public sealed class Probe : GameComponent
                    {
                    }
                }

                namespace Rect
                {
                    public sealed class Shape
                    {
                    }
                }
                """));

            Assert.Equal("dotnet build failed", error.Message);
        }

        [Fact]
        public void a_namespace_root_named_like_a_native_stops_the_script_while_it_loads()
        {
            var error = Assert.Throws<NLua.Exceptions.LuaScriptException>(() => Run(Start + """
                        private static string Text() => "shape";
                """ + End + """

                namespace Rect
                {
                    public sealed class Shape
                    {
                    }
                }
                """));

            Assert.Contains("bad argument #1 to 'rawget' (table expected, got function)", error.Message);
        }

        [Fact]
        public void a_class_with_no_namespace_named_like_a_native_stops_the_script_while_it_loads()
        {
            var error = Assert.Throws<NLua.Exceptions.LuaScriptException>(() => Run(Start + """
                        private static string Text() => "shape";
                """ + End + """

                public sealed class Rect
                {
                }
                """));

            Assert.Contains("Rect", error.Message);
        }

        [Fact]
        public void a_repeated_full_name_stops_the_lua_step_on_moonwells_own_file()
        {
            // Unused, so the map's own compile says nothing: CS0436 comes only where the class is named. The fixture references
            // Moonwell's projects, so the Lua step compiles the two together. A map on the packages compiles alone, and from
            // reading CoreSystem's defIn its script would stop at an assert while it loads (not run).
            var error = Assert.Throws<BuildException>(() => Run(Start + """
                        private static string Text() => "hero";
                """ + End + """

                namespace Moonwell
                {
                    public sealed class Hero
                    {
                    }
                }
                """));

            Assert.Contains("error CS0101: The namespace 'Moonwell' already contains a definition for 'Hero'", error.Message);
        }

        [Fact]
        public void a_static_method_named_like_the_native_it_calls_never_returns()
        {
            var shown = Run(Start + """
                        private static string Text() => "cos " + Cos(0.0f);

                        private static float Cos(float x)
                        {
                            return Natives.Cos(x);
                        }
                """ + End);

            Assert.Contains("no end after", shown);
        }

        [Fact]
        public void an_instance_method_named_like_the_native_it_calls_never_returns()
        {
            var shown = Run(Start + """
                        private static string Text() => "cos " + new Probe().Cos(0.0f);

                        private float Cos(float x)
                        {
                            return Natives.Cos(x);
                        }
                """ + End);

            Assert.Contains("failed", shown);
        }

        [Fact]
        public void a_method_named_like_a_native_another_method_calls_takes_its_place()
        {
            var shown = Run(Start + """
                        private static string Text() => "sin " + (int)Natives.Sin(0.0f);

                        private static float Sin(float x)
                        {
                            return 5.0f;
                        }
                """ + End);

            Assert.Equal("sin 5", shown);
        }

        [Fact]
        public void a_field_or_a_property_named_like_a_native_shadows_nothing()
        {
            var shown = Run(Start + """
                        private static float Sin = 5.0f;

                        private static float Tan => 7.0f;

                        private static string Text() => (int)Sin + " " + (int)Tan + " " + (int)Natives.Sin(0.0f) + " " + (int)Natives.Tan(0.0f);
                """ + End);

            Assert.Equal("5 7 0 0", shown);
        }

        [Fact]
        public void a_class_named_like_a_moonwell_class_in_the_maps_namespace_runs()
        {
            var shown = Run(Start + """
                        private static string Text() => "left " + new Timer().Left + " depth " + new Scope().Depth;

                        private sealed class Unused
                        {
                        }
                """ + End + """

                namespace NameClash
                {
                    public sealed class Timer
                    {
                        public int Left = 3;
                    }

                    public sealed class Scope
                    {
                        public int Depth = 2;
                    }
                }
                """);

            Assert.Equal("left 3 depth 2", shown);
        }

        // Builds the fixture with the file as its Probe.cs, runs main and every timer in Lua, and returns what was shown.
        private string Run(string source)
        {
            var folder = folders.TemporaryCopyOf("NameClash");
            File.WriteAllText(Path.Combine(folder, "Probe.cs"), source);
            MapBuild.Run(folder, new BuildOptions(), TextWriter.Null);
            var script = File.ReadAllText(Path.Combine(MapProject.Find(folder).StagedFolder, "war3map.lua"));

            using var lua = new NLua.Lua();
            lua.DoString("screen = {}; pending = {}");
            lua.DoString("print = function(...) end");
            lua.DoString("setmetatable(_G, { __index = function(_, name) return function() return nil end end })");
            lua.DoString("bj_MAX_PLAYER_SLOTS = 0");
            lua.DoString("Rect = function() return nil end; Cos = math.cos; Sin = math.sin; Tan = math.tan");
            lua.DoString("TimerGetElapsed = function() return 0 end");
            lua.DoString("TimerStart = function(timer, timeout, periodic, callback) pending[#pending + 1] = callback end");
            lua.DoString("BJDebugMsg = function(text) screen[#screen + 1] = text end");
            lua.DoString("DisplayTimedTextToPlayer = function(p, x, y, duration, text) screen[#screen + 1] = text end");
            lua.DoString(script, "war3map.lua");

            // A Lua tail call never overflows the stack; in the game such a loop has no end, and here the count stops it.
            lua.DoString("debug.sethook(function() error('no end after 200 million instructions') end, '', 200000000)");
            lua.DoString("main()");
            lua.DoString("for round = 1, 10 do local due = pending; pending = {}; for i = 1, #due do due[i]() end end");
            return (string)lua.DoString("return table.concat(screen, ' | ')")[0];
        }
    }
}
