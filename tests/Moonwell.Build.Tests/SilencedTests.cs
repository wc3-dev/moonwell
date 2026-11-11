using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The build's list of the places where a map turns a Moonwell rule off.</summary>
    [Collection("Sample")]
    public sealed class SilencedTests : IDisposable
    {
        private const string Library = """
            using Moonwell;

            public class Opening
            {
                [SyncUnchecked]
                public Opening()
                {
                }

                [SyncUnchecked]
                public int Count => 1;

                [SyncUnchecked]
                public void Start()
                {
                }

                public void Show()
                {
                    System.Action later = [SyncUnchecked] () => { };
                    later();
                }

                public void Checked()
                {
                }
            }
            """;

        private readonly SampleBuilds builds;
        private readonly TestFolders folders;

        public SilencedTests(SampleBuilds builds, ITestOutputHelper output)
        {
            this.builds = builds;
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_source_lists_each_mark_pragma_and_suppression_at_its_line()
        {
            const string Source = """
                using System.Diagnostics.CodeAnalysis;
                using Moonwell;

                public sealed class Hud
                {
                    private static readonly System.Action Later = [SyncUnchecked] () => { };

                    [SyncUnchecked]
                    public Hud()
                    {
                    }

                    [Moonwell.SyncUncheckedAttribute]
                    public void Refresh(Player player)
                    {
                        Local.For(player, [SyncUnchecked] () => { });

                        [SyncUnchecked]
                        void Draw()
                        {
                        }
                #pragma warning disable MW0048, CS0168, MW0002
                #pragma warning restore MW0048
                #pragma warning disable CS0168
                #pragma warning disable
                    }

                    [SuppressMessage("Multiplayer", "MW0006:Changes the game")]
                    [SuppressMessage("Style", "IDE0051")]
                    public int Count { [SyncUnchecked] get; set; }
                }
                """;

            var found = Silenced.InSource(Source, new[] { "DEBUG" });

            Assert.Equal(
                new[]
                {
                    (6, "[SyncUnchecked] on a lambda in Hud.Later"),
                    (8, "[SyncUnchecked] on Hud.Hud"),
                    (13, "[SyncUnchecked] on Hud.Refresh"),
                    (16, "[SyncUnchecked] on a lambda in Hud.Refresh"),
                    (18, "[SyncUnchecked] on Draw in Hud.Refresh"),
                    (22, "#pragma warning disable MW0048, MW0002"),
                    (25, "#pragma warning disable with no rule named, which turns every rule off"),
                    (28, "[SuppressMessage] for MW0006"),
                    (30, "[SyncUnchecked] on Hud.Count"),
                },
                found);
        }

        [Fact]
        public void a_pragma_in_a_branch_the_build_leaves_out_is_not_listed()
        {
            const string Source = "#if RELEASE\n#pragma warning disable MW0001\n#endif\nclass A { }\n";

            Assert.Empty(Silenced.InSource(Source, new[] { "DEBUG" }));
            Assert.Equal(new[] { (2, "#pragma warning disable MW0001") }, Silenced.InSource(Source, new[] { "RELEASE" }));
        }

        [Fact]
        public void a_config_file_lists_the_lines_that_hide_a_moonwell_rule()
        {
            var found = Silenced.InConfig(new[]
            {
                "root = true",
                "[*.cs]",
                "dotnet_diagnostic.MW0004.severity = none",
                "dotnet_diagnostic.MW0005.severity = warning",
                "dotnet_diagnostic.CA1000.severity = none",
                "  dotnet_analyzer_diagnostic.category-Multiplayer.severity = silent",
                "dotnet_analyzer_diagnostic.category-Style.severity = none",
                "dotnet_analyzer_diagnostic.severity=suggestion",
                "# dotnet_diagnostic.MW0001.severity = none",
            });

            Assert.Equal(
                new[]
                {
                    (3, "dotnet_diagnostic.MW0004.severity = none"),
                    (6, "dotnet_analyzer_diagnostic.category-Multiplayer.severity = silent"),
                    (8, "dotnet_analyzer_diagnostic.severity=suggestion"),
                },
                found);
        }

        [Fact]
        public void a_library_lists_its_marked_members()
        {
            var library = Compile("Code", Library, folders.TemporaryFolderOutsideRepository());

            Assert.Equal(
                new[] { "Opening.Count", "Opening.Opening", "Opening.Start", "a lambda or local function in Opening.Show" },
                Silenced.InLibrary(library).OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void the_project_and_a_library_of_the_maps_own_are_listed()
        {
            var project = MapProject.Find(folders.TemporaryCopyOf("CallsGame"));
            File.WriteAllText(Path.Combine(project.Folder, "Hud.cs"), "class Hud\n{\n    [Moonwell.SyncUnchecked]\n    void Refresh() { }\n}\n");
            project.NoWarn = "1701;1702;MW0013";
            project.AnalyzersOff = "RunAnalyzers";
            var bin = folders.TemporaryFolderOutsideRepository();
            var library = Compile("Code", Library, bin);
            var map = Compile("MapCode", "class Uses { void F() { new Opening().Start(); } }", bin, library);

            var places = Silenced.Find(project, map);

            Assert.Equal(
                new[]
                {
                    "Hud.cs:3: [SyncUnchecked] on Hud.Refresh",
                    "CallsGame.csproj: NoWarn MW0013 (set in a file the project imports)",
                    "CallsGame.csproj: RunAnalyzers is false, so no Moonwell rule ran",
                },
                places.Where(place => !place.StartsWith("Code.dll", StringComparison.Ordinal)));
            Assert.Contains("Code.dll: [SyncUnchecked] on Opening.Start", places);
            Assert.Equal(4, places.Count(place => place.StartsWith("Code.dll: ", StringComparison.Ordinal)));
        }

        [Fact]
        public void few_places_are_printed_and_many_only_counted()
        {
            var project = MapProject.Find(folders.TemporaryCopyOf("CallsGame"));
            Directory.CreateDirectory(project.BuildFolder);
            var file = Path.Combine(project.BuildFolder, "silenced.txt");

            var one = new StringWriter();
            Silenced.Write(project, new[] { "Hud.cs:3: [SyncUnchecked] on Hud.Refresh" }, one);
            Assert.Equal(new[] { @"moonwell: 1 place turns a Moonwell rule off (build\silenced.txt):", "  Hud.cs:3: [SyncUnchecked] on Hud.Refresh" }, Lines(one));
            Assert.Equal(new[] { "Hud.cs:3: [SyncUnchecked] on Hud.Refresh" }, File.ReadAllLines(file));

            var five = new StringWriter();
            var places = Enumerable.Range(1, 5).Select(line => $"Hud.cs:{line}: #pragma warning disable MW0001").ToList();
            Silenced.Write(project, places, five);
            Assert.Equal(places.Select(place => "  " + place).Prepend(@"moonwell: 5 places turn a Moonwell rule off (build\silenced.txt):"), Lines(five));

            var six = new StringWriter();
            places.Add("Map.csproj:6: NoWarn MW0005");
            Silenced.Write(project, places, six);
            Assert.Equal(new[] { @"moonwell: 6 places turn a Moonwell rule off, listed in build\silenced.txt" }, Lines(six));
            Assert.Equal(places, File.ReadAllLines(file));

            // A build that turns nothing off takes an earlier build's list away.
            var none = new StringWriter();
            Silenced.Write(project, Array.Empty<string>(), none);
            Assert.Empty(none.ToString());
            Assert.False(File.Exists(file));
        }

        [Fact]
        public void a_map_that_turns_nothing_off_gets_no_list()
        {
            Assert.Equal(0, builds.Development.ExitCode);
            Assert.DoesNotContain("Moonwell rule off", builds.Development.Output);
            Assert.DoesNotContain("Moonwell rule off", builds.Release.Output);
            Assert.False(File.Exists(Path.Combine(builds.Development.BuildFolder, "silenced.txt")));
            Assert.False(File.Exists(Path.Combine(builds.Release.BuildFolder, "silenced.txt")));
        }

        // Each line of Silencing.cs breaks a rule, so the build passing shows that each kind does silence, and the list names it.
        [Fact]
        public void a_build_lists_each_kind_of_silencing()
        {
            var project = folders.TemporaryCopyOf("Maths");
            File.WriteAllText(Path.Combine(project, "Silencing.cs"), """
                using System.Collections.Generic;
                using System.Diagnostics.CodeAnalysis;
                using Moonwell;

                namespace MathsFixture
                {
                    public static class Silencing
                    {
                        [SyncUnchecked]
                        public static int Marked(Player player) => Natives.GetHandleId(player.Handle);

                        public static void Block(Player player)
                        {
                            Local.For(player, [SyncUnchecked] () => Natives.CreateUnit(player.Handle, 0, 0f, 0f, 0f));
                        }

                        public static float Camera()
                        {
                #pragma warning disable MW0002
                            return Natives.GetCameraTargetPositionX();
                #pragma warning restore MW0002
                        }

                        [SuppressMessage("Multiplayer", "MW0006")]
                        public static void Select(Player player) => Natives.SelectGroupForPlayerBJ(Natives.CreateGroup(), player.Handle);

                        public static long Total() => 1;

                        public static void Loop()
                        {
                            foreach (var pair in new Dictionary<int, int>())
                            {
                            }
                        }
                    }
                }
                """);
            File.WriteAllText(Path.Combine(project, ".editorconfig"), "[*.cs]\ndotnet_diagnostic.MW0004.severity = none\n");
            var projectFile = Path.Combine(project, "Maths.csproj");
            const string Line = "<ImplicitUsings>disable</ImplicitUsings>";
            File.WriteAllText(projectFile, File.ReadAllText(projectFile).Replace(Line, Line + "\n    <NoWarn>$(NoWarn);MW0005</NoWarn>", StringComparison.Ordinal));

            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));

            Assert.True(exitCode == 0, error.ToString() + output);
            Assert.Equal(
                new[]
                {
                    "Silencing.cs:9: [SyncUnchecked] on Silencing.Marked",
                    "Silencing.cs:14: [SyncUnchecked] on a lambda in Silencing.Block",
                    "Silencing.cs:19: #pragma warning disable MW0002",
                    "Silencing.cs:24: [SuppressMessage] for MW0006",
                    "Maths.csproj:6: NoWarn MW0005",
                    ".editorconfig:2: dotnet_diagnostic.MW0004.severity = none",
                },
                File.ReadAllLines(Path.Combine(project, "build", "silenced.txt")));
            var last = Lines(output).TakeLast(2).ToArray();
            Assert.Equal(new[] { @"moonwell: 6 places turn a Moonwell rule off, listed in build\silenced.txt", @"moonwell: built build\Maths.w3x" }, last);
        }

        private static string[] Lines(StringWriter writer)
        {
            return writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();
        }

        // Compiles code against the built Moonwell.Natives, and the libraries given, and writes the assembly into the folder.
        private static string Compile(string name, string source, string folder, params string[] libraries)
        {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(path => !Path.GetFileName(path).StartsWith("Moonwell", StringComparison.Ordinal))
                .Append(Path.Combine(AppContext.BaseDirectory, "Moonwell.Natives.dll"))
                .Concat(libraries)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(source) }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var assembly = Path.Combine(folder, name + ".dll");
            var result = compilation.Emit(assembly);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            return assembly;
        }
    }
}
