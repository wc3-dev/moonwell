using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The object checks of decision 260: each case builds a copy of the Sample with its own objects.</summary>
    [Collection("Sample")]
    public sealed class ObjectChecksBuildTests : IDisposable
    {
        private const string StormBolt = "Moonwell.Objects.Human.Abilities.StormBolt";

        private readonly TestFolders folders;

        public ObjectChecksBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void clashes_on_a_command_card_and_unused_objects_are_warnings_the_attributes_keep_quiet()
        {
            var log = Build("""
                using Moonwell.Objects;

                namespace Sample
                {
                    // A Storm Bolt of its own: at Storm Bolt's place, with its hotkey and order.
                    [ObjectId("A001")]
                    public sealed class Smash : Moonwell.Objects.Human.Abilities.StormBolt
                    {
                    }

                    [ObjectId("H001")]
                    public sealed class Brute : Moonwell.Objects.Human.Units.MountainKing
                    {
                        public Brute()
                        {
                            HeroAbilities.Add<Smash>();
                        }
                    }

                    [ObjectId("H002")]
                    [AllowClash]
                    public sealed class Quiet : Moonwell.Objects.Human.Units.MountainKing
                    {
                        public Quiet()
                        {
                            HeroAbilities.Add<Smash>();
                        }
                    }

                    [ObjectId("A002")]
                    public sealed class Spare : Moonwell.Objects.Human.Abilities.HolyLight
                    {
                    }

                    [ObjectId("A003")]
                    [AllowUnused]
                    public sealed class Kept : Moonwell.Objects.Human.Abilities.HolyLight
                    {
                    }

                    [ObjectId("h001")]
                    public sealed class Grunt : Moonwell.Objects.Human.Units.Footman
                    {
                    }

                    public static class Uses
                    {
                        public static System.Type Grunts()
                        {
                            return typeof(Grunt);
                        }
                    }
                }
                """);

            Assert.Contains($"warning: Sample.Brute: {StormBolt} and Sample.Smash are both at button (0, 2) of its command card, so the game moves all but the first to other places ([AllowClash] keeps it quiet)", log);
            Assert.Contains($"warning: Sample.Brute: {StormBolt} and Sample.Smash both have the hotkey T ([AllowClash] keeps it quiet)", log);
            Assert.Contains($"warning: Sample.Brute: {StormBolt} and Sample.Smash both take the order thunderbolt, so only one of them casts ([AllowClash] keeps it quiet)", log);
            Assert.DoesNotContain("Sample.Quiet:", log);
            Assert.Contains("warning: Sample.Spare is declared but nothing uses it: no other object lists it, no code names it and the map places none ([AllowUnused] keeps it quiet)", log);
            Assert.DoesNotContain("Sample.Kept is declared", log);
            Assert.DoesNotContain("Sample.Grunt is declared", log);
            Assert.DoesNotContain("Sample.Smash is declared", log);
        }

        [Fact]
        public void what_a_unit_trains_builds_researches_and_sells_is_checked_as_its_abilities_are()
        {
            var log = Build("""
                using Moonwell.Objects;

                namespace Sample
                {
                    [ObjectId("o001")]
                    public sealed class WolfA : Moonwell.Objects.Orc.Units.Grunt
                    {
                    }

                    [ObjectId("o002")]
                    public sealed class WolfB : Moonwell.Objects.Orc.Units.Grunt
                    {
                    }

                    [ObjectId("R001")]
                    public sealed class PlateA : Moonwell.Objects.Orc.Upgrades.SteelArmor
                    {
                    }

                    [ObjectId("R002")]
                    public sealed class PlateB : Moonwell.Objects.Orc.Upgrades.SteelArmor
                    {
                    }

                    [ObjectId("I001")]
                    public sealed class ClawA : Moonwell.Objects.Items.ClawsOfAttack3
                    {
                    }

                    [ObjectId("I002")]
                    public sealed class ClawB : Moonwell.Objects.Items.ClawsOfAttack3
                    {
                    }

                    [ObjectId("o003")]
                    public sealed class Camp : Moonwell.Objects.Orc.Units.Barracks
                    {
                        public Camp()
                        {
                            Trains.Set<WolfA, WolfB>();
                            ResearchesAvailable.Set<PlateA, PlateB>();
                            Sells.Set<ClawA, ClawB>();
                        }
                    }

                    [ObjectId("o004")]
                    [AllowClash]
                    public sealed class QuietCamp : Moonwell.Objects.Orc.Units.Barracks
                    {
                        public QuietCamp()
                        {
                            Trains.Set<WolfA, WolfB>();
                        }
                    }

                    [ObjectId("o007")]
                    public sealed class Plain : Moonwell.Objects.Orc.Units.Barracks
                    {
                        public Plain()
                        {
                            Trains.Add<WolfA>();
                        }
                    }

                    [ObjectId("o005")]
                    public sealed class Builder : Moonwell.Objects.Orc.Units.Peon
                    {
                        public Builder()
                        {
                            Builds.Set<Camp, QuietCamp>();
                        }
                    }

                    [ObjectId("o006")]
                    public sealed class Crowded : Moonwell.Objects.Orc.Units.Barracks
                    {
                        public Crowded()
                        {
                            Trains.Add<Moonwell.Objects.Orc.Units.Peon>();
                            Trains.Add<Moonwell.Objects.Orc.Units.Raider>();
                            Trains.Add<Moonwell.Objects.Orc.Units.Shaman>();
                            Trains.Add<Moonwell.Objects.Orc.Units.WitchDoctor>();
                            Trains.Add<Moonwell.Objects.Orc.Units.KodoBeast>();
                            Trains.Add<Moonwell.Objects.Orc.Units.WindRider>();
                            Trains.Add<Moonwell.Objects.Orc.Units.Tauren>();
                            Trains.Add<Moonwell.Objects.Orc.Units.SpiritWalker>();
                        }
                    }

                    public static class Uses
                    {
                        public static System.Type[] All()
                        {
                            return new[] { typeof(Builder), typeof(Crowded), typeof(Plain) };
                        }
                    }
                }
                """);

            Assert.Contains("warning: Sample.Camp: Sample.WolfA, Sample.WolfB, Sample.ClawA and Sample.ClawB are all at button (0, 0) of its command card, so the game moves all but the first to other places ([AllowClash] keeps it quiet)", log);
            Assert.Contains("warning: Sample.Camp: Sample.WolfA and Sample.WolfB both have the hotkey G ([AllowClash] keeps it quiet)", log);
            Assert.Contains("warning: Sample.Camp: Sample.PlateA and Sample.PlateB are both at button", log);
            Assert.Contains("warning: Sample.Camp: Sample.ClawA and Sample.ClawB both have the hotkey K", log);
            Assert.Contains("warning: Sample.Plain: Moonwell.Objects.Orc.Units.Grunt", log);
            Assert.DoesNotContain("Sample.Plain: Moonwell.Objects.Orc.Units.Headhunter", log);
            Assert.Matches(@"warning: Sample\.Builder: Sample\.Camp and Sample\.QuietCamp are both at button \(\d, \d\) of its build menu, so the game moves all but the first to other places \(\[AllowClash\] keeps it quiet\)", log);
            Assert.Matches(@"warning: Sample\.Crowded: its command card holds 1[3-5] buttons, and has 12 places \(\[AllowClash\] keeps it quiet\)", log);
            Assert.DoesNotContain("Sample.QuietCamp:", log);
            Assert.DoesNotContain("Sample.Camp: its command card holds", log);
        }

        [Theory]
        [InlineData("Deals <A0O1,DataA1> damage.", "Sample.Bolt: arut has the placeholder <A0O1,DataA1>, which names no object, and the game shows 0 there (ids are case-sensitive)")]
        [InlineData("Deals <AHtb,DataA1> and <A001,DataA4> damage.", "Sample.Bolt: arut has the placeholder <A001,DataA4>, a level past the 3 levels of A001, and the game shows 0 there")]
        public void a_placeholder_the_game_shows_as_0_fails_the_build(string text, string expected)
        {
            var copy = folders.TemporaryCopyOfSample();
            File.WriteAllText(Path.Combine(copy, "Objects.cs"), $$"""
                namespace Sample
                {
                    [Moonwell.Objects.ObjectId("A001")]
                    public sealed class Bolt : Moonwell.Objects.Human.Abilities.StormBolt
                    {
                        public Bolt()
                        {
                            TooltipLearnExtended = "{{text}} <A001,DataA3,%>";
                        }
                    }
                }
                """);

            var error = Assert.Throws<BuildException>(() => MapBuild.Run(copy, new BuildOptions(), TextWriter.Null));

            Assert.Equal(expected, error.Message);
        }

        private string Build(string objects)
        {
            var copy = folders.TemporaryCopyOfSample();
            File.WriteAllText(Path.Combine(copy, "Objects.cs"), objects);
            var log = new StringWriter();
            MapBuild.Run(copy, new BuildOptions(), log);
            return log.ToString();
        }
    }
}
