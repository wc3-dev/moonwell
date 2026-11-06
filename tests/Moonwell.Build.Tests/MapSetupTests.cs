using War3Net.Build.Info;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>The map class's slots and teams, read on .NET and written into the map's war3map.w3i (decision 198).</summary>
    [Collection("Sample")]
    public sealed class MapSetupTests : IDisposable
    {
        private const string Teams =
            "        public static readonly Team West = new Team(\"West\") { SharedVision = true };\n"
            + "        public static readonly Team East = new Team(\"East\") { SharedControl = true };\n";

        private readonly TestFolders folders;

        public MapSetupTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        [Fact]
        public void slots_replace_the_players_and_forces()
        {
            var folder = SampleWithSlots(
                "Slots.Add(new Slot(5, East, Controller.Computer, Race.Undead) { Name = \"The Scourge\" }, new Slot(0, West, Controller.User), new Slot(2, East, Controller.User, Race.Orc));");
            var project = MapProject.Find(folder);
            var assembly = DotnetBuild.Run(project, false, TextWriter.Null);
            var log = new StringWriter();

            var settings = MapSettingsReader.Read(assembly, project);
            var info = SourceMap.Open(project, settings, log).Info!;

            Assert.Equal(new[] { new TeamSetting("East", false, true), new TeamSetting("West", true, false) }, settings.Teams);
            Assert.Equal(new SlotSetting(5, 0, 1, 2, "The Scourge"), settings.Slots![0]);
            Assert.Equal(new SlotSetting(0, 1, 0, -1, ""), settings.Slots[1]);

            Assert.Equal(new[] { 0, 2, 5 }, info.Players.Select(player => player.Id));
            var first = info.Players[0];
            Assert.Equal(PlayerController.User, first.Controller);
            Assert.Equal(PlayerRace.Selectable, first.Race);
            Assert.Equal(PlayerFlags.FixedStartPosition | PlayerFlags.RaceSelectable, first.Flags);
            Assert.Equal("Player 1", first.Name);
            Assert.Equal(new System.Numerics.Vector2(128, -896), first.StartPosition);
            Assert.Equal(PlayerRace.Orc, info.Players[1].Race);
            Assert.Equal(PlayerController.Computer, info.Players[2].Controller);
            Assert.Equal("The Scourge", info.Players[2].Name);

            Assert.Equal(2, info.Forces.Count);
            Assert.Equal("East", info.Forces[0].Name);
            Assert.Equal(ForceFlags.Allied | ForceFlags.ShareUnitControl, info.Forces[0].Flags);
            Assert.Equal((1 << 2) | (1 << 5), (int)info.Forces[0].Players);
            Assert.Equal(ForceFlags.Allied | ForceFlags.ShareVision, info.Forces[1].Flags);
            Assert.Equal(1, (int)info.Forces[1].Players);
            Assert.True(info.MapFlags.HasFlag(MapFlags.UseCustomForces | MapFlags.FixedPlayerSettingsForCustomForces));
            Assert.Contains("note: slot 5 has no start location", log.ToString());
        }

        [Fact]
        public void no_slots_keep_world_editors()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var assembly = DotnetBuild.Run(project, false, TextWriter.Null);

            var settings = MapSettingsReader.Read(assembly, project);
            var info = SourceMap.Open(project, settings, TextWriter.Null).Info!;

            Assert.Empty(settings.Slots!);
            Assert.Equal(new[] { 0, 1, 2, 3, 11 }, info.Players.Select(player => player.Id));
            Assert.Equal(2, info.Forces.Count);
        }

        [Theory]
        [InlineData("Slots.Add(new Slot(24, West, Controller.User), new Slot(0, West, Controller.User));", "slot 24 is not one a person or a computer plays; slots go from 0 to 23")]
        [InlineData("Slots.Add(new Slot(1, West, Controller.User), new Slot(1, East, Controller.User));", "slot 1 is set up twice")]
        [InlineData("Slots.Add(new Slot(0, West, Controller.Creep), new Slot(1, West, Controller.User));", "slot 0 cannot be played by Creep; use User, Computer, Rescuable or Neutral")]
        [InlineData("Slots.Add(new Slot(0, West, Controller.User, Race.Demon), new Slot(1, West, Controller.User));", "slot 0 cannot play Demon; use Human, Orc, Undead, NightElf, or none for the player's pick")]
        public void a_slot_that_cannot_be_set_up_is_an_error(string slots, string message)
        {
            var project = MapProject.Find(SampleWithSlots(slots));
            var assembly = DotnetBuild.Run(project, false, TextWriter.Null);

            var error = Assert.Throws<BuildException>(() => MapSettingsReader.Read(assembly, project));

            Assert.Equal("Sample.SampleMap.Slots: " + message, error.Message);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        // A copy of the Sample whose map class declares West and East and sets up the slots.
        private string SampleWithSlots(string slots)
        {
            var folder = folders.TemporaryCopyOfSample();
            File.WriteAllText(
                Path.Combine(folder, "Map.cs"),
                "using Moonwell;\n\nnamespace Sample\n{\n    public sealed class SampleMap : Map\n    {\n"
                + Teams
                + "\n        public SampleMap()\n        {\n            Components.Add(new Hello());\n            "
                + slots
                + "\n        }\n    }\n}\n");
            return folder;
        }
    }
}
