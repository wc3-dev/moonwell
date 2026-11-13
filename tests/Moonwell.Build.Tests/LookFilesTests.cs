using Xunit;
using static Moonwell.Build.LookFiles;

namespace Moonwell.Build.Tests
{
    /// <summary>The looks tables beside textures (decisions 320 to 324): their typed names and the copies' pixels.</summary>
    public sealed class LookFilesTests
    {
        // 64 by 32, a quarter each: red top left, green top right, blue bottom left, yellow bottom right.
        private static readonly Picture Quad = Make(64, 32, (x, y) => (x < 32, y < 16) switch
        {
            (true, true) => (255, 0, 0, 255),
            (false, true) => (0, 255, 0, 255),
            (true, false) => (0, 0, 255, 255),
            _ => (255, 255, 0, 255),
        });

        [Fact]
        public void a_table_gives_a_class_of_paths_and_round_finds_a_listed_icon()
        {
            var frame = Parse(@"war3mapImported\UI-TargetingFrame.looks.csv", "Name,Left,Top,Right,Bottom,Color,Round,Bar\nPlayer Frame,0.855,0.0625,0.102,0.664,,,\nGreen,,,,,00ff00,,yes\n");
            var icon = Parse(@"ReplaceableTextures\CommandButtons\BTNFootman.looks.csv", "Name,Round\nRound,yes\n");

            var written = Names(new[] { frame, icon });

            Assert.Contains("        public static class UITargetingFrame\n", written);
            Assert.Contains("            public const string PlayerFrame = \"war3mapImported\\\\UI-TargetingFrame.PlayerFrame.blp\";\n", written);
            Assert.Contains("            public const string Green = \"war3mapImported\\\\UI-TargetingFrame.Green.Bar.blp\";\n", written);
            Assert.Contains("            if (key == \"replaceabletextures\\\\commandbuttons\\\\btnfootman.blp\")\n            {\n                return BTNFootman.Round;\n", written);
            Assert.Equal(new Region(0.855, 0.0625, 0.102, 0.664), frame.Rows[0].Region);
            Assert.Equal(new Rgb(0, 255, 0), frame.Rows[1].Color);
            Assert.Equal(@"war3mapImported\UI-TargetingFrame.Green.Bar12.blp", StepPath(frame, frame.Rows[1], 12));
        }

        [Fact]
        public void a_piece_is_cut_at_a_power_of_two_and_mirrored_when_left_is_greater()
        {
            var top = Look(Quad, RowOf(new Region(0, 0, 1, 0.5)));
            var mirrored = Look(Quad, RowOf(new Region(1, 0, 0, 0.5)));
            var piece = Look(Quad, RowOf(new Region(0, 0, 0.75, 0.5)));

            Assert.Equal((64, 16), (top.Width, top.Height));
            Assert.Equal((255, 0, 0), Rgb(top, 0, 0));
            Assert.Equal((0, 255, 0), Rgb(top, 63, 15));
            Assert.Equal((0, 255, 0), Rgb(mirrored, 0, 0));
            Assert.Equal((255, 0, 0), Rgb(mirrored, 63, 15));
            Assert.Equal((64, 16), (piece.Width, piece.Height));
        }

        [Fact]
        public void a_colour_multiplies_and_round_clears_the_corners()
        {
            var white = Make(16, 16, (x, y) => (255, 255, 255, 255));

            var green = Look(white, RowOf(null, new Rgb(0, 255, 0)));
            var round = Look(white, RowOf(null, null, round: true));

            Assert.Equal((0, 255, 0), Rgb(green, 5, 5));
            Assert.Equal(0, round.At(1, 1, 3));
            Assert.Equal(255, round.At(8, 8, 3));
            Assert.InRange(round.At(2, 2, 3), 100, 220);
        }

        [Fact]
        public void a_bar_step_is_the_looks_left_part()
        {
            var top = Look(Quad, RowOf(new Region(0, 0, 1, 0.5)));

            var half = Cut(top, 32);
            var quarter = Cut(top, 16);

            Assert.Equal((32, 16), (half.Width, half.Height));
            Assert.Equal((255, 0, 0), Rgb(half, 31, 8));
            Assert.Equal((16, 16), (quarter.Width, quarter.Height));
        }

