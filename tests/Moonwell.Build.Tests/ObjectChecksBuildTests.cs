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

            Assert.Contains($"warning: Sample.Brute: {StormBolt} and Sample.Smash are both at button (0, 2) of its command card ([AllowClash] keeps it quiet)", log);
            Assert.Contains($"warning: Sample.Brute: {StormBolt} and Sample.Smash both have the hotkey T ([AllowClash] keeps it quiet)", log);
            Assert.Contains($"warning: Sample.Brute: {StormBolt} and Sample.Smash both take the order thunderbolt, so only one of them casts ([AllowClash] keeps it quiet)", log);
            Assert.DoesNotContain("Sample.Quiet:", log);
            Assert.Contains("warning: Sample.Spare is declared but nothing uses it: no other object lists it, no code names it and the map places none ([AllowUnused] keeps it quiet)", log);
            Assert.DoesNotContain("Sample.Kept is declared", log);
            Assert.DoesNotContain("Sample.Grunt is declared", log);
            Assert.DoesNotContain("Sample.Smash is declared", log);
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
