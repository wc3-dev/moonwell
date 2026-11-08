namespace Moonwell
{
    /// <summary>One cast of a spell: it begins when the caster starts to cast it (OnCasting), or when the spell takes effect for an ability that does not react to casts, and ends at the spell's end, when the caster's next cast takes effect, or when it is refused.</summary>
    public sealed class Cast : Scope
    {
        /// <summary>Makes the cast of an ability by a caster, at a target and a place, with the level the ability has now.</summary>
        internal Cast(Ability ability, Unit caster, Unit? target, float x, float y, int level)
        {
            Ability = ability;
            Caster = caster;
            Target = target;
            X = x;
            Y = y;
            Level = level;
        }

        /// <summary>The ability being cast.</summary>
        [LocalSafe]
        public Ability Ability { get; }

        /// <summary>The unit casting the ability.</summary>
        [LocalSafe]
        public Unit Caster { get; }

        /// <summary>The unit the spell is cast on; null when there is none.</summary>
        [LocalSafe]
        public Unit? Target { get; }

        /// <summary>The x of the place the spell is cast at.</summary>
        [LocalSafe]
        public float X { get; } = 0.0f;

        /// <summary>The y of the place the spell is cast at.</summary>
        [LocalSafe]
        public float Y { get; } = 0.0f;

        /// <summary>The level of the ability when the cast began.</summary>
        [LocalSafe]
        public int Level { get; }

        /// <summary>Whether the cast has taken effect: OnCast has run, or is running.</summary>
        internal bool TookEffect;

        /// <summary>The text of a refusal, set by Refuse in OnCasting; null while the cast goes on.</summary>
        internal string? Refusal;

        /// <summary>Inside the ability's OnCasting: stops the cast before anything is spent (no mana, no cooldown, no effect: seen 11-08), and shows the text to the caster's owner where the game shows its errors, with its sound. Empty text stops it with no line. Anywhere else it is too late, and a development build reports it.</summary>
        public void Refuse(string text)
        {
            if (TookEffect || HasEnded)
            {
                Dev.Report("Cast.Refuse", "the cast has " + (HasEnded ? "ended" : "taken effect") + ", so it cannot be refused: refuse it in OnCasting");
                return;
            }

            Refusal = text;
        }
    }
}
