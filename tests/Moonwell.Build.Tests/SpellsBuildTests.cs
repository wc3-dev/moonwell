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

        private static War3Net.Build.Object.LevelObjectModification AbilityIn(string project, string id)
        {
            using var reader = new BinaryReader(File.OpenRead(Path.Combine(project, "build", "Spells", "war3map.w3a")), Encoding.UTF8);
            return Assert.Single(reader.ReadAbilityObjectData().NewAbilities, one => one.NewId == Rawcodes.IdOf(id));
        }

        // The level 1 value of a field, or null when the field is left at Channel's own.
        private static object? Value(War3Net.Build.Object.LevelObjectModification ability, string field)
        {
            return ability.Modifications.FirstOrDefault(one => one.Id == Rawcodes.IdOf(field) && one.Level <= 1)?.Value;
        }
    }
}
