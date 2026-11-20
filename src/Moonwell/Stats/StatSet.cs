using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The stats a save keeps, in a fixed order: <c>new StatSet(Stat.MaxLife, Stats.Rage)</c>. Save gives a unit's or a player's
    /// stats as a <c>List&lt;float&gt;</c> for a <c>[Saved]</c> member of a save class, and Restore puts them back. Each stat takes
    /// six numbers in the list: 1 or 0 for whether the holder has a base of its own, that base, then the totals of the modifiers
    /// the holder itself holds (<c>heldBy:</c> the unit or the player): the flat amounts added, the percents added, the multipliers
    /// multiplied, and the late flat amounts added. Modifiers held by an item, a buff or a talent are not kept: they come back when
    /// the map gives those back. Keep the set's order, and add a stat only at the end.
    /// </summary>
    public sealed class StatSet
    {
        /// <summary>The numbers each stat takes in a list.</summary>
        private const int Width = 6;

        private readonly Stat[] stats;

        /// <summary>Names the stats a save keeps, in the order the list holds them.</summary>
        public StatSet(params Stat[] stats)
        {
            this.stats = stats;
        }

        /// <summary>The unit's stats as a list for a [Saved] member. A game stat's base is kept only when the unit has one of its own: Moonwell owns that stat on it.</summary>
        public List<float> Save(Unit unit) => Write(unit, unit.Stats);

        /// <summary>The player's stats as a list for a [Saved] member. A game stat is a unit's: it takes six numbers of nothing.</summary>
        public List<float> Save(Player player) => Write(player, player.Stats);

        /// <summary>Puts a list Save gave back on the unit: each base it had is set, and the modifiers the unit holds itself come back as one modifier for each stat, which a second restore replaces, never adding to. A list that does not fit the set restores what fits and is reported in a development build; a null list does nothing. What the list does not mention is left as it is.</summary>
        public void Restore(Unit unit, List<float> saved) => Put(unit, unit.Stats, saved);

        /// <summary>Puts a list Save gave back on the player, as Restore does for a unit.</summary>
        public void Restore(Player player, List<float> saved) => Put(player, player.Stats, saved);

        private List<float> Write(Actor holder, StatSheet sheet)
        {
            var saved = new List<float>();
            for (var i = 0; i < stats.Length; i++)
            {
                var stat = stats[i];
                var kept = stat.Kind == StatKind.Own || holder is Unit;
                var has = false;
                var value = 0.0f;
                var flat = 0.0f;
                var percent = 0.0f;
                var times = 1.0f;
                var late = 0.0f;
                if (kept)
                {
                    has = sheet.OwnBase(stat, out value);
                    var held = sheet.Modifiers(stat);
                    for (var j = 0; j < held.Count; j++)
                    {
                        // A modifier a stacking rule leaves out adds nothing now, so it is not kept.
                        if (held[j].Parent == holder && held[j].Counts)
                        {
                            flat += held[j].Amount;
                            percent += held[j].Percent;
                            times *= held[j].Times;
                            late += held[j].Late;
                        }
                    }
                }

                saved.Add(has ? 1.0f : 0.0f);
                saved.Add(has ? value : 0.0f);
                saved.Add(flat);
                saved.Add(percent);
                saved.Add(times);
                saved.Add(late);
            }

            return saved;
        }

        private void Put(Actor holder, StatSheet sheet, List<float>? saved)
        {
            if (saved == null)
            {
                return;
            }

            var whole = saved.Count / Width;
            if (saved.Count % Width != 0)
            {
                Dev.Report("StatSet.Restore", "the list has " + saved.Count + " numbers, which is not a whole number of stats (each takes " + Width + "): the last " + (saved.Count % Width) + " were left out");
            }

            if (whole > stats.Length)
            {
                Dev.Report("StatSet.Restore", "the list has " + whole + " stats and the set names " + stats.Length + ": the last " + (whole - stats.Length) + " were left out");
            }
            else if (whole < stats.Length)
            {
                Dev.Report("StatSet.Restore", "the list has " + whole + " stats and the set names " + stats.Length + ": the stats after the first " + whole + " were left as they are");
            }

            for (var i = 0; i < whole && i < stats.Length; i++)
            {
                var at = i * Width;
                var stat = stats[i];
                if (stat.Kind != StatKind.Own && !(holder is Unit))
                {
                    continue;
                }

                // The modifier an earlier restore made is replaced, so restoring twice never adds twice.
                var held = sheet.Modifiers(stat);
                for (var j = 0; j < held.Count; j++)
                {
                    if (held[j].Parent == holder && object.Equals(held[j].Group, this))
                    {
                        held[j].End();
                    }
                }

                if (saved[at] != 0.0f)
                {
                    sheet.SetBase(stat, saved[at + 1], "StatSet.Restore");
                }

                if (saved[at + 2] != 0.0f || saved[at + 3] != 0.0f || saved[at + 4] != 1.0f || saved[at + 5] != 0.0f)
                {
                    sheet.Add(stat, saved[at + 2], saved[at + 3], saved[at + 4], saved[at + 5], holder, this, Stacking.Refresh, null, "StatSet.Restore");
                }
            }
        }
    }
}
