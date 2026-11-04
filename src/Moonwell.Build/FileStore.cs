using System.Text;

namespace Moonwell.Build
{
    /// <summary>
    /// What a map using player files gets (decision 179): Moonwell's store ability, whose tooltips hold a file's pieces when
    /// it is read back, and the folder of CustomMapData its files go in. A tooltip holds a piece only on a level that has
    /// one (game facts, 11-03), so the store has 100 levels with a tooltip and an extended tooltip on each: 200 pieces.
    /// </summary>
    public static class FileStore
    {
        /// <summary>The store ability's id, Moonwell's own (the runtime's FileText.StoreId): no class of the map and no object of the source map may have it.</summary>
        public const string Id = "AMWf";

        /// <summary>The key of the store among the object ids, as the dummy type's.</summary>
        public const string ClassName = "Moonwell#files";

        private const int Levels = 100;

        /// <summary>The store as an object to write: a copy of Storm Bolt (the probe's, 11-03) with 100 levels and a "-" in both tooltips of each.</summary>
        public static DeclaredObject Object { get; } = new DeclaredObject(
            ClassName,
            "ability",
            "AHtb",
            false,
            Id,
            null,
            Changes(),
            Array.Empty<string>());

        /// <summary>Whether a map's code uses player files, PlayerFile or a scope's ReadFile, read from its built assemblies' metadata without loading them.</summary>
        public static bool UsedBy(string assemblyPath)
        {
            return Declarations.ReferencesType(assemblyPath, "Moonwell", "PlayerFile") || CompiledFiles.MapAssemblies(assemblyPath).Any(ReadsFiles);
        }

        // A call of ReadFile is a member reference whose parent is Moonwell's Scope, where it is declared.
        private static bool ReadsFiles(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in metadata.MemberReferences)
            {
                var member = metadata.GetMemberReference(handle);
                if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference || metadata.GetString(member.Name) != "ReadFile")
                {
                    continue;
                }

                var type = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)member.Parent);
                if (metadata.GetString(type.Name) == "Scope" && metadata.GetString(type.Namespace) == "Moonwell")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The install line of the save folder for a map that uses files; none when it does not.</summary>
        public static IReadOnlyList<string> Installs(string assemblyPath, MapSettings settings, MapProject project)
        {
            return UsedBy(assemblyPath)
                ? new[] { $"global::Moonwell.Runtime.InstallSaveFolder(\"{FolderOf(settings, project)}\");" }
                : Array.Empty<string>();
        }

        /// <summary>The save folder: the map's SaveFolder, else its name, else the project's, in letters and digits only (a packed file named with Å was not found, 10-26).</summary>
        public static string FolderOf(MapSettings settings, MapProject project)
        {
            foreach (var name in new[] { settings.SaveFolder, settings.Name, project.Name })
            {
                var folder = new StringBuilder();
                foreach (var c in name)
                {
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    {
                        folder.Append(c);
                    }
                }

                if (folder.Length > 0)
                {
                    return folder.ToString();
                }
            }

            return "Map";
        }

        /// <summary>Fails the build when a class of the map, or an object of the source map, has the store's id.</summary>
        /// <param name="ids">The object id of each declared class and carrier.</param>
        /// <param name="sourceMapIds">The ids the source map's object files define.</param>
        /// <exception cref="BuildException">One has the store's id.</exception>
        public static void Check(IReadOnlyDictionary<string, string> ids, IReadOnlySet<string> sourceMapIds)
        {
            foreach (var entry in ids.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (entry.Value == Id)
                {
                    throw new BuildException($"{entry.Key} has the id {Id}, which Moonwell keeps for the ability that reads player files back; give it another id");
                }
            }

            if (sourceMapIds.Contains(Id))
            {
                throw new BuildException($"the map's object files define {Id}, which Moonwell keeps for the ability that reads player files back; give that object another id in World Editor");
            }
        }

        /// <summary>
        /// Whether a map that uses files references Moonwell.Objects though its code uses none of it, so the compiler left the
        /// reference out of the assembly: the deps file each build writes anew still names the package.
        /// </summary>
        public static bool ReferencesObjects(string assemblyPath)
        {
            var deps = Path.ChangeExtension(assemblyPath, ".deps.json");
            if (!UsedBy(assemblyPath) || !File.Exists(deps))
            {
                return false;
            }

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(deps));
            return document.RootElement.TryGetProperty("libraries", out var libraries)
                && libraries.EnumerateObject().Any(library => library.Name.StartsWith(Declarations.ObjectsAssemblyName + "/", StringComparison.Ordinal));
        }

        /// <summary>Fails the build of a map that uses files without Moonwell.Objects, which writes the store into the object files.</summary>
        /// <exception cref="BuildException">The map uses files.</exception>
        public static void CheckWithoutObjects(string assemblyPath)
        {
            if (UsedBy(assemblyPath))
            {
                throw new BuildException("PlayerFile reads files back through an ability Moonwell declares, which needs the Moonwell.Objects package: add a reference to it");
            }
        }

        private static SortedDictionary<string, object?> Changes()
        {
            var changes = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["alev"] = Levels,
                ["anam"] = "Files (Moonwell)",
            };
            for (var level = 1; level <= Levels; level++)
            {
                changes["atp1:" + level] = "-";
                changes["aub1:" + level] = "-";
            }

            return changes;
        }
    }
}
