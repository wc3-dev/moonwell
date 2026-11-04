using Moonwell.Tool;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// Copies of fixtures built through the command once for the collection, as development builds, for the tests that read the build
    /// of a fixture they do not change. Such a test only reads the copy: another test reads it after.
    /// </summary>
    public sealed class FixtureBuilds : IDisposable
    {
        private readonly TestFolders folders = new TestFolders();

        private readonly Dictionary<string, (int ExitCode, BuiltFixture Built, string Error)> built = new Dictionary<string, (int, BuiltFixture, string)>();

        /// <summary>The build of a fixture, made the first time it is asked for; the test fails with the build's output when it failed.</summary>
        public BuiltFixture Of(string fixture)
        {
            (int ExitCode, BuiltFixture Built, string Error) one;
            lock (built)
            {
                if (!built.TryGetValue(fixture, out one))
                {
                    var folder = folders.TemporaryCopyOf(fixture);
                    var output = new StringWriter();
                    var error = new StringWriter();
                    var exitCode = Cli.Run(new[] { "build", folder }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
                    one = (exitCode, new BuiltFixture(folder, output.ToString()), error.ToString());
                    built.Add(fixture, one);
                }
            }

            Assert.True(one.ExitCode == 0, one.Error + one.Built.Output);
            return one.Built;
        }

        /// <summary>Deletes the copies.</summary>
        public void Dispose()
        {
            folders.Dispose();
        }
    }

    /// <summary>A fixture's copy, built in place, and what the command printed on its output.</summary>
    /// <param name="Folder">The copy of the fixture: its project folder, with the build's <c>build</c> folder in it.</param>
    /// <param name="Output">What the command printed on its output.</param>
    public sealed record BuiltFixture(string Folder, string Output);
}
