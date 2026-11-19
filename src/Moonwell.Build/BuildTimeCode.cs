using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Moonwell.BuildTime;

namespace Moonwell.Build
{
    /// <summary>
    /// Compiles and runs a map's build code, the C# files in its <c>buildtime</c> folder (decision 371), before the map's game
    /// code is compiled: each <see cref="BuildStep"/> reads the map's files and gives its results, which <see cref="DataWriter"/>
    /// writes into <c>Data</c>. The folder is compiled on its own, against .NET, <c>Moonwell.BuildTime</c> and War3Net, so it sees
    /// none of the game's code, and <c>dotnet build</c> and CSharp.lua leave it out.
    /// </summary>
    /// <remarks>
    /// A run's gives, warnings and reports are kept with a fingerprint of everything under <c>buildtime</c>, <c>map</c>,
    /// <c>assets</c> and <c>data</c>; while those are unchanged, the next build uses what was kept and runs nothing.
    /// </remarks>
    public static class BuildTimeCode
    {
        /// <summary>The folder's name in a map project.</summary>
        public const string FolderName = "buildtime";

        /// <summary>The step's name in the build's progress, shown only for a map that has build code.</summary>
        public const string StepName = "Run build code";

        private const string StateName = "buildtime.state.json";

        /// <summary>The JSON file of each give the last run left, in name order.</summary>
        /// <param name="project">The map project.</param>
        public static List<string> GiveFiles(MapProject project)
        {
            var folder = GivesFolder(project);
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.json").OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
        }

        /// <summary>Runs the map's build code, or takes the last run's results when nothing it could read has changed.</summary>
        /// <param name="project">The map project.</param>
        /// <param name="options">What kind of build this is.</param>
        /// <param name="log">Where the build code's warnings go.</param>
        /// <exception cref="BuildException">The build code does not compile, fails the build, or throws.</exception>
        public static void Run(MapProject project, BuildOptions options, TextWriter log)
        {
            var folder = Path.Combine(project.Folder, FolderName);
            var sources = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToList()
                : new List<string>();
            var gives = GivesFolder(project);
            var reports = Path.Combine(project.BuildFolder, "reports");
            var statePath = Path.Combine(project.GeneratedFolder, StateName);
            if (sources.Count == 0)
            {
                Delete(gives, reports, statePath);
                return;
            }

            options.Step?.Invoke(StepName);
            var fingerprint = Fingerprint(project, options.Release);
            var kept = ReadState(statePath);
            if (kept != null && kept.Fingerprint == fingerprint && kept.Reports.All(name => File.Exists(Path.Combine(reports, name))))
            {
                foreach (var line in kept.Lines)
                {
                    log.WriteLine(line);
                }

                return;
            }

            // A run that fails leaves nothing kept, so the next build runs again.
            Delete(gives, reports, statePath);
            var lines = new List<string>();
            var (assembly, symbols) = Compile(project, sources, options.Release, lines);
            var run = new BuildRun(new MapReader(project.MapFolder, project.AssetsFolder));
            var context = new AssemblyLoadContext("moonwell-buildtime", true);
            try
            {
                RunIn(context, assembly, symbols, run, project, lines);
            }
            finally
            {
                context.Unload();
            }

            lines.AddRange(run.Warnings.Select(warning => "warning: " + warning));
            Directory.CreateDirectory(gives);
            foreach (var give in run.Gives)
            {
                File.WriteAllText(Path.Combine(gives, give.Key + ".json"), give.Value);
            }

            if (run.Reports.Count > 0)
            {
                Directory.CreateDirectory(reports);
                foreach (var report in run.Reports)
                {
                    File.WriteAllBytes(Path.Combine(reports, report.Key), report.Value);
                }

                // The line is printed again by a build that runs nothing, so it says where they are, not that they were written.
                lines.Add($@"note: the build code's {(run.Reports.Count == 1 ? "report is" : run.Reports.Count + " reports are")} in build\reports\");
            }

            File.WriteAllText(statePath, JsonSerializer.Serialize(new State(fingerprint, lines, run.Reports.Keys.ToList())));
            foreach (var line in lines)
            {
                log.WriteLine(line);
            }
        }

        private static string GivesFolder(MapProject project)
        {
            return Path.Combine(project.GeneratedFolder, FolderName);
        }

        private static void Delete(string gives, string reports, string statePath)
        {
            foreach (var folder in new[] { gives, reports })
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }

            // File.Delete stops on a folder that is not there, as build\generated is not before a map's first build.
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }
        }

