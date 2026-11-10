using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Moonwell.GameData;
using Moonwell.MapFiles;

namespace Moonwell.Build
{
    /// <summary>
    /// The second half of <c>moonwell import</c> (decision 257): the map's object data as C# classes. Each object of the map's
    /// object files becomes a declared class in <c>Objects/</c>, a new one with its <c>[ObjectId]</c> and a changed standard one
    /// <c>[Replaces]</c>, each field set as the standard classes have it (a property, <c>Set(field, level, value)</c> for one
    /// value per level, a list or reference by its classes), and leaves the files. An object with a field the classes cannot
    /// say (no such field, an id with no class, a choice no value of its enum, an emptied list) stays whole in the files,
    /// which the build keeps beside the declared ones, and the log names it with why.
    /// </summary>
    public static class ObjectImport
    {
        private const string Folder = "Objects";

        // The field holding each kind's name, which names the class.
        private static readonly Dictionary<Kind, string> NameFields = new Dictionary<Kind, string>
        {
            [Kind.Unit] = "unam",
            [Kind.Item] = "unam",
            [Kind.Destructable] = "bnam",
            [Kind.Doodad] = "dnam",
            [Kind.Ability] = "anam",
            [Kind.Buff] = "fnam",
            [Kind.Upgrade] = "gnam",
        };

