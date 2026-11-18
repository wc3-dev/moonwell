using System.Text;
using Moonwell.Build;
using Moonwell.GameData;
using Spectre.Console;

namespace Moonwell.Tool
{
    /// <summary>
    /// <c>moonwell mdx</c>: a tree of the files a model names (its textures, and the models and popcorn effects of its emitters,
    /// with what those name in turn), each marked as the game's, custom (the map's own, or beside a model outside a map project),
    /// or found in neither.
    /// </summary>
    internal static class ModelView
    {
        // The textures the game picks for a replaceable id, which a texture with no path names.
        private static readonly Dictionary<int, string> Replaceable = new Dictionary<int, string>
        {
            [1] = "team colour",
            [2] = "team glow",
            [11] = "cliff",
            [31] = "Lordaeron tree",
            [32] = "Ashenvale tree",
            [33] = "Barrens tree",
            [34] = "Northrend tree",
            [35] = "mushroom tree",
            [36] = "ruins tree",
            [37] = "Outland mushroom tree",
        };

        /// <summary>Shows the tree of a model's files.</summary>
        /// <param name="model">A model file, a path in the map's assets, or a path in the game's storage.</param>
        /// <param name="mode">The game's art the paths are looked for in: sd, hd or de.</param>
        /// <param name="install">The game's folder, or null for the usual place.</param>
        /// <param name="output">Where the tree goes.</param>
        /// <exception cref="BuildException">No file and no game path is the model.</exception>
        public static void Show(string model, string mode, string? install, TextWriter output)
        {
            var say = new Say(output);

            GameFiles? game = null;
            try
            {
                game = GameFiles.Open(GameInstall.Require(install));
            }
            catch (Exception failure) when (failure is IOException or InvalidDataException)
            {
                say.Message("note", $"the game's files cannot be read ({failure.Message}), so no path is marked as the game's");
            }

            using (game)
            {
                // Custom files are the map project's assets and map files, or, for a model outside a project, those beside it.
                MapProject? project = null;
                try
                {
                    project = MapProject.Find(".");
                }
                catch (BuildException)
                {
                }

                var local = File.Exists(model);
                var folders = project != null ? new[] { project.AssetsFolder, project.MapFolder }
                    : local ? new[] { Path.GetDirectoryName(Path.GetFullPath(model))! }
                    : Array.Empty<string>();
                var custom = Custom(folders);

                var root = local ? new Found(model.EscapeMarkup() + Label(Kind.Custom, null), File.ReadAllBytes(model))
                    : Look(model, custom, game, mode);
                if (root.Bytes == null)
                {
                    throw new BuildException($"no model at {model}: it is no file, and neither the map nor the game has it");
                }

                var tree = new Tree(root.Text);
                var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AssetChecks.Key(model) };
                Add(tree.AddNode, root.Bytes, custom, game, mode, shown);
                say.Console.Write(tree);
            }
        }

