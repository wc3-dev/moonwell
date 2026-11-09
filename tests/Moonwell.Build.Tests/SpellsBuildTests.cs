using System.Text;
using Moonwell.MapFiles;
using War3Net.Build.Extensions;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map with the spell templates builds (decision 226): its script loads with the package's code, and each template's Channel ability is written with the targeting and options its template set, though the package names Channel's fields only in .NET.</summary>
    [Collection("Sample")]
    public sealed class SpellsBuildTests
    {
        private readonly FixtureBuilds fixtures;

        public SpellsBuildTests(FixtureBuilds fixtures)
        {
            this.fixtures = fixtures;
        }

        [Fact]
        public void map_with_the_templates_builds_and_loads()
        {
            var project = fixtures.Of("Spells").Folder;
            var script = File.ReadAllText(Path.Combine(project, "build", "Spells", "war3map.lua"));
            using var lua = new NLua.Lua();
            lua.DoString("print = function() end");
            lua.DoString("called = {}");
            lua.DoString("setmetatable(_G, { __index = function(_, name) return function() called[#called + 1] = name; return nil end end })");
            lua.DoString(script);
            lua.DoString("main()");
            Assert.Contains("Moonwell.Spells.Bolt", script);
            Assert.Contains("Firebolt", script);
        }

        [Fact]
        public void the_packages_the_templates_use_are_installed()
        {
            // The fixture references only Moonwell.Spells and names none of Physics or Terrain, so the compiler keeps no reference to them in its assembly (I55).
            var main = File.ReadAllText(Path.Combine(fixtures.Of("Spells").Folder, "build", "generated", "MapMain.g.cs"));
            var terrain = main.IndexOf("global::Moonwell.Terrain.Install(", StringComparison.Ordinal);
            Assert.True(terrain >= 0 && terrain < main.IndexOf("global::Moonwell.Physics.Install(", StringComparison.Ordinal), main);
        }

        [Theory]
        [InlineData("A0B1", 1)]
        [InlineData("A0B3", 2)]
        [InlineData("A0B4", 2)]
        [InlineData("A0B5", 2)]
        [InlineData("A0B6", 2)]
        [InlineData("A0B7", 1)]
        public void templates_set_their_target_and_show_their_button(string id, int targetType)
        {
            var ability = AbilityIn(fixtures.Of("Spells").Folder, id);
            Assert.Equal(Rawcodes.IdOf("ANcl"), ability.OldId);
            Assert.Equal(targetType, Convert.ToInt32(Value(ability, "Ncl2")));
            Assert.Equal(1, Convert.ToInt32(Value(ability, "Ncl3")));
        }

        [Fact]
        public void a_pulsing_channel_lasts_five_seconds_and_no_target_templates_have_none()
        {
            var folder = fixtures.Of("Spells").Folder;
            Assert.Equal(5.0, Convert.ToDouble(Value(AbilityIn(folder, "A0B3"), "Ncl1")));
            foreach (var id in new[] { "A0B2", "A0B8" })
            {
                var target = Value(AbilityIn(folder, id), "Ncl2");
                Assert.True(target == null || Convert.ToInt32(target) == 0, id + " has no target");
            }
        }

        [Theory]
        [InlineData("A0B1")]
        [InlineData("A0B2")]
        [InlineData("A0B4")]
        [InlineData("A0B5")]
        [InlineData("A0B6")]
        [InlineData("A0B7")]
        [InlineData("A0B8")]
        public void instant_templates_end_their_channel_with_the_cast(string id)
        {
            var times = AbilityIn(fixtures.Of("Spells").Folder, id).Modifications.Where(one => one.Id == Rawcodes.IdOf("Ncl1")).ToList();
            Assert.NotEmpty(times);
            Assert.All(times, one => Assert.Equal(0.0, Convert.ToDouble(one.Value)));
        }

        [Theory]
        [InlineData("A0B1")]
        [InlineData("A0B2")]
        [InlineData("A0B3")]
        [InlineData("A0B4")]
        [InlineData("A0B5")]
        [InlineData("A0B6")]
        [InlineData("A0B7")]
        [InlineData("A0B8")]
        public void templates_put_their_button_off_hold_positions_place(string id)
        {
            var ability = AbilityIn(fixtures.Of("Spells").Folder, id, "war3mapSkin.w3a");
            Assert.Equal(0, Assert.IsType<int>(Value(ability, "abpx")));
            Assert.Equal(2, Assert.IsType<int>(Value(ability, "abpy")));
        }

        private static War3Net.Build.Object.LevelObjectModification AbilityIn(string project, string id, string file = "war3map.w3a")
        {
            using var reader = new BinaryReader(File.OpenRead(Path.Combine(project, "build", "Spells", file)), Encoding.UTF8);
            return Assert.Single(reader.ReadAbilityObjectData().NewAbilities, one => one.NewId == Rawcodes.IdOf(id));
        }

        // The level 1 value of a field, or null when the field is left at Channel's own.
        private static object? Value(War3Net.Build.Object.LevelObjectModification ability, string field)
        {
            return ability.Modifications.FirstOrDefault(one => one.Id == Rawcodes.IdOf(field) && one.Level <= 1)?.Value;
        }
    }
}
