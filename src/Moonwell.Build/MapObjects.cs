using Moonwell.MapFiles;

namespace Moonwell.Build
{
    /// <summary>The map's declared objects as the build reads them, for <c>moonwell objects</c> (decision 253).</summary>
    public static class MapObjects
    {
        /// <summary>Builds the map's code and reads its declared objects: each class's name, kind, id and the standard object it is based on, by class name.</summary>
        /// <param name="path">A project folder, or the path of its <c>.csproj</c> file.</param>
        /// <param name="log">Where <c>dotnet build</c>'s output goes.</param>
        /// <returns>The objects; none for a map without Moonwell.Objects.</returns>
        /// <exception cref="BuildException">The project, its code or a declaration fails as it would in a build.</exception>
        public static IReadOnlyList<(string ClassName, string Kind, string Id, string Base)> List(string path, TextWriter log)
        {
            var project = MapProject.Find(path);
            var targetPath = DotnetBuild.Run(project, false, log);
            if (!Declarations.UsesObjects(targetPath))
            {
                return Array.Empty<(string, string, string, string)>();
            }

            var settings = MapSettingsReader.Read(targetPath, project);
            var data = GameData.Of(settings.DataSet, settings.DataVersion);
            var standard = StandardData.Read(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetPath))!, Declarations.ObjectsAssemblyName + ".dll"));
            var declarations = Declarations.Read(targetPath, project, standard, data.ValueSet);
            var ids = ObjectIds.Assign(declarations.Objects, standard.AllIds(), MapObjectIds.Defined(project));
            return declarations.Objects
                .Select(declared => (declared.ClassName, declared.Kind, ids[declared.ClassName], declared.StandardRoot))
                .OrderBy(entry => entry.ClassName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Builds the map, then writes the built object files into the source map, so that World Editor lists the declared
        /// objects (decision 275). No build calls this: decision 4 keeps the build out of the source map. The build has
        /// just merged the source map's own objects in, so they are kept as they are now. Moonwell's own helper objects
        /// are left out, as the build refuses a source map that defines their ids.
        /// </summary>
        /// <param name="path">A project folder, or the path of its <c>.csproj</c> file.</param>
        /// <param name="log">Where the build's output goes.</param>
        /// <returns>The names of the files written; none when the source map already held every one.</returns>
        /// <exception cref="BuildException">The build fails, or a file cannot be written.</exception>
        public static IReadOnlyList<string> WriteToMap(string path, TextWriter log)
        {
            MapBuild.Run(path, new BuildOptions(), log);
            var project = MapProject.Find(path);

            // The build passed, so a helper's id the source map defines is an object of the map's own, and stays.
            var helpers = PathingTypes.Ids
                .Concat(BonusAbilities.Objects.Select(bonus => bonus.PinnedId!))
                .Append(DummyType.Id)
                .Append(FileStore.Id)
                .Except(MapObjectIds.Defined(project))
                .ToHashSet(StringComparer.Ordinal);

            var written = new List<string>();
            foreach (var kind in Kinds.All)
            {
                foreach (var name in new[] { Kinds.MainName(kind), Kinds.SkinName(kind) })
                {
                    var staged = Path.Combine(project.StagedFolder, name);
                    var target = Path.Combine(project.MapFolder, name);
                    if (!File.Exists(staged))
                    {
                        continue;
                    }

                    // A file with no helper is copied as it is; only one that holds a helper is written anew without it.
                    var bytes = File.ReadAllBytes(staged);
                    var table = SourceObjectFile.Read(staged, kind);
                    var mine = table.Entries.Where(entry => helpers.Contains(SourceObjectFile.IdOf(entry))).ToList();
                    if (mine.Count > 0)
                    {
                        mine.ForEach(entry => table.File.Remove(entry));
                        bytes = table.File.ToBytes();
                    }

                    var exists = File.Exists(target);
                    if (exists ? File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes) : table.IsEmpty)
                    {
                        continue;
                    }

                    try
                    {
                        File.WriteAllBytes(target, bytes);
                    }
                    catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                    {
                        throw new BuildException($@"cannot write map\{name}: {failure.Message} (is World Editor using it?)");
                    }

                    written.Add(name);
                }
            }

            return written;
        }
    }
}
