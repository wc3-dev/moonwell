using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code names RealTime shares the first person's date while it loads; a map that does not shares nothing.</summary>
    [Collection("Sample")]
    public sealed class RealTimeBuildTests : IDisposable
    {
        private readonly SampleBuilds builds;
        private readonly TestFolders folders;

        public RealTimeBuildTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void map_naming_RealTime_shares_the_start_date()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(
                Path.Combine(project, "Season.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Season : GameComponent\n    {\n        public override void OnGameStart()\n        {\n            if (RealTime.Now.Month == 12)\n            {\n                Game.Print(\"winter\");\n            }\n        }\n    }\n}\n");

            Build(project);

            var main = File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs"));
            var share = main.IndexOf("global::Moonwell.Runtime.ShareStartDate();", StringComparison.Ordinal);
            var start = main.IndexOf("global::Moonwell.Runtime.Start(", StringComparison.Ordinal);
            Assert.True(share >= 0 && share < start, "the date is shared while the map loads, before the runtime starts: " + main);

            var script = File.ReadAllText(Path.Combine(project, "build", "Sample", "war3map.lua"));
            using var lua = new NLua.Lua();
            lua.DoString("print = function() end");
            lua.DoString("setmetatable(_G, { __index = function(_, name) return function() return nil end end })");
            lua.DoString(script);
            lua.DoString("main()");
            Assert.Contains("ShareStartDate", script);
        }

        [Fact]
        public void map_without_RealTime_shares_no_date()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            Assert.DoesNotContain("ShareStartDate", File.ReadAllText(Path.Combine(builds.Development.BuildFolder, "generated", "MapMain.g.cs")));
        }

        private static void Build(string project)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            Assert.True(exitCode == 0, error.ToString() + output);
        }
    }
}
