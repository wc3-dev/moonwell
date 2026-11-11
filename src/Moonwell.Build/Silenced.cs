using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Moonwell.Build
{
    /// <summary>
    /// Lists every place where a map turns a Moonwell rule off, so that what the analyzer did not check can be read: each
    /// <c>[SyncUnchecked]</c>, each <c>#pragma warning disable</c> and <c>[SuppressMessage]</c> on an MW rule, and each
    /// <c>NoWarn</c> or <c>.editorconfig</c> entry for one. The build prints the list and writes it to <c>build\silenced.txt</c>.
    /// </summary>
    internal static class Silenced
    {
        /// <summary>The list's file in the build folder.</summary>
        public const string FileName = "silenced.txt";

        // Past this many places the build prints the count and the file's name only.
        private const int Shown = 5;

        private static readonly Regex Rule = new Regex(@"\bMW\d{4}\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // A config line that hides a Moonwell rule: the rule itself, every analyzer rule, or one of the categories of
        // Moonwell.Analyzers' Rules.cs. none, silent and suggestion print nothing in a build; warning still prints.
        private static readonly Regex ConfigLine = new Regex(
            @"^\s*dotnet_(diagnostic\.MW\d{4}|analyzer_diagnostic(\.category-(Multiplayer|Lua|Objects))?)\.severity\s*=\s*(none|silent|suggestion)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>Finds the places, writes the list and prints it; a map that turns nothing off gets no file and no line.</summary>
        /// <param name="project">The map project, after <see cref="DotnetBuild.Run"/>.</param>
        /// <param name="targetPath">The map's built assembly; its own libraries are beside it.</param>
        /// <param name="log">Where the build's notes go.</param>
        public static void Report(MapProject project, string targetPath, TextWriter log)
        {
            Write(project, Find(project, targetPath), log);
        }

        /// <summary>The places, one line each, as <c>file:line: what</c> with the file's path from the project folder.</summary>
        internal static List<string> Find(MapProject project, string targetPath)
        {
            string Relative(string path) => Path.GetRelativePath(project.Folder, path).Replace('\\', '/');
            string Place(string file, int line, string what) => line > 0 ? $"{file}:{line}: {what}" : $"{file}: {what}";

            var places = new List<string>();
            var sources = CompiledFiles.Sources(project, project.BuildFolder).Select(path => (Path: path, Relative: Relative(path))).OrderBy(file => file.Relative, StringComparer.Ordinal);
            foreach (var file in sources)
            {
                // The symbols of this build: a #pragma in a branch this build leaves out silenced nothing in it.
                places.AddRange(InSource(CompiledFiles.ReadSource(file.Path), project.Symbols ?? Array.Empty<string>()).Select(found => Place(file.Relative, found.Line, found.What)));
            }

            var projectFile = Relative(project.ProjectFile);
            var projectLines = File.ReadAllLines(project.ProjectFile);
            foreach (var rule in Rule.Matches(project.NoWarn).Select(match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var line = Array.FindIndex(projectLines, text => text.Contains("NoWarn", StringComparison.OrdinalIgnoreCase) && text.Contains(rule, StringComparison.OrdinalIgnoreCase)) + 1;
                places.Add(Place(projectFile, line, line > 0 ? $"NoWarn {rule}" : $"NoWarn {rule} (set in a file the project imports)"));
            }

            if (project.AnalyzersOff is { } off)
            {
                var line = Array.FindIndex(projectLines, text => text.Contains(off, StringComparison.OrdinalIgnoreCase)) + 1;
                places.Add(Place(projectFile, line, $"{off} is false, so no Moonwell rule ran"));
            }

            foreach (var config in project.ConfigFiles.Where(File.Exists))
            {
                places.AddRange(InConfig(File.ReadAllLines(config)).Select(found => Place(Relative(config), found.Line, found.What)));
            }

            // A library's source is not the build's to read, so its marks are read from its assembly, without a line.
            foreach (var library in CompiledFiles.MapAssemblies(targetPath).Skip(1))
            {
                places.AddRange(InLibrary(library).Select(member => Place(Path.GetFileName(library), 0, $"[SyncUnchecked] on {member}")));
            }

            return places;
        }

        /// <summary>
        /// The places in one C# file, by syntax alone: an attribute named <c>SyncUnchecked</c>, a <c>#pragma warning disable</c>
        /// that names an MW rule or none (which turns every rule off), and a <c>[SuppressMessage]</c> whose rule is an MW one.
        /// </summary>
        /// <param name="source">The file's text.</param>
        /// <param name="symbols">The preprocessor symbols the build compiled with.</param>
        /// <returns>Each place's line, from 1, and what it is, in the file's order.</returns>
        // shortcut: an attribute behind a using alias is not seen; read the map's assembly as InLibrary does if that matters.
        internal static List<(int Line, string What)> InSource(string source, IEnumerable<string> symbols)
        {
            var tree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest).WithPreprocessorSymbols(symbols));
            var found = new List<(int Line, string What)>();
            int Line(SyntaxNode node) => tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

            foreach (var node in tree.GetRoot().DescendantNodes(descendIntoTrivia: true))
            {
                // A #pragma in a branch the symbols leave out is still parsed, as not active.
                if (node is PragmaWarningDirectiveTriviaSyntax { IsActive: true } pragma && pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
                {
                    var rules = pragma.ErrorCodes.Select(code => code.ToString()).Where(code => Rule.IsMatch(code)).ToList();
                    if (pragma.ErrorCodes.Count == 0)
                    {
                        found.Add((Line(pragma), "#pragma warning disable with no rule named, which turns every rule off"));
                    }
                    else if (rules.Count > 0)
                    {
                        found.Add((Line(pragma), "#pragma warning disable " + string.Join(", ", rules)));
                    }
                }
                else if (node is AttributeSyntax attribute)
                {
                    var name = attribute.Name.ToString();
                    name = name.Substring(name.LastIndexOf('.') + 1);
                    name = name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : name;
                    if (name == "SyncUnchecked")
                    {
                        found.Add((Line(attribute), "[SyncUnchecked] on " + Owner(attribute)));
                    }
                    else if (name is "SuppressMessage" or "UnconditionalSuppressMessage"
                        && attribute.ArgumentList is { Arguments.Count: >= 2 } arguments
                        && arguments.Arguments[1].Expression is LiteralExpressionSyntax literal
                        && Rule.Match(literal.Token.ValueText) is { Success: true, Index: 0 } rule)
                    {
                        found.Add((Line(attribute), $"[{name}] for {rule.Value}"));
                    }
                }
            }

            return found;
        }

        /// <summary>The lines of an <c>.editorconfig</c> or <c>.globalconfig</c> that hide a Moonwell rule from the build's output.</summary>
        /// <param name="lines">The file's lines.</param>
        /// <returns>Each such line's number, from 1, and its text.</returns>
        // shortcut: a line counts whatever its section, and a file above a root = true one is read too; follow the sections if a false line shows up.
        internal static List<(int Line, string What)> InConfig(IReadOnlyList<string> lines)
        {
            return Enumerable.Range(0, lines.Count).Where(index => ConfigLine.IsMatch(lines[index])).Select(index => (index + 1, lines[index].Trim())).ToList();
        }

        /// <summary>The members of a built library that carry <c>[SyncUnchecked]</c>, read from its metadata without loading it.</summary>
        /// <param name="assemblyPath">The library's assembly.</param>
        /// <returns>Each member as <c>Class.Member</c>; a lambda or local function as the member it is in.</returns>
        internal static List<string> InLibrary(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            var found = new List<string>();
            foreach (var handle in metadata.CustomAttributes)
            {
                var attribute = metadata.GetCustomAttribute(handle);
                if (attribute.Constructor.Kind != HandleKind.MemberReference
                    || metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent is not { Kind: HandleKind.TypeReference } parent)
                {
                    continue;
                }

                var type = metadata.GetTypeReference((TypeReferenceHandle)parent);
                if (metadata.GetString(type.Name) != "SyncUncheckedAttribute" || metadata.GetString(type.Namespace) != "Moonwell")
                {
                    continue;
                }

                if (attribute.Parent.Kind == HandleKind.MethodDefinition)
                {
                    found.Add(Named(metadata, metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Parent)));
                }
                else if (attribute.Parent.Kind == HandleKind.PropertyDefinition)
                {
                    var property = metadata.GetPropertyDefinition((PropertyDefinitionHandle)attribute.Parent);
                    var accessors = property.GetAccessors();
                    var accessor = metadata.GetMethodDefinition(accessors.Getter.IsNil ? accessors.Setter : accessors.Getter);
                    found.Add(TypeName(metadata, accessor.GetDeclaringType()) + "." + metadata.GetString(property.Name));
                }
            }

            return found;
        }

        // A method as a person names it: Hud.Refresh, Hud's constructor as Hud.Hud, and the compiler's name for a lambda or a
        // local function (<Show>b__0_0, <Show>g__Draw|0_0) as the method it is in.
        private static string Named(MetadataReader metadata, MethodDefinition method)
        {
            var type = TypeName(metadata, method.GetDeclaringType());
            var name = metadata.GetString(method.Name);
            var inner = Regex.Match(name, @"^<([^>]+)>[bg]__");
            var member = inner.Success ? inner.Groups[1].Value : name;
            member = member is ".ctor" or ".cctor" ? type : member;
            return inner.Success ? $"a lambda or local function in {type}.{member}" : $"{type}.{member}";
        }

        // A type's name; the compiler's own nested class for lambdas (<>c) is named as the class it is in.
        private static string TypeName(MetadataReader metadata, TypeDefinitionHandle handle)
        {
            var type = metadata.GetTypeDefinition(handle);
            var name = metadata.GetString(type.Name);
            return name.StartsWith('<') && !type.GetDeclaringType().IsNil ? TypeName(metadata, type.GetDeclaringType()) : name;
        }

        // What an attribute is on: Hud.Refresh, a lambda in Hud.Show, Draw in Hud.Show.
        private static string Owner(AttributeSyntax attribute)
        {
            var target = attribute.Parent?.Parent;
            var member = target?.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault();
            var type = member?.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
            var name = member switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
                PropertyDeclarationSyntax property => property.Identifier.ValueText,
                BaseFieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.ValueText,
                _ => null,
            };
            var inside = type == null ? "top-level code" : name == null ? type : $"{type}.{name}";
            return target switch
            {
                LambdaExpressionSyntax => "a lambda in " + inside,
                LocalFunctionStatementSyntax local => $"{local.Identifier.ValueText} in {inside}",
                _ => inside,
            };
        }

        /// <summary>
        /// Writes the list to <c>build\silenced.txt</c> and prints it: every place when there are few, else the count and the
        /// file's name. With no place, a list an earlier build left is deleted and nothing is printed.
        /// </summary>
        /// <exception cref="BuildException">The file cannot be written.</exception>
        internal static void Write(MapProject project, IReadOnlyList<string> places, TextWriter log)
        {
            var path = Path.Combine(project.BuildFolder, FileName);
            try
            {
                if (places.Count == 0)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    return;
                }

                File.WriteAllLines(path, places);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new BuildException($@"cannot write build\{FileName}: {error.Message}");
            }

            var count = places.Count == 1 ? "1 place turns" : $"{places.Count} places turn";
            if (places.Count > Shown)
            {
                log.WriteLine($@"moonwell: {count} a Moonwell rule off, listed in build\{FileName}");
                return;
            }

            log.WriteLine($@"moonwell: {count} a Moonwell rule off (build\{FileName}):");
            foreach (var place in places)
            {
                log.WriteLine("  " + place);
            }
        }
    }
}
