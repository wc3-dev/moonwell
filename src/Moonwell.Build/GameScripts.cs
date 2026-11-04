using Moonwell.GameData;

namespace Moonwell.Build
{
    /// <summary>
    /// The game's common.j and Blizzard.j, which CSharp.lua reads to compile the map's main and config. They are read from
    /// the install's storage once for each build of the game, as opening it takes most of a second, and kept in a folder
    /// named for that build.
    /// </summary>
    internal static class GameScripts
    {
        private const string Common = "common.j";
        private const string Blizzard = "Blizzard.j";

        /// <summary>Where the files of each build of the game are kept.</summary>
        public static string CacheRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Moonwell", "scripts");

        /// <summary>The two files of the install, read into <paramref name="cacheRoot"/> when they are not there yet.</summary>
        /// <param name="gameFolder">The install's folder; null for MOONWELL_WAR3, else the usual folder.</param>
        /// <param name="cacheRoot">The folder that holds a folder for each build of the game.</param>
        /// <exception cref="BuildException">There is no install, or its storage cannot be read or lacks a file.</exception>
        public static (string Common, string Blizzard) Find(string? gameFolder, string cacheRoot)
        {
            string install;
            GameBuild build;
            try
            {
                install = GameInstall.Require(gameFolder);
                build = GameInstall.Build(install);
            }
            catch (Exception failure) when (failure is IOException or InvalidDataException)
            {
                throw new BuildException(failure.Message + " The build reads common.j and Blizzard.j from the game.");
            }

            var folder = Path.Combine(cacheRoot, build.Key);
            var common = Path.Combine(folder, Common);
            var blizzard = Path.Combine(folder, Blizzard);
            if (!File.Exists(common) || !File.Exists(blizzard))
            {
                Directory.CreateDirectory(folder);
                try
                {
                    using var storage = CascStorage.Open(install);
                    Keep(storage, Common, common);
                    Keep(storage, Blizzard, blizzard);
                }
                catch (IOException failure)
                {
                    throw new BuildException(failure.Message.StartsWith("The game's storage", StringComparison.Ordinal)
                        ? failure.Message
                        : $"The game's storage in {install} cannot be read: {failure.Message}");
                }
            }

            return (common, blizzard);
        }

        // Written beside the file and moved into place, so a build running at the same time never reads half of it; the
        // first to move it wins.
        private static void Keep(CascStorage storage, string name, string path)
        {
            if (File.Exists(path))
            {
                return;
            }

            var bytes = storage.Read("War3.w3mod:Scripts/" + name) ?? throw new BuildException($"the game's storage has no Scripts/{name}");
            var written = path + "." + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(written, bytes);
            try
            {
                File.Move(written, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Delete(written);
            }
        }
    }
}
