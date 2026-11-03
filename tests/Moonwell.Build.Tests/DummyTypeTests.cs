namespace Moonwell.Build.Tests
{
    /// <summary>What the build registers for dummy casts (B13): the dummy's type, each spell's order and requirements, and the dummy's id kept free.</summary>
    [Collection("Sample")]
    public sealed class DummyTypeTests : IClassFixture<DeclarationsTests.BuiltObjects>
    {
        private readonly DeclarationsTests.BuiltObjects built;

        public DummyTypeTests(DeclarationsTests.BuiltObjects built)
        {
            this.built = built;
        }

        private static UsedClass Used(string name, string? standardId = null) =>
            new UsedClass(name, "Moonwell.Objects.StandardAbility", "ability", standardId, standardId != null, Array.Empty<UsedField>(), Array.Empty<UsedMethod>(), "", Array.Empty<UsedField>());

        private static DeclaredObject Ability(string name, string root, Dictionary<string, object?>? changed = null) =>
            new DeclaredObject(name, "ability", root, false, null, null, changed ?? new Dictionary<string, object?>(), Array.Empty<string>());

        private static string Order(string id, string order, bool selfOnly) =>
            $"global::Moonwell.ObjectTypes.CastOrder(global::Moonwell.ObjectTypes.FourCC(\"{id}\"), \"{order}\", {(selfOnly ? "true" : "false")});";

        private static string Requires(string id, string tech, int level) =>
            $"global::Moonwell.ObjectTypes.Requires(global::Moonwell.ObjectTypes.FourCC(\"{id}\"), global::Moonwell.ObjectTypes.FourCC(\"{tech}\"), {level});";

        private IReadOnlyList<string> Lines(DeclaredObject declared, string id)
        {
            return DummyType.Lines(true, new[] { Used(declared.ClassName) }, new[] { declared }, new Dictionary<string, string> { [declared.ClassName] = id }, new Dictionary<string, string>(), built.Standard, null);
        }

        [Fact]
        public void a_map_that_casts_registers_the_dummy_first()
        {
            var lines = DummyType.Lines(true, Array.Empty<UsedClass>(), Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), new Dictionary<string, string>(), built.Standard, null);

            Assert.Equal($"global::Moonwell.ObjectTypes.DummyType(global::Moonwell.ObjectTypes.FourCC(\"{DummyType.Id}\"));", Assert.Single(lines));
        }

        [Fact]
        public void a_map_that_does_not_cast_registers_nothing()
        {
            var lines = DummyType.Lines(false, new[] { Used("Moonwell.Objects.Human.Abilities.StormBolt", "AHtb") }, Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), new Dictionary<string, string>(), built.Standard, null);

            Assert.Empty(lines);
        }

        [Fact]
        public void a_standard_spell_the_code_names_registers_its_order()
        {
            var lines = DummyType.Lines(true, new[] { Used("Moonwell.Objects.Human.Abilities.StormBolt", "AHtb") }, Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), new Dictionary<string, string>(), built.Standard, null);

            Assert.Contains(Order("AHtb", "thunderbolt", false), lines);
        }

        [Fact]
        public void a_declared_copy_registers_its_roots_order()
        {
            Assert.Contains(Order("A010", "thunderbolt", false), Lines(Ability("Map.FrostBolt", "AHtb"), "A010"));
        }

        [Fact]
        public void a_channel_copy_casts_by_its_base_order()
        {
            var nova = Ability("Map.Nova", "ANcl", new Dictionary<string, object?> { ["Ncl6:1"] = "frostnova" });

            Assert.Contains(Order("A020", "frostnova", false), Lines(nova, "A020"));
        }

        [Fact]
        public void a_spell_with_no_targets_does_not_count_as_landing_on_its_caster()
        {
            Assert.Contains(Order("A030", "avatar", false), Lines(Ability("Map.Big", "AHav"), "A030"));
            Assert.Contains(Order("A031", "blizzard", false), Lines(Ability("Map.Storm", "AHbz"), "A031"));
        }

        [Fact]
        public void a_spell_whose_targets_are_only_its_caster_lands_on_it()
        {
            var targets = built.Standard.Fields("ability").Single(field => field.Id == "atar").Choice!;
            var fire = Ability("Map.SelfFire", "Ainf", new Dictionary<string, object?> { ["atar:1"] = targets.Number("self") });

            Assert.Contains(Order("A032", "innerfire", true), Lines(fire, "A032"));
            Assert.Contains(Order("A033", "innerfire", false), Lines(Ability("Map.Fire", "Ainf"), "A033"));
        }

        [Fact]
        public void a_polymorph_copy_requires_its_research_at_its_level()
        {
            Assert.Contains(Requires("A040", "Rhst", 2), Lines(Ability("Map.Sheep", "Aply"), "A040"));
        }

        [Fact]
        public void a_copy_without_its_requirement_requires_nothing()
        {
            var sheep = Ability("Map.FreeSheep", "Aply", new Dictionary<string, object?> { ["areq"] = new List<string>() });

            Assert.DoesNotContain(Lines(sheep, "A041"), line => line.Contains(".Requires(", StringComparison.Ordinal));
        }

        private static DeclaredObject MapAbility(string id, string root, Dictionary<string, object?>? changed = null) =>
            new DeclaredObject(id, "ability", root, false, id, null, changed ?? new Dictionary<string, object?>(), Array.Empty<string>());

        private IReadOnlyList<string> MapLines(params DeclaredObject[] abilities)
        {
            return DummyType.Lines(true, Array.Empty<UsedClass>(), Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), new Dictionary<string, string>(), built.Standard, null, abilities);
        }

        [Fact]
        public void a_world_editor_ability_registers_its_bases_order()
        {
            Assert.Contains(Order("A000", "acidbomb", false), MapLines(MapAbility("A000", "ANab")));
        }

        [Fact]
        public void a_world_editor_channel_casts_by_its_base_order_id()
        {
            Assert.Contains(Order("A001", "frostnova", false), MapLines(MapAbility("A001", "ANcl", new Dictionary<string, object?> { ["Ncl6:1"] = "frostnova" })));
        }

        [Fact]
        public void a_world_editor_abilitys_own_requirements_count()
        {
            var free = MapLines(MapAbility("A002", "Aply", new Dictionary<string, object?> { ["areq"] = "" }));
            var changed = MapLines(MapAbility("A003", "Aply", new Dictionary<string, object?> { ["areq"] = "Rhpt", ["arqa"] = "1" }));

            Assert.DoesNotContain(free, line => line.Contains(".Requires(", StringComparison.Ordinal));
            Assert.Contains(Requires("A003", "Rhpt", 1), changed);
        }

        [Fact]
        public void a_world_editor_ability_the_code_also_names_is_registered_once()
        {
            var lines = DummyType.Lines(true, new[] { Used("Moonwell.Objects.Human.Abilities.StormBolt", "AHtb") }, Array.Empty<DeclaredObject>(), new Dictionary<string, string>(), new Dictionary<string, string>(), built.Standard, null, new[] { MapAbility("AHtb", "AHtb") });

            Assert.Single(lines, line => line.Contains("CastOrder(global::Moonwell.ObjectTypes.FourCC(\"AHtb\")", StringComparison.Ordinal));
        }

        [Fact]
        public void the_source_maps_abilities_are_read_from_its_object_files()
        {
            var ability = Assert.Single(DummyType.MapAbilities(TestFolders.Fixture("WorldEditorObjects")));

            Assert.Equal("A000", ability.PinnedId);
            Assert.Equal("ANab", ability.StandardRoot);
        }

        [Fact]
        public void a_map_class_with_the_dummys_id_fails_the_build()
        {
            var error = Assert.Throws<BuildException>(() => DummyType.Check(new Dictionary<string, string> { ["Map.Mine"] = DummyType.Id }, new HashSet<string>()));

            Assert.Contains("Map.Mine", error.Message, StringComparison.Ordinal);
            Assert.Contains(DummyType.Id, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void a_source_map_object_with_the_dummys_id_fails_the_build()
        {
            var error = Assert.Throws<BuildException>(() => DummyType.Check(new Dictionary<string, string>(), new HashSet<string> { DummyType.Id }));

            Assert.Contains(DummyType.Id, error.Message, StringComparison.Ordinal);
        }
    }
}
