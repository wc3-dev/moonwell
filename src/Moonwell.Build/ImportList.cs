using War3Net.Build.Extensions;
using War3Net.Build.Import;

namespace Moonwell.Build
{
    /// <summary>
    /// Writes <c>war3map.imp</c>, World Editor's list of the map's imported files (decision 278). The game does not read it;
    /// World Editor's save drops a file of the archive that it does not name.
    /// </summary>
    public static class ImportList
    {
        /// <summary>The list's file name.</summary>
        public const string FileName = ImportedFiles.MapFileName;

        // A file at the path the entry gives, as World Editor writes one with a custom path.
        private const ImportedFileFlags OwnPath = ImportedFileFlags.UNK1 | ImportedFileFlags.UNK4 | ImportedFileFlags.UNK8;

        // Without the custom path flag, an entry's file is under this folder, whether or not its path says so.
        private const string ImportedFolder = @"war3mapImported\";

        /// <summary>Adds the files to the staged map's list, keeping the entries the source map's list has. Writes nothing when there is nothing to add.</summary>
        /// <param name="stagedFolder">The staged map folder.</param>
        /// <param name="files">The paths, in the map, of the files to list.</param>
        /// <exception cref="BuildException">The source map's list cannot be read.</exception>
        public static void Write(string stagedFolder, IEnumerable<string> files)
        {
            var path = Path.Combine(stagedFolder, FileName);
            var list = File.Exists(path) ? Read(path) : new ImportedFiles(ImportedFilesFormatVersion.v1);
            var listed = new HashSet<string>(list.Files.Select(PathInMap), StringComparer.OrdinalIgnoreCase);
            var added = files
                .Select(file => file.Replace('/', '\\'))
                .Where(file => listed.Add(file))
                .OrderBy(file => file, StringComparer.Ordinal)
                .Select(file => new ImportedFile { Flags = OwnPath, FullPath = file })
                .ToList();
            if (added.Count == 0)
            {
                return;
            }

            list.Files.AddRange(added);
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(list);
        }

        /// <summary>Reads a list's entries as the paths their files have in the map.</summary>
        /// <param name="path">The <c>war3map.imp</c> file.</param>
        /// <returns>The paths, in the file's order.</returns>
        /// <exception cref="BuildException">The file cannot be read.</exception>
        public static IReadOnlyList<string> Paths(string path)
        {
            return Read(path).Files.Select(PathInMap).ToList();
        }

        private static ImportedFiles Read(string path)
        {
            try
            {
                using var reader = new BinaryReader(File.OpenRead(path));
                return reader.ReadImportedFiles();
            }
            catch (Exception failure) when (failure is EndOfStreamException or InvalidDataException or ArgumentException)
            {
                throw new BuildException($@"map\{FileName} cannot be read ({failure.Message}); save the map in World Editor to write it anew");
            }
        }

        private static string PathInMap(ImportedFile file)
        {
            var custom = (file.Flags & ImportedFileFlags.UNK4) != 0;
            return custom || file.FullPath.StartsWith(ImportedFolder, StringComparison.OrdinalIgnoreCase) ? file.FullPath : ImportedFolder + file.FullPath;
        }
    }
}
