namespace Moonwell.Build
{
    /// <summary>
    /// The ready-made stats whose effect is the Moonwell.Stats package's (decision 390): a map whose code reads one without the
    /// package would have a bare number that nothing acts on, so the build stops it.
    /// </summary>
    public static class StatsPackage
    {
        /// <summary>The package's assembly: a map that references it gets it installed by its Main, after the damage package.</summary>
        public const string AssemblyName = "Moonwell.Stats";

        // The getters of Stat's properties, with the name a map writes; Resistance is a method.
        private static readonly string[] Names =
        {
            "CritChance", "CritDamage", "HitChance", "Evasion", "Parry", "BlockChance", "BlockAmount", "AttackPower", "SpellPower",
            "ArmorPenetration", "SpellPenetration", "AreaDamageReduction", "Barrier", "Stagger", "StaggerTime", "LifeSteal", "ManaLeech",
            "HealingBonus", "CooldownReduction", "ManaCostReduction", "CastRange", "CastSpeed", "ExperienceGain", "GoldBonus",
        };

        /// <summary>The stats a map's code reads that need the package, written as the map writes them (<c>Stat.CritChance</c>), in name order; read from its built assemblies' metadata without loading them.</summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        public static List<string> UsedBy(string assemblyPath)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in CompiledFiles.MapAssemblies(assemblyPath))
            {
                Read(path, found);
            }

            return found.OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        /// <summary>Fails the build of a map that uses a stat the package gives its effect without referencing the package: by its project or package reference, whatever its code names of it.</summary>
        /// <exception cref="BuildException">The map reads such a stat and does not reference Moonwell.Stats.</exception>
        public static void Check(string assemblyPath)
        {
            var used = UsedBy(assemblyPath);
            if (used.Count > 0 && !Declarations.Depends(assemblyPath, AssemblyName))
            {
                throw new BuildException($"{string.Join(", ", used)} {(used.Count == 1 ? "has" : "have")} no effect without the Moonwell.Stats package, which acts on them: add a reference to it");
            }
        }

        // A read of one is a member reference whose parent is Moonwell's Stat.
        private static void Read(string assemblyPath, HashSet<string> found)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in metadata.MemberReferences)
            {
                var member = metadata.GetMemberReference(handle);
                if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference)
                {
                    continue;
                }

                var name = metadata.GetString(member.Name);
                var stat = name == "Resistance" ? "Stat.Resistance(...)" : name.StartsWith("get_", StringComparison.Ordinal) && Names.Contains(name.Substring(4)) ? "Stat." + name.Substring(4) : null;
                if (stat == null)
                {
                    continue;
                }

                var type = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)member.Parent);
                if (metadata.GetString(type.Name) == "Stat" && metadata.GetString(type.Namespace) == "Moonwell")
                {
                    found.Add(stat);
                }
            }
        }
    }
}
