using System.Globalization;

namespace Moonwell.GameData;

/// <summary>How the values of a choice type make one field value.</summary>
public enum ChoiceForm
{
    /// <summary>The field holds one of the values.</summary>
    One,

    /// <summary>The field holds any of the values, as a comma list of their texts; value n of the list is bit n of the enum.</summary>
    List,

    /// <summary>The field holds any of the values as a whole number: value <c>n</c> (its text) is bit n of the number.</summary>
    Bits,
}

/// <summary>One value of a choice type: one member of its enum.</summary>
/// <param name="Name">The member's C# name.</param>
/// <param name="Value">The member's number: the game's number for a value the data spells as one, its bit for flags, else its place in World Editor's list.</param>
/// <param name="Text">How the game's data and the object file spell the value: the editor's text, or the number for a value stored as a number.</param>
/// <param name="Alternates">The other spellings the game's data uses for the value; empty when none.</param>
/// <param name="Label">The editor's label for the value, without its accelerator mark (<c>&amp;</c>); empty when the tables have none.</param>
public sealed record ChoiceMember(string Name, int Value, string Text, IReadOnlyList<string> Alternates, string Label);

/// <summary>A metadata type whose values World Editor offers as a list (<c>attackType</c>, <c>targetList</c>), as a C# enum.</summary>
/// <param name="Type">The metadata type, which names its section of <c>ui/uniteditordata.txt</c>.</param>
/// <param name="Name">The enum's C# name.</param>
/// <param name="Form">How the values make one field value.</param>
/// <param name="Store">How an object file stores a field of the type: <c>int</c> or <c>string</c>.</param>
/// <param name="Members">The values, in World Editor's order.</param>
public sealed record ChoiceType(string Type, string Name, ChoiceForm Form, string Store, IReadOnlyList<ChoiceMember> Members)
{
    /// <summary>True for a <c>[Flags]</c> enum: the field holds any of the values.</summary>
    public bool Flags => Form != ChoiceForm.One;

    /// <summary>True when the enum is core Moonwell's (<c>Moonwell.AttackType</c>), not one of <c>Moonwell.Objects</c>: see <see cref="Choices.IsCore"/>.</summary>
    public bool Core => Choices.IsCore(Type);

    /// <summary>True for a placement type, whose members are named as World Editor shows them: see <see cref="Choices.IsPlacement"/>.</summary>
    public bool Placement => Choices.IsPlacement(Type);

    /// <summary>True when World Editor lists fewer values than the core enum has: see <see cref="Choices.IsPartial"/>.</summary>
    public bool Partial => Choices.IsPartial(Type);

    /// <summary>Whether a value's text is a code, so that its member is named by its label: see <see cref="Choices.IsCoded"/>.</summary>
    public bool IsCoded(string text) => Choices.IsCoded(Type, text);

    /// <summary>The enum as generated C# names it: <c>global::Moonwell.AttackType</c>, <c>global::Moonwell.Objects.Targets</c>.</summary>
    public string TypeName => (Core ? "global::Moonwell." : "global::Moonwell.Objects.") + Name;
}

/// <summary>The choice types of the tables: every metadata type a field uses that <c>ui/uniteditordata.txt</c> has a section for.</summary>
/// <remarks>
/// <para>
/// A member is named by its text (<c>magic</c> is <c>Magic</c>), with the word breaks of its label when the label
/// spells the same letters (<c>notself</c>, labelled <c>Not self</c>, is <c>NotSelf</c>); a text with a digit or no
/// letter (<c>AS01</c>, <c>3</c>, <c>_</c>) is named by its label alone, and so is a text that is a code
/// (<see cref="IsCoded"/>: <c>CLPB</c> is <c>ChainLightningPrimary</c>). A placement type's enum and members are named
/// as World Editor shows them (<see cref="IsPlacement"/>).
/// </para>
/// <para>
/// A value the data spells as a whole number has that number (the game's numbers of <c>defenseTypeInt</c> are not in
/// World Editor's order); another value of a single choice has its place in the list. A flags enum has a bit for each
/// value: bit n for the list's value n, or for a type stored as a number (<c>morphFlags</c>), the bit whose number the
/// value's text is. The game's Lua has 32-bit integers, so a flags type of more than 31 values is an error.
/// </para>
/// </remarks>
public static class Choices
{
    private const int LabelDepth = 8;
    private const int MostBits = 31;

