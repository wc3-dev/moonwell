using War3Net.Build.Extensions;
using War3Net.Build.Info;
using War3Net.Build.Widget;
using War3Net.Common.Extensions;

namespace Moonwell.Build.Tests
{
    /// <summary>The build stops at placed doodads or units in a layout the map's war3map.w3i makes the game read wrongly (J139).</summary>
    public sealed class WidgetLayoutTests
    {
        [Fact]
        public void doodads_without_skin_ids_beside_a_war3map_w3i_of_2_0_stop_the_build()
        {
            var map = new War3Net.Build.Map { Doodads = Doodads(skins: false) };

            var error = Assert.Throws<BuildException>(() => SourceMap.CheckLayouts(map, Info(new Version(2, 0, 3, 22988)), "map"));

            Assert.Equal(
                @"map\war3map.doo has no skin ids (the layout before 1.32), but map\war3map.w3i names game 2.0, so the game reads it with them and places its doodads wrong; "
                + "write the doodads again with skin ids, the layout that war3map.w3i wants (the two files likely come from different maps)",
                error.Message);
        }

        [Fact]
        public void doodads_with_skin_ids_beside_a_war3map_w3i_with_no_game_version_stop_the_build()
        {
            var map = new War3Net.Build.Map { Doodads = Doodads(skins: true) };

            var error = Assert.Throws<BuildException>(() => SourceMap.CheckLayouts(map, new MapInfo(MapInfoFormatVersion.v25), "map"));

            Assert.StartsWith(@"map\war3map.doo has skin ids (the layout of 1.32 on), but map\war3map.w3i names no game version (format 25), so the game reads it without them", error.Message);
        }

        [Fact]
        public void units_without_skin_ids_beside_a_war3map_w3i_of_3_0_stop_the_build()
        {
            var units = new MapUnits(MapWidgetsFormatVersion.v8, MapWidgetsSubVersion.v11, false);
            units.Units.Add(new UnitData { TypeId = "hfoo".FromRawcode(), SkinId = "hfoo".FromRawcode(), OwnerId = 0 });
            var map = new War3Net.Build.Map { Units = units };

            var error = Assert.Throws<BuildException>(() => SourceMap.CheckLayouts(map, Info(new Version(3, 0, 1, 24371)), "map"));

            Assert.StartsWith(@"map\war3mapUnits.doo has no skin ids", error.Message);
        }

        [Fact]
        public void doodads_in_the_layout_war3map_w3i_wants_or_none_at_all_pass()
        {
            SourceMap.CheckLayouts(new War3Net.Build.Map { Doodads = Doodads(skins: true) }, Info(new Version(2, 0, 3, 22988)), "map");
            SourceMap.CheckLayouts(new War3Net.Build.Map { Doodads = Doodads(skins: false) }, Info(new Version(1, 31, 1, 12164)), "map");
            SourceMap.CheckLayouts(new War3Net.Build.Map { Doodads = Doodads(skins: false) }, new MapInfo(MapInfoFormatVersion.v25), "map");
            SourceMap.CheckLayouts(
                new War3Net.Build.Map { Doodads = new MapDoodads(MapWidgetsFormatVersion.v8, MapWidgetsSubVersion.v11, false) },
                Info(new Version(2, 0, 3, 22988)),
                "map");
        }

        private static MapInfo Info(Version game)
        {
            return new MapInfo(MapInfoFormatVersion.v31) { GameVersion = game };
        }

        // Written and read back, so the layout is the one War3Net tells from the file's bytes, as the build reads it.
        private static MapDoodads Doodads(bool skins)
        {
            var doodads = new MapDoodads(MapWidgetsFormatVersion.v8, MapWidgetsSubVersion.v11, skins);
            doodads.Doodads.Add(new DoodadData { TypeId = "LTlt".FromRawcode(), SkinId = "LTlt".FromRawcode(), Variation = 1, Life = 100, State = DoodadState.Normal });
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(doodads);
            }

            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            var read = reader.ReadMapDoodads();
            Assert.Equal(skins, read.UseNewFormat);
            return read;
        }
    }
}
