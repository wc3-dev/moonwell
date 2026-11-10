using System;
using System.Globalization;

namespace Moonwell.Objects
{
    /// <summary>
    /// The numbers of a tooltip <c>Describe</c> writes: in a level's text, <c>tip[Damage]</c> is that level's value in the
    /// game's tooltip gold; in the learn tooltip, which tells of every level, it is each level's value as <c>75/150/225</c>, or
    /// one number when every level has the same.
    /// </summary>
    /// @CSharpLua.Ignore
    public sealed class Tip
    {
        private const string Gold = "|cffffcc00";

        private readonly int level;
        private readonly int count;

        /// <summary>The numbers of one level's text, or of the learn tooltip's (level 0) over <paramref name="count"/> levels.</summary>
        internal Tip(int level, int count)
        {
            this.level = level;
            this.count = count;
        }

        /// <summary>The level the text is for, from 1; 0 in the learn tooltip, which tells of every level.</summary>
        public int Level
        {
            get { return level; }
        }

        /// <summary>The value of a field with one for each level: this level's in gold, or every level's in the learn tooltip.</summary>
        public string this[Levels<float> values]
        {
            get { return Show(at => values[at], 1.0); }
        }

        /// <summary>The value of a field with one for each level: this level's in gold, or every level's in the learn tooltip.</summary>
        public string this[Levels<int> values]
        {
            get { return Show(at => values[at], 1.0); }
        }

        /// <summary>A number that is the same at every level, in gold in a level's text.</summary>
        public string Number(float value)
        {
            return Show(at => value, 1.0);
        }

        /// <summary>A fraction as a percent, 0.25 as 25%: this level's in gold, or every level's in the learn tooltip.</summary>
        public string Percent(Levels<float> values)
        {
            return Show(at => values[at], 100.0) + "%";
        }

        /// <summary>A fraction that is the same at every level as a percent, 0.25 as 25%, in gold in a level's text.</summary>
        public string Percent(float value)
        {
            return Show(at => value, 100.0) + "%";
        }

        private string Show(Func<int, double> valueAt, double scale)
        {
            if (level > 0)
            {
                return Gold + Format(valueAt(level) * scale) + "|r";
            }

            var first = Format(valueAt(1) * scale);
            var all = first;
            var same = true;
            for (var at = 2; at <= count; at++)
            {
                var next = Format(valueAt(at) * scale);
                same &= next == first;
                all += "/" + next;
            }

            return same ? first : all;
        }

        // A float widened to double carries noise (0.1f is 0.100000001...): four places keep what a map writes.
        private static string Format(double value)
        {
            return Math.Round(value, 4).ToString(CultureInfo.InvariantCulture);
        }

#if !MOONWELL_LUA
        /// <summary>The titles of an ability's levels: <c>Smite - [Level 2]</c> with the level in gold, or the name alone for a unit's ability of one level, as the game's own.</summary>
        internal static string[] AbilityTitles(string name, int count, bool levelled)
        {
            var titles = new string[count];
            for (var at = 1; at <= count; at++)
            {
                titles[at - 1] = levelled ? name + " - [" + Gold + "Level " + at + "|r]" : name;
            }

            return titles;
        }

        /// <summary>The titles of an upgrade's levels, <c>Upgrade to Steel Forged Swords</c> from each level's name, as the game's own.</summary>
        internal static string[] UpgradeTitles(Levels<string> name, int count)
        {
            var titles = new string[count];
            for (var at = 1; at <= count; at++)
            {
                titles[at - 1] = "Upgrade to " + name[at];
            }

            return titles;
        }

        /// <summary>The learn tooltip's title,<c>Learn Smite - [Level %d]</c>; the game puts the level the hero learns in place of %d.</summary>
        internal static string LearnTitle(string name)
        {
            return "Learn " + name + " - [" + Gold + "Level %d|r]";
        }

        /// <summary>The text of each level, from level 1.</summary>
        internal static string[] Texts(int count, Func<Tip, string> text)
        {
            return Texts(count, text, TipLines.None, null);
        }

