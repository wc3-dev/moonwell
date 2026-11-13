using System.Security.Cryptography;
using War3Net.Build.Extensions;
using War3Net.Build.Info;
using War3Net.IO.Mpq;
using Xunit;

namespace Moonwell.Build.Tests
{
    /// <summary>
    /// A copy of the Sample compiled as a map without objects, for the staging tests. The Sample declares objects, and the Lua
    /// compile of a map without objects reads the stubs of Moonwell.Objects that only a full build writes into the build
    /// folder, so the copy is built in full first. The repository's own Fixtures/Sample is not built.
    /// </summary>
    public sealed class StagedSample : IDisposable
    {
        private readonly TestFolders folders = new TestFolders();

        public StagedSample()
        {
            var folder = folders.TemporaryCopyOfSample();
            MapBuild.Run(folder, new BuildOptions(), TextWriter.Null);
            Project = MapProject.Find(folder);
            var assembly = DotnetBuild.Run(Project, false, TextWriter.Null);
            var settings = MapSettingsReader.Read(assembly, Project);
            Map = SourceMap.Open(Project, settings, TextWriter.Null);
            EntryWriter.Write(Project, settings);
            LuaCompiler.Compile(Project, Map, assembly, false);
        }

        public MapProject Project { get; }

        public War3Net.Build.Map Map { get; }

        public void Dispose()
        {
            folders.Dispose();
        }
    }

    [Collection("Sample")]
    public sealed class StageTests : IClassFixture<StagedSample>, IDisposable
    {
        private static readonly string[] DroppedFiles = { "war3map.j" };

        private readonly (MapProject Project, War3Net.Build.Map Map) compiledSample;

        private readonly TestFolders folders;

        public StageTests(StagedSample staged, ITestOutputHelper output)
        {
            compiledSample = (staged.Project, staged.Map);
            folders = new TestFolders(output);
        }

        [Fact]
        public void source_map_is_untouched()
        {
            var (project, map) = compiledSample;
            var before = Hashes(project.MapFolder).Concat(Hashes(project.AssetsFolder)).ToList();
            Assert.NotEmpty(before);

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);
            Archive.Pack(project.StagedFolder, project.ArchivePath);

            var after = Hashes(project.MapFolder).Concat(Hashes(project.AssetsFolder)).ToList();
            Assert.Equal(before, after);
        }

