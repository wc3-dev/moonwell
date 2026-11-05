using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Moonwell.Build
{
    /// <summary>Which files a map project's code is compiled from and against: the one place the build decides.</summary>
    internal static class CompiledFiles
    {
        /// <summary>
        /// The C# files of the map's code that CSharp.lua compiles: those <c>dotnet build</c> compiled, under the project folder
        /// (<see cref="MapProject.Sources"/>, which leaves out hidden folders and what the project removes), and the code the build
        /// generates in <c>build</c>, but for the folders given. Before <see cref="DotnetBuild.Run"/>, every <c>*.cs</c> under the folder.
        /// </summary>
        /// <param name="project">The map project.</param>
        /// <param name="skipped">Full paths of folders under it to leave out, such as the build folder.</param>
        /// <returns>The files' full paths.</returns>
        public static IEnumerable<string> Sources(MapProject project, params string[] skipped)
        {
            if (project.Sources == null)
            {
                return Sources(project.Folder, skipped);
            }

            // dotnet build leaves out build, whose generated code is CSharp.lua's alone.
            var compiled = project.Sources.Where(path => IsUnder(path, project.Folder) && !IsUnder(path, project.BuildFolder) && !skipped.Any(one => IsUnder(path, one)));
            var generated = Directory.Exists(project.BuildFolder) && !skipped.Any(one => IsUnder(project.BuildFolder, one)) ? Sources(project.BuildFolder, skipped) : Enumerable.Empty<string>();
            return compiled.Concat(generated);
        }

        /// <summary>
        /// The preprocessor symbols CSharp.lua compiles the map with: those of <c>dotnet build</c> (<see cref="MapProject.Symbols"/>),
        /// MOONWELL_LUA, which leaves out the generated classes of Moonwell.Objects for the build's stubs, and __CSharpLua__.
        /// </summary>
        public static string[] LuaSymbols(MapProject project)
        {
            return project.Symbols.Append("MOONWELL_LUA").Append("__CSharpLua__").Distinct(StringComparer.Ordinal).ToArray();
        }

        /// <summary>The symbols of the other kind of build, DEBUG and RELEASE swapped, for a look at the source that serves both.</summary>
        public static string[] OtherKind(IEnumerable<string> symbols)
        {
            return symbols.Select(one => one == "DEBUG" ? "RELEASE" : one == "RELEASE" ? "DEBUG" : one).ToArray();
        }

        /// <summary>
        /// A C# file's text as the compiler reads it with no <c>CodePage</c> set: by its byte order mark, else as UTF-8, else, as it
        /// is not UTF-8, in the system's ANSI code page.
        /// </summary>
        public static string ReadSource(string path)
        {
            var bytes = File.ReadAllBytes(path);
            try
            {
                using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }
            catch (DecoderFallbackException)
            {
                return (CodePagesEncodingProvider.Instance.GetEncoding(0) ?? CodePagesEncodingProvider.Instance.GetEncoding(1252)!).GetString(bytes);
            }
        }

        private static bool IsUnder(string path, string folder)
        {
            var full = Path.GetFullPath(path);
            return full.Equals(folder, StringComparison.OrdinalIgnoreCase) || full.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Every <c>*.cs</c> under a folder but the top-level <c>bin</c> and <c>obj</c> (their files are the compiler's own) and the
        /// folders given.
        /// </summary>
        /// <param name="folder">The project folder, or a copy of it.</param>
        /// <param name="skipped">Full paths of folders under it to leave out, such as the build folder.</param>
        /// <returns>The files' full paths.</returns>
        public static IEnumerable<string> Sources(string folder, params string[] skipped)
        {
            bool Skipped(string path) => skipped.Any(one => path.Equals(one, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(one + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }

            foreach (var sub in Directory.EnumerateDirectories(folder))
            {
                var name = Path.GetFileName(sub);
                if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase) || Skipped(sub))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(sub, "*.cs", SearchOption.AllDirectories))
                {
                    if (!Skipped(file))
                    {
                        yield return file;
                    }
                }
            }
        }

        /// <summary>The libraries <c>dotnet build</c> put beside the map's assembly, which the map's code references.</summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        /// <returns>The libraries' paths, the assembly itself left out.</returns>
        public static IEnumerable<string> LibrariesBeside(string assemblyPath)
        {
            var assembly = Path.GetFullPath(assemblyPath);
            return Directory.EnumerateFiles(Path.GetDirectoryName(assembly)!, "*.dll")
                .Where(path => !string.Equals(Path.GetFullPath(path), assembly, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The map's built assembly and each library of its own it reaches, as a map's code may be in a library project: the
        /// references beside it, followed through one another. Moonwell's assemblies are not followed: what a package references
        /// is no use of it by the map (the physics package references the interactions package).
        /// </summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        /// <returns>The assemblies' paths, the map's first.</returns>
        public static IEnumerable<string> MapAssemblies(string assemblyPath)
        {
            var assembly = Path.GetFullPath(assemblyPath);
            var folder = Path.GetDirectoryName(assembly)!;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var waiting = new Queue<string>();
            waiting.Enqueue(assembly);
            while (waiting.Count > 0)
            {
                var path = waiting.Dequeue();
                if (!seen.Add(path))
                {
                    continue;
                }

                yield return path;
                foreach (var name in ReferencedAssemblies(path))
                {
                    var library = Path.Combine(folder, name + ".dll");
                    if (name != "Moonwell" && !name.StartsWith("Moonwell.", StringComparison.Ordinal) && File.Exists(library))
                    {
                        waiting.Enqueue(library);
                    }
                }
            }
        }

        /// <summary>The names of the assemblies an assembly references, read from its metadata without loading it; the compiler keeps only the references the code uses.</summary>
        /// <param name="assemblyPath">A built assembly.</param>
        /// <returns>The names, such as <c>Moonwell.Damage</c>.</returns>
        public static List<string> ReferencedAssemblies(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            return metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)).ToList();
        }
    }
}
