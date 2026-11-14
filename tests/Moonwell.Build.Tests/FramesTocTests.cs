using System;
using System.IO;
using System.Text;
using Xunit;

namespace Moonwell.Build.Tests
{
    public class FramesTocTests
    {
        [Fact]
        public void a_map_with_the_kit_carries_its_templates_first_in_the_maps_toc()
        {
            var root = Path.Combine(Path.GetTempPath(), "moonwell-toc-" + Guid.NewGuid().ToString("N"));
            var staged = Path.Combine(root, "staged");
            var assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(staged);
            Directory.CreateDirectory(Path.Combine(assets, "UI"));
            File.WriteAllText(Path.Combine(assets, "UI", "Shop.fdf"), "");
            try
            {
                FramesToc.Write(staged, assets, kit: true);

                Assert.Equal(Encoding.ASCII.GetBytes("MoonwellText.fdf\r\nMoonwellKit.fdf\r\nUI\\Shop.fdf\r\n\r\n"), File.ReadAllBytes(Path.Combine(staged, "MoonwellMap.toc")));
                var templates = File.ReadAllText(Path.Combine(staged, "MoonwellKit.fdf"));
                foreach (var race in new[] { "Human", "Orc", "NightElf", "Undead" })
                {
                    Assert.Contains("Frame \"BACKDROP\" \"MoonwellKitWindow" + race + "\" {", templates);
                }

                Assert.Contains(@"BackdropEdgeFile  ""UI\Widgets\EscMenu\NightElf\nightelf-options-menu-border.blp""", templates);
                Assert.DoesNotContain("\n", templates.Replace("\r\n", ""));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void a_map_with_the_kit_carries_the_sweeps_64_images_and_one_without_none()
        {
            var root = Path.Combine(Path.GetTempPath(), "moonwell-toc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                FramesToc.Write(root, Path.Combine(root, "no-assets"), kit: true);
                Assert.Equal(KitSweep.Steps, Directory.GetFiles(Path.Combine(root, "MoonwellKit"), "Sweep*.blp").Length);
                Assert.True(File.Exists(Path.Combine(root, @"MoonwellKit\Sweep64.blp")));

                var bare = Path.Combine(root, "bare");
                Directory.CreateDirectory(bare);
                FramesToc.Write(bare, Path.Combine(root, "no-assets"), kit: false);
                Assert.False(Directory.Exists(Path.Combine(bare, "MoonwellKit")));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void a_sweep_image_is_dark_over_the_share_left_ending_at_the_top_and_clear_over_the_rest()
        {
            // Half left: lit clockwise from the top to the bottom (the right half), dark over the left half.
            var half = KitSweep.Pixels(32);
            Assert.Equal(153, half[((32 * 64) + 10) * 4 + 3]);
            Assert.Equal(0, half[((32 * 64) + 54) * 4 + 3]);
            // A quarter left: dark only in the top left quarter.
            var quarter = KitSweep.Pixels(16);
            Assert.Equal(153, quarter[((10 * 64) + 10) * 4 + 3]);
            Assert.Equal(0, quarter[((54 * 64) + 10) * 4 + 3]);
            // All left: dark everywhere, corners too.
            Assert.Equal(153, KitSweep.Pixels(64)[3]);
        }

        [Fact]
        public void a_map_with_no_fdf_of_its_own_still_has_a_maps_toc_with_the_text_templates()
        {
            var root = Path.Combine(Path.GetTempPath(), "moonwell-toc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                FramesToc.Write(root, Path.Combine(root, "no-assets"), kit: true);
                Assert.Equal(Encoding.ASCII.GetBytes("MoonwellText.fdf\r\nMoonwellKit.fdf\r\n\r\n"), File.ReadAllBytes(Path.Combine(root, "MoonwellMap.toc")));

                File.Delete(Path.Combine(root, "MoonwellMap.toc"));
                File.Delete(Path.Combine(root, "MoonwellKit.fdf"));
                FramesToc.Write(root, Path.Combine(root, "no-assets"), kit: false);
                Assert.Equal(Encoding.ASCII.GetBytes("MoonwellText.fdf\r\n\r\n"), File.ReadAllBytes(Path.Combine(root, "MoonwellMap.toc")));
                Assert.False(File.Exists(Path.Combine(root, "MoonwellKit.fdf")));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void the_text_templates_cover_each_size_with_and_without_a_shadow_for_the_games_font_and_each_ttf_in_the_assets()
        {
            var root = Path.Combine(Path.GetTempPath(), "moonwell-toc-" + Guid.NewGuid().ToString("N"));
            var staged = Path.Combine(root, "staged");
            var assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(staged);
            Directory.CreateDirectory(Path.Combine(assets, "war3mapImported"));
            File.WriteAllText(Path.Combine(assets, "war3mapImported", "ProbeComic.ttf"), "");
            try
            {
                FramesToc.Write(staged, assets, kit: false);

                var text = File.ReadAllText(Path.Combine(staged, "MoonwellText.fdf"));
                Assert.Contains("Frame \"TEXT\" \"MoonwellText_Game_10\" {\r\n    FrameFont \"Fonts\\FRIZQT__.TTF\", 0.005, \"\",\r\n    FontColor 1.0 1.0 1.0 1.0,\r\n}\r\n", text);
                Assert.Contains("Frame \"TEXT\" \"MoonwellText_Game_80S\" {\r\n    FrameFont \"Fonts\\FRIZQT__.TTF\", 0.04, \"\",\r\n    FontColor 1.0 1.0 1.0 1.0,\r\n    FontShadowColor 0.0 0.0 0.0 1.0,\r\n    FontShadowOffset 0.002 -0.002,\r\n}\r\n", text);
                Assert.Contains("Frame \"TEXT\" \"MoonwellText_war3mapimported_probecomic_ttf_30S\" {\r\n    FrameFont \"war3mapImported\\ProbeComic.ttf\", 0.015, \"\",\r\n", text);
                Assert.DoesNotContain("_9\"", text);
                Assert.DoesNotContain("_81", text);
                Assert.Equal(2 * 71 * 2, text.Split("Frame \"TEXT\"").Length - 1);
                Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
