using System.Text;
using War3Net.Build.Audio;
using War3Net.Build.Environment;
using War3Net.Build.Extensions;
using War3Net.Build.Import;
using War3Net.Build.Info;
using War3Net.Build.Script;
using War3Net.Build.Widget;

namespace Moonwell.BuildTime
{
    /// <summary>
    /// Reads a map project's files: the one way in for code that runs while the map builds. Each file is handed out as War3Net
    /// parsed it, read the first time it is asked for and kept; any file is also there as its bytes, by name. A file in
    /// <c>assets\</c> stands in for the map's file of that name, as it does in the built map.
    /// </summary>
    public sealed class MapReader
    {
        private readonly string mapFolder;

        private readonly string? assetsFolder;

        private readonly Dictionary<string, object> parsed = new Dictionary<string, object>(StringComparer.Ordinal);

        // Files a test stood in over the folders' (WithFile), by name.
        private readonly Dictionary<string, byte[]> added;

        private Cells? cells;

        private float[]? heights;

        /// <summary>Makes a reader of a World Editor map folder.</summary>
        /// <param name="mapFolder">The map's folder, <c>map</c> in a map project.</param>
        /// <param name="assetsFolder">The project's <c>assets</c> folder, whose files stand in for the map's, or null for none.</param>
        public MapReader(string mapFolder, string? assetsFolder = null)
        {
            this.mapFolder = mapFolder;
            this.assetsFolder = assetsFolder != null && Directory.Exists(assetsFolder) ? assetsFolder : null;
            added = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        }

        private MapReader(MapReader from, Dictionary<string, byte[]> added)
        {
            mapFolder = from.mapFolder;
            assetsFolder = from.assetsFolder;
            this.added = added;
        }

        /// <summary>The map's info, <c>war3map.w3i</c>: its name, players, size and playable area.</summary>
        /// <exception cref="BuildFailure">The map has no such file, or it cannot be read.</exception>
        public MapInfo Info => Needed(MapInfo.FileName, reader => reader.ReadMapInfo());

        /// <summary>The map's ground, <c>war3map.w3e</c>: each corner's height, cliff level, water and tile.</summary>
        /// <exception cref="BuildFailure">The map has no such file, or it cannot be read.</exception>
        public MapEnvironment Environment => Needed(MapEnvironment.FileName, reader => reader.ReadMapEnvironment());

        /// <summary>
        /// The map's pathing, <c>war3map.wpm</c>: a byte for each 32 by 32 cell, row by row from the bottom left, in which a set
        /// flag means "cannot". <see cref="Cells"/> reads it by world coordinates.
        /// </summary>
        /// <exception cref="BuildFailure">The map has no such file, or it cannot be read.</exception>
        public MapPathingMap PathingMap => Needed(MapPathingMap.FileName, reader => reader.ReadMapPathingMap());

        /// <summary>The map's shadows, <c>war3map.shd</c>; empty when the map has none.</summary>
        public MapShadowMap ShadowMap => Optional(MapShadowMap.FileName, reader => reader.ReadMapShadowMap(), () => new MapShadowMap());

        /// <summary>The minimap's icons, <c>war3map.mmp</c>; empty when the map has none.</summary>
        public MapPreviewIcons PreviewIcons => Optional(MapPreviewIcons.FileName, reader => reader.ReadMapPreviewIcons(), () => new MapPreviewIcons(default));

        /// <summary>The map's regions, <c>war3map.w3r</c>; empty when the map has none.</summary>
        public MapRegions Regions => Optional(MapRegions.FileName, reader => reader.ReadMapRegions(), () => new MapRegions(default));

        /// <summary>The map's cameras, <c>war3map.w3c</c>; empty when the map has none.</summary>
        public MapCameras Cameras => Optional(MapCameras.FileName, reader => reader.ReadMapCameras(), () => new MapCameras(default, false));

        /// <summary>The Sound Editor's sounds, <c>war3map.w3s</c>; empty when the map has none.</summary>
        public MapSounds Sounds => Optional(MapSounds.FileName, reader => reader.ReadMapSounds(), () => new MapSounds(default));

        /// <summary>The placed doodads and destructables, trees among them, <c>war3map.doo</c>; empty when the map has none.</summary>
        public MapDoodads Doodads => Optional(MapDoodads.FileName, reader => reader.ReadMapDoodads(), () => new MapDoodads(default, default, false));

