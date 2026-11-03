using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>A map whose code reads a unit's last orders listens for orders from the start; one that doesn't is not made to.</summary>
    [Collection("Sample")]
    public sealed class OrdersBuildTests : IDisposable
    {
        private readonly TestFolders folders;

        public OrdersBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void map_reading_last_orders_listens()
        {
            var project = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(project, "Knockback.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Knockback : GameComponent\n    {\n        public void Push(Unit unit)\n        {\n            unit.Reissue();\n        }\n    }\n}\n");

            Build(project);

            Assert.Contains("global::Moonwell.Runtime.ListenForOrders();", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
        }

        [Fact]
        public void map_reading_a_last_order_listens()
        {
            var project = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(project, "Watch.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class Watch : GameComponent\n    {\n        public string Was(Unit unit)\n        {\n            return unit.LastOrder == null ? \"\" : unit.LastOrder.Name;\n        }\n    }\n}\n");

            Build(project);

            Assert.Contains("global::Moonwell.Runtime.ListenForOrders();", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
        }

        [Fact]
        public void map_without_orders_does_not_listen()
        {
            var project = folders.TemporaryCopyOfSample();

            Build(project);

            Assert.DoesNotContain("ListenForOrders", File.ReadAllText(Path.Combine(project, "build", "generated", "MapMain.g.cs")));
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
