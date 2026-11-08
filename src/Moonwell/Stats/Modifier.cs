namespace Moonwell
{
    /// <summary>A modifier on a stat, made by Modify (decision 221): it adds while its holder holds it, and End takes it off sooner.</summary>
    public sealed class Modifier : Scoped
    {
        internal readonly StatSheet sheet;

        internal Modifier(StatSheet sheet, Stat stat, float amount, float percent)
        {
            this.sheet = sheet;
            Stat = stat;
            Amount = amount;
            Percent = percent;
        }

        /// <summary>The stat it changes.</summary>
        [LocalSafe]
        public Stat Stat { get; }

        /// <summary>The flat amount it adds; 0 for a percent modifier.</summary>
        [LocalSafe]
        public float Amount { get; }

        /// <summary>The percent it adds; 0 for a flat modifier.</summary>
        [LocalSafe]
        public float Percent { get; }

        /// <summary>Takes it off its stat.</summary>
        internal override void Finish()
        {
            sheet.Remove(this);
        }
    }
}