        /// <summary>The placed units, buildings and items, and the start locations, <c>war3mapUnits.doo</c>; empty when the map has none.</summary>
        public MapUnits Units => Optional(MapUnits.FileName, reader => reader.ReadMapUnits(), () => new MapUnits(default, default, false));

        /// <summary>World Editor's list of imported files, <c>war3map.imp</c>; empty when the map has none.</summary>
        public ImportedFiles ImportedFiles => Optional(ImportedFiles.MapFileName, reader => reader.ReadImportedFiles(), () => new ImportedFiles(default));

        /// <summary>The map's texts by number, <c>war3map.wts</c>; empty when the map has none.</summary>
        public TriggerStrings TriggerStrings
        {
            get
            {
                if (!parsed.TryGetValue(TriggerStrings.MapFileName, out var strings))
                {
                    strings = new TriggerStrings();
                    if (Has(TriggerStrings.MapFileName))
                    {
                        strings = Read(TriggerStrings.MapFileName, () =>
                        {
                            using var reader = new StreamReader(new MemoryStream(Bytes(TriggerStrings.MapFileName)), Encoding.UTF8);
                            return reader.ReadTriggerStrings();
                        });
                    }

                    parsed[TriggerStrings.MapFileName] = strings;
                }

                return (TriggerStrings)strings;
            }
        }

        /// <summary>Every pathing cell of the map, to narrow to a region, filter and measure.</summary>
        /// <exception cref="BuildFailure">The map has no pathing or ground file, or one cannot be read.</exception>
        public Cells Cells => cells ??= Cells.All(new PathingGrid(PathingMap, Environment));

        /// <summary>
        /// The ground's height at a point, as <c>Terrain.Height</c> gives it in the game before anything changes it: the four
        /// corners of its 128 tile blended, each from <c>war3map.w3e</c> by the rule the build writes the game's terrain with
        /// (cliff level 2 is 0, ramps raised as the game raises them, water's surface where it is above the ground). A point
        /// outside the map reads its edge; on a bridge it reads the water or ground beneath, as <c>Terrain.Height</c> does.
        /// </summary>
        /// <param name="x">The point's x.</param>
        /// <param name="y">The point's y.</param>
        /// <exception cref="BuildFailure">The map has no ground file, or it cannot be read.</exception>
        public float HeightAt(float x, float y)
        {
            var environment = Environment;
            var corners = heights ??= Corners(environment);
            return GroundHeights.Blend(GroundHeights.Columns(environment), GroundHeights.Rows(environment), environment.Left, environment.Bottom, x, y, index => corners[index]);
        }

        /// <summary>The names of every file of the map, folders with <c>\</c>, in name order: those in <c>assets\</c> and those in <c>map.w3x\</c>, and any a test added.</summary>
        public IReadOnlyList<string> Files
        {
            get
            {
                var names = new SortedSet<string>(added.Keys, StringComparer.OrdinalIgnoreCase);
                foreach (var folder in new[] { assetsFolder, mapFolder })
                {
                    if (folder != null && Directory.Exists(folder))
                    {
                        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                        {
                            names.Add(Path.GetRelativePath(folder, file).Replace('/', '\\'));
                        }
                    }
                }

                return names.ToList();
            }
        }

        /// <summary>Whether the map has a file of a name, in <c>assets\</c> or in <c>map.w3x\</c>.</summary>
        /// <param name="name">The file's name, such as <c>war3map.shd</c> or <c>war3mapImported\Rules.txt</c>.</param>
        public bool Has(string name)
        {
            return added.ContainsKey(Normal(name)) || PathOf(name) != null;
        }

        /// <summary>A file's bytes as they are: the one in <c>assets\</c> when there is one, else the map's.</summary>
        /// <param name="name">The file's name, such as <c>war3map.shd</c> or <c>war3mapImported\Rules.txt</c>.</param>
        /// <exception cref="BuildFailure">The map has no such file, or it cannot be read.</exception>
        public byte[] Bytes(string name)
        {
            if (added.TryGetValue(Normal(name), out var bytes))
            {
                return bytes;
            }

            var path = PathOf(name) ?? throw new BuildFailure($"the map has no file {name}");
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                throw new BuildFailure($"cannot read {name}: {failure.Message}");
            }
        }