    // The types whose fields hold a comma list of values although their name has no List: the standard data spells a
    // unit's classification (utyp) as "undead,Mechanical" and the stat modifiers of ASde (Asdf) as "AS01,AS02,...".
    private static readonly HashSet<string> ListTypes = new(StringComparer.Ordinal) { "unitClass", "abilityModifierStats" };

    // A type stored as a number whose name ends in Flags but is one choice, not bits: fullFlags' values Never, Life
    // Only, Mana Only and Always are the numbers 0 to 3, and Rejuvenation's standard 3 is Always.
    private static readonly HashSet<string> NumberedFlags = new(StringComparer.Ordinal) { "fullFlags" };

    // The types whose enums are core Moonwell's: the damage package's hits hold them too, and it cannot reference
    // Moonwell.Objects (CSharp.lua problem C19), so a map has one of each, in namespace Moonwell.
    private static readonly HashSet<string> CoreTypes = new(StringComparer.Ordinal) { "attackType", "combatSound", "defenseType" };

    // The core types World Editor lists only some values of: the game's natives have more sounds than its list has. The enum
    // holds them all, so that a hit can make any, and a field takes the listed ones (decision 97).
    private static readonly HashSet<string> PartialTypes = new(StringComparer.Ordinal) { "combatSound" };

    // The core types named otherwise than their metadata type.
    private static readonly Dictionary<string, string> CoreNames = new(StringComparer.Ordinal) { ["combatSound"] = "WeaponSound" };

    // The types whose values the game's data spells as codes (decision 96): rhpx, CLPB, aline, STR.
    private static readonly HashSet<string> CodedTypes = new(StringComparer.Ordinal)
    {
        "attributeType", "lightningEffect", "moveType", "upgradeEffect", "weaponType",
    };

    // The values that are codes in types whose other values read well (decision 96): World Editor calls standon Walkable.
    private static readonly HashSet<string> CodedValues = new(StringComparer.Ordinal) { "fort", "nonsapper", "peon", "sapper", "standon" };

    // The placement types and the names of their enums (decision 70). The game stores World Editor's "Placement Requires"
    // (upap) as preventPlace, of type pathingListPrevent, and "Placement Prevented By" (upar) as requirePlace, of type
    // pathingListRequire; World Editor shows each value as its opposite ("unbuildable" as "Buildable", "blighted" as
    // "Unblighted"). Each enum is named for the field World Editor shows, and its members by World Editor's labels; the
    // object file keeps the game's texts, so PlacementRequires = PathingRequire.Buildable writes "unbuildable".
    private static readonly Dictionary<string, string> PlacementTypes = new(StringComparer.Ordinal)
    {
        ["pathingListPrevent"] = "PathingRequire",
        ["pathingListRequire"] = "PathingPrevent",
    };

    /// <summary>
    /// Whether the enum of a metadata type is core Moonwell's (<c>attackType</c> is <c>Moonwell.AttackType</c>), written by
    /// hand in <c>src/Moonwell/Types</c> and compiled to the map's Lua with its names, rather than generated into
    /// <c>Moonwell.Objects</c>. The fields hold the core enum, and <c>Moonwell.Objects</c> keeps the game's texts of its
    /// members (<c>ObjectsWriter.Choices</c> in Moonwell's generator).
    /// </summary>
    public static bool IsCore(string type)
    {
        return CoreTypes.Contains(type);
    }

    /// <summary>
    /// Whether a metadata type is one of the placement fields' (<c>pathingListPrevent</c>, <c>pathingListRequire</c>), whose
    /// values World Editor shows as their opposites: its enum is named for the field World Editor shows (<c>PathingRequire</c>
    /// for Placement Requires) and each member by World Editor's label (<c>Buildable</c> for the game's <c>unbuildable</c>).
    /// </summary>
    public static bool IsPlacement(string type)
    {
        return PlacementTypes.ContainsKey(type);
    }

    /// <summary>
    /// Whether World Editor lists fewer values of a metadata type than its core enum has (<c>combatSound</c> has 13 of
    /// <c>Moonwell.WeaponSound</c>'s 24): the members take the core enum's numbers, and a field allows only those.
    /// </summary>
    public static bool IsPartial(string type)
    {
        return PartialTypes.Contains(type);
    }