        /// <summary>The paths a model (.mdx) names: each texture's path or replaceable id (TEXS), and the path of each particle emitter's model (PREM) and popcorn effect (CORN).</summary>
        /// <param name="model">The model's bytes; a file that is no .mdx gives nothing.</param>
        /// <returns>The paths, once each, and the replaceable ids of the textures with no path.</returns>
        internal static (List<string> Paths, List<int> Replaceable) Read(byte[] model)
        {
            var paths = new List<string>();
            var replaceable = new List<int>();
            if (model.Length < 4 || Encoding.ASCII.GetString(model, 0, 4) != "MDLX")
            {
                return (paths, replaceable);
            }

            void AddPath(int at)
            {
                if (at + 260 > model.Length)
                {
                    return;
                }

                var path = Encoding.Latin1.GetString(model, at, 260);
                var end = path.IndexOf('\0');
                path = (end < 0 ? path : path[..end]).Trim();
                if (path.Length > 0 && !paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }

            for (var at = 4; at + 8 <= model.Length;)
            {
                var tag = Encoding.ASCII.GetString(model, at, 4);
                var size = BitConverter.ToInt32(model, at + 4);
                var end = at + 8 + (long)size;
                if (size < 0 || end > model.Length)
                {
                    break;
                }

                if (tag == "TEXS")
                {
                    // 268 bytes a texture: its replaceable id, the path's 260, its flags.
                    for (var texture = at + 8; texture + 268 <= end; texture += 268)
                    {
                        var id = BitConverter.ToInt32(model, texture);
                        AddPath(texture + 4);
                        if (id != 0 && !replaceable.Contains(id))
                        {
                            replaceable.Add(id);
                        }
                    }
                }
                else if (tag is "PREM" or "CORN")
                {
                    // Each emitter: its size, its node (sized), then numbers before the path: 16 bytes in a particle emitter
                    // (emission rate, gravity, longitude, latitude), 32 in a popcorn one (life span, emission rate, speed, colour,
                    // alpha, replaceable id).
                    var before = tag == "PREM" ? 16 : 32;
                    for (var emitter = at + 8; emitter + 8 <= end;)
                    {
                        var emitterSize = BitConverter.ToInt32(model, emitter);
                        var nodeSize = BitConverter.ToInt32(model, emitter + 4);
                        if (emitterSize <= 0 || nodeSize < 0)
                        {
                            break;
                        }

                        AddPath(emitter + 4 + nodeSize + before);
                        emitter += emitterSize;
                    }
                }

                at = (int)end;
            }

            return (paths, replaceable);
        }

        private static void Add(Func<string, TreeNode> add, byte[] model, Dictionary<string, string> custom, GameFiles? game, string mode, HashSet<string> shown)
        {
            var (paths, replaceable) = Read(model);
            foreach (var id in replaceable)
            {
                add($"[dim]replaceable {id}[/] {(Replaceable.TryGetValue(id, out var name) ? name : "texture")}{Label(Kind.Game, null)}");
            }

            foreach (var path in paths)
            {
                var found = Look(path, custom, game, mode);
                var node = add(found.Text);

                // A model named twice, or naming one above it, shows what it names once.
                if (found.Bytes != null && shown.Add(AssetChecks.Key(path)))
                {
                    Add(node.AddNode, found.Bytes, custom, game, mode, shown);
                }
            }
        }

        // Where a path is found: the map's own file wins, as the game reads it over its own.
        private static Found Look(string path, Dictionary<string, string> custom, GameFiles? game, string mode)
        {
            // A model names a popcorn effect's .pkfx; the game's storage holds it baked, as .pkb (seen in its storage, 11-14).
            var inGame = game == null ? null
                : AssetChecks.FindInGame(game, path, mode)
                ?? (Path.GetExtension(path).Equals(".pkfx", StringComparison.OrdinalIgnoreCase) ? game.Find(Path.ChangeExtension(path, ".pkb"), mode) : null);
            var text = path.EscapeMarkup();
            if (custom.TryGetValue(AssetChecks.Key(path), out var file))
            {
                var bytes = Images.IsPng(file) ? null : File.ReadAllBytes(file);
                return new Found(text + Label(inGame != null ? Kind.Replaces : Kind.Custom, file), bytes);
            }

            if (inGame != null)
            {
                return new Found(text + Label(Kind.Game, null), game!.Read(inGame, mode));
            }

            return new Found(text + Label(game == null ? Kind.NotInMap : Kind.Missing, null), null);
        }

        // Each file under the folders by the key of the path the map gives it (a .png packed as a .blp), to its full path.
        private static Dictionary<string, string> Custom(IEnumerable<string> folders)
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in folders.Where(Directory.Exists))
            {
                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    files.TryAdd(AssetChecks.Key(Images.PackedName(Path.GetRelativePath(folder, file))), file);
                }
            }

            return files;
        }

        private static string Label(Kind kind, string? file)
        {
            var where = file == null ? string.Empty : $"  [dim]{Path.GetRelativePath(Environment.CurrentDirectory, file).EscapeMarkup()}[/]";
            return kind switch
            {
                Kind.Game => "  [green]game[/]",
                Kind.Custom => "  [cyan]custom[/]" + where,
                Kind.Replaces => "  [cyan]custom, replaces the game's[/]" + where,
                Kind.NotInMap => "  [yellow]not in the map[/]",
                _ => "  [red]in neither the game nor the map[/]",
            };
        }

        private enum Kind
        {
            Game,
            Custom,
            Replaces,
            NotInMap,
            Missing,
        }

        private sealed record Found(string Text, byte[]? Bytes);
    }
}