        private static State? ReadState(string path)
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<State>(File.ReadAllText(path)) : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // The tool's version, the kind of build, and the name, size and time of every file build code is likely to read.
        private static string Fingerprint(MapProject project, bool release)
        {
            var text = new StringBuilder();
            text.Append(PackageSources.ToolVersion).Append('\n').Append(release ? "release" : "development").Append('\n');
            foreach (var folder in new[] { Path.Combine(project.Folder, FolderName), project.MapFolder, project.AssetsFolder, project.DataFolder })
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
                {
                    var info = new FileInfo(file);
                    text.Append(Path.GetRelativePath(project.Folder, file)).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
                }
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }

        // The folder's files as one assembly and its symbols, compiled as the map's project compiles its code: C# 13, nullable on,
        // DEBUG or RELEASE. Errors stop the build in the compiler's own form; warnings go to the lines the build prints.
        private static (byte[] Assembly, byte[] Symbols) Compile(MapProject project, List<string> sources, bool release, List<string> lines)
        {
            var parse = new CSharpParseOptions(LanguageVersion.CSharp13, preprocessorSymbols: new[] { "TRACE", release ? "RELEASE" : "DEBUG" });
            var trees = sources.Select(path => CSharpSyntaxTree.ParseText(SourceText.From(CompiledFiles.ReadSource(path), Encoding.UTF8), parse, path)).ToList();
            var compilation = CSharpCompilation.Create(
                project.Name + ".BuildTime",
                trees,
                References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug, nullableContextOptions: NullableContextOptions.Enable));

            using var assembly = new MemoryStream();
            using var symbols = new MemoryStream();
            var result = compilation.Emit(assembly, symbols, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
            var problems = result.Diagnostics
                .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                .OrderBy(diagnostic => diagnostic.Location.GetLineSpan().Path, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .ToList();
            if (!result.Success)
            {
                throw new BuildException(string.Join(Environment.NewLine, problems.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.ToString())));
            }

            lines.AddRange(problems.Select(diagnostic => diagnostic.ToString()));
            return (assembly.ToArray(), symbols.ToArray());
        }

        // .NET's own libraries, and Moonwell.BuildTime with the libraries it brings (War3Net's): nothing else of the tool's.
        private static List<MetadataReference> References()
        {
            var framework = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(path => string.Equals(Path.GetDirectoryName(path), framework, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var waiting = new Queue<Assembly>();
            waiting.Enqueue(typeof(BuildStep).Assembly);
            while (waiting.TryDequeue(out var assembly))
            {
                if (assembly.Location.Length == 0 || !paths.Add(assembly.Location))
                {
                    continue;
                }

                foreach (var name in assembly.GetReferencedAssemblies())
                {
                    try
                    {
                        waiting.Enqueue(Assembly.Load(name));
                    }
                    catch (Exception failure) when (failure is FileNotFoundException or FileLoadException or BadImageFormatException)
                    {
                        // A library War3Net names and never ships: code that needs it gets the compiler's error.
                    }
                }
            }

            return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
        }

        // Kept apart from Run so that nothing of the loaded assembly is on Run's frame when the context unloads.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunIn(AssemblyLoadContext context, byte[] assembly, byte[] symbols, BuildRun run, MapProject project, List<string> lines)
        {
            var loaded = context.LoadFromStream(new MemoryStream(assembly), new MemoryStream(symbols));
            var steps = loaded.GetTypes()
                .Where(type => !type.IsAbstract && typeof(BuildStep).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
            if (steps.Count == 0)
            {
                lines.Add($@"warning: {FolderName}\ has no class that inherits BuildStep, so no build code ran");
            }

            foreach (var type in steps)
            {
                var name = MapClasses.Describe(type);
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new BuildException($"{name} needs a public constructor without parameters");
                }

                try
                {
                    var step = (BuildStep)Activator.CreateInstance(type)!;
                    step.Start(run);
                    step.Run();
                }
                catch (Exception failure)
                {
                    throw Stopped(failure, name, project);
                }
            }
        }

        // A build step's own error is the line the build prints; anything else names the step and its line, as a constructor's does.
        private static BuildException Stopped(Exception failure, string name, MapProject project)
        {
            while (failure is TargetInvocationException or TypeInitializationException && failure.InnerException != null)
            {
                failure = failure.InnerException;
            }

            if (failure is BuildFailure)
            {
                return new BuildException(failure.Message);
            }

            var place = MapClasses.FirstLineInProject(new StackTrace(failure, true), project);
            return new BuildException($"{(place == null ? string.Empty : place + ": ")}{name} threw: {failure.Message}");
        }

        private sealed record State(string Fingerprint, List<string> Lines, List<string> Reports);
    }
}
