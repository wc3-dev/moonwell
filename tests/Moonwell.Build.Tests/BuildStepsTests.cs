using System.Drawing;
using Moonwell.BuildTime;
using War3Net.Build.Environment;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary><c>BuildSteps</c> (J124): a test runs a map's build step outside the build and reads what it left.</summary>
    public sealed class BuildStepsTests : IDisposable
    {
        private readonly TestFolders folders;

        public BuildStepsTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_step_run_on_a_map_folder_gives_back_what_it_gave_warned_and_reported()
        {
            var map = Path.Combine(folders.TemporaryCopyOf("BuildTime"), "map");

            var result = BuildSteps.Run<Counted>(map);

            Assert.False(result.Failed);
            Assert.Null(result.Failure);
            var given = Assert.IsType<RegionCount>(result.Given["Regions"]);
            Assert.Equal((2, "Arena"), (given.Count, given.Last));
            Assert.Contains("\"Count\": 2", result.GivenJson["Regions"]);
            Assert.Equal(new[] { "region Shop is small" }, result.Warnings);
            Assert.Equal("Shop\nArena\n", result.ReportText("regions.txt"));
            Assert.Equal(0x89, result.Reports["water.png"][0]);
            Assert.Contains("it wrote regions.txt, water.png", Assert.Throws<KeyNotFoundException>(() => result.ReportText("other.txt")).Message);
        }

        [Fact]
        public void a_failed_step_keeps_its_line_and_what_it_left_before()
        {
            var map = new MapReader(Path.Combine(folders.TemporaryCopyOf("BuildTime"), "map"));

            var result = BuildSteps.Run(new Failing(), map);

            Assert.True(result.Failed);
            Assert.Equal("region Harbor is missing", result.Failure);
            Assert.Equal(new[] { "looking for Harbor" }, result.Warnings);
            Assert.Equal(1, result.Given["Before"]);
            Assert.Contains("the map has no region named Missing", BuildSteps.Run(new Reading("Missing"), map).Failure);
        }

        [Fact]
        public void any_other_error_is_the_tests_and_goes_on_up()
        {
            var map = new MapReader(Path.Combine(folders.TemporaryCopyOf("BuildTime"), "map"));

            Assert.Equal("a bug", Assert.Throws<InvalidOperationException>(() => BuildSteps.Run(new Throwing(), map)).Message);
        }

        [Fact]
        public void a_test_can_run_a_step_on_regions_and_files_of_its_own()
        {
            var map = new MapReader(Path.Combine(folders.TemporaryCopyOf("BuildTime"), "map"));
            var harbor = new Region { Name = "Harbor", Left = -256f, Bottom = -256f, Right = 256f, Top = 256f, Color = Color.Blue };

            var withHarbor = map.WithRegions(new[] { harbor });
            var result = BuildSteps.Run<Counted>(withHarbor);

            Assert.Equal((1, "Harbor"), (((RegionCount)result.Given["Regions"]).Count, ((RegionCount)result.Given["Regions"]).Last));
            Assert.Equal(512f, withHarbor.Region("Harbor").Width);

            // The reader it came from still reads the map's own; a file added is read and listed.
            Assert.Equal(2, map.Regions.Regions.Count);
            var notes = map.WithFile(@"Notes\rules.txt", System.Text.Encoding.UTF8.GetBytes("added")).WithFile("Notes/more.txt", new byte[] { 1 });
            Assert.True(notes.Has("Notes/rules.txt"));
            Assert.Equal("added", System.Text.Encoding.UTF8.GetString(notes.Bytes(@"Notes\rules.txt")));
            Assert.Contains(@"Notes\more.txt", notes.Files);
            Assert.False(map.Has(@"Notes\rules.txt"));
            Assert.Equal(2, notes.Regions.Regions.Count);
        }

        public sealed record RegionCount(int Count, string Last);

        public sealed class Counted : BuildStep
        {
            public override void Run()
            {
                var regions = Map.Regions.Regions;
                if (regions.Any(region => region.Name == "Shop"))
                {
                    Warn("region Shop is small");
                }

                Give("Regions", new RegionCount(regions.Count, regions.Last().Name));
                Report("regions.txt", string.Concat(regions.Select(region => region.Name + "\n")));
                Report("water.png", new Picture(Map).Fill(Map.Cells.Where(cell => cell.IsWater), Color.Blue));
            }
        }

        public sealed class Failing : BuildStep
        {
            public override void Run()
            {
                Give("Before", 1);
                Warn("looking for Harbor");
                Fail("region Harbor is missing");
            }
        }

        public sealed class Reading(string name) : BuildStep
        {
            public override void Run()
            {
                Map.Region(name);
            }
        }

        public sealed class Throwing : BuildStep
        {
            public override void Run()
            {
                throw new InvalidOperationException("a bug");
            }
        }
    }
}
