using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Moonwell.Build.Tests
{
    /// <summary>What the build registers for Ability.CanTarget (J100): each ability's Targets Allowed at each level, which the natives cannot read.</summary>
    [Collection("Sample")]
    public sealed class AbilityTargetsTests : IClassFixture<DeclarationsTests.BuiltObjects>
    {
        private readonly DeclarationsTests.BuiltObjects built;

        public AbilityTargetsTests(DeclarationsTests.BuiltObjects built)
        {
            this.built = built;
        }

        private static UsedClass Used(string name, string? standardId = null) =>
            new UsedClass(name, "Moonwell.Objects.StandardAbility", "ability", standardId, standardId != null, Array.Empty<UsedField>(), Array.Empty<UsedMethod>(), "", Array.Empty<UsedField>());

        private static string Line(string id, params string[] levels) =>
            $"global::Moonwell.ObjectTypes.TargetsAllowed(global::Moonwell.ObjectTypes.FourCC(\"{id}\"), new string[] {{ {string.Join(", ", levels.Select(level => "\"" + level + "\""))} }});";

        private const string StormBolt = "air,ground,debris,enemy,neutral,organic";

        private const string Table = "global::Moonwell.ObjectTypes.StandardTargets(";

        // The lines of the abilities the map names, not the grouped table of the other standard ones, nor those of the
        // standard abilities whose lists differ by level, which the table leaves to lines of their own.
        private IReadOnlyList<string> Named(IReadOnlyList<string> lines)
        {
            Assert.StartsWith(Table, lines[0], StringComparison.Ordinal);
            var own = Standard().Where(line => !line.StartsWith(Table, StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
            return lines.Skip(1).Where(line => !own.Contains(line)).ToList();
        }

        // The lines the build writes for the standard abilities when the map names none.
        private IReadOnlyList<string> Standard() =>
            AbilityTargets.Lines(true, Array.Empty<UsedClass>(), Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), built.Standard, null);

        // The groups of the table line: each list with the ids that have it, run together.
        private static Dictionary<string, string> Groups(string table)
        {
            var texts = System.Text.RegularExpressions.Regex.Matches(table, "\"([^\"]*)\"").Select(match => match.Groups[1].Value).ToList();
            var groups = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i + 1 < texts.Count; i += 2)
            {
                groups[texts[i]] = texts[i + 1];
            }

            return groups;
        }

        private static string? GroupOf(Dictionary<string, string> groups, string id) =>
            groups.Where(group => Enumerable.Range(0, group.Value.Length / 4).Any(i => group.Value.Substring(i * 4, 4) == id)).Select(group => group.Key).FirstOrDefault();

        [Fact]
        public void every_other_standard_ability_is_in_one_grouped_table_first()
        {
            var lines = Standard();
            var groups = Groups(lines[0]);

            Assert.StartsWith(Table, lines[0], StringComparison.Ordinal);
            Assert.Equal(StormBolt, GroupOf(groups, "AHtb"));
            Assert.Equal("air,ground,friend,self,vuln,invu", GroupOf(groups, "AHab"));
            var ids = string.Concat(groups.Values);
            Assert.Equal(0, ids.Length % 4);
            var all = Enumerable.Range(0, ids.Length / 4).Select(i => ids.Substring(i * 4, 4)).ToList();
            Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
            Assert.All(lines.Skip(1), line => Assert.StartsWith("global::Moonwell.ObjectTypes.TargetsAllowed(", line, StringComparison.Ordinal));
            Assert.Equal(built.Standard.Ids("ability").Count(id => id.Length == 4 && !built.Standard.IsMissing(id, null)), all.Count + lines.Count - 1);
        }

        [Fact]
        public void a_standard_ability_whose_lists_differ_by_level_keeps_a_line_of_its_own()
        {
            var lines = Standard();
            var groups = Groups(lines[0]);

            // Chemical Rage's (ANcr) and a Shockwave's (ANsh) lists are empty past level 1.
            Assert.Equal(new[] { Line("ANcr", "self", "", ""), Line("ANsh", "ground,structure", "", "") }, lines.Skip(1));
            Assert.Null(GroupOf(groups, "ANcr"));
            Assert.Null(GroupOf(groups, "ANsh"));
        }

        [Fact]
        public void the_table_leaves_out_the_abilities_the_map_names()
        {
            var lines = AbilityTargets.Lines(true, new[] { Used("Moonwell.Objects.Human.Abilities.StormBolt", "AHtb") }, Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), built.Standard, null);

            Assert.Null(GroupOf(Groups(lines[0]), "AHtb"));
            Assert.Equal("air,ground,organic,notself,invu,vuln,nonancient", GroupOf(Groups(lines[0]), "AHhb"));
        }

        [Fact]
        public void a_map_that_does_not_ask_registers_nothing()
        {
            Assert.Empty(AbilityTargets.Lines(false, new[] { Used("Moonwell.Objects.Human.Abilities.StormBolt", "AHtb") }, Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), built.Standard, null));
        }

        [Fact]
        public void a_standard_ability_the_code_names_registers_each_levels_list()
        {
            var lines = AbilityTargets.Lines(true, new[] { Used("Moonwell.Objects.Human.Abilities.StormBolt", "AHtb") }, Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), built.Standard, null);

            Assert.Equal(Line("AHtb", StormBolt, StormBolt, StormBolt), Assert.Single(Named(lines)));
        }

        [Fact]
        public void a_declared_copy_registers_its_own_list_and_levels()
        {
            var targets = built.Standard.Fields("ability").Single(field => field.Id == "atar").Choice!;
            var copy = new DeclaredObject("Map.Hammer", "ability", "AHtb", false, null, null, new Dictionary<string, object?>
            {
                ["alev"] = 2,
                ["atar:1"] = targets.Number("air"),
            }, Array.Empty<string>());

            var lines = AbilityTargets.Lines(true, new[] { Used("Map.Hammer") }, new[] { copy }, new Dictionary<string, string> { ["Map.Hammer"] = "A050" }, built.Standard, null);

            Assert.Equal(Line("A050", "air", StormBolt), Assert.Single(Named(lines)));
        }

        [Fact]
        public void a_world_editor_ability_registers_its_changed_list_once()
        {
            var edited = new DeclaredObject("AHtb", "ability", "AHtb", false, "AHtb", null, new Dictionary<string, object?> { ["atar:1"] = "ground,enemies" }, Array.Empty<string>());
            var other = new DeclaredObject("A060", "ability", "AHtb", false, "A060", null, new Dictionary<string, object?> { ["alev"] = "1", ["atar:1"] = "" }, Array.Empty<string>());

            var lines = AbilityTargets.Lines(true, new[] { Used("Moonwell.Objects.Human.Abilities.StormBolt", "AHtb") }, Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), built.Standard, null, new[] { edited, other });

            Assert.Equal(new[] { Line("AHtb", "ground,enemies", StormBolt, StormBolt), Line("A060", "") }, Named(lines));
        }

        [Fact]
        public void the_source_maps_abilities_carry_their_targets_and_levels()
        {
            var ability = Assert.Single(DummyType.MapAbilities(TestFolders.Fixture("WorldEditorObjects")));

            Assert.DoesNotContain(ability.Changed.Keys, key => !(key.StartsWith("Ncl6", StringComparison.Ordinal) || key.StartsWith("areq", StringComparison.Ordinal) || key.StartsWith("arqa", StringComparison.Ordinal) || key.StartsWith("atar", StringComparison.Ordinal) || key.StartsWith("alev", StringComparison.Ordinal)));
        }
    }

    /// <summary>Whether a map's code asks Ability.CanTarget, read from its assembly and the libraries of its own.</summary>
    public sealed class AbilityTargetsUseTests : IDisposable
    {
        // A stand-in for the runtime: only the members the build looks for.
        private const string RuntimeSource = "namespace Moonwell { public class Unit { } public class Ability { public bool CanTarget(Unit u) { return false; } public int Level { get { return 0; } } } }";

        private readonly TestFolders folders;

        public AbilityTargetsUseTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_map_that_asks_can_target_is_found()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            var runtime = Compile(folder, "Moonwell", RuntimeSource);
            var map = Compile(folder, "MapCode", "class Smite : Moonwell.Ability { } class A { bool F(Smite s) { return s.CanTarget(null); } }", runtime);

            Assert.True(AbilityTargets.UsedBy(map));
        }

        [Fact]
        public void a_map_that_only_reads_an_abilitys_level_is_not()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            var runtime = Compile(folder, "Moonwell", RuntimeSource);
            var map = Compile(folder, "MapCode", "class A { int F(Moonwell.Ability a) { return a.Level; } }", runtime);

            Assert.False(AbilityTargets.UsedBy(map));
        }

        // Compiles an assembly of one source into the folder, referencing the runtime and the given assemblies.
        private static string Compile(string folder, string name, string source, params string[] references)
        {
            var metadata = new List<MetadataReference>();
            foreach (var path in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
            {
                if (!Path.GetFileName(path).StartsWith("Moonwell", StringComparison.Ordinal))
                {
                    metadata.Add(MetadataReference.CreateFromFile(path));
                }
            }

            metadata.AddRange(references.Select(path => MetadataReference.CreateFromFile(path)));
            var compilation = CSharpCompilation.Create(
                name,
                new[] { CSharpSyntaxTree.ParseText(source) },
                metadata,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var assembly = Path.Combine(folder, name + ".dll");
            var result = compilation.Emit(assembly);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            return assembly;
        }
    }
}
