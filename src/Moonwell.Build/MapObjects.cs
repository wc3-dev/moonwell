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
    }
}
