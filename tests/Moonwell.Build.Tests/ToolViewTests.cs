using System.Text;
using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The build's checklist (decisions 361 and 363), shown from made-up logs, and moonwell mdx's tree; output that is no terminal is plain text.</summary>
    public sealed class ToolViewTests
    {
        private static readonly string Here = Environment.CurrentDirectory;

        [Fact]
        public void a_clean_build_shows_its_steps_and_hides_dotnets_lines()
        {
            var (archive, output, error) = Show(false, (log, step) =>
            {
                log.WriteLine("moonwell: Arena (development build)");
                step("Compile C#");
                log.WriteLine("  Determining projects to restore...");
                log.WriteLine("Build succeeded.");
                step("Compile to Lua");
                log.WriteLine("note: assets\\x.blp replaces the map's file");
                step("Pack the map");
                log.WriteLine(@"moonwell: 2 places turn a Moonwell rule off (build\silenced.txt):");
                log.WriteLine("  Start.cs(3,1): #pragma warning disable MW0001");
                log.WriteLine("  Start.cs(9,1): #pragma warning disable MW0002");
                log.WriteLine(@"moonwell: built build\Arena.w3x");
                return "Arena.w3x";
            });

            Assert.Equal("Arena.w3x", archive);
            Assert.Equal(string.Empty, error);
            var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            Assert.Equal("Arena · development build", lines[0]);
            Assert.Matches(@"^  ✓ Compile C#      \d+\.\ds$", lines[2]);
            Assert.Matches(@"^  ✓ Compile to Lua  \d+\.\ds$", lines[3]);
            Assert.Equal(@"      note     assets\x.blp replaces the map's file", lines[4]);
            Assert.Equal(@"      note     2 places turn a Moonwell rule off (build\silenced.txt)", lines[6]);
            Assert.Matches(@"^✓ Built build\\Arena\.w3x in \d+\.\ds$", lines[8]);
            Assert.DoesNotContain("Determining", output);
            Assert.DoesNotContain("Build succeeded", output);
            Assert.DoesNotContain("MW0001", output);
        }

        [Fact]
        public void a_warnings_list_stays_on_its_one_line_outside_a_terminal()
        {
            var (_, output, _) = Show(false, (log, step) =>
            {
                step("Check assets");
                log.WriteLine("warning: these may be unused:");
                log.WriteLine(@"  assets\a.png");
                log.WriteLine(@"  assets\b.wav");
                log.WriteLine("note: after the list");
                return "Arena.w3x";
            });

            var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            Assert.Equal(@"      warning  these may be unused: assets\a.png, assets\b.wav", lines[1]);
            Assert.Equal("      note     after the list", lines[2]);
        }

        [Fact]
        public void verbose_shows_the_whole_log_too()
        {
            var (_, output, _) = Show(true, (log, step) =>
            {
                step("Compile C#");
                log.WriteLine("  Determining projects to restore...");
                log.WriteLine(@"moonwell: 1 place turns a Moonwell rule off (build\silenced.txt):");
                log.WriteLine("  Start.cs(3,1): #pragma warning disable MW0001");
                return "Arena.w3x";
            });

            Assert.Contains("  Determining projects to restore...", output);
            Assert.Contains("MW0001", output);
        }

        [Fact]
        public void a_failed_compile_shows_each_error_once_on_the_error_stream()
        {
            var file = Path.Combine(Here, "Start.cs");
            var project = Path.Combine(Here, "Arena.csproj");
            var (archive, output, error) = Show(false, (log, step) =>
            {
                step("Compile C#");
                for (var i = 0; i < 2; i++)
                {
                    log.WriteLine($"{file}(15,28): error CS1061: 'Player' does not contain a definition for 'StartXX' [{project}]");
                    log.WriteLine($"{file}(16,17): warning CS0168: The variable 'unused' is declared but never used [{project}]");
                }

                throw new BuildException("dotnet build failed" + Environment.NewLine + "AttackType is Moonwell.AttackType since Moonwell 0.2.1");
            });

            Assert.Null(archive);
            Assert.Matches(@"(?m)^  ✗ Compile C#      \d+\.\ds\r?$", output);
            var lines = error.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToList();
            Assert.Equal(new[]
            {
                "  error    Start.cs(15,28)  CS1061  'Player' does not contain a definition for 'StartXX'",
                "  warning  Start.cs(16,17)  CS0168  The variable 'unused' is declared but never used",
                "  note     AttackType is Moonwell.AttackType since Moonwell 0.2.1",
                "✗ Build failed: 1 error, 1 warning",
            }, lines);
        }

        [Fact]
        public void a_lua_step_error_reads_like_a_csharp_one()
        {
            var file = Path.Combine(Here, "Start.cs");
            var (_, output, error) = Show(false, (log, step) =>
            {
                step("Compile C#");
                step("Compile to Lua");
                throw new BuildException($"{file}(13,13): CSharp.lua cannot compile this code; write it another way" + Environment.NewLine + $"{file}(20,5): error CS0121: The call is ambiguous");
            });

            Assert.Matches(@"(?m)^  ✓ Compile C#      \d+\.\ds\r?\n  ✗ Compile to Lua  \d+\.\ds\r?$", output);
            Assert.Contains("  error    Start.cs(13,13)  CSharp.lua cannot compile this code; write it another way", error);
            Assert.Contains("  error    Start.cs(20,5)  CS0121  The call is ambiguous", error);
            Assert.EndsWith("✗ Build failed: 2 errors", error.TrimEnd());
        }

        [Fact]
        public void an_error_before_any_step_is_shown_alone()
        {
            var (_, output, error) = Show(false, (_, _) => throw new BuildException(@"no World Editor map: map\war3map.w3i is missing"));

            Assert.Equal(string.Empty, output);
            Assert.Equal(new[] { @"  error    no World Editor map: map\war3map.w3i is missing", "✗ Build failed: 1 error" }, error.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0));
        }

        [Fact]
        public void a_model_names_its_textures_and_its_emitters_models()
        {
            var model = Model(
                Chunk("TEXS", Texture(1, string.Empty), Texture(0, @"Textures\Zap1.blp"), Texture(0, @"Textures\Zap1.blp")),
                Chunk("PREM", Emitter(16, @"SharedModels\Lightning2.MDL")),
                Chunk("CORN", Emitter(32, @"Abilities\Spells\Bolt.pkfx")));

            var (paths, replaceable) = ModelView.Read(model);

            Assert.Equal(new[] { @"Textures\Zap1.blp", @"SharedModels\Lightning2.MDL", @"Abilities\Spells\Bolt.pkfx" }, paths);
            Assert.Equal(new[] { 1 }, replaceable);
            Assert.Empty(ModelView.Read(Encoding.ASCII.GetBytes("MDLVersion 800")).Paths);
        }

        [InstallFact]
        public void mdx_shows_the_games_files_a_model_names_and_what_those_name()
        {
            var (exitCode, output, error) = Mdx(@"Abilities\Spells\Orc\LightningBolt\LightningBoltMissile.mdx", "--mode", "sd");

            Assert.True(exitCode == 0, error);
            var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            Assert.EndsWith(@"LightningBoltMissile.mdx  game", lines[0]);
            var zap = Assert.Single(lines, line => line.EndsWith(@"Textures\Zap1.blp  game", StringComparison.Ordinal));
            var shared = Assert.Single(lines, line => line.EndsWith(@"SharedModels\Lightning2.MDL  game", StringComparison.Ordinal));
            var nested = lines[lines.IndexOf(shared) + 1];
            Assert.EndsWith(@"Textures\LightningBall.blp  game", nested);
            Assert.True(nested.IndexOf('T') > zap.IndexOf('T'), output);
        }

        [InstallFact]
        public void mdx_marks_a_file_beside_the_model_as_custom()
        {
            var folder = Path.Combine(Path.GetTempPath(), "moonwell-mdx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "Textures"));
            try
            {
                var model = Path.Combine(folder, "Mine.mdx");
                File.WriteAllBytes(model, Model(Chunk("TEXS", Texture(0, @"Textures\Mine.blp"), Texture(0, @"Textures\Zap1.blp"), Texture(0, @"Textures\NoSuchTexture.blp"))));
                File.WriteAllText(Path.Combine(folder, "Textures", "Mine.blp"), string.Empty);

                var (exitCode, output, error) = Mdx(model);

                Assert.True(exitCode == 0, error);
                Assert.Matches(@"(?m)Textures\\Mine\.blp  custom  .*Textures\\Mine\.blp\r?$", output);
                Assert.Matches(@"(?m)Textures\\Zap1\.blp  game\r?$", output);
                Assert.Matches(@"(?m)Textures\\NoSuchTexture\.blp  in neither the game nor the map\r?$", output);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static (int ExitCode, string Output, string Error) Mdx(params string[] arguments)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "mdx" }.Concat(arguments).ToArray(), output, error, (_, _) => throw new InvalidOperationException("mdx starts nothing"));
            return (exitCode, output.ToString(), error.ToString());
        }

        private static (string? Archive, string Output, string Error) Show(bool verbose, Func<TextWriter, Action<string>, string> build)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var archive = BuildView.Show(output, error, verbose, build);
            return (archive, output.ToString(), error.ToString());
        }

        private static byte[] Model(params byte[][] chunks) => Encoding.ASCII.GetBytes("MDLX").Concat(chunks.SelectMany(chunk => chunk)).ToArray();

        private static byte[] Chunk(string tag, params byte[][] entries)
        {
            var body = entries.SelectMany(entry => entry).ToArray();
            return Encoding.ASCII.GetBytes(tag).Concat(BitConverter.GetBytes(body.Length)).Concat(body).ToArray();
        }

        private static byte[] Texture(int replaceable, string path) => BitConverter.GetBytes(replaceable).Concat(Fixed(path)).Concat(new byte[4]).ToArray();

        // An emitter: its size, a node of 12 bytes (its own size and 8 more), the numbers before the path, the path, and 8 bytes after.
        private static byte[] Emitter(int before, string path)
        {
            var rest = BitConverter.GetBytes(12).Concat(new byte[8]).Concat(new byte[before]).Concat(Fixed(path)).Concat(new byte[8]).ToArray();
            return BitConverter.GetBytes(rest.Length + 4).Concat(rest).ToArray();
        }

        private static byte[] Fixed(string text)
        {
            var bytes = new byte[260];
            Encoding.ASCII.GetBytes(text).CopyTo(bytes, 0);
            return bytes;
        }
    }
}