        [Theory]
        [InlineData("Name,Size\nA,1\n", @"assets\a.looks.csv:1: no column Size; a looks table has Name, Left, Top, Right, Bottom, Color, Round, Bar")]
        [InlineData("Name,Left,Top\nA,0,0\n", @"assets\a.looks.csv:2: a piece needs all of Left, Top, Right and Bottom")]
        [InlineData("Name,Left,Top,Right,Bottom\nA,0,0,1.5,1\n", @"assets\a.looks.csv:2: 1.5 is not a number from 0 to 1; a piece's sides are shares of the texture, as WoW's TexCoords")]
        [InlineData("Name,Color\nA,green\n", @"assets\a.looks.csv:2: the colour green is not six hex digits, red, green and blue, as 00ff00")]
        [InlineData("Name,Round\nA,maybe\n", @"assets\a.looks.csv:2: Round is yes, no or empty, not maybe")]
        [InlineData("Name\nA\na\n", @"assets\a.looks.csv:3: two rows make the name A")]
        public void a_wrong_table_is_refused_naming_its_line(string text, string message)
        {
            var error = Assert.Throws<BuildException>(() => Parse("a.looks.csv", text));

            Assert.Equal(message, error.Message);
        }

        private static LookFiles.Row RowOf(Region? region, Rgb? color = null, bool round = false)
        {
            return new LookFiles.Row("A", "A", 2, region, color, round, false);
        }

        private static (int, int, int) Rgb(Picture picture, int x, int y)
        {
            return (picture.At(x, y, 2), picture.At(x, y, 1), picture.At(x, y, 0));
        }

        private static Picture Make(int width, int height, Func<int, int, (int R, int G, int B, int A)> colour)
        {
            var pixels = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var (r, g, b, a) = colour(x, y);
                    var i = ((y * width) + x) * 4;
                    (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = ((byte)b, (byte)g, (byte)r, (byte)a);
                }
            }

            return new Picture(width, height, pixels);
        }
    }
}

namespace Moonwell.Build.Tests
{
    /// <summary>A map built with a looks table: its code names the copies and asks for a round one.</summary>
    [Collection("Sample")]
    public sealed class LookFilesBuildTests : IDisposable
    {
        private readonly TestFolders folders;

        public LookFilesBuildTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_maps_code_names_its_copies_and_finds_a_round_one()
        {
            var copy = folders.TemporaryCopyOfSample();
            var assets = Path.Combine(copy, "assets", "war3mapImported");
            Directory.CreateDirectory(assets);
            File.Copy(Path.Combine(TestFolders.Fixture("Images"), "red-64x32.png"), Path.Combine(assets, "Bar.png"));
            File.WriteAllText(Path.Combine(assets, "Bar.looks.csv"), "Name,Left,Top,Right,Bottom,Round\nHalf,0,0,0.5,1,\nRing,,,,,yes\n");
            File.WriteAllText(Path.Combine(copy, "Hello.cs"), """
                namespace Sample
                {
                    public sealed class Hello : Moonwell.GameComponent
                    {
                        public override void OnGameStart()
                        {
                            Moonwell.Natives.DisplayTextToPlayer(Moonwell.Natives.Player(0), 0f, 0f, Moonwell.Looks.Bar.Half + Moonwell.Looks.Round("x"));
                        }
                    }
                }
                """);

            MapBuild.Run(copy, new BuildOptions(), TextWriter.Null);

            var staged = Path.Combine(copy, "build", "Sample");
            Assert.True(File.Exists(Path.Combine(staged, @"war3mapImported\Bar.Ring.blp")));
            var lua = File.ReadAllText(Path.Combine(staged, "war3map.lua"));
            Assert.Contains(@"war3mapImported\\Bar.Half.blp", lua);
            Assert.Contains(@"war3mapimported\\bar.blp", lua);
        }
    }
}
