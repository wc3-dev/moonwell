using System.IO.Enumeration;
using System.Reflection;
using Moonwell.Build;
using Moonwell.GameData;
using Spectre.Console;

namespace Moonwell.Tool
{
    /// <summary>The <c>moonwell</c> command: parses its arguments and runs the build or another command.</summary>
    internal static class Cli
    {
        private static readonly (string Command, string Takes)[] Commands =
        {
            ("build", "[folder] [--release] [--verbose]"),
            ("run", "[folder] [--release] [--verbose] [--game <exe>]"),
            ("watch", "[folder] [--release] [--verbose] [--run] [--game <exe>]"),
            ("new", "<name>"),
            ("import", "<map> <name>"),
            ("clean", "[folder]"),
            ("objects", "[folder] [--to-map]"),
            ("mdx", "<model> [--mode sd|hd|de] [--game <folder>]"),
            ("game find", "<path> [--mode sd|hd|de] [--game <folder>]"),
            ("game extract", "<path> [--out <path>] [--mode sd|hd|de] [--game <folder>]"),
        };

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
            // moonwell alone, in a terminal, asks which command to run.
            if (args.Length == 0 && CommandPicker.Ask(output) is { } picked)
            {
                args = picked;
            }

            if (args.Length == 1 && args[0] == "--version")
            {
                new Say(output).Line(Version());
                return 0;
            }

            if (args.Length > 0 && args[0] == "mdx")
            {
                if (!TryParseGame(args, out var model))
                {
                    return Usage(error);
                }

                return Report(error, () => ModelView.Show(model.Path, model.Mode, model.Install, output));
            }

            if (args.Length > 0 && args[0] == "game")
            {
                if (!TryParseGame(args, out var game))
                {
                    return Usage(error);
                }

                return Report(error, () => Game(game, output));
            }

            if (!TryParse(args, out var options))
            {
                return Usage(error);
            }

            var say = new Say(output);
            return Report(error, () =>
            {
                switch (options.Command)
                {
                    case "new":
                        var folder = NewMap.Write(options.Name, ".", Version());
                        say.Result($"Made {folder}");
                        break;
                    case "import":
                        var imported = NewMap.Write(options.Name, ".", Version());
                        using (var log = say.Log())
                        {
                            OldMapImport.Run(options.Source, imported, log);
                            try
                            {
                                ObjectImport.Run(imported, log);
                            }
                            catch (BuildException failure)
                            {
                                say.Message("note", $"the object data stays in the map's object files, which the build keeps: {failure.Message}");
                            }
                        }

                        say.Result($"Made {imported}");
                        break;
                    case "clean":
                        Clean(options.Folder, say);
                        break;
                    case "objects" when options.ToMap:
                        IReadOnlyList<string> written = Array.Empty<string>();
                        _ = BuildView.Show(output, error, false, (log, step) =>
                        {
                            written = MapObjects.WriteToMap(options.Folder, log, step);
                            return string.Empty;
                        }) ?? throw new ReportedException();
                        if (written.Count > 0)
                        {
                            say.Result($@"Wrote {string.Join(", ", written.Take(8))}{(written.Count > 8 ? $" and {written.Count - 8} more" : string.Empty)} into map\");
                        }
                        else
                        {
                            say.Message("note", @"map\ already holds the built objects and assets");
                        }

                        break;
                    case "objects":
                        Objects(options.Folder, say);
                        break;
                    case "watch":
                        Watch(options, output, error, start, stop ?? (() => { }), cancel);
                        break;
                    default:
                        Build(options, output, error, start);
                        break;
                }
            });
        }

        // The commands and what each takes, for arguments the tool does not understand.
        private static int Usage(TextWriter error)
        {
            var say = new Say(error);
            var width = Commands.Max(command => command.Command.Length);
            foreach (var (command, takes) in Commands)
            {
                say.Line($"[bold]moonwell {command.PadRight(width)}[/]  [dim]{takes.EscapeMarkup()}[/]");
            }

            return 2;
        }

