using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using Moonwell.GameData;

namespace Moonwell.Build
{
    /// <summary>
    /// Warns of the file paths the map's object data, code and models name that neither the game nor the map has (decision 251), and
    /// of the textures, models and sounds in the assets that nothing in the built map names (decision 252).
    /// </summary>
    public static class AssetChecks
    {
        // The extensions the game swaps for one another when it reads a file (game facts: .mdl reads .mdx, .blp reads .dds,
        // a sound path works with .wav, .flac or .ogg).
        private static readonly string[][] Kinds =
        {
            new[] { ".blp", ".dds", ".tga" },
            new[] { ".mdx", ".mdl" },
            new[] { ".wav", ".flac", ".ogg" },
            new[] { ".mp3" },
        };

        private static readonly Regex PathText = new Regex(@"[\w\-. ()\\/]+\.(?:blp|dds|tga|png|mdx|mdl|wav|flac|ogg|mp3)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Runs both checks on the staged map.</summary>
        /// <param name="project">The map project, staged.</param>
        /// <param name="targetPath">The map's built assembly.</param>
        /// <param name="objects">The declared objects; none for a map without Moonwell.Objects.</param>
        /// <param name="log">Where the warnings go.</param>
        public static void Run(MapProject project, string targetPath, IReadOnlyList<DeclaredObject> objects, TextWriter log)
        {
            GameFiles game;
            try
            {
                game = GameFiles.Open(GameInstall.Require(null));
            }
            catch (Exception failure) when (failure is IOException or InvalidDataException)
            {
                log.WriteLine($"note: the game's files cannot be read ({failure.Message}), so the paths the map names are not checked");
                return;
            }

            using (game)
            {
                var staged = Directory.GetFiles(project.StagedFolder, "*", SearchOption.AllDirectories)
                    .Select(file => Path.GetRelativePath(project.StagedFolder, file).Replace('/', '\\'))
                    .ToList();
                var inMap = new HashSet<string>(staged.Select(Key), StringComparer.OrdinalIgnoreCase);
                foreach (var (user, path) in Named(targetPath, objects))
                {
                    if (!inMap.Contains(Key(path)) && !InGame(game, path))
                    {
                        log.WriteLine($"warning: {user} names {path}, which neither the game nor the map has");
                    }
                }

                foreach (var model in staged.Where(file => file.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var texture in Textures(File.ReadAllBytes(Path.Combine(project.StagedFolder, model))))
                    {
                        if (!inMap.Contains(Key(texture)) && !InGame(game, texture))
                        {
                            log.WriteLine($"warning: the model {model} names the texture {texture}, which neither the game nor the map has");
                        }
                    }
                }

                var unused = Unused(project, staged, game);
                if (unused.Count > 0)
                {
                    log.WriteLine("warning: nothing in the map names these assets, so they may be unused (a path the code puts together while the game runs is not seen): "
                        + string.Join(", ", unused.Select(file => $@"assets\{file}")));
                }
            }
        }

        /// <summary>
        /// Each path the declared objects' values and the map's own code name, once for each user: the class and field, or the
        /// type whose code has it. A path object data names is not named again for code, as a constructor that sets it has it.
        /// </summary>
        public static IEnumerable<(string User, string Path)> Named(string targetPath, IReadOnlyList<DeclaredObject> objects)
        {
            var seen = new HashSet<(string, string)>();
            var data = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var declared in objects)
            {
                foreach (var (field, value) in declared.Changed)
                {
                    if (value is not string text)
                    {
                        continue;
                    }

                    foreach (var path in text.Split(',').Select(piece => piece.Trim()).Where(IsPath))
                    {
                        data.Add(path);
                        if (seen.Add(($"{declared.ClassName}: {field}", path)))
                        {
                            yield return ($"{declared.ClassName}: {field}", path);
                        }
                    }
                }
            }

            foreach (var assembly in CompiledFiles.MapAssemblies(targetPath))
            {
                foreach (var (type, text) in CodeStrings(assembly))
                {
                    var path = text.Trim();
                    if (IsPath(path) && !data.Contains(path) && seen.Add((type, path)))
                    {
                        yield return ($"{type}'s code", path);
                    }
                }
            }
        }

        /// <summary>
        /// The texture paths a model (.mdx) names, once each: its TEXS chunk, 268 bytes a texture, the path the 260 after the
        /// first 4. A texture with no path (team colour and the other replaceable ones) is left out, and a file that is no
        /// model gives none.
        /// </summary>
        public static IReadOnlyList<string> Textures(byte[] model)
        {
            var found = new List<string>();
            if (model.Length < 4 || Encoding.ASCII.GetString(model, 0, 4) != "MDLX")
            {
                return found;
            }

            for (var at = 4; at + 8 <= model.Length;)
            {
                var size = BitConverter.ToInt32(model, at + 4);
                if (size < 0 || at + 8 + (long)size > model.Length)
                {
                    break;
                }

                if (Encoding.ASCII.GetString(model, at, 4) == "TEXS")
                {
                    for (var texture = at + 8; texture + 268 <= at + 8 + size; texture += 268)
                    {
                        var path = Encoding.Latin1.GetString(model, texture + 4, 260);
                        var end = path.IndexOf('\0');
                        path = (end < 0 ? path : path[..end]).Trim();
                        if (path.Length > 0 && !found.Contains(path, StringComparer.OrdinalIgnoreCase))
                        {
                            found.Add(path);
                        }
                    }
                }

                at += 8 + size;
            }

            return found;
        }

