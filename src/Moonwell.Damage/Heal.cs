namespace Moonwell
{
    /// <summary>One heal, as the damage package hands it to its two stages: the life a unit gives another, what it came to, and what really went in.</summary>
    public sealed class Heal
    {
        /// <summary>The amount, kept behind the property that checks the stage.</summary>
        private float amount;

        /// <summary>Makes a heal that starts with the amount.</summary>
        internal Heal(Unit target, Unit source, float amount, object? tag, int chain)
        {
            Target = target;
            Source = source;
            this.amount = amount;
            Original = amount;
            Tag = tag;
            Chain = chain;
        }

        /// <summary>The unit that is healed.</summary>
        [LocalSafe]
        public Unit Target { get; }

        /// <summary>The unit that heals.</summary>
        [LocalSafe]
        public Unit Source { get; }

        /// <summary>The amount to add; a reaction of the first stage may change it, and one of the second stage changes nothing by setting it.</summary>
        [LocalSafe]
        public float Amount
        {
            get => amount;
            set
            {
                if (!Landed)
                {
                    amount = value;
                }
            }
        }

        /// <summary>The amount the heal started with, before any reaction changed it.</summary>
        [LocalSafe]
        public float Original { get; }

        /// <summary>The tag the code that healed gave it (Unit.Heal); null otherwise.</summary>
        [LocalSafe]
        public object? Tag { get; }

        /// <summary>Whether the heal is a critical one: a plain mark a first-stage reaction sets and later reactions read.</summary>
        [LocalSafe]
        public bool IsCrit { get; set; }

        /// <summary>The life really added, known in the second stage: the amount, less the part that did not fit.</summary>
        [LocalSafe]
        public float Done { get; internal set; } = 0.0f;

        /// <summary>The part of the amount that did not fit under the target's most life, known in the second stage.</summary>
        [LocalSafe]
        public float Over { get; internal set; } = 0.0f;

        /// <summary>How many hits and heals led to this one, each made while the reactions of the one before ran: 0 for one made while no reaction ran.</summary>
        internal int Chain;

        /// <summary>Whether the life has been set and the second stage is running or over.</summary>
        internal bool Landed;
    }
}
