using System.Text;
using System.Text.RegularExpressions;
using CSharpLua;
using Moonwell.GameData;
using Moonwell.MapFiles;
using War3Net.Build.Extensions;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Transpilers;
using War3Net.IO.Mpq;

namespace Moonwell.Build
{
    /// <summary>
    /// <c>moonwell import</c> (decision 257): an existing map, JASS or Lua, made a Moonwell project. Its files go into <c>map</c>,
    /// and its script, turned into Lua once if it is JASS, becomes the Lua library <c>lua/old_map.lua</c>, less what Moonwell's own
    /// script makes (the placed things, the map's setup, <c>main</c> and <c>config</c>). The component <c>OldScript</c> starts it
    /// when the game starts, as World Editor's <c>main</c> did: its globals, its triggers, then its map initialization triggers.
    /// </summary>
    public static class OldMapImport
    {
        /// <summary>The library the old script becomes, in the project's <c>lua</c> folder; its functions are <c>Lua.OldMap</c>'s.</summary>
        public const string LibraryName = "old_map.lua";

        private static readonly string[] ScriptNames = { "war3map.j", "scripts\\war3map.j", "war3map.lua" };

        private static readonly string[] Skipped = { "(listfile)", "(attributes)", "(signature)" };

        // The files an archive may hold without listing them, as Moonwell Editor knows them; the imports come from war3map.imp.
        private static readonly string[] KnownNames =
        {
            "war3map.w3i", "war3map.w3e", "war3map.wpm", "war3map.doo", "war3mapUnits.doo", "war3map.shd", "war3map.mmp", "war3map.w3r",
            "war3map.w3c", "war3map.w3s", "war3map.wtg", "war3map.wct", "war3map.wts", "war3map.j", "scripts\\war3map.j", "war3map.lua",
            "war3map.imp", "war3mapMap.blp", "war3mapMap.tga", "war3mapPreview.tga", "war3mapPath.tga", "war3mapMisc.txt",
            "war3mapSkin.txt", "war3mapExtra.txt", "war3map.w3o", "war3map.triggerlayout.json", "conversation.json",
        };

        // The JASS the old script defines and Moonwell's makes again, read line by line as World Editor and JassHelper write it.
        private static readonly Regex JassFunction = new Regex(@"^\s*(?:constant\s+)?function\s+(\w+)\s+takes\s+(.+?)\s+returns\s+(\w+)", RegexOptions.Compiled);

        private static readonly Regex JassGlobal = new Regex(@"^\s*(constant\s+)?(\w+)\s+(array\s+)?(\w+)\b", RegexOptions.Compiled);

        // A function at the line's start, a table's (LH.color) and a method (T:f) among them.
        private static readonly Regex LuaFunction = new Regex(@"^function\s+([\w.:]+)\s*\(", RegexOptions.Compiled);

        private static readonly Regex LuaGlobal = new Regex(@"^(\w+)\s*=(?!=)", RegexOptions.Compiled);

        /// <summary>Fills a project <c>moonwell new</c> made with an existing map, in place of the template's map and code.</summary>
        /// <param name="source">The map: a <c>.w3x</c> or <c>.w3m</c> archive, or a map saved as a folder.</param>
        /// <param name="projectFolder">The new project's folder.</param>
        /// <param name="log">Where what was done is told.</param>
        /// <exception cref="BuildException">The map cannot be read, or has no script.</exception>
        public static void Run(string source, string projectFolder, TextWriter log)
        {
            var project = MapProject.Find(projectFolder);
            Directory.Delete(project.MapFolder, true);
            foreach (var sample in new[] { "Heroes" })
            {
                var folder = Path.Combine(project.Folder, sample);
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }

            File.Delete(Path.Combine(project.Folder, "Start.cs"));

            var (scriptName, script) = Directory.Exists(source) ? CopyFolder(source, project.MapFolder) : CopyArchive(source, project.MapFolder);
            if (!Regex.IsMatch(script, @"^\s*function\s+main\b", RegexOptions.Multiline))
            {
                log.WriteLine($"moonwell: warning: {scriptName} defines no main on a line of its own, so it looks protected: it is kept as it is, and Moonwell's own script cannot be told apart from it");
            }

            var map = OpenMap(project);
            var (dropFunctions, dropGlobals) = MoonwellMakes(map);
            var library = scriptName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)
                ? FromLua(script, dropFunctions, dropGlobals)
                : FromJass(script, dropFunctions, dropGlobals);
            Directory.CreateDirectory(Path.Combine(project.Folder, "lua"));
            File.WriteAllText(Path.Combine(project.Folder, "lua", LibraryName), library);

