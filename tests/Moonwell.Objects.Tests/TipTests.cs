namespace Moonwell.Objects.Tests
{
    /// <summary>Storm Bolt (a hero ability of 3 levels, 75 mana and a 9 second cooldown at each) described by a map.</summary>
    public class DescribedBolt : Moonwell.Objects.Human.Abilities.StormBolt
    {
        public DescribedBolt()
        {
            Damage = Levels(75f, 150f, 225f);
            Cooldown = Levels(9f, 8f, 1f);
            Describe("Smite", tip => $"Deals {tip[Damage]} damage, {tip.Percent(0.25f)} more to {tip[ManaCost]} foes, level {tip.Level}.", TipLines.ManaCost | TipLines.Cooldown | TipLines.Area);
        }
    }

    /// <summary>Defend, a unit's ability of one level, described by a map.</summary>
    public class DescribedDefend : Moonwell.Objects.Human.Abilities.Defend
    {
        public DescribedDefend()
        {
            Describe("Guard", tip => $"Blocks {tip.Percent(PerLevel(level => 0.1f * level))} of the damage.", TipLines.Cooldown);
        }
    }

    public class TipTests
    {
        private const string Gold = "|cffffcc00";

        [Fact]
        public void a_hero_ability_gets_a_title_and_a_text_for_each_level_and_the_learn_tooltips()
        {
            var bolt = new DescribedBolt();

            Assert.Equal($"Smite - [{Gold}Level 2|r]", bolt.Tooltip[2]);
            Assert.Equal(
                $"Deals {Gold}150|r damage, {Gold}25|r% more to {Gold}75|r foes, level 2.|n|nMana: {Gold}75|r|nCooldown: {Gold}8|r seconds",
                bolt.TooltipExtended[2]);
            Assert.Equal($"Learn Smite - [{Gold}Level %d|r]", bolt.TooltipLearn);
            Assert.Equal(
                "Deals 75/150/225 damage, 25% more to 75 foes, level 0.|n|nMana: 75|nCooldown: 9/8/1 seconds",
                bolt.TooltipLearnExtended);
        }

        [Fact]
        public void a_line_of_one_second_is_singular_and_a_line_of_0_is_left_out()
        {
            var bolt = new DescribedBolt();

            Assert.EndsWith($"Cooldown: {Gold}1|r second", bolt.TooltipExtended[3]);
            Assert.DoesNotContain("Area", bolt.TooltipLearnExtended);
        }

        [Fact]
        public void a_units_ability_of_one_level_is_titled_by_its_name_alone()
        {
            var defend = new DescribedDefend();

            Assert.Equal("Guard", defend.Tooltip[1]);
            Assert.Equal($"Blocks {Gold}10|r% of the damage.", defend.TooltipExtended[1]);
            Assert.Equal(1, defend.TooltipExtended.Count);
        }
    }
}