        [Fact]
        public void staged_files()
        {
            var (project, map) = compiledSample;

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            var staged = RelativeFiles(project.StagedFolder);
            foreach (var dropped in DroppedFiles)
            {
                Assert.DoesNotContain(dropped, staged);
            }

            Assert.Equal(map.Script, File.ReadAllText(Path.Combine(project.StagedFolder, "war3map.lua")));
            foreach (var trigger in new[] { "war3map.wtg", "war3map.wct" })
            {
                Assert.Contains(trigger, staged);
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(project.MapFolder, trigger)),
                    File.ReadAllBytes(Path.Combine(project.StagedFolder, trigger)));
            }

            foreach (var file in RelativeFiles(project.MapFolder))
            {
                if (DroppedFiles.Contains(file) || file == "war3map.lua" || file == "war3map.w3i")
                {
                    continue;
                }

                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(project.MapFolder, file)),
                    File.ReadAllBytes(Path.Combine(project.StagedFolder, file)));
            }

            Assert.Contains(@"Moonwell\hello.txt", staged);
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(project.AssetsFolder, "Moonwell", "hello.txt")),
                File.ReadAllBytes(Path.Combine(project.StagedFolder, "Moonwell", "hello.txt")));
        }

        [Fact]
        public void info_has_settings()
        {
            var (project, map) = compiledSample;

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            using var reader = new BinaryReader(File.OpenRead(Path.Combine(project.StagedFolder, "war3map.w3i")));
            var info = reader.ReadMapInfo();
            Assert.Equal("Sample map", info.MapName);
            Assert.Equal("Moonwell", info.MapAuthor);
            Assert.Equal("Built by the tests.", info.MapDescription);
            Assert.Equal("Any", info.RecommendedPlayers);
            Assert.Equal(ScriptLanguage.Lua, info.ScriptLanguage);
        }

        [Fact]
        public void info_round_trip()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var settings = new MapSettings("Sample.SampleMap", string.Empty, string.Empty, string.Empty, string.Empty);
            var map = SourceMap.Open(project, settings, TextWriter.Null);
            map.Script = "-- script";

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            Assert.Equal(
                File.ReadAllBytes(Path.Combine(project.MapFolder, "war3map.w3i")),
                File.ReadAllBytes(Path.Combine(project.StagedFolder, "war3map.w3i")));
        }

        [Fact]
        public void asset_wins_over_map_file()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            File.WriteAllText(Path.Combine(project.AssetsFolder, "war3map.wts"), "STRING 1 { replaced }");
            var log = new StringWriter();

            Stage.WriteWithoutObjects(project, map, log);

            Assert.Equal("STRING 1 { replaced }", File.ReadAllText(Path.Combine(project.StagedFolder, "war3map.wts")));
            Assert.Contains(@"note: assets\war3map.wts replaces the map's file", log.ToString());
        }

        [Fact]
        public void a_png_in_the_assets_is_packed_as_a_blp_at_its_path()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            CopyImage("red-64x32.png", project, @"Icons\BTNRed.png");
            CopyImage("glass-32.png", project, @"Icons\Glass.png");

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            Assert.False(File.Exists(Path.Combine(project.StagedFolder, @"Icons\BTNRed.png")));
            var red = Path.Combine(project.StagedFolder, @"Icons\BTNRed.blp");
            using (var blp = new War3Net.Drawing.Blp.BlpFile(File.OpenRead(red)))
            {
                // Down to 2x1: War3Net's encoder stops when the shorter side is 1.
                Assert.Equal(6, blp.MipMapCount);
                var pixels = blp.GetPixels(0, out var width, out var height);
                Assert.Equal((64, 32), (width, height));

                // BGRA, within what JPEG loses.
                Assert.InRange(pixels[0], 0, 8);
                Assert.InRange(pixels[1], 0, 8);
                Assert.InRange(pixels[2], 247, 255);
            }

            // An opaque image keeps the game's alpha depth at 0, a see-through one gets 8.
            Assert.Equal(0u, BitConverter.ToUInt32(File.ReadAllBytes(red), 8));
            var glass = File.ReadAllBytes(Path.Combine(project.StagedFolder, @"Icons\Glass.blp"));
            Assert.Equal(8u, BitConverter.ToUInt32(glass, 8));
            using (var blp = new War3Net.Drawing.Blp.BlpFile(new MemoryStream(glass)))
            {
                Assert.InRange(blp.GetPixels(0, out _, out _)[3], 120, 136);
            }
        }

        [Fact]
        public void a_png_whose_side_is_not_a_power_of_two_is_refused_naming_it()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            CopyImage("wide-100x64.png", project, "Wide.png");

            var error = Assert.Throws<BuildException>(() => Stage.WriteWithoutObjects(project, map, TextWriter.Null));

            Assert.Equal(@"assets\Wide.png is 100x64; each side of a texture must be a power of two (32, 64, 128, 256...), so resize it", error.Message);
        }

        [Fact]
        public void a_png_the_build_cannot_read_is_refused_naming_it()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            File.WriteAllText(Path.Combine(project.AssetsFolder, "Broken.png"), "not an image");

            var error = Assert.Throws<BuildException>(() => Stage.WriteWithoutObjects(project, map, TextWriter.Null));

            Assert.StartsWith(@"assets\Broken.png is not a PNG the build can read (", error.Message);
        }

        [Fact]
        public void a_png_and_a_blp_of_one_name_are_refused()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            CopyImage("red-64x32.png", project, "Fire.png");
            File.WriteAllText(Path.Combine(project.AssetsFolder, "fire.blp"), "x");

            var error = Assert.Throws<BuildException>(() => Stage.WriteWithoutObjects(project, map, TextWriter.Null));

            Assert.Equal(@"assets\Fire.png and assets\fire.blp are both fire.blp in the map; keep one", error.Message);
        }

        [Fact]
        public void an_icon_gets_its_grey_copy_where_the_game_reads_it()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            CopyImage("red-64x32.png", project, @"Icons\BTNRed.png");
            CopyImage("red-64x32.png", project, @"Icons\PASBTNRed.png");
            CopyImage("glass-32.png", project, @"Icons\Glass.png");
            Images.WriteBlp(Path.Combine(TestFolders.Fixture("Images"), "red-64x32.png"), Path.Combine(project.AssetsFolder, "BTNFromBlp.blp"), "red");

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            var greys = Path.Combine(project.StagedFolder, @"ReplaceableTextures\CommandButtonsDisabled");
            Assert.Equal(
                new[] { "DISBTNFromBlp.blp", "DISBTNRed.blp", "DISPASBTNRed.blp" },
                Directory.GetFiles(greys).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
            using var blp = new War3Net.Drawing.Blp.BlpFile(File.OpenRead(Path.Combine(greys, "DISBTNRed.blp")));
            var pixels = blp.GetPixels(0, out var width, out var height);
            Assert.Equal((64, 32), (width, height));

            // Red's brightness, 76, at the game's HD grey: 0.459 x 76 + 8.5 is 44, within what JPEG loses.
            for (var channel = 0; channel < 3; channel++)
            {
                Assert.InRange(pixels[channel], 38, 50);
            }
        }

        [Fact]
        public void a_grey_copy_of_the_assets_is_kept_and_one_name_twice_is_warned_of()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            CopyImage("red-64x32.png", project, "BTNOwn.png");
            var own = Path.Combine(project.AssetsFolder, @"ReplaceableTextures\CommandButtonsDisabled\DISBTNOwn.blp");
            Directory.CreateDirectory(Path.GetDirectoryName(own)!);
            File.WriteAllText(own, "own");
            CopyImage("red-64x32.png", project, @"Fire\BTNFire.png");
            CopyImage("glass-32.png", project, @"Ice\BTNFire.png");
            File.WriteAllText(Path.Combine(project.AssetsFolder, "BTNBroken.blp"), "not an image");
            var log = new StringWriter();

            Stage.WriteWithoutObjects(project, map, log);

            Assert.Equal("own", File.ReadAllText(Path.Combine(project.StagedFolder, @"ReplaceableTextures\CommandButtonsDisabled\DISBTNOwn.blp")));
            Assert.Contains(@"warning: assets\Fire\BTNFire.png and assets\Ice\BTNFire.png share the grey copy ReplaceableTextures\CommandButtonsDisabled\DISBTNFire.blp, as the game finds it by the file name alone; it is made from assets\Fire\BTNFire.png", log.ToString());
            Assert.Contains(@"warning: assets\BTNBroken.blp is not a BLP the build can read to make its grey copy (", log.ToString());
            Assert.Contains(@"put one at assets\ReplaceableTextures\CommandButtonsDisabled\DISBTNBroken.blp yourself", log.ToString());
        }

        [Fact]
        public void the_import_list_names_each_asset_and_made_copy_but_no_file_of_the_map()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            Directory.Delete(project.AssetsFolder, true);
            CopyImage("red-64x32.png", project, @"Icons\BTNRed.png");
            CopyImage("glass-32.png", project, @"war3mapImported\Glass.png");
            File.WriteAllText(Path.Combine(project.AssetsFolder, "war3map.wts"), "STRING 1 { replaced }");

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            Assert.Equal(
                new[] { @"Icons\BTNRed.blp", @"ReplaceableTextures\CommandButtonsDisabled\DISBTNRed.blp", @"war3mapImported\Glass.blp" },
                ImportList.Paths(Path.Combine(project.StagedFolder, "war3map.imp")));

            // Version 1, then flag 13 and the whole path: a file at its own path, which World Editor keeps (game facts, 10-31).
            var bytes = File.ReadAllBytes(Path.Combine(project.StagedFolder, "war3map.imp"));
            Assert.Equal((1, 3, 13), (BitConverter.ToInt32(bytes, 0), BitConverter.ToInt32(bytes, 4), bytes[8]));
        }

        [Fact]
        public void the_import_list_keeps_the_source_maps_entries_and_adds_only_what_they_lack()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            Directory.Delete(project.AssetsFolder, true);
            CopyImage("glass-32.png", project, @"war3mapImported\Old.png");
            CopyImage("glass-32.png", project, @"war3mapImported\Glass.png");
            CopyImage("glass-32.png", project, @"war3mapImported\New.png");

            // As World Editor writes them: 24 with the folder left out, 29 at a path of its own, 13 with the folder in the path.
            var before = new MemoryStream();
            using (var writer = new BinaryWriter(before, System.Text.Encoding.UTF8, true))
            {
                writer.Write(1);
                writer.Write(3);
                foreach (var (flags, path) in new[] { (24, "old.blp"), (29, @"Sounds\Gone.wav"), (13, @"war3mapImported\Glass.blp") })
                {
                    writer.Write((byte)flags);
                    writer.Write(System.Text.Encoding.UTF8.GetBytes(path));
                    writer.Write((byte)0);
                }
            }

            File.WriteAllBytes(Path.Combine(project.MapFolder, "war3map.imp"), before.ToArray());

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            var after = File.ReadAllBytes(Path.Combine(project.StagedFolder, "war3map.imp"));
            Assert.Equal(4, BitConverter.ToInt32(after, 4));
            Assert.Equal(before.ToArray()[8..], after[8..(int)before.Length]);
            Assert.Equal(
                new[] { @"war3mapImported\old.blp", @"Sounds\Gone.wav", @"war3mapImported\Glass.blp", @"war3mapImported\New.blp" },
                ImportList.Paths(Path.Combine(project.StagedFolder, "war3map.imp")));
        }

        [Fact]
        public void a_map_with_no_assets_gets_no_import_list()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            Directory.Delete(project.AssetsFolder, true);

            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            Assert.False(File.Exists(Path.Combine(project.StagedFolder, "war3map.imp")));
        }

        [Theory]
        [InlineData("assets", @"Ljud\Hälsning.txt", "ä")]
        [InlineData("assets", "Pris€.txt", "€")]
        [InlineData("map", @"war3mapImported\Öga.mdx", "Ö")]
        public void a_file_named_past_ascii_is_refused_naming_it(string folder, string file, string letter)
        {
            // War3Net hashes a name's UTF-16 letters, the game its bytes; from U+0200 War3Net cannot pack the name at all.
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            var path = Path.Combine(folder == "map" ? project.MapFolder : project.AssetsFolder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x");

            var error = Assert.Throws<BuildException>(() => Stage.WriteWithoutObjects(project, map, TextWriter.Null));

            Assert.Equal($@"{folder}\{file} has ""{letter}"" in its name; a file in the map must be named with ASCII characters only, so rename it", error.Message);
        }

        [Fact]
        public void object_files_are_written_by_object_files()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            var fixture = TestFolders.Fixture("WorldEditorObjects");
            foreach (var name in new[] { "war3map.w3u", "war3mapSkin.w3u" })
            {
                File.Copy(Path.Combine(fixture, name), Path.Combine(project.MapFolder, name));
            }

            // A map that does not use Moonwell.Objects has its object files come through unchanged, with no word about them.
            var log = new StringWriter();
            Stage.WriteWithoutObjects(project, map, log);

            foreach (var name in new[] { "war3map.w3u", "war3mapSkin.w3u" })
            {
                Assert.Equal(File.ReadAllBytes(Path.Combine(fixture, name)), File.ReadAllBytes(Path.Combine(project.StagedFolder, name)));
            }

            Assert.Equal(string.Empty, log.ToString());

            // A declared object is written beside them.
            var declared = new DeclaredObject(
                "Objects.Peasant",
                "unit",
                "hpea",
                false,
                null,
                null,
                new SortedDictionary<string, object?>(),
                Array.Empty<string>());
            Stage.Write(
                project,
                map,
                new[] { declared },
                new Dictionary<string, string> { ["Objects.Peasant"] = "h001" },
                new Dictionary<string, string>(),
                StandardData.Empty,
                TextWriter.Null);

            Assert.NotEqual(File.ReadAllBytes(Path.Combine(fixture, "war3map.w3u")), File.ReadAllBytes(Path.Combine(project.StagedFolder, "war3map.w3u")));
        }

        [Fact]
        public void stale_staged_files_are_gone()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            Stage.WriteWithoutObjects(project, map, TextWriter.Null);
            Archive.Pack(project.StagedFolder, project.ArchivePath);
            Assert.True(File.Exists(Path.Combine(project.StagedFolder, "Moonwell", "hello.txt")));

            File.Delete(Path.Combine(project.AssetsFolder, "Moonwell", "hello.txt"));
            Stage.WriteWithoutObjects(project, map, TextWriter.Null);
            Archive.Pack(project.StagedFolder, project.ArchivePath);

            Assert.False(File.Exists(Path.Combine(project.StagedFolder, "Moonwell", "hello.txt")));
            using var archive = MpqArchive.Open(project.ArchivePath, true);
            Assert.False(archive.FileExists(@"Moonwell\hello.txt"));
            Assert.True(archive.FileExists("war3map.lua"));
        }

        [Fact]
        public void archive_matches_stage()
        {
            var project = MapProject.Find(folders.TemporaryCopyOfSample());
            var map = OpenWithPlaceholderScript(project);
            Stage.WriteWithoutObjects(project, map, TextWriter.Null);

            Archive.Pack(project.StagedFolder, project.ArchivePath);

            var staged = RelativeFiles(project.StagedFolder);
            using (var archive = MpqArchive.Open(project.ArchivePath, true))
            {
                foreach (var file in staged)
                {
                    Assert.True(archive.FileExists(file), file);
                    using var stream = archive.OpenFile(file);
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    Assert.Equal(File.ReadAllBytes(Path.Combine(project.StagedFolder, file)), copy.ToArray());
                }

                Assert.True(archive.FileExists("(listfile)"));
                using var names = archive.OpenFile("(listfile)");
                using var reader = new StreamReader(names);
                var listed = reader.ReadToEnd()
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                Assert.Equal(staged.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList(), listed);
                Assert.Equal(staged.Count + 1, archive.GetMpqFiles().Count());
            }

            // Packing leaves no staged file open.
            Directory.Delete(project.StagedFolder, true);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        private static War3Net.Build.Map OpenWithPlaceholderScript(MapProject project)
        {
            var settings = new MapSettings("Sample.SampleMap", "Sample map", "Moonwell", "Built by the tests.", "Any");
            var map = SourceMap.Open(project, settings, TextWriter.Null);
            map.Script = "-- script";
            return map;
        }

        private static List<string> RelativeFiles(string folder)
        {
            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(folder, file))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();
        }

        private static void CopyImage(string fixture, MapProject project, string relative)
        {
            var target = Path.Combine(project.AssetsFolder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(TestFolders.Fixture("Images"), fixture), target);
        }

        private static List<string> Hashes(string folder)
        {
            return RelativeFiles(folder)
                .Select(file => file + " " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, file)))))
                .ToList();
        }
    }
}
