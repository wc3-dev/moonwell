using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Moonwell.Build
{
    /// <summary>The copy of a map project, in <c>build\marked</c>, whose files carry <see cref="LineMarks"/>; CSharp.lua compiles it.</summary>
    public sealed class MarkedProject
    {
        private readonly string projectFolder;

        private readonly Dictionary<string, MarkedColumns> columns;

        private MarkedProject(string folder, string projectFile, IReadOnlyList<string> files, string projectFolder, Dictionary<string, MarkedColumns> columns)
        {
            Folder = folder;
            ProjectFile = projectFile;
            Files = files;
            this.projectFolder = projectFolder;
            this.columns = columns;
        }

        /// <summary>The copy's folder, <c>build\marked</c>.</summary>
        public string Folder { get; }

        /// <summary>The copy's <c>.csproj</c>, its project references made absolute.</summary>
        public string ProjectFile { get; }

        /// <summary>The marked files, relative to the project folder with <c>/</c>, in order: the first is file 1.</summary>
        public IReadOnlyList<string> Files { get; }

        /// <summary>
        /// Empties <c>build\marked</c> and copies into it the project file and every <c>*.cs</c> CSharp.lua compiles (all under the
        /// project folder but the top-level <c>bin</c> and <c>obj</c>), marking the files outside <c>build</c>.
        /// </summary>
        /// <param name="project">The map project.</param>
        /// <param name="symbols">The preprocessor symbols CSharp.lua compiles with.</param>
        /// <returns>The copy.</returns>
        public static MarkedProject Stage(MapProject project, IEnumerable<string> symbols)
        {
            var folder = Path.Combine(project.BuildFolder, "marked");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);
            var symbolList = symbols.ToList();
            var sources = CompiledFiles.Sources(project.Folder, folder)
                .Select(path => Path.GetRelativePath(project.Folder, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            var marked = new List<string>();
            var columns = new Dictionary<string, MarkedColumns>(StringComparer.OrdinalIgnoreCase);
            foreach (var relative in sources)
            {
                var text = File.ReadAllText(Path.Combine(project.Folder, relative));
                if (!relative.StartsWith("build/", StringComparison.OrdinalIgnoreCase))
                {
                    marked.Add(relative);
                    text = LineMarks.Mark(text, marked.Count, symbolList, out var fileColumns);
                    columns[relative] = fileColumns;
                }

                var target = Path.Combine(folder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, text);
            }

            var projectFile = Path.Combine(folder, Path.GetFileName(project.ProjectFile));
            WriteProjectFile(project, projectFile);
            return new MarkedProject(folder, projectFile, marked, project.Folder, columns);
        }

        /// <summary>
        /// A message from CSharp.lua with the copy's paths changed back to the project's, whichever separators they use, and a
        /// marked file's columns changed back to the file's own, in Roslyn's <c>path(line,column)</c> and CSharp.lua's <c>path@line:column</c>.
        /// </summary>
        public string Unstage(string message)
        {
            const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            var marked = Path.DirectorySeparatorChar + "build" + Path.DirectorySeparatorChar + "marked";
            var pattern = Regex.Escape(projectFolder).Replace(@"\\", @"[\\/]") + Regex.Escape(marked).Replace(@"\\", @"[\\/]");
            var located = pattern + @"[\\/](?<file>[^\r\n]+?\.cs)(?:\((?<line>\d+),|@(?<line>\d+):)(?<column>\d+)";
            var own = Regex.Replace(message, located, OwnColumn, Options);
            return Regex.Replace(own, pattern, projectFolder.Replace("$", "$$"), Options);
        }

        private string OwnColumn(Match location)
        {
            var file = location.Groups["file"].Value.Replace('\\', '/');
            if (!columns.TryGetValue(file, out var fileColumns))
            {
                return location.Value;
            }

            var line = int.Parse(location.Groups["line"].Value, CultureInfo.InvariantCulture);
            var column = int.Parse(location.Groups["column"].Value, CultureInfo.InvariantCulture);
            return location.Value.Substring(0, location.Groups["column"].Index - location.Index) + fileColumns.Original(line, column).ToString(CultureInfo.InvariantCulture);
        }

        // The copy is deeper than the project, so its relative project references would point elsewhere.
        private static void WriteProjectFile(MapProject project, string path)
        {
            var document = XDocument.Load(project.ProjectFile, LoadOptions.PreserveWhitespace);
            foreach (var reference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
            {
                var include = reference.Attribute("Include");
                if (include != null && !Path.IsPathRooted(include.Value))
                {
                    include.Value = Path.GetFullPath(Path.Combine(project.Folder, include.Value));
                }
            }

            document.Save(path);
        }
    }
}
