using System;

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

        /// <summary>Whether the cast has taken effect: OnCast has run, or is running. Read when it ends, false says it was stopped before its effect, so nothing was spent.</summary>
        [LocalSafe]
        public bool TookEffect { get; internal set; }

        /// <summary>Whether the spell ran to its end (the game's finish event, which OnFinished hears): a channel its whole time, a casting time to its end. Read when the cast ends, false says it was stopped early, by a stun, an order, a death or the caster's next cast, or that the spell has no finish, as Mirror Image (seen 11-19).</summary>
        [LocalSafe]
        public bool RanToEnd { get; internal set; }

        /// <summary>Runs once when the cast ends, with the cast, before what was made on it ends: at the spell's end, when the caster's next cast takes effect, when it is refused or when the caster leaves. TookEffect and RanToEnd say how it went.</summary>
        public event Action<Cast>? Ended;

        /// <summary>The spell's casting time at the cast's level, read from the game at each read (see Ability.LiveCastTime); 0 once the caster is gone or lacks the ability.</summary>
        [LocalSafe]
        public float CastSeconds => Caster.IsGone || Ability.Level <= 0 ? 0.0f : Runtime.World.AbilityCastTime(Caster.Handle, Ability.Id, Level - 1);

        /// <summary>How long the spell channels at the cast's level: the Follow Through Time of a spell based on Channel (ANcl), read from the game at each read; 0 for a spell without the field, as the game's own channels (Blizzard, Starfall) have none, and once the caster is gone or lacks the ability.</summary>
        [LocalSafe]
        public float ChannelSeconds => Caster.IsGone || Ability.Level <= 0 ? 0.0f : Runtime.World.AbilityFollowThrough(Caster.Handle, Ability.Id, Level - 1);

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

        /// <summary>Runs Ended.</summary>
        protected override void OnEnd()
        {
            var ended = Ended;
            Ended = null;
            if (ended == null)
            {
                return;
            }

            try
            {
                ended(this);
            }
            catch (Exception error)
            {
                Errors.Report(this, ".Ended", error);
            }
        }
    }
}
