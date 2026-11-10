using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Moonwell.Build
{
    /// <summary>
    /// The map's own Lua libraries: the <c>.lua</c> files in the project's <c>lua</c> folder, linked into the script before the
    /// map's code in the order of their paths, and the C# the map calls them through, written before the compile from the
    /// LuaLS comments above each function (decision 255). A global function or one of a global table (<c>function Push(...)</c>,
    /// <c>function Knockback.push(...)</c>) gets a static method of the class <c>Lua.Knockback</c>, named after the table, or after
    /// the file for a global function; a method (<c>T:f</c>) and a local function are left out. A function's call is refused inside
    /// <c>Local.For</c> unless its comments hold <c>---@localsafe</c> (decision 256).
    /// </summary>
    public static class LuaLibraries
    {
        /// <summary>The bindings' file name in the generated folder, which the map's project compiles.</summary>
        public const string FileName = "LuaLibraries.g.cs";

        // The game's handle types, as Moonwell.Natives declares them; a library's comment names them as common.j does.
        private static readonly HashSet<string> Handles = new HashSet<string>(
            ("agent player widget unit destructable item ability buff force group trigger triggercondition triggeraction timer location region rect "
            + "boolexpr sound conditionfunc filterfunc unitpool itempool race alliancetype racepreference gamestate igamestate fgamestate playerstate "
            + "playerscore playergameresult unitstate aidifficulty eventid gameevent playerevent playerunitevent unitevent limitop widgetevent dialogevent "
            + "unittype gamespeed gamedifficulty gametype mapflag mapvisibility mapsetting mapdensity mapcontrol minimapicon playerslotstate volumegroup "
            + "camerafield camerasetup playercolor placement startlocprio raritycontrol blendmode texmapflags effect effecttype weathereffect "
            + "terraindeformation fogstate fogmodifier dialog button quest questitem defeatcondition timerdialog leaderboard multiboard multiboarditem "
            + "trackable gamecache version itemtype texttag attacktype damagetype weapontype soundtype lightning pathingtype mousebuttontype animtype "
            + "subanimtype image ubersplat hashtable framehandle originframetype framepointtype textaligntype frameeventtype oskeytype metakeytype "
            + "movetype targetflag armortype heroattribute defensetype regentype unitcategory pathingflag commandbuttoneffect fogstyle loadoutslot").Split(' '),
            StringComparer.Ordinal);

        // A handle the runtime has an object for: the C# type and how each way is converted.
        private static readonly Dictionary<string, string> Objects = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["unit"] = "Unit",
            ["item"] = "Item",
            ["destructable"] = "Destructable",
            ["player"] = "Player",
        };

        private static readonly Regex Function = new Regex(@"^function\s+([A-Za-z_][A-Za-z0-9_]*)(?:\.([A-Za-z_][A-Za-z0-9_]*))?\s*\(([^)]*)\)", RegexOptions.Compiled);

        private static readonly Regex Param = new Regex(@"^---\s*@param\s+([A-Za-z_][A-Za-z0-9_]*|\.\.\.)(\??)\s+(.+)$", RegexOptions.Compiled);

        private static readonly Regex Return = new Regex(@"^---\s*@return\s+(.+)$", RegexOptions.Compiled);

        /// <summary>The project's Lua libraries, by path relative to its <c>lua</c> folder, in ordinal order of those paths.</summary>
        /// <param name="project">The map project.</param>
        public static IReadOnlyList<string> Files(MapProject project)
        {
            var folder = Path.Combine(project.Folder, "lua");
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.lua", SearchOption.AllDirectories).OrderBy(path => Path.GetRelativePath(folder, path).Replace('\\', '/'), StringComparer.Ordinal).ToList()
                : new List<string>();
        }

        /// <summary>Writes the bindings into the project's generated folder, or removes an earlier build's when the map has no library.</summary>
        /// <param name="project">The map project.</param>
        /// <returns>The path of the written file; null when there is none.</returns>
        public static string? WriteBindings(MapProject project)
        {
            var path = Path.Combine(project.GeneratedFolder, FileName);
            var files = Files(project);
            if (files.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return null;
            }

            var classes = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(project.Folder, file).Replace('\\', '/');
                foreach (var (table, method) in Bindings(File.ReadAllLines(file), ClassOf(file), relative))
                {
                    if (!classes.TryGetValue(table, out var methods))
                    {
                        classes[table] = methods = new List<string>();
                    }

                    methods.Add(method);
                }
            }

            var text = new StringBuilder();
            text.Append("// Generated by Moonwell's build from the map's Lua libraries. Do not edit; every build writes it anew.\n");
            text.Append("#nullable enable\n");
            text.Append("namespace Lua\n{\n");
            foreach (var (table, methods) in classes)
            {
                text.Append($"    /// <summary>The Lua library's functions of {table}, as its comments type them.</summary>\n");
                text.Append($"    public static partial class {Identifier(table)}\n    {{\n");
                text.Append(string.Join("\n", methods));
                text.Append("    }\n\n");
            }

            text.Append("}\n");
            Directory.CreateDirectory(project.GeneratedFolder);
            File.WriteAllText(path, text.ToString());
            return path;
        }

        /// <summary>The C# of one file's functions: for each, the table it goes under and its two methods.</summary>
        /// <param name="lines">The file's lines.</param>
        /// <param name="fileClass">The class a global function goes under, made from the file's name.</param>
        /// <param name="relative">The file's path in the project, for the summaries.</param>
        internal static IEnumerable<(string Table, string Method)> Bindings(IReadOnlyList<string> lines, string fileClass, string relative)
        {
            var comments = new List<string>();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("---", StringComparison.Ordinal))
                {
                    comments.Add(line);
                    continue;
                }

                var found = Function.Match(line);
                if (found.Success && raw.Length > 0 && !char.IsWhiteSpace(raw[0]))
                {
                    var table = found.Groups[2].Success ? found.Groups[1].Value : fileClass;
                    var name = found.Groups[2].Success ? found.Groups[2].Value : found.Groups[1].Value;
                    var lua = found.Groups[2].Success ? found.Groups[1].Value + "." + name : name;
                    var parameters = found.Groups[3].Value.Split(',').Select(one => one.Trim()).Where(one => one.Length > 0).ToList();
                    yield return (table, Method(lua, name, parameters, comments, relative));
                }

                comments.Clear();
            }
        }

        // The public method, which turns Moonwell's objects into handles and back, and the private one CSharp.lua writes as the call.
        private static string Method(string lua, string name, List<string> parameters, List<string> comments, string relative)
        {
            var types = new Dictionary<string, (string Type, bool Optional)>(StringComparer.Ordinal);
            string? returns = null;
            var summary = new List<string>();
            var localSafe = false;
            foreach (var comment in comments)
            {
                if (Param.Match(comment) is { Success: true } param)
                {
                    types[param.Groups[1].Value] = (ReadType(param.Groups[3].Value), param.Groups[2].Value == "?");
                }
                else if (Return.Match(comment) is { Success: true } ret)
                {
                    returns ??= ReadType(ret.Groups[1].Value);
                }
                else if (Regex.IsMatch(comment, @"^---\s*@localsafe\b"))
                {
                    localSafe = true;
                }
                else if (!Regex.IsMatch(comment, @"^---\s*@"))
                {
                    summary.Add(comment.Substring(3).Trim());
                }
            }

            var outer = new List<string>();
            var inner = new List<string>();
            var passed = new List<string>();
            foreach (var parameter in parameters.Where(one => one != "..."))
            {
                var (luaType, optional) = types.TryGetValue(parameter, out var typed) ? typed : ("any", false);
                optional |= IsOptional(luaType);
                var id = Identifier(parameter);
                var type = Map(Bare(luaType));
                outer.Add(type.CSharp + (optional ? "? " + id + " = null" : " " + id));
                inner.Add(type.Raw + (optional || !type.Value ? "? " : " ") + id);
                passed.Add(type.ToRaw(id, optional));
            }

            var call = Identifier(name) + "__lua(" + string.Join(", ", passed) + ")";
            var template = lua + "(" + string.Join(", ", Enumerable.Range(0, passed.Count).Select(n => "{" + n + "}")) + ")";
            string result, rawResult, body;
            if (returns == null)
            {
                result = rawResult = "void";
                body = call;
            }
            else
            {
                var type = Map(Bare(returns));
                var nullable = IsOptional(returns) || !type.Value;
                result = type.CSharp + (nullable ? "?" : string.Empty);
                rawResult = type.Raw + (nullable ? "?" : string.Empty);
                body = type.FromRaw(call, false);
            }

            var text = new StringBuilder();
            var described = summary.Count > 0 ? string.Join(" ", summary) : $"{lua} of {relative}.";
            text.Append($"        /// <summary>{Escape(described)}</summary>\n");
            if (!localSafe)
            {
                text.Append("        [global::Moonwell.LuaLibrary]\n");
            }

            text.Append($"        public static {result} {Identifier(name)}({string.Join(", ", outer)}) => {body};\n\n");
            text.Append($"        /// @CSharpLua.Template = \"{template}\"\n");
            text.Append($"        private static {rawResult} {Identifier(name)}__lua({string.Join(", ", inner)}) => throw new global::System.InvalidOperationException(\"{lua} is Lua, which runs only in the game\");\n");
            return text.ToString();
        }

        // A LuaLS type as C#: the type the map sees, the type the call takes, whether it is a value type, and how a value goes
        // to the call (given whether it may be null) and back (given whether it must not be null, as a callback's argument).
        private static LuaType Map(string luaType)
        {
            switch (luaType)
            {
                case "number":
                    return new LuaType("float", "float", true, (id, _) => id, (value, _) => value);
                case "integer":
                    return new LuaType("int", "int", true, (id, _) => id, (value, _) => value);
                case "string":
                    return new LuaType("string", "string", false, (id, _) => id, (value, _) => value);
                case "boolean":
                    return new LuaType("bool", "bool", true, (id, _) => id, (value, _) => value);
            }

            if (Objects.TryGetValue(luaType, out var moonwell))
            {
                var type = "global::Moonwell." + moonwell;
                return new LuaType(type, "global::Moonwell." + luaType, false, (id, optional) => id + (optional ? "?" : string.Empty) + ".Handle", (value, sure) => type + ".Of(" + value + ")" + (sure ? "!" : string.Empty));
            }

            if (Handles.Contains(luaType))
            {
                var type = "global::Moonwell." + luaType;
                return new LuaType(type, type, false, (id, _) => id, (value, _) => value);
            }

            if (Regex.Match(luaType, @"^fun\((.*)\)(?::(.+))?$") is { Success: true } function && !function.Groups[1].Value.Contains("fun(", StringComparison.Ordinal))
            {
                return Callback(function.Groups[1].Value, function.Groups[2].Success ? Bare(function.Groups[2].Value.Trim()) : null);
            }

            // shortcut: a table, a union or a list is passed as it is, typed object; type the common shapes when one is needed.
            return new LuaType("object", "object", false, (id, _) => id, (value, _) => value);
        }

        // fun(a: unit, b: number): boolean as a delegate the map writes a lambda for; the call is given a lambda that turns the
        // library's handles into Moonwell's objects for it, and its answer back.
        private static LuaType Callback(string parameters, string? returns)
        {
            var arguments = parameters.Split(',').Select(one => one.Trim()).Where(one => one.Length > 0)
                .Select(one => Map(Bare(one.Contains(':') ? one.Substring(one.IndexOf(':') + 1).Trim() : "any"))).ToList();
            var result = returns == null ? null : Map(returns);
            var names = Enumerable.Range(0, arguments.Count).Select(n => "a" + n).ToList();
            string Delegate(IEnumerable<string> types, string? answer) =>
                answer == null
                    ? (arguments.Count == 0 ? "global::System.Action" : "global::System.Action<" + string.Join(", ", types) + ">")
                    : "global::System.Func<" + string.Join(", ", types.Append(answer)) + ">";
            var csharp = Delegate(arguments.Select(one => one.CSharp), result?.CSharp);
            var raw = Delegate(arguments.Select(one => one.Raw + (one.Value ? string.Empty : "?")), result == null ? null : result.Raw + (result.Value ? string.Empty : "?"));
            return new LuaType(csharp, raw, false, (id, optional) =>
            {
                var called = id + "(" + string.Join(", ", arguments.Select((one, n) => one.FromRaw(names[n], true))) + ")";
                var lambda = "(" + string.Join(", ", arguments.Select((one, n) => one.Raw + (one.Value ? " " : "? ") + names[n])) + ") => " + (result == null ? called : result.ToRaw(called, false));
                return optional ? id + " == null ? null : (" + raw + ")(" + lambda + ")" : "(" + raw + ")(" + lambda + ")";
            }, (value, _) => value);
        }

        // A type at the start of a comment's text, up to the description after it: a fun(...)'s parentheses and its ": answer"
        // are kept whole.
        private static string ReadType(string text)
        {
            var depth = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    depth++;
                }
                else if (text[i] == ')')
                {
                    depth--;
                }
                else if (depth == 0 && char.IsWhiteSpace(text[i]))
                {
                    var rest = text.Substring(i).TrimStart();
                    if (!rest.StartsWith(':') && !text.Substring(0, i).EndsWith(':'))
                    {
                        return text.Substring(0, i);
                    }
                }
            }

            return text.Trim();
        }

        private static bool IsOptional(string luaType) => luaType.EndsWith("?", StringComparison.Ordinal) || luaType.EndsWith("|nil", StringComparison.Ordinal);

        private static string Bare(string luaType) => luaType.TrimEnd('?').Replace("|nil", string.Empty, StringComparison.Ordinal);

        // The class a file's global functions go under: its name, made an identifier in PascalCase (knock_back.lua is KnockBack).
        private static string ClassOf(string file)
        {
            var parts = Regex.Split(Path.GetFileNameWithoutExtension(file), "[^A-Za-z0-9]+").Where(part => part.Length > 0);
            var name = string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
            return name.Length == 0 ? "Library" : name;
        }

        private static string Identifier(string name)
        {
            var id = char.IsDigit(name[0]) ? "_" + name : name;
            return SyntaxFacts.GetKeywordKind(id) != SyntaxKind.None ? "@" + id : id;
        }

        private sealed record LuaType(string CSharp, string Raw, bool Value, Func<string, bool, string> ToRaw, Func<string, bool, string> FromRaw);

        private static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    }
}
