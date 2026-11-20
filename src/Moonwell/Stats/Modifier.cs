namespace Moonwell
{
    /// <summary>
    /// A modifier on a stat, made by Modify (decision 221): it adds while its holder holds it, and End takes it off sooner. It
    /// has four numbers, one for each stage of the formula (decision 385), which can be set after it is made.
    /// </summary>
    public sealed class Modifier : Scoped
    {
        internal readonly StatSheet sheet;

        private float amount;

        private float percent;

        private float times;

        private float late;

        /// <summary>The stacking rule in force: null for a modifier with no group, which always adds up.</summary>
        internal Stacking? rule;

        /// <summary>Whether the sheet counts it; the sheet works it out again at every change of the stat.</summary>
        internal bool counts = true;

        internal Modifier(StatSheet sheet, Stat stat, float amount, float percent, float times, float late, object? group, Unit? source)
        {
            this.sheet = sheet;
            Stat = stat;
            this.amount = amount;
            this.percent = percent;
            this.times = times;
            this.late = late;
            Group = group;
            Source = source;
        }

        /// <summary>The stat it changes.</summary>
        [LocalSafe]
        public Stat Stat { get; }

        /// <summary>The flat amount it adds, before the percents; 0 for a modifier that adds none. Setting it works the stat out again; on an ended modifier it does nothing.</summary>
        [LocalSafe]
        public float Amount
        {
            get { return amount; }
            set
            {
                if (!HasEnded && amount != value)
                {
                    var before = sheet.Before(Stat);
                    amount = value;
                    sheet.Changed(Stat, before);
                }
            }
        }

        /// <summary>The percent it adds to the sum of the percents; 0 for a modifier that adds none. Setting it works the stat out again; on an ended modifier it does nothing.</summary>
        [LocalSafe]
        public float Percent
        {
            get { return percent; }
            set
            {
                if (!HasEnded && percent != value)
                {
                    var before = sheet.Before(Stat);
                    percent = value;
                    sheet.Changed(Stat, before);
                }
            }
        }

        /// <summary>The multiplier it gives, which multiplies after the percents; 1 for no change. Setting it works the stat out again; on an ended modifier it does nothing.</summary>
        [LocalSafe]
        public float Times
        {
            get { return times; }
            set
            {
                if (!HasEnded && times != value)
                {
                    var before = sheet.Before(Stat);
                    times = value;
                    sheet.Changed(Stat, before);
                }
            }
        }

        /// <summary>The flat amount it adds after the multipliers, outside the percents; 0 for a modifier that adds none. Setting it works the stat out again; on an ended modifier it does nothing.</summary>
        [LocalSafe]
        public float Late
        {
            get { return late; }
            set
            {
                if (!HasEnded && late != value)
                {
                    var before = sheet.Before(Stat);
                    late = value;
                    sheet.Changed(Stat, before);
                }
            }
        }

        /// <summary>What it stacks with: any object it was made with (a string, a class), compared as Equals does; null for a modifier that adds up with every other.</summary>
        [LocalSafe]
        public object? Group { get; }

        /// <summary>The unit it came from, which PerSource tells apart; null when none was given.</summary>
        [LocalSafe]
        public Unit? Source { get; }

        /// <summary>Whether the stat counts it: false while a stacking rule leaves it out (a weaker one of a strongest group), and once it has ended.</summary>
        [LocalSafe]
        public bool Counts => counts && !HasEnded;

        /// <summary>Takes it off its stat.</summary>
        internal override void Finish()
        {
            sheet.Remove(this);
        }
    }
}
