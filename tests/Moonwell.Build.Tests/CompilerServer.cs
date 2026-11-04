using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// The test run's own compiler server. The map builds the tests run compile on it, named by SharedCompilationId (MSBuild reads
    /// the environment as properties), so they share no server with other builds on the machine: a server stops when it is sent
    /// an analyzer that changed since it loaded it, as the pack's build of the analyzer does, and this one stops only this run's.
    /// It is shut down when the test run ends.
    /// </summary>
    internal static class CompilerServer
    {
        public static readonly string Name = "moonwell-tests-" + Guid.NewGuid().ToString("N");

        [ModuleInitializer]
        internal static void Start()
        {
            Environment.SetEnvironmentVariable("SharedCompilationId", Name);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
        }

        // Asks the server by its name to shut down, through the VBCSCompiler of any server running; none running, none to stop.
        private static void Stop()
        {
            var compiler = Process.GetProcessesByName("VBCSCompiler").Select(server => server.MainModule?.FileName).FirstOrDefault(path => path != null);
            if (compiler == null)
            {
                return;
            }

            using var shutdown = Process.Start(new ProcessStartInfo(compiler, "-shutdown -pipename:" + Name) { UseShellExecute = false, CreateNoWindow = true });
            shutdown?.WaitForExit(5000);
        }
    }
}
