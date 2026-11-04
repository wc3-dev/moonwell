using System.Diagnostics;
using System.Text.Json;

namespace Moonwell.Build.Tests
{
    /// <summary>Moonwell, Moonwell.Natives, Moonwell.Objects, Moonwell.Damage, Moonwell.Maths, Moonwell.Interactions, Moonwell.Terrain, Moonwell.Physics, Moonwell.Areas, Moonwell.Input, Moonwell.Frames, Moonwell.Json and Moonwell.Templates packed with a version of their own into a feed in the temporary folder; removes everything it made, including what a restore left in the NuGet folder.</summary>
    public sealed class PackedMoonwell : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "moonwell-feed-" + Guid.NewGuid().ToString("N"));

        private readonly Lazy<string> templateMap;

        /// <summary>
        /// Packs the twelve runtime projects and the template in one dotnet pack of a solution filter naming them, so MSBuild starts
        /// once and evaluates each project once, where a pack of each would start it thirteen times.
        /// </summary>
        public PackedMoonwell()
        {
            Version = "0.1.0-test." + DateTime.UtcNow.Ticks;
            Feed = Path.Combine(root, "feed");
            Directory.CreateDirectory(Feed);

            var repository = Path.GetFullPath(Path.Combine(TestFolders.Fixture("."), "..", "..", ".."));
            var projects = new[] { "Moonwell.Natives", "Moonwell", "Moonwell.Objects", "Moonwell.Damage", "Moonwell.Maths", "Moonwell.Interactions", "Moonwell.Terrain", "Moonwell.Physics", "Moonwell.Areas", "Moonwell.Input", "Moonwell.Frames", "Moonwell.Json" }
                .Select(name => Path.Combine("src", name, name + ".csproj"))
                .Append(Path.Combine("template", "Moonwell.Templates.csproj"));
            var filter = Path.Combine(root, "packed.slnf");
            File.WriteAllText(filter, JsonSerializer.Serialize(new { solution = new { path = Path.Combine(repository, "Moonwell.slnx"), projects } }));
            Dotnet(repository, "pack", filter, "-c", "Release", "-p:PackageOutputPath=" + Feed, "-p:Version=" + Version, "--disable-build-servers");
            templateMap = new Lazy<string>(MakeTemplateMap);
        }

        /// <summary>The version all thirteen packages were packed with, <c>0.1.0-test.</c> and a number nothing else uses.</summary>
        public string Version { get; }

        /// <summary>The folder holding the thirteen <c>.nupkg</c> files.</summary>
        public string Feed { get; }

        /// <summary>The path of the Moonwell package.</summary>
        public string MoonwellPackage => Path.Combine(Feed, "Moonwell." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Objects package.</summary>
        public string ObjectsPackage => Path.Combine(Feed, "Moonwell.Objects." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Damage package.</summary>
        public string DamagePackage => Path.Combine(Feed, "Moonwell.Damage." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Maths package.</summary>
        public string MathsPackage => Path.Combine(Feed, "Moonwell.Maths." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Interactions package.</summary>
        public string InteractionsPackage => Path.Combine(Feed, "Moonwell.Interactions." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Terrain package.</summary>
        public string TerrainPackage => Path.Combine(Feed, "Moonwell.Terrain." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Physics package.</summary>
        public string PhysicsPackage => Path.Combine(Feed, "Moonwell.Physics." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Areas package.</summary>
        public string AreasPackage => Path.Combine(Feed, "Moonwell.Areas." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Input package.</summary>
        public string InputPackage => Path.Combine(Feed, "Moonwell.Input." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Frames package.</summary>
        public string FramesPackage => Path.Combine(Feed, "Moonwell.Frames." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Json package.</summary>
        public string JsonPackage => Path.Combine(Feed, "Moonwell.Json." + Version + ".nupkg");

        /// <summary>The path of the Moonwell.Templates package.</summary>
        public string TemplatesPackage => Path.Combine(Feed, "Moonwell.Templates." + Version + ".nupkg");

        /// <summary>
        /// A map made with dotnet new from the packed template and named Keep, in its own folder under a parent folder of the test's.
        /// The template is installed and the map made once; each call copies it, as the template makes the same files each time.
        /// </summary>
        public string TemplateMap(string parent)
        {
            var map = Path.Combine(parent, "Keep");
            TestFolders.CopyFolder(templateMap.Value, map);
            return map;
        }

        /// <summary>Deletes the feed and the packages the restore copied into the NuGet folder (see TestFolders.Delete for a folder still in use).</summary>
        public void Dispose()
        {
            var packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            foreach (var id in new[] { "moonwell", "moonwell.natives", "moonwell.objects", "moonwell.damage", "moonwell.maths", "moonwell.interactions", "moonwell.terrain", "moonwell.physics", "moonwell.areas", "moonwell.input", "moonwell.frames", "moonwell.json" })
            {
                TestFolders.Delete(Path.Combine(packages, id, Version.ToLowerInvariant()));

                var idFolder = Path.Combine(packages, id);
                if (Directory.Exists(idFolder) && !Directory.EnumerateFileSystemEntries(idFolder).Any())
                {
                    TestFolders.Delete(idFolder, false);
                }
            }

            TestFolders.Delete(root);
        }

        private string MakeTemplateMap()
        {
            var hive = Path.Combine(root, "hive");
            var made = Path.Combine(root, "template");
            Directory.CreateDirectory(made);
            Dotnet(made, "new", "install", TemplatesPackage, "--debug:custom-hive", hive);
            Dotnet(made, "new", "moonwell", "-n", "Keep", "--debug:custom-hive", hive);
            return Path.Combine(made, "Keep");
        }

        /// <summary>Runs dotnet in a folder, and fails with its output when it fails.</summary>
        public static void Dotnet(string folder, params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed:\n{output.Result}\n{error.Result}");
            }
        }
    }
}
