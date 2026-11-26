using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Moonwell.Build.Tests
{
    /// <summary>Whether a map's code passes <c>encrypted: true</c> for a player file, read from its assembly's code (J190).</summary>
    public sealed class EncryptedFilesTests : IDisposable
    {
        // A stand-in for Moonwell: only the members the build looks for.
        private const string MoonwellSource = """
            namespace Moonwell
            {
                public sealed class Player { }
                public static class PlayerFile
                {
                    public static void Write(Player player, string path, string text, bool encrypted = false) { }
                    public static string Read(string path, bool encrypted = false) { return null; }
                }
                public class Scope
                {
                    public void ReadFile(Player player, string path, System.Action<string> arrived, bool encrypted = false) { }
                }
            }
            """;

        private readonly TestFolders folders;

        public EncryptedFilesTests(ITestOutputHelper output)
        {
            folders = new TestFolders(output);
        }

        public void Dispose()
        {
            folders.Dispose();
        }

        [Theory]
        [InlineData("void F(Moonwell.Player p) { Moonwell.PlayerFile.Write(p, \"a.txt\", \"x\", encrypted: true); }", true)]
        [InlineData("string F() { return Moonwell.PlayerFile.Read(\"a.txt\", encrypted: true); }", true)]
        [InlineData("void F(Moonwell.Scope s, Moonwell.Player p) { s.ReadFile(p, \"a.txt\", text => { }, encrypted: true); }", true)]
        [InlineData("void F(bool sealed_) { System.Action a = () => Moonwell.PlayerFile.Read(\"a.txt\", sealed_); }", true)]
        [InlineData("void F(Moonwell.Player p) { Moonwell.PlayerFile.Write(p, \"a.txt\", \"x\"); Moonwell.PlayerFile.Read(\"a.txt\", encrypted: false); }", false)]
        [InlineData("void F(Moonwell.Scope s, Moonwell.Player p) { s.ReadFile(p, \"a.txt\", text => { }); }", false)]
        public void a_call_passing_encrypted_true_is_found_in_both_builds(string method, bool found)
        {
            foreach (var optimize in new[] { OptimizationLevel.Debug, OptimizationLevel.Release })
            {
                var folder = folders.TemporaryFolderOutsideRepository();
                var moonwell = Compile(folder, "Moonwell", MoonwellSource, optimize);
                var map = Compile(folder, "MapCode", "class Unlocks { " + method + " }", optimize, moonwell);

                Assert.Equal(found ? "Unlocks" : null, FileStore.EncryptedBy(map));
            }
        }

        // Compiles an assembly of one source into the folder, referencing the runtime and the given assemblies.
        private static string Compile(string folder, string name, string source, OptimizationLevel optimize, params string[] references)
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
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: optimize));
            var assembly = Path.Combine(folder, name + ".dll");
            var result = compilation.Emit(assembly);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            return assembly;
        }
    }
}
