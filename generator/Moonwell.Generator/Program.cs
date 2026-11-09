using Moonwell.GameData;
using Moonwell.Generator;
using Moonwell.Generator.Objects;

const string Usage = """
    usage:
      Moonwell.Generator natives <repository root> [--install <game folder>]
      Moonwell.Generator natives <repository root> --scripts <folder with common.j, blizzard.j, common.ai> <game version>
      Moonwell.Generator objects <repository root> [--install <game folder>]
      Moonwell.Generator objects <repository root> --export <war3.w3mod folder> <game version>
      Moonwell.Generator tiles <repository root> [--install <game folder>]

    Without --install, the install is the folder MOONWELL_WAR3 names, else the usual folder. The game version is the
    install's own.
    """;

if (args.Length < 2 || (args[0] != "natives" && args[0] != "objects" && args[0] != "tiles"))
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var (command, root) = (args[0], args[1]);
var copied = command == "natives" ? "--scripts" : "--export";
if (args.Length == 5 && args[2] == copied)
{
    return command == "natives" ? Natives(ScriptsFolder(args[3]), root, args[4]) : Objects(GameTables.OpenExport(args[3]), root, args[4]);
}

if (args.Length != 2 && (args.Length != 4 || args[2] != "--install"))
{
    Console.Error.WriteLine(Usage);
    return 2;
}

string folder;
try
{
    folder = GameInstall.Require(args.Length == 4 ? args[3] : null);
}
catch (DirectoryNotFoundException missing)
{
    Console.Error.WriteLine(args.Length == 4 ? missing.Message : missing.Message + " Or pass --install <game folder>.");
    return 1;
}

using var files = GameFiles.Open(folder);
if (command == "natives")
{
    return Natives(InstallScripts(files), root, files.Build.Version);
}

if (command == "tiles")
{
    var terrain = GameTables.OpenInstall(files).Table("TerrainArt/Terrain.slk") ?? throw new FileNotFoundException("TerrainArt/Terrain.slk is not in the install");
    var target = Path.Combine(root, "src", "Moonwell.Terrain", "Generated", "Tile.g.cs");
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllText(target, TilesWriter.Write(terrain, files.Build.Version));
    Console.WriteLine($"{terrain.Count} tiles");
    return 0;
}

// The API is named from the English text whatever language the install has, so that every contributor generates the same one.
GameTables tables;
try
{
    tables = GameTables.OpenInstall(files, "enUS");
}
catch (FileNotFoundException missing)
{
    Console.Error.WriteLine(missing.Message + ". The objects are named from the English text: add English in the game's language settings.");
    return 1;
}

using (tables)
{
    return Objects(tables, root, files.Build.Version);
}

// The scripts by name, from a folder.
static Func<string, string> ScriptsFolder(string folder)
{
    return name => File.ReadAllText(Path.Combine(folder, name));
}

// The scripts by name, from the install's Scripts folder.
static Func<string, string> InstallScripts(GameFiles files)
{
    return name =>
    {
        var bytes = files.Read("Scripts/" + name) ?? throw new FileNotFoundException($"Scripts/{name} is not in the install", name);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        return text.StartsWith('\uFEFF') ? text.Substring(1) : text;
    };
}

static int Natives(Func<string, string> script, string root, string gameVersion)
{
    var scripts = Jass.Read(script("common.j"), script("blizzard.j"));
    LuaExtras.AddTo(scripts, script("common.ai"));

    var natives = Path.Combine(root, "src", "Moonwell.Natives");
    Directory.CreateDirectory(Path.Combine(natives, "Generated"));
    File.WriteAllText(Path.Combine(natives, "Generated", "Types.g.cs"), NativesWriter.WriteTypes(scripts, gameVersion));
    File.WriteAllText(Path.Combine(natives, "Generated", "Natives.g.cs"), NativesWriter.WriteNatives(scripts, gameVersion));
    File.WriteAllText(Path.Combine(natives, "Generated", "types.lua"), NativesWriter.WriteLuaTypes(scripts, gameVersion));

    Console.WriteLine($"{scripts.Types.Count} types, {scripts.Functions.Count} functions, {scripts.Globals.Count} globals");
    return 0;
}

static int Objects(GameTables tables, string root, string gameVersion)
{
    var runtime = RuntimeMembers.Read(MoonwellDll(root));
    var files = GeneratedObjects.Make(tables, runtime, gameVersion);

    var generated = Path.Combine(root, "src", "Moonwell.Objects", "Generated");
    if (Directory.Exists(generated))
    {
        Directory.Delete(generated, true);
    }

    long source = 0;
    long values = 0;
    foreach (var (path, text) in files)
    {
        var target = Path.Combine(generated, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, text);
        var size = new FileInfo(target).Length;
        if (path.EndsWith(".json", StringComparison.Ordinal))
        {
            values += size;
        }
        else
        {
            source += size;
        }
    }

    Console.WriteLine($"{files.Count} files: {source:N0} bytes of C#, {values:N0} bytes of values");
    return 0;
}

static string MoonwellDll(string root)
{
    string? newest = null;
    foreach (var configuration in new[] { "Debug", "Release" })
    {
        var path = Path.Combine(root, "src", "Moonwell", "bin", configuration, "net10.0", "Moonwell.dll");
        if (File.Exists(path) && (newest == null || File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(newest)))
        {
            newest = path;
        }
    }

    return newest ?? Path.Combine(root, "src", "Moonwell", "bin", "Debug", "net10.0", "Moonwell.dll");
}
