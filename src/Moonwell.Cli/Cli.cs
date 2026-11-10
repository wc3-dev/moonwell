using System.Reflection;
using Moonwell.Build;
using Moonwell.GameData;

namespace Moonwell.Tool
{
    /// <summary>The <c>moonwell</c> command: parses its arguments and runs the build or another command.</summary>
    internal static class Cli
    {
        private const string Usage =
            "usage: moonwell build [folder] [--release]\n" +
            "       moonwell run [folder] [--release] [--game <exe>]\n" +
            "       moonwell watch [folder] [--release] [--run] [--game <exe>]\n" +
            "       moonwell new <name>\n" +
            "       moonwell import <map> <name>\n" +
            "       moonwell clean [folder]\n" +
            "       moonwell objects [folder]";

        // Folders of the project a change in which is no reason to build again: what the builds write, and the tools'.
        private static readonly string[] Unwatched = { "build", "bin", "obj", ".git", ".vs", ".idea" };

        /// <summary>Runs the command.</summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="output">Where progress and the build's output go.</param>
        /// <param name="error">Where errors and the usage go.</param>
        /// <param name="start">Starts a process, given the program and its arguments.</param>
        /// <param name="stop">Closes the game <paramref name="start"/> started last, if it still runs: <c>watch --run</c> does before each build.</param>
        /// <param name="cancel">Ends <c>watch</c>.</param>
        /// <returns>0 when it worked, 1 for an error, 2 for arguments it does not understand.</returns>
        public static int Run(string[] args, TextWriter output, TextWriter error, Action<string, string> start, Action? stop = null, CancellationToken cancel = default)
        {
            if (args.Length == 1 && args[0] == "--version")
            {
                output.WriteLine(Version());
                return 0;
            }

            if (!TryParse(args, out var options))
            {
                error.WriteLine(Usage);
                return 2;
            }

            return Report(error, () =>
            {
                switch (options.Command)
                {
                    case "new":
                        var folder = NewMap.Write(options.Name, ".", Version());
                        output.WriteLine($"moonwell: made {folder}; cd {options.Name}, then moonwell run");
                        break;
                    case "import":
                        var imported = NewMap.Write(options.Name, ".", Version());
                        OldMapImport.Run(options.Source, imported, output);
                        output.WriteLine($"moonwell: made {imported}; cd {options.Name}, then moonwell run");
                        break;
                    case "clean":
                        Clean(options.Folder, output);
                        break;
                    case "objects":
                        Objects(options.Folder, output);
                        break;
                    case "watch":
                        Watch(options, output, error, start, stop ?? (() => { }), cancel);
                        break;
                    default:
                        Build(options, output, start);
                        break;
                }
            });
        }

        // Runs a command, reporting what fails: 0, or 1 with the message.
        private static int Report(TextWriter error, Action command)
        {
            try
            {
                command();
                return 0;
            }
            catch (BuildException failure)
            {
                error.WriteLine($"moonwell: {failure.Message}");
                return 1;
            }
            catch (Exception failure)
            {
                error.WriteLine($"moonwell: unexpected error: {failure.GetType().Name}: {failure.Message}");
                error.WriteLine(failure.StackTrace);
                return 1;
            }
        }

        private static void Build(Options options, TextWriter output, Action<string, string> start)
        {
            if (options.Launch && !File.Exists(options.Game))
            {
                throw new BuildException($"the game is not at {options.Game}");
            }

            var archive = MapBuild.Run(options.Folder, new BuildOptions { Release = options.Release }, output);
            if (options.Launch)
            {
                start(options.Game, $"-launch -windowmode windowed -loadfile \"{archive}\"");
            }
        }