    /// <summary>Whether the game's data spells a value as a code rather than a word, so that its member is named by its label: <c>rhpx</c> is <c>HitPointBonus</c>.</summary>
    public static bool IsCoded(string type, string text)
    {
        return CodedTypes.Contains(type) || CodedValues.Contains(text);
    }

    /// <summary>
    /// Fails unless the core enum of <paramref name="choice"/> has the members the game's data gives, by name and number in
    /// its order: the generated fields and the game's texts take the data's.
    /// </summary>
    /// <param name="choice">A core choice type.</param>
    /// <param name="members">The core enum's members in order (<c>RuntimeMembers.EnumMembers</c> in Moonwell's generator); null when Moonwell has no such enum.</param>
    /// <exception cref="InvalidOperationException">The members differ, or there is no enum.</exception>
    public static void CheckCore(ChoiceType choice, IReadOnlyList<(string Name, int Value)>? members)
    {
        var expected = choice.Members.Select(member => (member.Name, member.Value)).ToList();
        var given = string.Join(", ", expected.Select(member => $"{member.Name} = {member.Value.ToString(CultureInfo.InvariantCulture)}"));
        if (members == null)
        {
            throw new InvalidOperationException($"{choice.Type}: Moonwell has no enum {choice.Name}; the game's data gives {given}");
        }

        if (!members.SequenceEqual(expected))
        {
            var has = string.Join(", ", members.Select(member => $"{member.Name} = {member.Value.ToString(CultureInfo.InvariantCulture)}"));
            throw new InvalidOperationException($"{choice.Type}: Moonwell.{choice.Name} has {has}; the game's data gives {given}");
        }
    }

    /// <summary>
    /// The choice types of every metadata type a field of any kind uses, by type. An enum of <c>Moonwell.Objects</c> whose
    /// name is in <paramref name="taken"/> (a type of namespace <c>Moonwell</c> or a member the standard classes inherit,
    /// either of which would hide it) gets <c>Choice</c> appended; a core enum (<see cref="IsCore"/>) is that type of
    /// namespace <c>Moonwell</c> and keeps its name.
    /// </summary>
    public static IReadOnlyDictionary<string, ChoiceType> All(GameTables tables, IReadOnlySet<string>? taken)
    {
        var all = new SortedDictionary<string, ChoiceType>(StringComparer.Ordinal);
        foreach (var kind in Enum.GetValues<Kind>())
        {
            foreach (var row in tables.Meta(kind))
            {
                var type = row.Columns.TryGetValue("type", out var cell) ? cell.Trim() : "";
                if (all.ContainsKey(type))
                {
                    continue;
                }

                var choice = Of(tables, type, Fields.StoreOf(type));
                if (choice != null)
                {
                    all[type] = !choice.Core && taken != null && taken.Contains(choice.Name) ? choice with { Name = choice.Name + "Choice" } : choice;
                }
            }
        }

        return all;
    }

    /// <summary>The choice type of a metadata type stored as <paramref name="store"/>; null when <c>ui/uniteditordata.txt</c> has no section for it.</summary>
    public static ChoiceType? Of(GameTables tables, string type, string store)
    {
        var entries = tables.Choices(type);
        return entries == null ? null : Make(type, store, entries, key => LabelOf(tables, key));
    }

    /// <summary>The choice type of a section's entries; <paramref name="label"/> gives the English label of a label key.</summary>
    /// <exception cref="InvalidOperationException">A flags type has more than 31 values, or two values get one name.</exception>
    public static ChoiceType Make(string type, string store, IReadOnlyList<ChoiceEntry> entries, Func<string, string> label)
    {
        var form = FormOf(type, store);
        var numbered = entries.All(e => IsWhole(e.Text));
        var members = new List<ChoiceMember>(entries.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            int value;
            var text = entry.Text;
            switch (form)
            {
                case ChoiceForm.List:
                    value = Bit(type, entry.Index);
                    break;
                case ChoiceForm.Bits:
                    value = Bit(type, IsWhole(entry.Text) ? int.Parse(entry.Text, CultureInfo.InvariantCulture) : -1);
                    text = value.ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    value = numbered ? int.Parse(entry.Text, CultureInfo.InvariantCulture) : entry.Index;
                    break;
            }

            var shown = Shown(entry.Label.Length == 0 ? "" : label(entry.Label));
            var name = IsPlacement(type) ? FieldNames.FromLabel(shown)
                : IsCoded(type, entry.Text) && shown.Length > 0 ? CodedName(shown)
                : MemberName(entry.Text, shown);
            if (name.Length == 0)
            {
                throw new InvalidOperationException($"{type}: the value {entry.Text} has no name; it has no letter and no label");
            }

            if (!names.Add(name))
            {
                throw new InvalidOperationException($"{type}: two values are named {name}");
            }

            members.Add(new ChoiceMember(name, value, text, form == ChoiceForm.Bits ? Array.Empty<string>() : entry.Alternates, shown));
        }

        return new ChoiceType(type, EnumName(type), form, store, members);
    }

