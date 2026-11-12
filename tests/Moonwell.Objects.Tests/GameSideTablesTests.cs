using System.Reflection;
using System.Text.RegularExpressions;

namespace Moonwell.Objects.Tests
{
    /// <summary>
    /// A declared class's constructor runs in the game too, against the tables of moonwell.lua, not ObjectList and ObjectRef:
    /// a public member with no entry there builds, then stops the map in the game.
    /// </summary>
    public class GameSideTablesTests
    {
        [Theory]
        [InlineData(typeof(ObjectList<>), "listTable")]
        [InlineData(typeof(ObjectRef<>), "refTable")]
        public void EveryPublicMemberHasAnEntryInTheGameSideTable(Type type, string table)
        {
            var entries = Entries(table);
            var missing = LuaNames(type).Where(name => !entries.Contains(name)).ToList();
            Assert.True(missing.Count == 0, table + " in src/Moonwell/moonwell.lua has no entry for: " + string.Join(", ", missing));
        }

        [Fact]
        public void SetTakesUpToTwelveClasses()
        {
            var units = typeof(ObjectList<>).Assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && !type.IsGenericType && typeof(Moonwell.Unit).IsAssignableFrom(type))
                .Take(12)
                .ToArray();
            var set = typeof(ObjectList<Moonwell.Unit>).GetMethods().Single(method => method.Name == "Set" && method.GetGenericArguments().Length == 12);
            var list = new ObjectList<Moonwell.Unit>();
            set.MakeGenericMethod(units).Invoke(list, null);
            Assert.Equal(units, list.Types);
            list.Clear();
            Assert.Equal(0, list.Count);
        }

        // The names CSharp.lua calls a type's public members by: a property's getter is get and its name, and a method's
        // overloads are numbered in the order they are declared, the internal ones counted too (Set, Set1, Set2).
        private static List<string> LuaNames(Type type)
        {
            const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var names = new List<string>();
            var seen = new Dictionary<string, int>();
            foreach (var method in type.GetMethods(Declared).OrderBy(method => method.MetadataToken))
            {
                if (method.IsPrivate)
                {
                    continue;
                }

                var name = method.Name;
                if (method.IsSpecialName)
                {
                    name = name.StartsWith("get_", StringComparison.Ordinal) ? "get" + name.Substring(4) : "set" + name.Substring(4);
                }
                else
                {
                    seen.TryGetValue(name, out var index);
                    seen[name] = index + 1;
                    name = index == 0 ? name : name + index;
                }

                if (method.IsPublic)
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static HashSet<string> Entries(string table)
        {
            var lua = File.ReadAllText(LuaFile());
            var block = Regex.Match(lua, @"local " + table + @" = \{ __index = \{\r?\n(.*?)\r?\n\} \}", RegexOptions.Singleline);
            Assert.True(block.Success, table + " was not found in moonwell.lua");
            return Regex.Matches(block.Groups[1].Value, @"^  (\w+) = ", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToHashSet();
        }

        private static string LuaFile()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var file = Path.Combine(folder.FullName, "src", "Moonwell", "moonwell.lua");
                if (File.Exists(file))
                {
                    return file;
                }
            }

            throw new InvalidOperationException("src/Moonwell/moonwell.lua is not above " + AppContext.BaseDirectory);
        }
    }
}