        // Builds now and again after each change to the project, until cancelled; a failed build is reported and the watch goes on.
        private static void Watch(Options options, TextWriter output, TextWriter error, Action<string, string> start, Action stop, CancellationToken cancel)
        {
            var project = MapProject.Find(options.Folder);
            if (options.Launch && !File.Exists(options.Game))
            {
                throw new BuildException($"the game is not at {options.Game}");
            }

            using var changed = new SemaphoreSlim(0);
            using var watcher = new FileSystemWatcher(project.Folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            void OnChange(object sender, FileSystemEventArgs change)
            {
                var first = Path.GetRelativePath(project.Folder, change.FullPath).Split(Path.DirectorySeparatorChar)[0];
                if (!Unwatched.Contains(first, StringComparer.OrdinalIgnoreCase))
                {
                    changed.Release();
                }
            }

            watcher.Changed += OnChange;
            watcher.Created += OnChange;
            watcher.Deleted += OnChange;
            watcher.Renamed += OnChange;
            watcher.EnableRaisingEvents = true;
            while (true)
            {
                Report(error, () => Build(options, output, start));
                output.WriteLine("moonwell: watching for changes; Ctrl+C stops");
                try
                {
                    changed.Wait(cancel);

                    // A save touches several files: build once they have been quiet for a moment.
                    while (changed.Wait(300, cancel))
                    {
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                // The game holds the map open, so it is closed before the build writes it again.
                if (options.Launch)
                {
                    stop();
                }
            }
        }

        private static void Clean(string folder, TextWriter output)
        {
            var project = MapProject.Find(folder);
            if (!Directory.Exists(project.BuildFolder))
            {
                output.WriteLine(@"moonwell: no build\ to delete");
                return;
            }

            try
            {
                Directory.Delete(project.BuildFolder, true);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                throw new BuildException($@"cannot delete build\ (is the game or World Editor using the map?): {failure.Message}");
            }

            output.WriteLine(@"moonwell: deleted build\");
        }

        private static void Objects(string folder, TextWriter output)
        {
            var objects = MapObjects.List(folder, TextWriter.Null);
            if (objects.Count == 0)
            {
                output.WriteLine("moonwell: the map declares no objects");
                return;
            }

            var kind = objects.Max(entry => entry.Kind.Length);
            var name = objects.Max(entry => entry.ClassName.Length);
            foreach (var (className, kindName, id, root) in objects)
            {
                output.WriteLine($"{id}  {kindName.PadRight(kind)}  {className.PadRight(name)}  based on {root}");
            }
        }

        // The tool's version without the source revision the SDK appends after a plus sign.
        private static string Version()
        {
            var assembly = typeof(Cli).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString(3)
                ?? "unknown";
            var plus = version.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? version : version.Substring(0, plus);
        }

        private static bool TryParse(string[] args, out Options options)
        {
            options = new Options();
            if (args.Length == 0 || !new[] { "build", "run", "watch", "new", "import", "clean", "objects" }.Contains(args[0]))
            {
                return false;
            }

            options.Command = args[0];
            options.Launch = args[0] == "run";
            var builds = args[0] is "build" or "run" or "watch";
            var gameGiven = false;
            var positionals = new List<string>();
            for (var i = 1; i < args.Length; i++)
            {
                var argument = args[i];
                if (argument == "--release" && builds)
                {
                    options.Release = true;
                }
                else if (argument == "--run" && options.Command == "watch")
                {
                    options.Launch = true;
                }
                else if (argument == "--game" && options.Command is "run" or "watch" && i + 1 < args.Length)
                {
                    options.Game = args[++i];
                    gameGiven = true;
                }
                else if (argument.StartsWith("--", StringComparison.Ordinal) || positionals.Count == (options.Command == "import" ? 2 : 1))
                {
                    return false;
                }
                else
                {
                    positionals.Add(argument);
                }
            }

            // import takes the map, then the name; the others take one folder or name.
            if (options.Command == "import")
            {
                options.Source = positionals.Count == 2 ? positionals[0] : string.Empty;
                options.Name = positionals.Count == 2 ? positionals[1] : string.Empty;
            }
            else if (positionals.Count == 1)
            {
                options.Folder = positionals[0];
                options.Name = positionals[0];
            }

            // A game is named only for a command that starts one; new needs its name, import its map and name.
            return (!gameGiven || options.Launch) && (options.Command != "new" || positionals.Count == 1) && (options.Command != "import" || positionals.Count == 2);
        }

        private sealed class Options
        {
            public string Command { get; set; } = "build";

            public string Folder { get; set; } = ".";

            public string Name { get; set; } = string.Empty;

            // import: the map the project is made from.
            public string Source { get; set; } = string.Empty;

            public bool Release { get; set; }

            // run, and watch --run: start the game with the map.
            public bool Launch { get; set; }

            public string Game { get; set; } = Path.Combine(GameInstall.Places()[0], "_retail_", "x86_64", "Warcraft III.exe");
        }
    }
}
