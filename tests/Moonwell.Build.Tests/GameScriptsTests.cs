using Moonwell.GameData;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// The game's common.j and Blizzard.j, read from the install on this computer (MOONWELL_WAR3, else the usual folder);
    /// without one the test that needs it is skipped.
    /// </summary>
    public sealed class GameScriptsTests : IDisposable
    {
        private const string BuildInfo =
            "Branch!STRING:0|Active!DEC:1|Build Key!HEX:16|CDN Key!HEX:16|Tags!STRING:0|Version!STRING:0|Product!STRING:0\n"
            + "eu|1|0123456789abcdef0123456789abcdef|0|Windows EU? enUS text?|3.0.1.24371|w3\n";

        private readonly string root = Path.Combine(Path.GetTempPath(), "moonwell-game-scripts-" + Guid.NewGuid().ToString("N"));

        public GameScriptsTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        [InstallFact]
        public void reads_both_from_the_install_into_a_folder_named_for_its_build()
        {
            var install = GameInstall.Find()!;

            var scripts = GameScripts.Find(null, root);

            var folder = Path.Combine(root, GameInstall.Build(install).Key);
            Assert.Equal(Path.Combine(folder, "common.j"), scripts.Common);
            Assert.Equal(Path.Combine(folder, "Blizzard.j"), scripts.Blizzard);
            Assert.Contains("native BlzCameraGetCameraType", File.ReadAllText(scripts.Common));
            Assert.Contains("function BJDebugMsg", File.ReadAllText(scripts.Blizzard));
        }

        [Fact]
        public void a_build_of_the_game_already_read_takes_them_from_the_cache()
        {
            var install = Path.Combine(root, "install");
            Directory.CreateDirectory(install);
            File.WriteAllText(Path.Combine(install, ".build.info"), BuildInfo);
            var cached = Path.Combine(root, "cache", "0123456789abcdef0123456789abcdef");
            Directory.CreateDirectory(cached);
            File.WriteAllText(Path.Combine(cached, "common.j"), "kept");
            File.WriteAllText(Path.Combine(cached, "Blizzard.j"), "kept");

            var scripts = GameScripts.Find(install, Path.Combine(root, "cache"));

            Assert.Equal("kept", File.ReadAllText(scripts.Common));
            Assert.Equal("kept", File.ReadAllText(scripts.Blizzard));
        }

        [Fact]
        public void no_install_is_a_build_error_naming_the_folder()
        {
            var empty = Path.Combine(root, "empty");
            Directory.CreateDirectory(empty);

            var error = Assert.Throws<BuildException>(() => GameScripts.Find(empty, Path.Combine(root, "cache")));

            Assert.StartsWith("No Warcraft III install (a folder with .build.info) in " + empty + ".", error.Message);
            Assert.EndsWith("The build reads common.j and Blizzard.j from the game.", error.Message);
        }

        [Fact]
        public void an_install_whose_storage_cannot_be_read_is_a_build_error()
        {
            var install = Path.Combine(root, "install");
            Directory.CreateDirectory(install);
            File.WriteAllText(Path.Combine(install, ".build.info"), BuildInfo);

            var error = Assert.Throws<BuildException>(() => GameScripts.Find(install, Path.Combine(root, "cache")));

            Assert.Contains("cannot be read", error.Message);
        }
    }
}