            var starts = new[] { "InitGlobals", "InitCustomTriggers", "RunInitializationTriggers" }
                .Where(name => Regex.IsMatch(library, @"^function " + name + @"\s*\(", RegexOptions.Multiline)).ToList();
            var destructables = Regex.Matches(library, @"^---@type destructable\r?\n(gg_dest_(\w+))\s*=", RegexOptions.Multiline)
                .Select(found => (Global: found.Groups[1].Value, Name: found.Groups[2].Value)).ToList();
            File.WriteAllText(Path.Combine(project.Folder, "OldScript.cs"), StartCode(project.Name, starts, destructables));
            File.WriteAllText(Path.Combine(project.Folder, "Map.cs"), SettingsCode(project.Name));

            log.WriteLine($"moonwell: imported {Path.GetFileName(source)}: its files are in map, its script ({scriptName}) is lua\\{LibraryName}, started by OldScript.cs");
        }

        /// <summary>
        /// A JASS script as the library: turned into Lua whole by War3Net's transpiler (the code kept may name what is taken out,
        /// and the transpiler needs to know it), then filtered as a Lua script is, each function and global the JASS types given
        /// LuaLS comments for the bindings.
        /// </summary>
        internal static string FromJass(string jass, ISet<string> dropFunctions, ISet<string> dropGlobals)
        {
            var functions = new Dictionary<string, (List<(string Type, string Name)> Parameters, string Returns)>(StringComparer.Ordinal);
            var globals = new Dictionary<string, string>(StringComparer.Ordinal);
            var inGlobals = false;
            foreach (var line in jass.Replace("\r\n", "\n").Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed == "globals" || trimmed == "endglobals")
                {
                    inGlobals = trimmed == "globals";
                }
                else if (inGlobals && !trimmed.StartsWith("//", StringComparison.Ordinal) && JassGlobal.Match(trimmed) is { Success: true } global && !global.Groups[3].Success)
                {
                    globals[global.Groups[4].Value] = global.Groups[2].Value;
                }
                else if (JassFunction.Match(line) is { Success: true } function)
                {
                    var parameters = function.Groups[2].Value.Trim() == "nothing"
                        ? new List<(string, string)>()
                        : function.Groups[2].Value.Split(',').Select(one => one.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Select(parts => (parts[0], parts[1])).ToList();
                    functions[function.Groups[1].Value] = (parameters, function.Groups[3].Value);
                }
            }

            var (commonJ, blizzardJ) = GameScripts.Find(null, GameScripts.CacheRoot);
            var transpiler = new JassToLuaTranspiler
            {
                IgnoreComments = true,
                IgnoreEmptyDeclarations = true,
                IgnoreEmptyStatements = true,
                KeepFunctionsSeparated = true,
            };
            transpiler.RegisterJassFile(JassSyntaxFactory.ParseCompilationUnit(File.ReadAllText(commonJ)));
            transpiler.RegisterJassFile(JassSyntaxFactory.ParseCompilationUnit(File.ReadAllText(blizzardJ)));
            using var writer = new StringWriter();
            new LuaRenderer(new LuaSyntaxGenerator.SettingInfo { Indent = 4 }, writer).RenderCompilationUnit(transpiler.Transpile(JassSyntaxFactory.ParseCompilationUnit(jass)));

            return Filter(
                writer.ToString(),
                dropFunctions,
                dropGlobals,
                name =>
                {
                    return functions.TryGetValue(name, out var signature) && (IsStart(name) || !IsTriggers(name))
                        ? signature.Parameters.Select(one => $"---@param {one.Name} {LuaType(one.Type)}").Concat(signature.Returns == "nothing" ? Enumerable.Empty<string>() : new[] { "---@return " + LuaType(signature.Returns) })
                        : new[] { "---@private" };
                },
                name => globals.TryGetValue(name, out var type) ? "---@type " + LuaType(type) : null);
        }