        // Runs a command, reporting what fails: 0, or 1 with the message.
        private static int Report(TextWriter error, Action command)
        {
            var say = new Say(error);
            try
            {
                command();
                return 0;
            }
            catch (ReportedException)
            {
                return 1;
            }
            catch (BuildException failure)
            {
                say.Message("error", failure.Message);
                return 1;
            }
            catch (Exception failure)
            {
                say.Message("error", $"unexpected error: {failure.GetType().Name}: {failure.Message}");
                say.Line($"[dim]{(failure.StackTrace ?? string.Empty).EscapeMarkup()}[/]");
                return 1;
            }
        }

        private static void Build(Options options, TextWriter output, TextWriter error, Action<string, string> start)
        {
            if (options.Launch && !File.Exists(options.Game))
            {
                throw new BuildException($"the game is not at {options.Game}");
            }

            var archive = BuildView.Show(output, error, options.Verbose, (log, step) => MapBuild.Run(options.Folder, new BuildOptions { Release = options.Release, Step = step }, log))
                ?? throw new ReportedException();
            if (options.Launch)
            {
                start(options.Game, $"-launch -nowfpause -windowmode windowedfullscreen -loadfile \"{archive}\"");
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
                Report(error, () => Build(options, output, error, start));
                var say = new Say(output);
                say.Line();
                say.Line("[dim]Watching for changes; Ctrl+C stops[/]");
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

        private static void Clean(string folder, Say say)
        {
            var project = MapProject.Find(folder);
            if (!Directory.Exists(project.BuildFolder))
            {
                say.Message("note", @"no build\ to delete");
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

            say.Result(@"Deleted build\");
        }

        private static void Objects(string folder, Say say)
        {
            var objects = MapObjects.List(folder, TextWriter.Null);
            if (objects.Count == 0)
            {
                say.Message("note", "the map declares no objects");
                return;
            }

            var kind = objects.Max(entry => entry.Kind.Length);
            var name = objects.Max(entry => entry.ClassName.Length);
            foreach (var (className, kindName, id, root) in objects)
            {
                say.Line($"[bold]{id}[/]  [dim]{kindName.PadRight(kind)}[/]  {className.PadRight(name).EscapeMarkup()}  [dim]{root}[/]");
            }
        }

        // game find and game extract: one file as the game finds it, or every file under a folder or matching a pattern with *.
        private static void Game(GameOptions options, TextWriter output)
        {
            var say = new Say(output);
            GameFiles files;
            try
            {
                files = GameFiles.Open(GameInstall.Require(InstallOf(options.Install)));
            }
            catch (IOException failure)
            {
                throw new BuildException(failure.Message);
            }

            using (files)
            {
                var path = GameFiles.Normalized(options.Path);
                var star = path.IndexOf('*', StringComparison.Ordinal);
                if (star < 0 && !path.EndsWith('/') && files.Find(path, options.Mode) is { } found)
                {
                    if (!options.Extract)
                    {
                        say.Line(found.EscapeMarkup());
                        return;
                    }

                    // The name as the game stores it: a .blp asked for is written as the .dds it is.
                    var name = found[(found.LastIndexOfAny(new[] { '/', ':' }) + 1)..];
                    var target = options.Out == null ? name
                        : Directory.Exists(options.Out) || options.Out.EndsWith('/') || options.Out.EndsWith('\\') ? Path.Combine(options.Out, name)
                        : options.Out;
                    Write(target, files.Read(path, options.Mode));
                    say.Result($"Wrote {target}");
                    return;
                }

                var folder = star < 0 ? path.TrimEnd('/') + "/" : path[..(path.LastIndexOf('/', star) + 1)];
                var pattern = star < 0 ? folder + "*" : path;
                var listed = files.List(folder, options.Mode, string.Empty)
                    .Where(file => FileSystemName.MatchesSimpleExpression(pattern, file))
                    .ToList();
                if (listed.Count == 0)
                {
                    throw new BuildException($"the game has no file {options.Path}");
                }

                foreach (var file in listed)
                {
                    if (options.Extract)
                    {
                        Write(Path.Combine(options.Out ?? ".", file), files.Read(file, options.Mode));
                    }
                    else
                    {
                        say.Line(file.EscapeMarkup());
                    }
                }

                if (options.Extract)
                {
                    say.Result($"Wrote {listed.Count} files into {options.Out ?? "."}");
                }
            }
        }

        private static void Write(string target, byte[]? bytes)
        {
            if (Path.GetDirectoryName(Path.GetFullPath(target)) is { } folder)
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllBytes(target, bytes ?? throw new BuildException($"the game's storage does not hold {target}"));
        }

        // --game takes the install's folder or anything inside it, such as the exe that run takes.
        private static string? InstallOf(string? given)
        {
            for (var folder = given; !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
            {
                if (GameInstall.IsInstall(folder))
                {
                    return folder;
                }
            }

            return given;
        }

        private static bool TryParseGame(string[] args, out GameOptions options)
        {
            // mdx takes its model and the same options as game find, its paths looked for in HD art unless told.
            var model = args.Length > 0 && args[0] == "mdx";
            options = new GameOptions { Mode = model ? "hd" : "sd" };
            if (!model && (args.Length < 2 || args[1] is not ("find" or "extract")))
            {
                return false;
            }

            options.Extract = !model && args[1] == "extract";
            for (var i = model ? 1 : 2; i < args.Length; i++)
            {
                var argument = args[i];
                var next = i + 1 < args.Length ? args[i + 1] : null;
                if (argument == "--mode" && next is "sd" or "hd" or "de")
                {
                    options.Mode = args[++i];
                }
                else if (argument == "--game" && next != null)
                {
                    options.Install = args[++i];
                }
                else if (argument == "--out" && options.Extract && next != null)
                {
                    options.Out = args[++i];
                }
                else if (argument.StartsWith("--", StringComparison.Ordinal) || options.Path.Length > 0)
                {
                    return false;
                }
                else
                {
                    options.Path = argument;
                }
            }

            return options.Path.Length > 0;
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
                else if (argument == "--verbose" && builds)
                {
                    options.Verbose = true;
                }
                else if (argument == "--run" && options.Command == "watch")
                {
                    options.Launch = true;
                }
                else if (argument == "--to-map" && options.Command == "objects")
                {
                    options.ToMap = true;
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

        // A build failure the build view has shown already.
        private sealed class ReportedException : Exception
        {
        }

        private sealed class GameOptions
        {
            public bool Extract { get; set; }

            public string Path { get; set; } = string.Empty;

            public string Mode { get; set; } = "sd";

            // --game: the install's folder, or a path inside it; the usual place when not given.
            public string? Install { get; set; }

            // extract --out: the file to write, or the folder to write into; the current folder when not given.
            public string? Out { get; set; }
        }

        private sealed class Options
        {
            public string Command { get; set; } = "build";

            public string Folder { get; set; } = ".";

            public string Name { get; set; } = string.Empty;

            // import: the map the project is made from.
            public string Source { get; set; } = string.Empty;

            public bool Release { get; set; }

            // build, run and watch --verbose: every line of the build's log, dotnet's whole output among them, beside the checklist.
            public bool Verbose { get; set; }

            // run, and watch --run: start the game with the map.
            public bool Launch { get; set; }

            // objects --to-map: write the built object files into the source map. No build sets it (decisions 4 and 275).
            public bool ToMap { get; set; }

            public string Game { get; set; } = Path.Combine(GameInstall.Places()[0], "_retail_", "x86_64", "Warcraft III.exe");
        }
    }
}
