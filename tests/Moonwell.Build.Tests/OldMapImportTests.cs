using Moonwell.Tool;

namespace Moonwell.Build.Tests
{
    /// <summary><c>moonwell import</c>: an existing map as a Moonwell project, its old script kept running as a Lua library (decision 257).</summary>
    [Collection("Sample")]
    public sealed class OldMapImportTests : IDisposable
    {
        // An old map's script as World Editor writes it: its setup, which Moonwell's script makes again, a trigger and a variable.
        private const string Jass = """
            globals
                // User-defined
                integer                 udg_Gold                   = 0
                unit array              udg_Heroes
                // Generated
                unit                    gg_unit_Hpal_0002          = null
                trigger                 gg_trg_Hello               = null
            endglobals

            function InitGlobals takes nothing returns nothing
                set udg_Gold = 100
            endfunction

            function CreateAllUnits takes nothing returns nothing
                set gg_unit_Hpal_0002 = CreateUnit(Player(0), 'Hpal', 0.0, 0.0, 270.0)
            endfunction

            function Half takes integer value returns integer
                return value / 2
            endfunction

            function Trig_Hello_Actions takes nothing returns nothing
                call DisplayTextToPlayer(GetLocalPlayer(), 0, 0, I2S(Half(udg_Gold)))
                call KillUnit(gg_unit_Hpal_0002)
            endfunction

            function InitTrig_Hello takes nothing returns nothing
                set gg_trg_Hello = CreateTrigger()
                call TriggerRegisterTimerEventSingle(gg_trg_Hello, 0.00)
                call TriggerAddAction(gg_trg_Hello, function Trig_Hello_Actions)
            endfunction

            function InitCustomTriggers takes nothing returns nothing
                call InitTrig_Hello()
            endfunction

            function main takes nothing returns nothing
                call CreateAllUnits()
                call InitBlizzard()
                call InitGlobals()
                call InitCustomTriggers()
            endfunction

            function config takes nothing returns nothing
            endfunction
            """;

        private readonly TestFolders folders;

        public OldMapImportTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_jass_script_becomes_lua_without_what_moonwell_makes_and_typed_for_the_bindings()
        {
            var lua = Converted();

            Assert.DoesNotContain("function main(", lua);
            Assert.DoesNotContain("function config(", lua);
            Assert.DoesNotContain("function CreateAllUnits(", lua);
            Assert.DoesNotContain("gg_unit_Hpal_0002 = nil", lua);
            Assert.Contains("---@type integer\nudg_Gold = 0", lua);
            Assert.Contains("---@param value integer\n---@return integer\nfunction Half(value)", lua);
            Assert.Contains("---@private\nfunction Trig_Hello_Actions()", lua);
            Assert.Contains("---@private\nfunction InitTrig_Hello()", lua);
            Assert.Contains("\nfunction InitGlobals()", lua);
            Assert.DoesNotContain("---@private\nfunction InitCustomTriggers()", lua);
        }

        [Fact]
        public void a_lua_script_loses_what_moonwell_makes_and_hides_all_but_its_start()
        {
            var lua = OldMapImport.FromLua(
                "gg_rct_Spawn = nil\ngg_trg_Hello = nil\ngg_dest_LTlt_0012 = nil\nfunction CreateRegions()\n    gg_rct_Spawn = Rect(0, 0, 32, 32)\nend\n\nfunction Lib.helper(x) return x end\nfunction Trig_Hello_Actions()\nend\n\nfunction InitCustomTriggers()\nend\n",
                new HashSet<string> { "CreateRegions" },
                new HashSet<string> { "gg_rct_Spawn" });

            Assert.Equal("gg_trg_Hello = nil\n---@type destructable\ngg_dest_LTlt_0012 = nil\n\n---@private\nfunction Lib.helper(x) return x end\n---@private\nfunction Trig_Hello_Actions()\nend\n\nfunction InitCustomTriggers()\nend\n\n", lua);
        }

        [Fact]
        public void a_folder_map_becomes_a_project_whose_old_script_starts_with_the_game()
        {
            var parent = folders.TemporaryFolderOutsideRepository();
            var project = NewMap.Write("OldOne", parent, "1.2.3");

            OldMapImport.Run(Path.Combine(TestFolders.Fixture("Sample"), "map"), project, new StringWriter());

            Assert.True(File.Exists(Path.Combine(project, "map", "war3map.w3i")));
            Assert.False(File.Exists(Path.Combine(project, "map", "war3map.lua")));
            Assert.False(Directory.Exists(Path.Combine(project, "Heroes")));
            Assert.False(File.Exists(Path.Combine(project, "Start.cs")));
            Assert.True(File.Exists(Path.Combine(project, "lua", "old_map.lua")));
            Assert.Contains("Components.Add(new OldScript());", File.ReadAllText(Path.Combine(project, "Map.cs")));
            Assert.Contains("Lua.OldMap.InitGlobals();", File.ReadAllText(Path.Combine(project, "OldScript.cs")));
        }

        [Fact]
        public void a_map_with_the_converted_script_builds_and_reaches_its_variable_from_csharp()
        {
            var project = folders.TemporaryCopyOf("Sample");
            Directory.CreateDirectory(Path.Combine(project, "lua"));
            File.WriteAllText(Path.Combine(project, "lua", "old_map.lua"), Converted());
            File.WriteAllText(
                Path.Combine(project, "OldScript.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class OldScript : GameComponent\n    {\n        public override void OnGameStart()\n        {\n            Lua.OldMap.InitGlobals();\n            Lua.OldMap.udg_Gold = Lua.OldMap.Half(Lua.OldMap.udg_Gold);\n            Lua.OldMap.InitCustomTriggers();\n        }\n    }\n}\n");

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 0, error.ToString() + output);
            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            Assert.Contains("function Trig_Hello_Actions()", script);
            Assert.Contains("udg_Gold = ", script);
        }

        // The JASS above as import makes it, with the names Moonwell's script for the Sample makes taken out.
        private static string Converted() =>
            OldMapImport.FromJass(Jass, new HashSet<string> { "main", "config", "CreateAllUnits" }, new HashSet<string> { "gg_unit_Hpal_0002" }).Replace("\r\n", "\n");
    }
}
