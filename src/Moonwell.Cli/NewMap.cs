using Moonwell.Build;

namespace Moonwell.Tool
{
    /// <summary><c>moonwell new</c>: a map project from the template the tool carries, as <c>dotnet new moonwell</c> makes it (decision 253).</summary>
    internal static class NewMap
    {
        // The template's name, replaced by the map's in its file names and code, as dotnet new's sourceName.
        private const string TemplateName = "MoonwellMap";
        private const string Prefix = "template/";

        /// <summary>Writes the project into a new folder of the map's name in <paramref name="parent"/>.</summary>
        /// <param name="name">The map's name: the folder, the project file and the code's namespace.</param>
        /// <param name="parent">The folder to make it in.</param>
        /// <param name="version">The Moonwell version its packages reference.</param>
        /// <returns>The project's folder.</returns>
        /// <exception cref="BuildException">The name is not a namespace, or the folder exists and is not empty.</exception>
        public static string Write(string name, string parent, string version)
        {
            if (name.Length == 0 || char.IsAsciiDigit(name[0]) || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                throw new BuildException($"\"{name}\" cannot be a map's name: it is the code's namespace too, so use letters, digits and _ only, not starting with a digit");
            }

            var folder = Path.Combine(Path.GetFullPath(parent), name);
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
            {
                throw new BuildException($"{folder} is not empty; pick another name or folder");
            }

            var assembly = typeof(NewMap).Assembly;
            foreach (var resource in assembly.GetManifestResourceNames().Where(resource => resource.StartsWith(Prefix, StringComparison.Ordinal)))
            {
                var relative = resource[Prefix.Length..].Replace('\\', '/').Replace(TemplateName, name, StringComparison.Ordinal);
                var target = Path.Combine(folder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var source = assembly.GetManifestResourceStream(resource)!;
                if (IsText(relative))
                {
                    using var reader = new StreamReader(source);
                    File.WriteAllText(target, reader.ReadToEnd().Replace(TemplateName, name, StringComparison.Ordinal).Replace("MOONWELL_VERSION", version, StringComparison.Ordinal));
                }
                else
                {
                    using var written = File.Create(target);
                    source.CopyTo(written);
                }
            }

            return folder;
        }

        // The code and project files; the World Editor map's files are copied byte for byte.
        private static bool IsText(string relative)
        {
            return Path.GetExtension(relative) is ".cs" or ".csproj" || Path.GetFileName(relative) == ".gitignore";
        }
    }
}
