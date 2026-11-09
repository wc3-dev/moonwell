namespace Moonwell.Build
{
    /// <summary>
    /// What a map using the game's damage, armor or attack speed stats gets (decision 221): Moonwell's three bonus abilities,
    /// copies of the item bonuses AItg, AId1 and AIsx that give nothing until the runtime sets their field, which the game
    /// takes at once and shows as the green bonus (seen 11-08). Moonwell's own ids keep them apart from a carried item's.
    /// </summary>
    public static class BonusAbilities
    {
        /// <summary>The abilities as objects to write; their ids are the runtime's StatSheet ids.</summary>
        public static IReadOnlyList<DeclaredObject> Objects { get; } = new[]
        {
            Bonus("Moonwell#damage", "AItg", "AMWd", "Iatt:1", 0, "Damage bonus (Moonwell)"),
            Bonus("Moonwell#armor", "AId1", "AMWa", "Idef:1", 0, "Armor bonus (Moonwell)"),
            Bonus("Moonwell#attackspeed", "AIsx", "AMWs", "Isx1:1", 0.0f, "Attack speed bonus (Moonwell)"),
        };

        private static readonly string[] Getters = { "get_Damage", "get_Armor", "get_AttackSpeed" };

        /// <summary>Whether a map's code reads Stat.Damage, Stat.Armor or Stat.AttackSpeed, read from its built assemblies' metadata without loading them.</summary>
        public static bool UsedBy(string assemblyPath)
        {
            return CompiledFiles.MapAssemblies(assemblyPath).Any(ReadsBonusStats);
        }

        // A read of one of them is a member reference whose parent is Moonwell's Stat.
        private static bool ReadsBonusStats(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in metadata.MemberReferences)
            {
                var member = metadata.GetMemberReference(handle);
                if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference || !Getters.Contains(metadata.GetString(member.Name)))
                {
                    continue;
                }

                var type = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)member.Parent);
                if (metadata.GetString(type.Name) == "Stat" && metadata.GetString(type.Namespace) == "Moonwell")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Fails the build when a class of the map, or an object of the source map, has one of the abilities' ids.</summary>
        /// <exception cref="BuildException">One has such an id.</exception>
        public static void Check(IReadOnlyDictionary<string, string> ids, IReadOnlySet<string> sourceMapIds)
        {
            foreach (var bonus in Objects)
            {
                foreach (var entry in ids.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                {
                    if (entry.Value == bonus.PinnedId)
                    {
                        throw new BuildException($"{entry.Key} has the id {bonus.PinnedId}, which Moonwell keeps for the ability that gives stats their green bonus; give it another id");
                    }
                }

                if (sourceMapIds.Contains(bonus.PinnedId!))
                {
                    throw new BuildException($"the map's object files define {bonus.PinnedId}, which Moonwell keeps for the ability that gives stats their green bonus; give that object another id in World Editor");
                }
            }
        }

        /// <summary>Fails the build of a map that uses the bonus stats without Moonwell.Objects, which writes the abilities into the object files.</summary>
        /// <exception cref="BuildException">The map uses them.</exception>
        public static void CheckWithoutObjects(string assemblyPath)
        {
            if (UsedBy(assemblyPath))
            {
                throw new BuildException("Stat.Damage, Stat.Armor and Stat.AttackSpeed work through abilities Moonwell declares, which needs the Moonwell.Objects package: add a reference to it");
            }
        }

        private static DeclaredObject Bonus(string className, string baseId, string id, string field, object none, string name)
        {
            var changes = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["anam"] = name,
                [field] = none,
            };
            return new DeclaredObject(className, "ability", baseId, false, id, null, changes, Array.Empty<string>());
        }
    }
}