    /// <summary>
    /// The choice types with a member added for each bit that a standard value of a type stored as bits
    /// (<see cref="ChoiceForm.Bits"/>) holds and World Editor's list lacks: the game's own data uses it, so a map may
    /// write it too (<c>versionFlags</c> 6 on 20 units is The Frozen Throne and bit 4). The member is named
    /// <c>Unlisted</c> and its number (<c>Unlisted4</c>) and comes after the listed ones. Other types are returned as
    /// they are.
    /// </summary>
    /// <param name="choices">The choice types by type.</param>
    /// <param name="kinds">Each kind's fields and standard objects.</param>
    /// <exception cref="InvalidOperationException">A standard value needs bit 31 or more.</exception>
    public static IReadOnlyDictionary<string, ChoiceType> WithStandardBits(
        IReadOnlyDictionary<string, ChoiceType> choices,
        IEnumerable<(IReadOnlyList<Field> Fields, IReadOnlyList<StandardObject> Objects)> kinds)
    {
        var unlisted = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (fields, objects) in kinds)
        {
            var bits = fields.Where(f => f.Choice is { Form: ChoiceForm.Bits }).ToDictionary(f => f.Id, f => f.Choice!, StringComparer.Ordinal);
            foreach (var obj in objects)
            {
                foreach (var values in new[] { obj.Values }.Concat(obj.Sets.Values))
                {
                    foreach (var (key, value) in values)
                    {
                        var colon = key.IndexOf(':');
                        if (bits.TryGetValue(colon < 0 ? key : key.Substring(0, colon), out var choice)
                            && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                        {
                            var listed = choice.Members.Aggregate(0, (all, member) => all | member.Value);
                            var left = number & ~listed;
                            if (left != 0)
                            {
                                unlisted[choice.Type] = unlisted.GetValueOrDefault(choice.Type) | left;
                            }
                        }
                    }
                }
            }
        }

        var result = new SortedDictionary<string, ChoiceType>(StringComparer.Ordinal);
        foreach (var (type, choice) in choices)
        {
            if (!unlisted.TryGetValue(type, out var left))
            {
                result[type] = choice;
                continue;
            }

            var members = choice.Members.ToList();
            for (var n = 0; n < 32; n++)
            {
                if ((left & (1 << n)) != 0)
                {
                    var value = Bit(type, n);
                    var text = value.ToString(CultureInfo.InvariantCulture);
                    members.Add(new ChoiceMember("Unlisted" + text, value, text, Array.Empty<string>(), "A bit World Editor does not list, which the game's standard data uses"));
                }
            }

            result[type] = choice with { Members = members };
        }

        return result;
    }

