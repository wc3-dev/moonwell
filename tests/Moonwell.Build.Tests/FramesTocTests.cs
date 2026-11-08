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

                Assert.Equal(Encoding.ASCII.GetBytes("MoonwellKit.fdf\r\nUI\\Shop.fdf\r\n\r\n"), File.ReadAllBytes(Path.Combine(staged, "MoonwellMap.toc")));
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
        public void a_map_with_the_kit_and_no_fdf_of_its_own_still_has_a_maps_toc()
        {
            var root = Path.Combine(Path.GetTempPath(), "moonwell-toc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                FramesToc.Write(root, Path.Combine(root, "no-assets"), kit: true);
                Assert.Equal(Encoding.ASCII.GetBytes("MoonwellKit.fdf\r\n\r\n"), File.ReadAllBytes(Path.Combine(root, "MoonwellMap.toc")));

                File.Delete(Path.Combine(root, "MoonwellMap.toc"));
                File.Delete(Path.Combine(root, "MoonwellKit.fdf"));
                FramesToc.Write(root, Path.Combine(root, "no-assets"), kit: false);
                Assert.False(File.Exists(Path.Combine(root, "MoonwellMap.toc")));
                Assert.False(File.Exists(Path.Combine(root, "MoonwellKit.fdf")));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
