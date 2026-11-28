namespace Moonwell.Build
{
    /// <summary>The files of a map's assets the build reads and packs: all but those in a folder named <see cref="KeptOutFolder"/>.</summary>
    internal static class AssetFiles
    {
        /// <summary>
        /// A folder of this name, anywhere in the assets, is not read or packed: a place for downloads and source art beside the
        /// files they give (J192). The game reads no folder of this name, so no path in the map is lost to it.
        /// </summary>
        internal const string KeptOutFolder = "unpacked";

        /// <summary>The assets' files that match a pattern, by their paths in the assets, in ordinal order, leaving out the kept-out folders.</summary>
        /// <param name="assetsFolder">The map's assets folder.</param>
        /// <param name="pattern">The file pattern, as <see cref="Directory.GetFiles(string, string)"/> takes it.</param>
        /// <returns>The paths; none when there is no assets folder.</returns>
        internal static List<string> Find(string assetsFolder, string pattern = "*")
        {
            if (!Directory.Exists(assetsFolder))
            {
                return new List<string>();
            }

            return Directory.GetFiles(assetsFolder, pattern, SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(assetsFolder, file).Replace('/', '\\'))
                .Where(file => !IsKeptOut(file))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Whether a path in the assets is in a kept-out folder.</summary>
        /// <param name="file">The path in the assets, its folders split by backslashes.</param>
        internal static bool IsKeptOut(string file)
        {
            var folders = file.Split('\\');
            return folders.Take(folders.Length - 1).Any(folder => string.Equals(folder, KeptOutFolder, StringComparison.OrdinalIgnoreCase));
        }
    }
}