    /// <summary>
    /// The choice types with the members of a partial type (<see cref="IsPartial"/>) numbered as its core enum numbers
    /// them, and the core enum's <c>None</c> (0) added last as an empty text: the game's data leaves <c>combatSound</c>
    /// empty on 581 of its 929 weapons (the missile units), and the fields can be empty. Other types are returned as
    /// they are.
    /// </summary>
    /// <param name="choices">The choice types by type.</param>
    /// <param name="core">The members of a core enum by its name (<c>RuntimeMembers.EnumMembers</c> in Moonwell's generator); null when Moonwell has no such enum.</param>
    /// <exception cref="InvalidOperationException">There is no such enum, or it lacks a member.</exception>
    public static IReadOnlyDictionary<string, ChoiceType> WithCoreNumbers(
        IReadOnlyDictionary<string, ChoiceType> choices,
        Func<string, IReadOnlyList<(string Name, int Value)>?> core)
    {
        var result = new SortedDictionary<string, ChoiceType>(StringComparer.Ordinal);
        foreach (var (type, choice) in choices)
        {
            if (!choice.Partial)
            {
                result[type] = choice;
                continue;
            }

            var numbers = core(choice.Name) ?? throw new InvalidOperationException($"{type}: Moonwell has no enum {choice.Name}");
            var members = new List<ChoiceMember>(choice.Members.Count);
            foreach (var member in choice.Members)
            {
                var found = numbers.Where(number => number.Name == member.Name).ToList();
                if (found.Count == 0)
                {
                    throw new InvalidOperationException($"{type}: Moonwell.{choice.Name} has no member {member.Name}");
                }

                members.Add(member with { Value = found[0].Value });
            }

            if (numbers.Contains(("None", 0)))
            {
                members.Add(new ChoiceMember("None", 0, "", Array.Empty<string>(), "None"));
            }

            result[type] = choice with { Members = members };
        }

        return result;
    }

    /// <summary>
    /// The enum's name: the type's in PascalCase, and a type ending in List names its values in the plural (<c>targetList</c>
    /// is <c>Targets</c>); a placement type's is the field's World Editor shows (<c>pathingListPrevent</c> is <c>PathingRequire</c>),
    /// and a core type may have a name of its own (<c>combatSound</c> is <c>WeaponSound</c>).
    /// </summary>
    public static string EnumName(string type)
    {
        if (PlacementTypes.TryGetValue(type, out var placement))
        {
            return placement;
        }

        if (CoreNames.TryGetValue(type, out var core))
        {
            return core;
        }

        var name = type.EndsWith("List", StringComparison.Ordinal) ? type.Substring(0, type.Length - 4) + "s" : type;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private static ChoiceForm FormOf(string type, string store)
    {
        if (type.Contains("List", StringComparison.Ordinal) || ListTypes.Contains(type))
        {
            return ChoiceForm.List;
        }

        return store == "int" && type.EndsWith("Flags", StringComparison.Ordinal) && !NumberedFlags.Contains(type) ? ChoiceForm.Bits : ChoiceForm.One;
    }

    private static int Bit(string type, int number)
    {
        if (number < 0 || number >= MostBits)
        {
            throw new InvalidOperationException(
                $"{type}: a flags value needs bit {number}, and the game's 32-bit integers hold bits 0 to {MostBits - 1} (at most {MostBits} values)");
        }

        return 1 << number;
    }

    private static bool IsWhole(string text)
    {
        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
    }

    // A code's name from its label. "Hit Point Bonus (%)" stays apart from "Hit Point Bonus", and the game's misspelled
    // "Forked Lighting" is corrected.
    private static string CodedName(string label)
    {
        return FieldNames.FromLabel(label.Replace("%", "Percent", StringComparison.Ordinal).Replace("Lighting", "Lightning", StringComparison.Ordinal));
    }

    private static string MemberName(string text, string label)
    {
        var fromText = FieldNames.FromLabel(text);
        var fromLabel = FieldNames.FromLabel(label);
        if (fromText.Length == 0 || fromText.TrimStart('_').Length == 0 || text.Any(char.IsAsciiDigit))
        {
            return fromLabel;
        }

        return fromLabel.Length > 0 && string.Equals(fromLabel, fromText, StringComparison.OrdinalIgnoreCase) ? fromLabel : fromText;
    }

    // A label as it reads: without the accelerator mark, and "Air, &Air" (a name and its menu entry) once.
    private static string Shown(string label)
    {
        var shown = label.Replace("&", "", StringComparison.Ordinal).Trim();
        var comma = shown.IndexOf(", ", StringComparison.Ordinal);
        if (comma > 0 && shown.Substring(0, comma) == shown.Substring(comma + 2))
        {
            return shown.Substring(0, comma);
        }

        return shown;
    }

    // A label key looked up as Fields does: a label may name another label. Empty when the tables have none.
    private static string LabelOf(GameTables tables, string key)
    {
        var label = key;
        for (var i = 0; i < LabelDepth; i++)
        {
            var next = tables.Label(label);
            if (next == label)
            {
                break;
            }

            label = next;
        }

        return label == key ? "" : label;
    }
}