        // A path ends in one of the extensions the game swaps (.png is not one, as the map holds an asset's PNG as a BLP) and
        // has no character a path cannot ("{0}.blp" is a format).
        private static bool IsPath(string text)
        {
            var extension = Path.GetExtension(text);
            return text.Length > extension.Length
                && text.IndexOfAny(new[] { '{', '}', '<', '>', '|', '"', '*', '?', '\n', '\r', '\t' }) < 0
                && Kinds.Any(kind => kind.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        // The path without its extension and the kind of file, as the game finds one of a kind under another of its extensions.
        private static string Key(string path)
        {
            var normalized = path.Trim().Replace('/', '\\').TrimStart('\\');
            var extension = Path.GetExtension(normalized);
            var kind = Array.FindIndex(Kinds, members => members.Contains(extension, StringComparer.OrdinalIgnoreCase));
            return kind < 0 ? normalized : normalized[..^extension.Length] + "|" + kind;
        }

        private static bool InGame(GameFiles game, string path)
        {
            var extension = Path.GetExtension(path);
            var kind = Kinds.FirstOrDefault(members => members.Contains(extension, StringComparer.OrdinalIgnoreCase)) ?? new[] { extension };
            var stem = path[..^extension.Length];
            return kind.Any(other => game.Find(stem + other, "hd") != null);
        }

        // The textures, models and sounds of the assets whose path no other file of the built map has, that replace no file of
        // the game and that the game does not read by a name made from another's (a grey copy, a portrait).
        private static List<string> Unused(MapProject project, IReadOnlyList<string> staged, GameFiles game)
        {
            if (!Directory.Exists(project.AssetsFolder))
            {
                return new List<string>();
            }

            var mentions = staged.ToDictionary(file => file, file => Mentions(Path.Combine(project.StagedFolder, file)), StringComparer.OrdinalIgnoreCase);
            var unused = new List<string>();
            foreach (var file in Directory.GetFiles(project.AssetsFolder, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(project.AssetsFolder, file).Replace('/', '\\'))
                .OrderBy(file => file, StringComparer.Ordinal))
            {
                var packed = Images.PackedName(file);
                if (!IsPath(packed) || InGame(game, packed) || IsGreyCopy(packed))
                {
                    continue;
                }

                var stems = new List<string> { Stem(packed) };
                if (stems[0].EndsWith("_portrait", StringComparison.Ordinal))
                {
                    stems.Add(stems[0][..^"_portrait".Length]);
                }

                var named = mentions.Any(entry => !string.Equals(entry.Key, packed, StringComparison.OrdinalIgnoreCase)
                    && entry.Value.Any(text => stems.Any(stem => text.EndsWith(stem, StringComparison.Ordinal))));
                if (!named)
                {
                    unused.Add(file);
                }
            }

            return unused;
        }

        private static bool IsGreyCopy(string packed)
        {
            return packed.StartsWith(@"ReplaceableTextures\CommandButtonsDisabled\DIS", StringComparison.OrdinalIgnoreCase);
        }

        // Lower case, with \ between folders and no extension: how a mention and an asset are compared.
        private static string Stem(string path)
        {
            var normalized = path.Replace(@"\\", @"\").Replace('/', '\\').ToLowerInvariant();
            return normalized[..^Path.GetExtension(normalized).Length];
        }

        // The paths a file of the map has in it, read as bytes, as a script, an object file, a TOC or a model holds them.
        private static HashSet<string> Mentions(string file)
        {
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(file));
            return PathText.Matches(text).Select(match => Stem(match.Value)).ToHashSet(StringComparer.Ordinal);
        }

        // Each string literal of an assembly's code with the type whose method loads it, the outer type for a lambda's.
        private static IEnumerable<(string Type, string Text)> CodeStrings(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            var strings = new HashSet<int>();
            for (var handle = MetadataTokens.UserStringHandle(1); !handle.IsNil; handle = metadata.GetNextHandle(handle))
            {
                strings.Add(MetadataTokens.GetHeapOffset(handle));
            }

            var found = new List<(string, string)>();
            foreach (var typeHandle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(typeHandle);
                var owner = OuterName(metadata, type);
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = metadata.GetMethodDefinition(methodHandle);
                    if (method.RelativeVirtualAddress == 0)
                    {
                        continue;
                    }

                    var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
                    for (var i = 0; i + 4 < il.Length; i++)
                    {
                        // ldstr and a user string's token; a byte of another operand that looks like one is taken only if the token is a string's.
                        if (il[i] != 0x72 || il[i + 4] != 0x70)
                        {
                            continue;
                        }

                        var offset = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16);
                        if (strings.Contains(offset))
                        {
                            found.Add((owner, metadata.GetUserString(MetadataTokens.UserStringHandle(offset))));
                            i += 4;
                        }
                    }
                }
            }

            return found;
        }

        // The type's full name, nested types after their outer type's; a type the compiler made (a lambda's) gives its outer type's.
        private static string OuterName(MetadataReader metadata, TypeDefinition type)
        {
            while (type.IsNested && metadata.GetString(type.Name).StartsWith('<'))
            {
                type = metadata.GetTypeDefinition(type.GetDeclaringType());
            }

            var name = metadata.GetString(type.Name);
            if (type.IsNested)
            {
                return OuterName(metadata, metadata.GetTypeDefinition(type.GetDeclaringType())) + "." + name;
            }

            var space = metadata.GetString(type.Namespace);
            return space.Length == 0 ? name : space + "." + name;
        }
    }
}