        /// <summary>The text of each level, from level 1, with the lines the object's fields give after it.</summary>
        internal static string[] Texts(int count, Func<Tip, string> text, TipLines lines, Func<Tip, string>? linesOf)
        {
            var texts = new string[count];
            for (var at = 1; at <= count; at++)
            {
                texts[at - 1] = Text(new Tip(at, count), text, lines, linesOf);
            }

            return texts;
        }

        /// <summary>The learn tooltip's text: the text with every level's values, and the lines the object's fields give.</summary>
        internal static string Learn(int count, Func<Tip, string> text, TipLines lines, Func<Tip, string>? linesOf)
        {
            return Text(new Tip(0, count), text, lines, linesOf);
        }

        /// <summary>
        /// The lines an ability's fields give, one per flag in <paramref name="lines"/> whose value is not 0, in the order of
        /// <see cref="TipLines"/>: <c>Mana: 75</c>, <c>Cooldown: 8 seconds</c>, <c>Range: 600</c>, <c>Area: 250</c>, <c>Duration: 5 seconds</c>.
        /// </summary>
        internal static string AbilityLines(Tip tip, TipLines lines, Levels<int> manaCost, Levels<float> cooldown, Levels<float> range, Levels<float> area, Levels<float> duration)
        {
            var text = string.Empty;
            text = Line(text, tip, lines, TipLines.ManaCost, "Mana", at => manaCost[at], null);
            text = Line(text, tip, lines, TipLines.Cooldown, "Cooldown", at => cooldown[at], "second");
            text = Line(text, tip, lines, TipLines.Range, "Range", at => range[at], null);
            text = Line(text, tip, lines, TipLines.Area, "Area", at => area[at], null);
            return Line(text, tip, lines, TipLines.Duration, "Duration", at => duration[at], "second");
        }

        private static string Text(Tip tip, Func<Tip, string> text, TipLines lines, Func<Tip, string>? linesOf)
        {
            var main = text(tip);
            var extra = lines == TipLines.None || linesOf == null ? string.Empty : linesOf(tip);
            return extra.Length == 0 ? main : main + "|n|n" + extra;
        }

        // A line is left out where its value is 0 at every level it tells of; "1 second", else "seconds".
        private static string Line(string text, Tip tip, TipLines lines, TipLines line, string label, Func<int, double> valueAt, string? unit)
        {
            if ((lines & line) == 0 || Every(tip, valueAt, "0"))
            {
                return text;
            }

            var units = unit == null ? string.Empty : " " + unit + (Every(tip, valueAt, "1") ? string.Empty : "s");
            return (text.Length == 0 ? string.Empty : text + "|n") + label + ": " + tip.Show(valueAt, 1.0) + units;
        }

        // Whether the value shows as the text at every level the tip tells of: its own, or all in the learn tooltip.
        private static bool Every(Tip tip, Func<int, double> valueAt, string shown)
        {
            for (var at = tip.level > 0 ? tip.level : 1; at <= (tip.level > 0 ? tip.level : tip.count); at++)
            {
                if (Format(valueAt(at)) != shown)
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }

    /// <summary>The lines <c>Describe</c> adds under an ability's text from its own fields, each left out where its value is 0.</summary>
    /// @CSharpLua.Ignore
    [Flags]
    public enum TipLines
    {
        /// <summary>No lines.</summary>
        None = 0,

        /// <summary><c>Mana: 75</c>, from <c>ManaCost</c>.</summary>
        ManaCost = 1,

        /// <summary><c>Cooldown: 8 seconds</c>, from <c>Cooldown</c>.</summary>
        Cooldown = 2,

        /// <summary><c>Range: 600</c>, from <c>Range</c>.</summary>
        Range = 4,

        /// <summary><c>Area: 250</c>, from <c>Area</c>.</summary>
        Area = 8,

        /// <summary><c>Duration: 5 seconds</c>, from <c>Duration</c>.</summary>
        Duration = 16,
    }
}