        /// <summary>A Lua script as the library: filtered, a <c>gg_dest_</c> global typed so <c>OldScript</c> can give it its destructable.</summary>
        internal static string FromLua(string script, ISet<string> dropFunctions, ISet<string> dropGlobals)
        {
            return Filter(
                script,
                dropFunctions,
                dropGlobals,
                name => IsStart(name) ? Enumerable.Empty<string>() : new[] { "---@private" },
                name => name.StartsWith("gg_dest_", StringComparison.Ordinal) ? "---@type destructable" : null);
        }

        // Takes out the functions and globals Moonwell's script makes, line by line as World Editor and the transpiler write them (a
        // function from its line to the first end at the line's start), and puts the comments given above each function and global
        // kept. A function the script names but defines on no line of its own (an obfuscated script) is left as it is.
        private static string Filter(string script, ISet<string> dropFunctions, ISet<string> dropGlobals, Func<string, IEnumerable<string>> functionComments, Func<string, string?> globalComment)
        {
            var text = new StringBuilder();
            var skipping = false;
            foreach (var line in script.Replace("\r\n", "\n").Split('\n'))
            {
                if (skipping)
                {
                    skipping = !line.StartsWith("end", StringComparison.Ordinal);
                    continue;
                }

                if (LuaFunction.Match(line) is { Success: true } function)
                {
                    var name = function.Groups[1].Value;
                    if (dropFunctions.Contains(name))
                    {
                        skipping = !Regex.IsMatch(line, @"\bend\s*$");
                        continue;
                    }

                    foreach (var comment in functionComments(name))
                    {
                        text.Append(comment).Append('\n');
                    }
                }
                else if (LuaGlobal.Match(line) is { Success: true } global)
                {
                    if (dropGlobals.Contains(global.Groups[1].Value))
                    {
                        continue;
                    }

                    if (globalComment(global.Groups[1].Value) is { } comment)
                    {
                        text.Append(comment).Append('\n');
                    }
                }

                text.Append(line).Append('\n');
            }

            return text.ToString();
        }

        // The functions OldScript calls, which stay in the bindings.
        private static bool IsStart(string function) => function is "InitGlobals" or "InitCustomTriggers" or "RunInitializationTriggers";


        // The functions and globals Moonwell's own script makes for this map, from the JASS War3Net's builder writes for it, as the build
        // does; main and config always.
        private static (HashSet<string> Functions, HashSet<string> Globals) MoonwellMakes(War3Net.Build.Map map)
        {
            var jass = ScriptBuilder.WithScriptDestructables(map, () => new ScriptBuilder().Build(map));
            var functions = new HashSet<string>(StringComparer.Ordinal) { "main", "config" };
            var globals = new HashSet<string>(StringComparer.Ordinal);
            var inGlobals = false;
            foreach (var line in jass.Replace("\r\n", "\n").Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed == "globals" || trimmed == "endglobals")
                {
                    inGlobals = trimmed == "globals";
                }
                else if (inGlobals && JassGlobal.Match(trimmed) is { Success: true } global)
                {
                    globals.Add(global.Groups[4].Value);
                }
                else if (JassFunction.Match(line) is { Success: true } function)
                {
                    functions.Add(function.Groups[1].Value);
                }
            }

