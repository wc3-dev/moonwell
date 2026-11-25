using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Moonwell.Build.Tests
{
    /// <summary>Whether a map asks a unit type's size or movement by its id (decision 396), read from its assembly, the libraries of its own and the Moonwell packages it uses (J180).</summary>
    public sealed class PathingTypesUseTests : IDisposable
    {
        // A stand-in for the pathing package: only the members the build looks for.
        private const string PathingSource = "namespace Moonwell { public static class Paths { public static float CollisionOf(int id) { return 0f; } public static void Install(int[] ids) { } } }";

        private readonly TestFolders folders;

        public PathingTypesUseTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Fact]
        public void a_map_that_calls_one_itself_asks_types()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            var pathing = Compile(folder, "Moonwell.Pathing", PathingSource);
            var map = Compile(folder, "MapCode", "class A { float F() { return Moonwell.Paths.CollisionOf(1); } }", pathing);

            Assert.True(PathingTypes.TypesUsedBy(map));
        }

        [Fact]
        public void a_moonwell_package_the_map_uses_that_calls_one_asks_types()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            var pathing = Compile(folder, "Moonwell.Pathing", PathingSource);
            var package = Compile(folder, "Moonwell.Ferries", "namespace Moonwell { public static class Ferries { public static float Size(int id) { return Paths.CollisionOf(id); } } }", pathing);
            var map = Compile(folder, "MapCode", "class A { float F() { return Moonwell.Ferries.Size(1); } }", pathing, package);

            Assert.True(PathingTypes.TypesUsedBy(map));
        }

        [Fact]
        public void a_map_and_packages_that_only_install_the_package_do_not_ask_types()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            var pathing = Compile(folder, "Moonwell.Pathing", PathingSource);
            var package = Compile(folder, "Moonwell.Ferries", "namespace Moonwell { public static class Ferries { public static void Start() { Paths.Install(new int[0]); } } }", pathing);
            var map = Compile(folder, "MapCode", "class A { void F() { Moonwell.Ferries.Start(); } }", pathing, package);

            Assert.False(PathingTypes.TypesUsedBy(map));
        }

        // Compiles an assembly of one source into the folder, referencing the runtime and the given assemblies.
        private static string Compile(string folder, string name, string source, params string[] references)
        {
            var metadata = new List<MetadataReference>();
            foreach (var path in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
            {
                if (!Path.GetFileName(path).StartsWith("Moonwell", StringComparison.Ordinal))
                {
                    metadata.Add(MetadataReference.CreateFromFile(path));
                }
            }

            metadata.AddRange(references.Select(path => MetadataReference.CreateFromFile(path)));
            var compilation = CSharpCompilation.Create(
                name,
                new[] { CSharpSyntaxTree.ParseText(source) },
                metadata,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var assembly = Path.Combine(folder, name + ".dll");
            var result = compilation.Emit(assembly);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            return assembly;
        }
    }
}
