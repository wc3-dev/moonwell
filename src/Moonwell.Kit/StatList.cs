using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// Rows of a unit's stats, of the UI kit's HUD: each a stat's name on the left and its value on the right, a whole stat
    /// whole and another with at most one decimal. A row's tooltip gives the breakdown: the base, then each modifier that
    /// counts with its amount (+5 for a flat one, +20% a percent, x1.5 a multiplier, +10 after a late flat one) and what
    /// holds it, then those a stacking rule leaves out. Show gives it a unit and the stats; it follows them from then on,
    /// redrawn when one of them changes. Each row is 0.014 high and 0.18 wide, and the list is
    /// as high as the rows it shows.
    /// </summary>
    public class StatList : Grid
    {
        /// <summary>A row's width and height.</summary>
        internal const float RowWidth = 0.18f, RowHeight = 0.014f;

        internal readonly List<StatRow> rows = new List<StatRow>();

        internal Unit? unit;

        internal Stat[] stats = new Stat[0];

        internal bool watching;

        internal bool ticking;

        internal Theme theme;

        /// <summary>Makes a list that shows nothing yet, to be added to the screen or to another frame.</summary>
        public StatList()
        {
            columns = 1;
            CellWidth = RowWidth;
            CellHeight = RowHeight;
            theme = Kit.Theme;
            KitTicks.Fit(this, 0);
        }

        /// <summary>The list's look, Kit.Theme as it was made unless set: each row's name and value take its colours, size and font, and its tooltip the theme. Set it before the list is added, as the sizes and font are read when the labels are made.</summary>
        [LocalSafe]
        public Theme Theme
        {
            get { return theme; }
            set
            {
                if (HasEnded)
                {
                    return;
                }

                theme = value;
                Dress();
            }
        }

        private void Dress()
        {
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].Dress(theme);
            }
        }

        /// <summary>The unit whose stats show; null when none.</summary>
        [LocalSafe]
        public Unit? Unit => unit;

        /// <summary>Shows a unit's stats, one row for each in the order given, from now on: each row's value and tooltip are drawn again when its stat changes on the unit. Null or no stats empties the list.</summary>
        public void Show(Unit? shown, params Stat[] shownStats)
        {
            if (HasEnded)
            {
                return;
            }

            unit = shown;
            stats = shown == null || shownStats == null ? new Stat[0] : shownStats;
            Read();
            if (shown != null && stats.Length > 0 && !watching)
            {
                watching = true;
                StatWatch.Join(Changed);
            }

            if (shown != null && stats.Length > 0 && !ticking)
            {
                ticking = true;
                KitTicks.Join(Tick);
            }
        }

        /// <summary>Looks for what the engine does not announce, as a modifier a stacking rule leaves out coming or going, which leaves the stat's total as it was: the tooltips are drawn again when the modifiers' number or the number that count has changed. Stops when the list shows nothing.</summary>
        private bool Tick()
        {
            if (HasEnded || unit == null || stats.Length == 0)
            {
                ticking = false;
                return false;
            }

            for (var i = 0; i < stats.Length; i++)
            {
                rows[i].Check(unit);
            }

            return true;
        }

        /// <summary>Draws a row again when a stat of its unit changes; false once the list has ended.</summary>
        private bool Changed(Unit changed, Stat which)
        {
            if (HasEnded)
            {
                watching = false;
                return false;
            }

            if (changed == unit)
            {
                for (var i = 0; i < stats.Length; i++)
                {
                    if (stats[i] == which)
                    {
                        rows[i].Draw(unit!);
                    }
                }
            }

            return true;
        }

        /// <summary>Puts the stats in the rows, making rows as needed and hiding those left over.</summary>
        internal void Read()
        {
            for (var i = 0; i < stats.Length; i++)
            {
                if (i == rows.Count)
                {
                    var row = new StatRow();
                    row.Dress(theme);
                    rows.Add(Add(row));
                }

                rows[i].Show(unit!, stats[i]);
            }

            for (var i = stats.Length; i < rows.Count; i++)
            {
                rows[i].Clear();
            }

            KitTicks.Fit(this, stats.Length);
        }

        /// <summary>A number as the list shows it: rounded to the decimals (0 to 2), with no trailing zeros, and a minus when it is below 0.</summary>
        internal static string Number(float value, int decimals)
        {
            var scale = decimals == 0 ? 1 : decimals == 1 ? 10 : 100;
            var scaled = (int)Math.Floor(Math.Abs(value) * scale + 0.5f);
            var text = (scaled / scale).ToString();
            var part = scaled % scale;
            if (part != 0)
            {
                var digits = decimals == 1 ? part.ToString() : part >= 10 ? (part % 10 == 0 ? (part / 10).ToString() : part.ToString()) : "0" + part;
                text += "." + digits;
            }

            return value < 0.0f && scaled != 0 ? "-" + text : text;
        }

        /// <summary>The name of what holds a modifier: an item's carry gives its item's class name, a buff or another component its class name, the game "Game", a player "Player" and its number. The names are the same on every machine.</summary>
        internal static string HolderName(Scope? holder)
        {
            if (holder is Carrying carry)
            {
                var carrier = carry.Parent as Unit;
                var found = carrier == null ? null : ItemOf(carrier, carry);
                return found != null ? Names.NameOf(found) : "an item";
            }

            if (holder is Player player)
            {
                return "Player " + (player.Slot + 1);
            }

            return holder == null ? "nothing" : Names.NameOf(holder);
        }

        /// <summary>The item whose carry this is, among those the unit holds; null when it holds none that has it.</summary>
        private static Item? ItemOf(Unit carrier, Carrying carry)
        {
            for (var slot = 0; slot < carrier.InventorySize; slot++)
            {
                var item = carrier.ItemIn(slot);
                if (item != null && item.carry == carry)
                {
                    return item;
                }
            }

            var equipped = carrier.Equipped;
            for (var i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].carry == carry)
                {
                    return equipped[i];
                }
            }

            return null;
        }
    }

    /// <summary>A row of a StatList: a stat's name, its value and a tooltip with the breakdown.</summary>
    public class StatRow : Panel
    {
        internal readonly Label name;

        internal readonly Label value;

        internal readonly RichTooltip tip;

        internal Stat? stat;

        /// <summary>How many modifiers the stat had, and how many of them counted, when the tooltip was last drawn; -1 before the first.</summary>
        internal int shownMods = -1, shownCounting = -1;

        internal StatRow()
        {
            Template = "";
            width = StatList.RowWidth;
            height = StatList.RowHeight;
            sized = true;
            Visible = false;
            name = KitTicks.Place(this, new Label { Alignment = FramePoint.Left }, 0.0f, 0.0f, StatList.RowWidth * 0.6f, StatList.RowHeight);
            value = KitTicks.Place(this, new Label { Alignment = FramePoint.Right }, StatList.RowWidth * 0.6f, 0.0f, StatList.RowWidth * 0.4f, StatList.RowHeight);
            tip = new RichTooltip();
            Tooltip = tip;
        }

        /// <summary>Gives the name, the value and the tooltip the list's theme.</summary>
        internal void Dress(Theme theme)
        {
            name.Color = theme.Title;
            value.Color = theme.Text;
            theme.Style(name, theme.TextSize);
            theme.Style(value, theme.TextSize);
            tip.Theme = theme;
        }

        /// <summary>The stat's name, in the theme's Title colour.</summary>
        [LocalSafe]
        public Label NameLabel => name;

        /// <summary>The stat's value, in the theme's Text colour.</summary>
        [LocalSafe]
        public Label ValueLabel => value;

        /// <summary>The stat shown; null when the row is hidden.</summary>
        [LocalSafe]
        public Stat? Stat => stat;

        /// <summary>Shows a stat of a unit, and the row.</summary>
        internal void Show(Unit of, Stat shown)
        {
            stat = shown;
            Visible = true;
            name.Text = shown.Name;
            Draw(of);
        }

        /// <summary>Hides the row.</summary>
        internal void Clear()
        {
            stat = null;
            Visible = false;
        }

        /// <summary>Draws again if the stat's modifiers or the number that count are not what the tooltip drew.</summary>
        internal void Check(Unit of)
        {
            if (stat == null || HasEnded)
            {
                return;
            }

            var mods = of.Modifiers(stat);
            if (mods.Count != shownMods || CountingOf(mods) != shownCounting)
            {
                Draw(of);
            }
        }

        private static int CountingOf(IReadOnlyList<Modifier> mods)
        {
            var counting = 0;
            for (var i = 0; i < mods.Count; i++)
            {
                if (mods[i].Counts)
                {
                    counting++;
                }
            }

            return counting;
        }

        /// <summary>Draws the value and the breakdown of the stat now.</summary>
        internal void Draw(Unit of)
        {
            if (stat == null || HasEnded)
            {
                return;
            }

            var decimals = stat.Whole ? 0 : 1;
            value.Text = of.IsGone ? "" : StatList.Number(of.Get(stat), decimals);
            tip.Title = stat.Name;
            var mods = of.Modifiers(stat);
            shownMods = mods.Count;
            shownCounting = CountingOf(mods);
            var lines = Breakdown(of, stat, decimals, mods);
            var text = "";
            for (var i = 0; i < lines.Count; i++)
            {
                text += (i > 0 ? "\n" : "") + lines[i];
            }

            tip.Text = text;
            tip.Height = 2.0f * RichTooltip.Inset + RichTooltip.TitleLine + 0.004f + (lines.Count * 0.0115f);
        }

        /// <summary>The tooltip's lines: the base, each modifier that counts, then "Left out:" and those a rule leaves out.</summary>
        private static List<string> Breakdown(Unit of, Stat stat, int decimals, IReadOnlyList<Modifier> mods)
        {
            var lines = new List<string>();
            lines.Add("Base " + StatList.Number(of.GetBase(stat), decimals));
            var left = new List<string>();
            for (var i = 0; i < mods.Count; i++)
            {
                var line = LineOf(mods[i]);
                if (line.Length == 0)
                {
                    continue;
                }

                if (mods[i].Counts)
                {
                    lines.Add(line);
                }
                else
                {
                    left.Add(line);
                }
            }

            if (left.Count > 0)
            {
                lines.Add("Left out:");
                lines.AddRange(left);
            }

            return lines;
        }

        /// <summary>A modifier's numbers and its holder: "+5 from Ring", "+20%, x1.5 from Frost"; empty for one that changes nothing.</summary>
        private static string LineOf(Modifier modifier)
        {
            var parts = new List<string>();
            if (modifier.Amount != 0.0f)
            {
                parts.Add(Signed(modifier.Amount));
            }

            if (modifier.Percent != 0.0f)
            {
                parts.Add(Signed(modifier.Percent) + "%");
            }

            if (modifier.Times != 1.0f)
            {
                parts.Add("x" + StatList.Number(modifier.Times, 2));
            }

            if (modifier.Late != 0.0f)
            {
                parts.Add(Signed(modifier.Late) + " after");
            }

            if (parts.Count == 0)
            {
                return "";
            }

            var line = parts[0];
            for (var i = 1; i < parts.Count; i++)
            {
                line += ", " + parts[i];
            }

            return line + " from " + StatList.HolderName(modifier.Parent);
        }

        private static string Signed(float amount)
        {
            return (amount > 0.0f ? "+" : "") + StatList.Number(amount, 2);
        }
    }
}
