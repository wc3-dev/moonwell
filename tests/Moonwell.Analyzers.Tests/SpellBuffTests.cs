namespace Moonwell.Analyzers.Tests;

/// <summary>MW0051: a buff class a spell lists is the game's to put on (decisions 152 and 154).</summary>
public class SpellBuffTests
{
    private const string Listed = """
        [Moonwell.Objects.ObjectId("A010")]
        public sealed class FrostBolt : Moonwell.Objects.Human.Abilities.StormBolt
        {
            public FrostBolt() { Buffs.Set<Frozen>(); }
        }

        [Moonwell.Objects.ObjectId("B010", Carrier = "A011")]
        public sealed class Frozen : Moonwell.Objects.Other.Buffs.StunnedBPSE { }

        [Moonwell.Objects.ObjectId("B020", Carrier = "A021")]
        public sealed class Burning : Moonwell.Objects.NightElf.Buffs.Immolation { }
        """;

    [Fact]
    public void applying_a_buff_a_spell_lists_is_rejected()
    {
        var found = MapCode.Check("""
            var unit = Unit.Of(u)!;
            unit.Apply(new Frozen());
            var kept = new Frozen();
            unit.Apply(kept);
            unit.Apply(new Burning());
            """, Listed);

        Assert.Equal(new[] { "MW0051: new Frozen()", "MW0051: kept" }, found);
    }

    [Fact]
    public void a_buff_listed_by_add_or_replace_and_a_replaced_buff_count_as_listed()
    {
        var found = MapCode.Check("""
            var unit = Unit.Of(u)!;
            unit.Apply(new Added());
            unit.Apply(new Swapped());
            unit.Apply(new Slowed());
            """, """
            [Moonwell.Objects.ObjectId("A030")]
            public sealed class Adds : Moonwell.Objects.Human.Abilities.Slow
            {
                public Adds() { Buffs.Add<Added>(); Buffs.Replace<Moonwell.Objects.Human.Buffs.Slow, Swapped>(); }
            }

            [Moonwell.Objects.ObjectId("B030", Carrier = "A031")]
            public sealed class Added : Moonwell.Objects.Human.Buffs.Slow { }

            [Moonwell.Objects.ObjectId("B031", Carrier = "A032")]
            public sealed class Swapped : Moonwell.Objects.Human.Buffs.Slow { }

            [Moonwell.Objects.Replaces, Moonwell.Objects.ObjectId(Carrier = "A033")]
            public sealed class Slowed : Moonwell.Objects.Human.Buffs.Slow { }
            """);

        Assert.Equal(new[] { "MW0051: new Added()", "MW0051: new Swapped()", "MW0051: new Slowed()" }, found);
    }

    [Fact]
    public void a_listed_buff_setting_what_the_game_decides_is_rejected_and_its_tick_is_not()
    {
        var found = MapCode.CheckFile("""
            using Moonwell;

            namespace Map
            {
                [Moonwell.Objects.ObjectId("A010")]
                public sealed class FrostBolt : Moonwell.Objects.Human.Abilities.StormBolt
                {
                    public FrostBolt() { Buffs.Set<Frozen>(); }
                }

                [Moonwell.Objects.ObjectId("B010", Carrier = "A011")]
                public sealed class Frozen : Moonwell.Objects.Other.Buffs.StunnedBPSE
                {
                    public Frozen()
                    {
                        Duration = 3f;
                        Stacking = Stacking.Separate;
                        EndsOnDeath = false;
                        Tick = 1f;
                    }
                }

                [Moonwell.Objects.ObjectId("B020", Carrier = "A021")]
                public sealed class Burning : Moonwell.Objects.NightElf.Buffs.Immolation
                {
                    public Burning() { Duration = 3f; }
                }
            }
            """);

        Assert.Equal(new[] { "MW0051: Duration = 3f", "MW0051: Stacking = Stacking.Separate", "MW0051: EndsOnDeath = false" }, found);
    }

    [Fact]
    public void a_listed_buff_needs_a_constructor_with_no_parameters()
    {
        var found = MapCode.CheckFile("""
            namespace Map
            {
                [Moonwell.Objects.ObjectId("A010")]
                public sealed class FrostBolt : Moonwell.Objects.Human.Abilities.StormBolt
                {
                    public FrostBolt() { Buffs.Set<Frozen>(); }
                }

                [Moonwell.Objects.ObjectId("B010", Carrier = "A011")]
                public sealed class Frozen : Moonwell.Objects.Other.Buffs.StunnedBPSE
                {
                    public Frozen(int level) { }
                }
            }
            """);

        Assert.Equal(new[] { "MW0051: Frozen" }, found);
    }

    [Fact]
    public void the_message_says_why_and_what_to_write()
    {
        Assert.Equal(
            "'Frozen' is a buff a spell lists: the game puts it on and Moonwell watches for it, so it cannot also be applied (decision 154); give the applied one a class of its own, sharing the code through a base class",
            MapCode.Message("Unit.Of(u)!.Apply(new Frozen());", Listed));
    }
}
