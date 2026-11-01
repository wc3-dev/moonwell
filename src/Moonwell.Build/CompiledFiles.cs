using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Moonwell.Build
{
    /// <summary>Which files a map project's code is compiled from and against: the one place the build decides.</summary>
    internal static class CompiledFiles
    {
        /// <summary>
        /// The C# files <c>dotnet build</c> and CSharp.lua compile: every <c>*.cs</c> under a folder but the top-level <c>bin</c> and
        /// <c>obj</c> (their files are the compiler's own) and the folders given.
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