        /// <summary>Converts the project's object files; the project must build, as the standard classes are read from its copy of Moonwell.Objects.</summary>
        /// <param name="projectFolder">The project <c>moonwell import</c> made.</param>
        /// <param name="log">Where what was converted and what stayed is told.</param>
        public static void Run(string projectFolder, TextWriter log)
        {
            var project = MapProject.Find(projectFolder);
            var placed = PlacedWriter.Read(project);
            PlacedWriter.Write(project, placed, false);
            LuaLibraries.WriteBindings(project);
            DataWriter.Write(project);
            var targetPath = DotnetBuild.Run(project, false, TextWriter.Null);
            var objectsDll = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetPath))!, Declarations.ObjectsAssemblyName + ".dll");
            var context = new MapLoadContext(Path.GetDirectoryName(objectsDll)!);
            try
            {
                Convert(project, new Standards(context.Open(objectsDll)), log);
            }
            finally
            {
                context.Unload();
            }
        }

        /// <summary>Converts the project's object files with the standard classes given.</summary>
        internal static void Convert(MapProject project, Standards standards, TextWriter log)
        {
            var texts = MapTypes.TextsOf(War3Net.Build.Map.Open(project.MapFolder, War3Net.Build.MapFiles.TriggerStrings).TriggerStrings);
            var files = new List<(Kind Kind, string Path, ObjectFile File)>();
            foreach (var kind in Enum.GetValues<Kind>())
            {
                foreach (var name in new[] { Kinds.MainName(kind), Kinds.SkinName(kind) })
                {
                    var path = Path.Combine(project.MapFolder, name);
                    if (File.Exists(path))
                    {
                        files.Add((kind, path, ObjectFile.ReadLenient(kind, File.ReadAllBytes(path), name)));
                    }
                }
            }

            // Every object of the files, the main and the skin file's halves together, named before any is written, as one may
            // name another in a list.
            var objects = new List<MapObject>();
            foreach (var (kind, _, file) in files)
            {
                foreach (var (entry, isNew) in file.Original.Select(one => (one, false)).Concat(file.Custom.Select(one => (one, true))))
                {
                    var id = SourceObjectFile.IdOf(entry);
                    var found = objects.Find(one => one.Kind == kind && one.Id == id);
                    if (found == null)
                    {
                        found = new MapObject(kind, id, entry.BaseId.Length > 0 ? entry.BaseId : id, isNew);
                        objects.Add(found);
                    }

                    found.Entries.Add((file, entry));
                }
            }

            var namespaceName = project.Name + ".Objects";
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var classes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var one in objects)
            {
                if (standards.Find(one.BaseId) is { } baseType)
                {
                    one.Base = baseType;
                    var name = one.Entries.SelectMany(part => part.Entry.Mods).FirstOrDefault(mod => mod.Field == NameFields[one.Kind])?.Value is FieldValue.Text text
                        ? MapTypes.TextOf(text.Value, texts)
                        : baseType.Name;
                    one.ClassName = Unique(taken, ClassNameOf(name, baseType.Name), one.Id);
                    if (one.IsNew)
                    {
                        classes[one.Id] = "global::" + namespaceName + "." + one.ClassName;
                    }
                }
            }

            // Each buff's carrier takes an ability id nothing else has: AZ00, AZ01 and on.
            var used = new HashSet<string>(objects.Select(one => one.Id), StringComparer.Ordinal);
            const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var next = 0;
            foreach (var buff in objects.Where(one => one.Kind == Kind.Buff))
            {
                string id;
                do
                {
                    id = "AZ" + Digits[next / Digits.Length % Digits.Length] + Digits[next % Digits.Length];
                    next++;
                }
                while (used.Contains(id) || standards.Find(id) != null);
                used.Add(id);
                buff.Carrier = id;
            }

            // An object that names one kept in the files has no class to name it by, so it stays too, until none more does.
            var bodies = new Dictionary<MapObject, string>();
            var kept = new Dictionary<MapObject, string>();
            for (var changed = true; changed;)
            {
                changed = false;
                bodies.Clear();
                foreach (var one in objects.Where(one => !kept.ContainsKey(one)))
                {
                    var why = one.Base == null ? $"its base {one.BaseId} has no standard class" : null;
                    var body = why == null ? Body(one, standards, classes, texts, out why) : null;
                    if (body == null)
                    {
                        kept[one] = why!;
                        classes.Remove(one.Id);
                        changed = true;
                    }
                    else
                    {
                        bodies[one] = body;
                    }
                }
            }

            foreach (var (one, why) in kept)
            {
                log.WriteLine($"moonwell: kept {one.Id} in the object files: {why}");
            }

            var written = new Dictionary<Kind, StringBuilder>();
            var converted = 0;
            foreach (var (one, body) in bodies)
            {
                if (!written.TryGetValue(one.Kind, out var text))
                {
                    written[one.Kind] = text = new StringBuilder();
                }

                text.Append(body);
                foreach (var (file, entry) in one.Entries)
                {
                    file.Remove(entry);
                }

                converted++;
            }

            foreach (var (kind, path, file) in files)
            {
                if (file.Original.Count == 0 && file.Custom.Count == 0)
                {
                    File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, file.ToBytes());
                }
            }

            Directory.CreateDirectory(Path.Combine(project.Folder, Folder));
            foreach (var (kind, text) in written)
            {
                File.WriteAllText(
                    Path.Combine(project.Folder, Folder, (kind == Kind.Ability ? "Abilities" : kind + "s") + ".cs"),
                    $"using Moonwell.Objects;\n\nnamespace {namespaceName}\n{{\n{text.ToString().TrimEnd('\n')}\n}}\n");
            }

            log.WriteLine($"moonwell: {converted} of {objects.Count} objects are C# classes in {Folder}");
        }

        // One class, or null with why when a field cannot be said.
        private static string? Body(MapObject one, Standards standards, IReadOnlyDictionary<string, string> classes, IReadOnlyDictionary<uint, string> texts, out string? why)
        {
            var fields = standards.FieldsOf(one.Base!);
            var plain = new List<string>();
            var levelled = new List<string>();
            var beyond = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var mod in one.Entries.SelectMany(part => part.Entry.Mods))
            {
                if (!fields.TryGetValue(mod.Field, out var field))
                {
                    why = $"{one.Base!.Name} has no field {mod.Field}";
                    return null;
                }

                string? line;
                if (field.Held.IsGenericType && field.Held.GetGenericTypeDefinition().Name.StartsWith("Levels", StringComparison.Ordinal))
                {
                    var element = field.Held.GetGenericArguments()[0];
                    line = Literal(element, mod.Value, texts, standards) is { } value ? $"Set(\"{mod.Field}\", {Math.Max(1, mod.Level)}, {value});" : null;
                    if (line != null && field.Property != null && OutOfLimits(field, mod.Value))
                    {
                        beyond.Add(field.Property);
                    }

                    if (line != null)
                    {
                        levelled.Add(line);
                    }
                }
                else if (field.Held.IsGenericType && field.Held.GetGenericTypeDefinition().Name.StartsWith("Object", StringComparison.Ordinal))
                {
                    line = field.Property == null ? null : References(field.Property, field.Held, mod.Value, classes, standards);
                    if (line != null)
                    {
                        plain.Add(line);
                    }
                }
                else
                {
                    line = Literal(field.Held, mod.Value, texts, standards) is { } value
                        ? field.Property != null ? $"{field.Property} = {value};" : $"Set(\"{mod.Field}\", {value});"
                        : null;
                    if (line != null && field.Property != null && OutOfLimits(field, mod.Value))
                    {
                        beyond.Add(field.Property);
                    }

                    if (line != null)
                    {
                        plain.Add(line);
                    }
                }

                if (line == null)
                {
                    why = $"its field {mod.Field} holds {Show(mod.Value)}, which {field.Held.Name} cannot say";
                    return null;
                }
            }

            why = null;
            var text = new StringBuilder();
            text.Append($"    /// <summary>{Escape(one.ClassName!)} ({one.Id}), from the old map's object data.</summary>\n");
            // A declared buff needs the id of its carrier, the hidden ability that shows its icon; the old map had none.
            var carrier = one.Kind == Kind.Buff ? $"Carrier = \"{one.Carrier}\"" : null;
            text.Append(one.IsNew
                ? $"    [ObjectId(\"{one.Id}\"{(carrier == null ? string.Empty : ", " + carrier)})]\n"
                : "    [Replaces]\n" + (carrier == null ? string.Empty : $"    [ObjectId({carrier})]\n"));
            if (beyond.Count > 0)
            {
                text.Append($"    [BeyondLimits({string.Join(", ", beyond.Select(name => "\"" + name + "\""))})]\n");
            }

            text.Append($"    public sealed class {one.ClassName} : {GlobalName(one.Base!)}\n    {{\n");
            text.Append($"        public {one.ClassName}()\n        {{\n");
            foreach (var line in plain.Concat(levelled))
            {
                text.Append("            ").Append(line).Append('\n');
            }

            text.Append("        }\n    }\n\n");
            return text.ToString();
        }

        // A list or reference set by its classes: the map's own new objects by theirs, the rest by their standard class.
        private static string? References(string property, Type held, FieldValue value, IReadOnlyDictionary<string, string> classes, Standards standards)
        {
            if (value is not FieldValue.Text text)
            {
                return null;
            }

            var names = new List<string>();
            foreach (var id in text.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (classes.TryGetValue(id, out var own))
                {
                    names.Add(own);
                }
                else if (standards.Find(id) is { } standard)
                {
                    names.Add(GlobalName(standard));
                }
                else
                {
                    return null;
                }
            }

            // An emptied list is cleared; an emptied reference cannot be told from one never set, so it stays in the files.
            if (names.Count == 0)
            {
                return held.Name.StartsWith("ObjectList", StringComparison.Ordinal) ? $"{property}.Clear();" : null;
            }

            var lines = new List<string> { $"{property}.Set<{string.Join(", ", names.Take(8))}>();" };
            lines.AddRange(names.Skip(8).Select(name => $"{property}.Add<{name}>();"));
            return string.Join("\n            ", lines);
        }

        // A value as a C# literal of the type the field holds; null when the type cannot hold it.
        private static string? Literal(Type held, FieldValue value, IReadOnlyDictionary<uint, string> texts, Standards standards)
        {
            var raw = value switch
            {
                FieldValue.Int one => one.Value.ToString(CultureInfo.InvariantCulture),
                FieldValue.Real one => one.Value.ToString("R", CultureInfo.InvariantCulture),
                FieldValue.Unreal one => one.Value.ToString("R", CultureInfo.InvariantCulture),
                FieldValue.Text one => MapTypes.TextOf(one.Value, texts),
                FieldValue.Bool one => one.Value ? "1" : "0",
                FieldValue.Char one => one.Value.ToString(),
                _ => null,
            };
            if (raw == null)
            {
                return null;
            }

            if (held == typeof(string))
            {
                return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(raw, true);
            }

            if (held == typeof(int))
            {
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) ? whole.ToString(CultureInfo.InvariantCulture)
                    : float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var part) ? ((int)part).ToString(CultureInfo.InvariantCulture) : null;
            }

            if (held == typeof(float))
            {
                return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number.ToString("R", CultureInfo.InvariantCulture) + "f" : null;
            }

            if (held == typeof(bool))
            {
                return raw is "1" or "true" ? "true" : raw is "0" or "false" ? "false" : null;
            }

            if (held.IsEnum)
            {
                return EnumLiteral(held, raw, standards);
            }

            return null;
        }

        // A choice by its game value (Metal), or flags joined from their game values (ground,structure), as the enum's members say.
        // An enum of core Moonwell (DefenseType) has no game values; Moonwell.Objects' own enum of its name has them, member for
        // member by number.
        private static string? EnumLiteral(Type held, string raw, Standards standards)
        {
            var members = MembersOf(held);
            if (members.All(one => one.Texts.Count == 0) && standards.MirrorOf(held) is { } mirror)
            {
                var byNumber = members.GroupBy(one => NumberOf(one.Member)).ToDictionary(group => group.Key, group => group.First().Member);
                members = MembersOf(mirror).Where(one => byNumber.ContainsKey(NumberOf(one.Member))).Select(one => (byNumber[NumberOf(one.Member)], one.Texts)).ToList();
            }

            string? Find(string token) => members.FirstOrDefault(one => one.Texts.Any(text => string.Equals(text, token, StringComparison.OrdinalIgnoreCase))).Member?.Name
                ?? (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? members.FirstOrDefault(one => NumberOf(one.Member) == number).Member?.Name : null);

            var type = GlobalName(held);
            if (held.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name == "FlagsAttribute"))
            {
                var tokens = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(token => token != "_").ToList();
                if (tokens.Count == 0)
                {
                    return $"({type})0";
                }

                // Flags the file holds as their number.
                if (tokens.Count == 1 && int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits))
                {
                    return $"({type}){bits.ToString(CultureInfo.InvariantCulture)}";
                }

                var names = tokens.Select(Find).ToList();
                return names.Any(name => name == null) ? null : string.Join(" | ", names.Select(name => type + "." + name));
            }

            return Find(raw.Trim()) is { } found ? type + "." + found : null;
        }

        private static List<(FieldInfo Member, List<string> Texts)> MembersOf(Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(member => (member, member.GetCustomAttributesData()
                    .Where(attribute => attribute.AttributeType.Name == "GameValueAttribute")
                    .SelectMany(attribute => attribute.ConstructorArguments.SelectMany(argument => argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> alternates
                        ? alternates.Select(item => item.Value as string)
                        : new[] { argument.Value as string }))
                    .Where(text => text != null).Select(text => text!).ToList()))
                .ToList();
        }

        private static long NumberOf(FieldInfo member) => System.Convert.ToInt64(member.GetRawConstantValue(), CultureInfo.InvariantCulture);

        private static bool OutOfLimits(Field field, FieldValue value)
        {
            double? number = value switch
            {
                FieldValue.Int one => one.Value,
                FieldValue.Real one => one.Value,
                FieldValue.Unreal one => one.Value,
                _ => null,
            };
            return number is { } known && (known < field.Min || known > field.Max);
        }

        // A class name from an object's name: its words in PascalCase, colour codes and anything not a letter or digit dropped.
        private static string ClassNameOf(string name, string fallback)
        {
            var plain = Regex.Replace(name, @"\|c[0-9A-Fa-f]{8}|\|r|\|n", " ");
            var words = Regex.Split(plain, "[^A-Za-z0-9]+").Where(word => word.Length > 0);
            var joined = string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
            if (joined.Length == 0)
            {
                return fallback;
            }

            return char.IsDigit(joined[0]) ? "_" + joined : joined;
        }

        private static string Unique(HashSet<string> taken, string name, string id)
        {
            var unique = taken.Contains(name) ? name + id : name;
            for (var n = 2; !taken.Add(unique); n++)
            {
                unique = name + id + n.ToString(CultureInfo.InvariantCulture);
            }

            return unique;
        }

        private static string GlobalName(Type type) => "global::" + type.FullName!.Replace('+', '.');

        private static string Show(FieldValue value) => value switch
        {
            FieldValue.Text text => "\"" + text.Value + "\"",
            FieldValue.Int one => one.Value.ToString(CultureInfo.InvariantCulture),
            FieldValue.Real one => one.Value.ToString("R", CultureInfo.InvariantCulture),
            FieldValue.Unreal one => one.Value.ToString("R", CultureInfo.InvariantCulture),
            FieldValue.Bool one => one.Value ? "true" : "false",
            FieldValue.Char one => "'" + one.Value + "'",
            _ => value.ToString() ?? string.Empty,
        };

        private static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

        /// <summary>One object of the map's files: its halves in the main and the skin file, and the class it becomes.</summary>
        private sealed class MapObject
        {
            public MapObject(Kind kind, string id, string baseId, bool isNew)
            {
                Kind = kind;
                Id = id;
                BaseId = baseId;
                IsNew = isNew;
            }

            public Kind Kind { get; }

            public string Id { get; }

            public string BaseId { get; }

            public bool IsNew { get; }

            public List<(ObjectFile File, ObjectEntry Entry)> Entries { get; } = new List<(ObjectFile, ObjectEntry)>();

            public Type? Base { get; set; }

            public string? ClassName { get; set; }

            // A buff's carrier ability id, free in the map and among the standard objects.
            public string? Carrier { get; set; }
        }

        /// <summary>A field of a standard class: the type it holds, its property (null for one the class sets by id only) and limits.</summary>
        internal sealed record Field(Type Held, string? Property, double Min, double Max);

        /// <summary>The standard classes of a copy of Moonwell.Objects, read by reflection: each by its id, and each one's fields.</summary>
        internal sealed class Standards
        {
            private readonly Dictionary<string, Type> byId = new Dictionary<string, Type>(StringComparer.Ordinal);

            private readonly Assembly objects;

            private readonly Dictionary<Type, Dictionary<string, Field>> fields = new Dictionary<Type, Dictionary<string, Field>>();

            public Standards(Assembly objects)
            {
                this.objects = objects;
                foreach (var type in objects.GetTypes())
                {
                    var id = type.GetCustomAttributesData().FirstOrDefault(attribute => attribute.AttributeType.Name == "StandardAttribute")?.ConstructorArguments[0].Value as string;
                    if (id != null && !type.IsAbstract)
                    {
                        byId.TryAdd(id, type);
                    }
                }
            }

            /// <summary>Moonwell.Objects' own enum of an enum's name, whose members carry the game's texts; null when it has none.</summary>
            public Type? MirrorOf(Type type) => objects.GetTypes().FirstOrDefault(one => one.IsEnum && one != type && one.Name == type.Name);

            /// <summary>The standard class of an id; null for one Moonwell has none of.</summary>
            public Type? Find(string id) => byId.TryGetValue(id, out var type) ? type : null;

            /// <summary>
            /// A standard class's fields by id: the types from its kind's table (a private static <c>fieldTypes</c> on the kind's
            /// class), the properties and limits from the <c>[Field]</c>, <c>[Limits]</c> and <c>[SharedField]</c> of it and its bases.
            /// </summary>
            public Dictionary<string, Field> FieldsOf(Type type)
            {
                if (fields.TryGetValue(type, out var known))
                {
                    return known;
                }

                var held = new Dictionary<string, Type>(StringComparer.Ordinal);
                var names = new Dictionary<string, (string Name, double Min, double Max)>(StringComparer.Ordinal);
                for (var current = type; current != null; current = current.BaseType)
                {
                    if (current.GetField("fieldTypes", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) is System.Collections.IDictionary table)
                    {
                        foreach (System.Collections.DictionaryEntry entry in table)
                        {
                            held.TryAdd((string)entry.Key, (Type)entry.Value!);
                        }
                    }

                    foreach (var shared in current.GetCustomAttributesData().Where(attribute => attribute.AttributeType.Name == "SharedFieldAttribute"))
                    {
                        var hasLimits = shared.NamedArguments.Any(named => named.MemberName == "HasLimits" && named.TypedValue.Value is true);
                        double Named(string member, double otherwise) => shared.NamedArguments.Where(named => named.MemberName == member).Select(named => System.Convert.ToDouble(named.TypedValue.Value, CultureInfo.InvariantCulture)).DefaultIfEmpty(otherwise).First();
                        names.TryAdd((string)shared.ConstructorArguments[0].Value!, ((string)shared.ConstructorArguments[1].Value!, hasLimits ? Named("Min", double.NegativeInfinity) : double.NegativeInfinity, hasLimits ? Named("Max", double.PositiveInfinity) : double.PositiveInfinity));
                    }

                    foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        var attributes = property.GetCustomAttributesData();
                        if (attributes.FirstOrDefault(attribute => attribute.AttributeType.Name == "FieldAttribute")?.ConstructorArguments[0].Value is string id)
                        {
                            var limits = attributes.FirstOrDefault(attribute => attribute.AttributeType.Name == "LimitsAttribute");
                            names.TryAdd(id, (property.Name,
                                limits == null ? double.NegativeInfinity : System.Convert.ToDouble(limits.ConstructorArguments[0].Value, CultureInfo.InvariantCulture),
                                limits == null ? double.PositiveInfinity : System.Convert.ToDouble(limits.ConstructorArguments[1].Value, CultureInfo.InvariantCulture)));
                        }
                    }
                }

                var result = new Dictionary<string, Field>(StringComparer.Ordinal);
                foreach (var (id, holds) in held)
                {
                    result[id] = names.TryGetValue(id, out var name) ? new Field(holds, name.Name, name.Min, name.Max) : new Field(holds, null, double.NegativeInfinity, double.PositiveInfinity);
                }

                fields[type] = result;
                return result;
            }
        }
    }
}