        /// <summary>The region of a name, as World Editor shows it.</summary>
        /// <param name="name">The region's name.</param>
        /// <exception cref="BuildFailure">The map has no region of that name; the message lists the ones it has.</exception>
        public Region Region(string name)
        {
            var found = Regions.Regions.FirstOrDefault(region => region.Name == name);
            if (found != null)
            {
                return found;
            }

            var names = Regions.Regions.Select(region => region.Name).OrderBy(one => one, StringComparer.Ordinal).ToList();
            throw new BuildFailure(names.Count == 0
                ? $"the map has no region named {name}: it has no regions"
                : $"the map has no region named {name}: it has {string.Join(", ", names)}");
        }

        /// <summary>
        /// A reader of the same map with one file stood in over the map's and the assets' file of its name, or added: for a test
        /// that runs a step on the map with a change of its own. This reader is left as it is.
        /// </summary>
        /// <param name="name">The file's name, such as <c>war3map.w3r</c> or <c>war3mapImported\Rules.txt</c>.</param>
        /// <param name="bytes">The file's bytes.</param>
        public MapReader WithFile(string name, byte[] bytes)
        {
            var files = new Dictionary<string, byte[]>(added, StringComparer.OrdinalIgnoreCase)
            {
                [Normal(name)] = bytes,
            };
            return new MapReader(this, files);
        }

        /// <summary>
        /// A reader of the same map with these regions in place of the map's, as its <c>war3map.w3r</c>: for a test that runs a
        /// step on regions of its own. This reader is left as it is.
        /// </summary>
        /// <param name="regions">The regions, such as <c>new Region { Name = "Harbor", Left = 0, Bottom = 0, Right = 512, Top = 512 }</c>.</param>
        public MapReader WithRegions(IEnumerable<Region> regions)
        {
            var file = new MapRegions(MapRegionsFormatVersion.v5);
            file.Regions.AddRange(regions);
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                writer.Write(file);
            }

            return WithFile(MapRegions.FileName, bytes.ToArray());
        }

        // A file's name with \ between folders, as the map's names are listed.
        private static string Normal(string name) => name.Replace('/', '\\');

        // Every corner's height as the game's grid keeps it, worked out once.
        private static float[] Corners(MapEnvironment environment)
        {
            var count = GroundHeights.Columns(environment) * GroundHeights.Rows(environment);
            if (environment.TerrainTiles.Count != count)
            {
                throw new BuildFailure($"war3map.w3e lists {environment.TerrainTiles.Count} corners, but a map of {environment.Width} by {environment.Height} tiles has {count}");
            }

            var corners = new float[count];
            for (var index = 0; index < count; index++)
            {
                corners[index] = GroundHeights.Kept(GroundHeights.Corner(environment, index, out _));
            }

            return corners;
        }

        private string? PathOf(string name)
        {
            foreach (var folder in new[] { assetsFolder, mapFolder })
            {
                if (folder != null && File.Exists(Path.Combine(folder, name)))
                {
                    return Path.Combine(folder, name);
                }
            }

            return null;
        }

        private T Needed<T>(string name, Func<BinaryReader, T> read)
            where T : class
        {
            if (!parsed.TryGetValue(name, out var file))
            {
                if (!Has(name))
                {
                    throw new BuildFailure($@"the map has no {name}: save the map in World Editor");
                }

                file = Binary(name, read);
                parsed[name] = file;
            }

            return (T)file;
        }

        private T Optional<T>(string name, Func<BinaryReader, T> read, Func<T> empty)
            where T : class
        {
            if (!parsed.TryGetValue(name, out var file))
            {
                file = Has(name) ? Binary(name, read) : empty();
                parsed[name] = file;
            }

            return (T)file;
        }

        private T Binary<T>(string name, Func<BinaryReader, T> read)
        {
            return Read(name, () =>
            {
                using var reader = new BinaryReader(new MemoryStream(Bytes(name)));
                return read(reader);
            });
        }

        // War3Net stops on a file it cannot read with an exception of whatever kind its reader met.
        private static T Read<T>(string name, Func<T> read)
        {
            try
            {
                return read();
            }
            catch (Exception failure) when (failure is not BuildFailure)
            {
                throw new BuildFailure($"cannot read {name}: {failure.Message}");
            }
        }
    }
}
