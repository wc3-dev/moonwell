using Moonwell.Tool;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The paths the map names that neither the game nor the map has (decision 251), and the assets nothing names (decision 252).</summary>
    [Collection("Sample")]
    public sealed class AssetChecksBuildTests : IDisposable
    {
        private readonly TestFolders folders;

        public AssetChecksBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_path_neither_the_game_nor_the_map_has_is_warned_of_naming_its_user()
        {
            var project = folders.TemporaryCopyOf("Sample");
            var objects = Path.Combine(project, "Objects.cs");
            File.WriteAllText(objects, File.ReadAllText(objects).Replace(
                "Name = \"Arthas\";",
                "Name = \"Arthas\";\n            Icon = @\"war3mapImported\\BTNArthas.blp\";\n            Model = @\"Units\\Human\\Footman\\Footman.mdl\";"));
            File.WriteAllText(Path.Combine(project, "Looks.cs"), """
                using Moonwell;

                namespace Sample
                {
                    public sealed class Looks : GameComponent
                    {
                        public override void OnGameStart()
                        {
                            System.Action show = () => Game.Print(@"war3mapImported\Typo.blp");
                            show();
                            Game.Print(@"war3mapImported\Shield.blp");
                            Game.Print(@"ReplaceableTextures\CommandButtons\BTNFootman.blp");
                            Game.Print(@"Units\Human\Footman\FootmanYes1.wav");
                            Game.Print(@"{0}.blp");
                        }
                    }
                }
                """);
            CopyImage("glass-32.png", project, @"war3mapImported\Shield.png");

            var output = Build(project);

            var warnings = Lines(output, "warning:");
            Assert.Equal(
                new[]
                {
                    @"warning: Sample.Arthas: uico names war3mapImported\BTNArthas.blp, which neither the game nor the map has",
                    @"warning: Sample.Looks's code names war3mapImported\Typo.blp, which neither the game nor the map has",
                },
                warnings);
        }

        [Fact]
        public void the_assets_nothing_names_are_listed_in_one_warning()
        {
            var project = folders.TemporaryCopyOf("Sample");
            File.WriteAllText(Path.Combine(project, "Looks.cs"), """
                using Moonwell;

                namespace Sample
                {
                    public sealed class Looks : GameComponent
                    {
                        public override void OnGameStart()
                        {
                            Game.Print("war3mapImported\\BTNNamed.blp");
                            Game.Print("Hero.mdx");
                        }
                    }
                }
                """);
            CopyImage("red-64x32.png", project, @"war3mapImported\BTNNamed.png");
            CopyImage("red-64x32.png", project, @"war3mapImported\Lost.png");
            CopyImage("red-64x32.png", project, @"ReplaceableTextures\CommandButtons\BTNFootman.png");
            foreach (var model in new[] { "Hero.mdx", "Hero_portrait.mdx", @"Sounds\Lost.wav" })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(project, "assets", model))!);
                File.WriteAllText(Path.Combine(project, "assets", model), "x");
            }

            var output = Build(project);

            // Named by the code, a game file it replaces, a portrait of a named model, its own grey copy and a text file are not listed.
            Assert.Equal(
                new[] { @"warning: nothing in the map names these assets, so they may be unused (a path the code puts together while the game runs is not seen): assets\Sounds\Lost.wav, assets\war3mapImported\Lost.png" },
                Lines(output, "warning:"));
        }

        private static string[] Lines(string output, string start)
        {
            return output.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.StartsWith(start, StringComparison.Ordinal)).ToArray();
        }

        private static void CopyImage(string fixture, string project, string relative)
        {
            var target = Path.Combine(project, "assets", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(TestFolders.Fixture("Images"), fixture), target);
        }

        private static string Build(string project)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Cli.Run(new[] { "build", project }, output, error, (_, _) => throw new InvalidOperationException("build starts nothing"));
            Assert.True(exitCode == 0, error.ToString() + output);
            return output.ToString();
        }
    }
}
