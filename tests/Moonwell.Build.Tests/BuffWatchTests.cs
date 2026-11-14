namespace Moonwell.Build.Tests
{
    /// <summary>Which buff classes the build tells the runtime to watch for, and which abilities list each (decision 152).</summary>
    public sealed class BuffWatchTests
    {
        private static DeclaredObject Declared(string name, string kind, string root, bool replaces = false, IReadOnlyList<string>? buffs = null)
        {
            var changed = new Dictionary<string, object?>();
            if (buffs != null)
            {
                changed["abuf"] = buffs;
            }

            return new DeclaredObject(name, kind, root, replaces, null, null, changed, buffs ?? new List<string>());
        }

        private static string Watched(string type, string id) =>
            $"global::Moonwell.ObjectTypes.WatchedBuff<global::{type}>(global::Moonwell.ObjectTypes.FourCC(\"{id}\"), () => new global::{type}());";

        private static string Lists(string ability, string buff, bool aura) =>
            $"global::Moonwell.ObjectTypes.ListsBuff(global::Moonwell.ObjectTypes.FourCC(\"{ability}\"), global::Moonwell.ObjectTypes.FourCC(\"{buff}\"), {(aura ? "true" : "false")});";

        [Fact]
        public void a_spell_that_lists_a_declared_buff_watches_it()
        {
            var declared = new[] { Declared("Map.FrostBolt", "ability", "AHtb", buffs: new[] { "Map.Frozen" }), Declared("Map.Frozen", "buff", "BPSE") };
            var ids = new Dictionary<string, string> { ["Map.FrostBolt"] = "A010", ["Map.Frozen"] = "B010" };
            var standard = new Dictionary<string, IReadOnlyList<string>> { ["AHtb"] = new[] { "BPSE" } };

            var lines = BuffWatch.Lines(declared, ids, new Dictionary<string, string>(), standard);

            Assert.Equal(new[] { Watched("Map.Frozen", "B010"), Lists("A010", "B010", false) }, lines);
        }

        [Fact]
        public void a_copy_of_an_aura_lists_it_as_an_aura()
        {
            var declared = new[] { Declared("Map.Frost", "ability", "AHad", buffs: new[] { "Map.Chilled" }), Declared("Map.Chilled", "buff", "BHad") };
            var ids = new Dictionary<string, string> { ["Map.Frost"] = "A020", ["Map.Chilled"] = "B020" };

            var lines = BuffWatch.Lines(declared, ids, new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<string>>());

            Assert.Contains(Lists("A020", "B020", true), lines);
        }

        [Fact]
        public void a_replaced_standard_buff_is_watched_through_the_standard_spells_that_list_it()
        {
            var declared = new[] { Declared("Map.Slowed", "buff", "Bslo", replaces: true) };
            var ids = new Dictionary<string, string> { ["Map.Slowed"] = "Bslo" };
            var standard = new Dictionary<string, IReadOnlyList<string>> { ["Aslo"] = new[] { "Bslo" }, ["AHtb"] = new[] { "BPSE" } };

            var lines = BuffWatch.Lines(declared, ids, new Dictionary<string, string>(), standard);

            Assert.Equal(new[] { Watched("Map.Slowed", "Bslo"), Lists("Aslo", "Bslo", false) }, lines);
        }

        [Fact]
        public void a_declared_copy_that_keeps_its_standard_list_lists_the_replaced_buff()
        {
            var declared = new[] { Declared("Map.Slowed", "buff", "Bslo", replaces: true), Declared("Map.BigSlow", "ability", "Aslo") };
            var ids = new Dictionary<string, string> { ["Map.Slowed"] = "Bslo", ["Map.BigSlow"] = "A030" };
            var standard = new Dictionary<string, IReadOnlyList<string>> { ["Aslo"] = new[] { "Bslo" } };

            var lines = BuffWatch.Lines(declared, ids, new Dictionary<string, string>(), standard);

            Assert.Contains(Lists("A030", "Bslo", false), lines);
            Assert.Contains(Lists("Aslo", "Bslo", false), lines);
        }

        [Fact]
        public void a_buff_no_ability_lists_is_not_watched()
        {
            var declared = new[] { Declared("Map.Mark", "buff", "Bblo") };
            var ids = new Dictionary<string, string> { ["Map.Mark"] = "B040" };
            var standard = new Dictionary<string, IReadOnlyList<string>> { ["AOsf"] = new[] { "Bblo" } };

            Assert.Empty(BuffWatch.Lines(declared, ids, new Dictionary<string, string>(), standard));
        }

        [Fact]
        public void a_standard_buff_class_named_in_a_declared_list_resolves_by_its_standard_id()
        {
            var declared = new[] { Declared("Map.Slowed", "buff", "Bslo", replaces: true), Declared("Map.Mix", "ability", "AHtb", buffs: new[] { "Moonwell.Objects.Human.Buffs.Slow" }) };
            var ids = new Dictionary<string, string> { ["Map.Slowed"] = "Bslo", ["Map.Mix"] = "A050" };
            var named = new Dictionary<string, string> { ["Moonwell.Objects.Human.Buffs.Slow"] = "Bslo" };

            var lines = BuffWatch.Lines(declared, ids, named, new Dictionary<string, IReadOnlyList<string>>());

            Assert.Contains(Lists("A050", "Bslo", false), lines);
        }

        [Fact]
        public void a_declared_buffs_icon_is_registered_for_the_kits_buff_row_and_one_with_none_is_not()
        {
            var burning = Declared("Map.Burning", "buff", "BPSE");
            ((Dictionary<string, object?>)burning.Changed)["fart"] = @"ReplaceableTextures\CommandButtons\BTNImmolationOn.blp";
            var declared = new[] { burning, Declared("Map.Plain", "buff", "Bxxx"), Declared("Map.Bolt", "ability", "AHtb") };

            var lines = BuffWatch.IconLines(declared, StandardData.Empty, null);

            Assert.Equal(new[] { @"global::Moonwell.ObjectTypes.BuffIcon<global::Map.Burning>(""ReplaceableTextures\\CommandButtons\\BTNImmolationOn.blp"");" }, lines);
        }
    }
}