            return (functions, globals);
        }

        private static War3Net.Build.Map OpenMap(MapProject project)
        {
            try
            {
                var map = War3Net.Build.Map.Open(project.MapFolder, SourceMap.ScriptFiles);
                return map.Info is null ? throw new BuildException("the map has no war3map.w3i, so it is no map World Editor saved") : map;
            }
            catch (Exception failure) when (failure is not BuildException)
            {
                throw new BuildException($"cannot read the map: {failure.Message}");
            }
        }

        private static (string Name, string Text) CopyFolder(string source, string mapFolder)
        {
            (string, string)? script = null;
            foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, path);
                if (ScriptNames.Contains(relative, StringComparer.OrdinalIgnoreCase))
                {
                    script ??= (relative, File.ReadAllText(path));
                    continue;
                }

                var target = Path.Combine(mapFolder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(path, target);
            }

            return script ?? throw new BuildException($"{source} has no war3map.j or war3map.lua");
        }

        private static (string Name, string Text) CopyArchive(string source, string mapFolder)
        {
            if (!File.Exists(source))
            {
                throw new BuildException($"there is no map at {source}");
            }

            try
            {
                using var archive = MpqArchive.Open(source, loadListFile: true);
                archive.AddFileNames(KnownNames);
                foreach (var kind in Enum.GetValues<Kind>())
                {
                    archive.AddFileNames(new[] { Kinds.MainName(kind), Kinds.SkinName(kind) });
                }

                if (archive.TryOpenFile("war3map.imp", out var importsStream))
                {
                    using (importsStream)
                    using (var reader = new BinaryReader(importsStream))
                    {
                        archive.AddFileNames(reader.ReadImportedFiles().Files.Select(file => file.FullPath));
                    }
                }

                (string, string)? script = null;
                for (var i = 0; i < archive.Count; i++)
                {
                    if (archive[i].FileName is not { } name || Skipped.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using var stream = archive.OpenFile(name);
                    if (ScriptNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(stream);
                        script ??= (name, reader.ReadToEnd());
                        continue;
                    }

                    var target = Path.Combine(mapFolder, name.Replace('\\', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    using var written = File.Create(target);
                    stream.CopyTo(written);
                }

                return script ?? throw new BuildException($"{source} has no war3map.j or war3map.lua it names; a protected map hides its script");
            }
            catch (Exception failure) when (failure is not BuildException)
            {
                throw new BuildException($"cannot read {source}: {failure.Message}");
            }
        }

        private static bool IsTriggers(string function) => function.StartsWith("Trig_", StringComparison.Ordinal) || function.StartsWith("InitTrig_", StringComparison.Ordinal);

        // A JASS type as LuaLS writes it: the game's handle types keep their names, which the bindings know.
        private static string LuaType(string jass) => jass switch
        {
            "integer" => "integer",
            "real" => "number",
            "boolean" => "boolean",
            "string" => "string",
            "code" => "fun()",
            _ => jass,
        };

        private static string StartCode(string name, IReadOnlyList<string> starts, IReadOnlyList<(string Global, string Name)> destructables)
        {
            var text = new StringBuilder();
            text.Append("using Moonwell;\n\n");
            text.Append($"namespace {name}\n{{\n");
            text.Append("    /// <summary>\n");
            text.Append($"    /// Starts the old map's script, lua\\{LibraryName}, when the game starts, as World Editor's main did. Move a trigger to C# piece by\n");
            text.Append("    /// piece: write it in C#, then take its functions and its line in InitCustomTriggers out of the Lua.\n");
            text.Append("    /// </summary>\n");
            text.Append("    public sealed class OldScript : GameComponent\n    {\n");
            text.Append("        public override void OnGameStart()\n        {\n");
            foreach (var (global, destructable) in destructables)
            {
                text.Append($"            Lua.OldMap.{global} = Destructable.Named(\"{destructable}\");\n");
            }

            foreach (var start in starts)
            {
                text.Append($"            Lua.OldMap.{start}();\n");
            }

            text.Append("        }\n    }\n}\n");
            return text.ToString();
        }

        private static string SettingsCode(string name)
        {
            return "using Moonwell;\n\n"
                + $"namespace {name}\n{{\n"
                + "    /// <summary>The map's settings, which World Editor's are as long as none is set here, and the old script, which starts with the game.</summary>\n"
                + "    public sealed class Settings : Map\n    {\n"
                + "        public Settings()\n        {\n"
                + "            Components.Add(new OldScript());\n"
                + "        }\n    }\n}\n";
        }
    }
}
