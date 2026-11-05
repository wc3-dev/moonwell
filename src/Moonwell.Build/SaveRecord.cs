using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Moonwell.Build
{
    /// <summary>
    /// The record file a map with saves keeps beside its project file, committed with it (decision 180): each save and part
    /// class's numbers and types (a part's or an enum's with its class), each save class's id, a fingerprint of the SaveKey
    /// and the save folder. The build stops on a number used again after its field was removed or with another type, and on
    /// a changed id, key or folder, naming the line to delete when the change is meant. A lost record loses only these
    /// checks: it is written anew.
    /// </summary>
    public static class SaveRecord
    {
        /// <summary>The record file's name.</summary>
        public const string FileName = "moonwell-saves.txt";

        private const string Removed = "removed";

        /// <summary>Checks the save classes against the record and writes it up to date.</summary>
        /// <exception cref="BuildException">A number is reused, a type changed, or the key or the folder changed.</exception>
        public static void Keep(MapProject project, IReadOnlyList<SaveClass> classes, string key, string folder)
        {
            var path = Path.Combine(project.Folder, FileName);
            var lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            var fingerprint = Fingerprint(key);
            var fields = new SortedDictionary<(string Owner, int Number), (string Type, bool Removed, int Line)>();
            var ids = new Dictionary<string, (string Id, int Line)>(StringComparer.Ordinal);
            for (var i = 0; i < lines.Length; i++)
            {
                var words = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var line = i + 1;
                if (words.Length == 0 || words[0].StartsWith('#'))
                {
                    continue;
                }

                if (words.Length == 2 && words[0] == "key" && words[1] != fingerprint)
                {
                    throw Stop(line, "the map's SaveKey has changed, so every save made before fails its check and does not load. Change SaveKey back in Map.cs");
                }

                if (words.Length == 2 && words[0] == "folder" && words[1] != folder)
                {
                    throw Stop(line, $"the save folder has changed from {words[1]} to {folder}, so the saves players have in the old folder of CustomMapData do not load. Set SaveFolder = \"{words[1]}\" in Map.cs to keep it");
                }

                // A class's name never starts with a digit, so an id line is told from a field of a save whose id is "id".
                if (words.Length == 3 && words[0] == "id" && !char.IsDigit(words[1][0]))
                {
                    ids[words[1]] = (words[2], line);
                    continue;
                }

                if (words.Length >= 3 && int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                {
                    fields[(words[0], number)] = (words[2], words.Length > 3 && words[3] == Removed, line);
                }
            }

            foreach (var one in classes.Where(one => !one.IsPart))
            {
                if (ids.TryGetValue(one.Name, out var kept) && kept.Id != IdOf(one))
                {
                    throw Stop(kept.Line, $"{one.Name}'s [SaveId] has changed from {Shown(kept.Id)} to {Shown(IdOf(one))}, so every save of it made before fails its check and does not load. Change it back");
                }
            }

            var now = new SortedDictionary<(string Owner, int Number), string>();
            foreach (var one in classes)
            {
                var owner = one.IsPart ? "part:" + one.Name : IdOf(one);
                foreach (var member in one.Members)
                {
                    now[(owner, member.Number)] = member.Type.RecordName;
                    if (!fields.TryGetValue((owner, member.Number), out var kept))
                    {
                        continue;
                    }

                    var name = $"{one.Name}.{member.Name}";
                    if (kept.Removed)
                    {
                        throw Stop(kept.Line, $"{name} is [Saved({member.Number})], a number whose field was removed; saves made before still hold that field's value, which {name} would read. Give it a number never used");
                    }

                    if (!Same(kept.Type, member.Type.RecordName))
                    {
                        throw Stop(kept.Line, $"{name} is saved as {member.Type.RecordName}, and saves made before hold [Saved({member.Number})] as {kept.Type}, so they would read it as its default. Give it a number never used");
                    }
                }
            }

            var text = new StringBuilder();
            text.Append("# Moonwell's record of the map's saves; commit it with the map. The build stops on a change that old saves would not load,\n");
            text.Append("# naming the line to delete when the change is meant.\n");
            text.Append("key ").Append(fingerprint).Append('\n');
            text.Append("folder ").Append(folder).Append('\n');
            foreach (var one in classes.Where(one => !one.IsPart).OrderBy(one => one.Name, StringComparer.Ordinal))
            {
                text.Append("id ").Append(one.Name).Append(' ').Append(IdOf(one)).Append('\n');
            }

            foreach (var entry in fields.Where(entry => !now.ContainsKey(entry.Key)).Select(entry => (entry.Key, Type: entry.Value.Type + " " + Removed))
                .Concat(now.Select(entry => (entry.Key, Type: entry.Value)))
                .OrderBy(entry => entry.Key.Owner, StringComparer.Ordinal).ThenBy(entry => entry.Key.Number))
            {
                text.Append(entry.Key.Owner).Append(' ').Append(entry.Key.Number.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(entry.Type).Append('\n');
            }

            File.WriteAllText(path, text.ToString());
        }

        /// <summary>The key's fingerprint: the first 8 hex digits of its SHA-256, so the record does not repeat the key.</summary>
        public static string Fingerprint(string key)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).Substring(0, 8).ToLowerInvariant();
        }

        private static string IdOf(SaveClass save)
        {
            return save.Id.Length == 0 ? "-" : save.Id;
        }

        private static string Shown(string id)
        {
            return id == "-" ? "none" : "\"" + id + "\"";
        }

        // A type without its class's name, as a record from before 0.6.1 writes a part or an enum, or as an int is, is the same as
        // one of that kind with a name: an int made an enum, or back, reads the same numbers.
        private static bool Same(string kept, string now)
        {
            return kept == now || (kept.Split(':')[0] == now.Split(':')[0] && (!kept.Contains(':') || !now.Contains(':')));
        }

        private static BuildException Stop(int line, string why)
        {
            return new BuildException($"{FileName} line {line}: {why}, or delete line {line} if the change is meant");
        }
    }
}
