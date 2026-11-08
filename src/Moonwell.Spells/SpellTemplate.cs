#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>
    /// The base of the spell templates (decision 226): a Channel ability whose cast does the template's work. A template sets
    /// Channel's targeting and shows its button, so a map's class adds only its object data (name, icon, hotkey, levels, an Area
    /// of Effect for the targeting circle, a Base Order ID of its own when one unit has two Channel abilities) and its numbers.
    /// By default a template hits living enemies of the caster's player; CanHit says otherwise, and OnHit adds to each hit.
    /// </summary>
    public abstract class SpellTemplate : Objects.Creeps.Abilities.Channel
    {
        /// <summary>Makes a template whose button shows. The object data is set only when the map is built: the game's script has no object fields.</summary>
        protected SpellTemplate()
        {
#if !MOONWELL_LUA
            Options = ChannelFlags.Visible;
#endif
        }

        /// <summary>The damage of each hit, by the cast's level; 0 unless set.</summary>
        public PerLevel Damage { get; set; } = 0.0f;

        /// <summary>The kind of the damage; magic unless set.</summary>
        public DamageKind DamageKind { get; set; } = DamageKind.Magic;

        /// <summary>Whether the cast hits a unit: a living enemy of the caster's player, unless overridden.</summary>
        public virtual bool CanHit(Cast cast, Unit target)
        {
            return target.IsAlive && cast.Caster.Owner.IsEnemy(target.Owner);
        }

        /// <summary>Runs for each unit the cast hits, after its damage.</summary>
        public virtual void OnHit(Cast cast, Unit target)
        {
        }

        /// <summary>Hits a unit: the damage, from the caster, with this ability as the hit's tag, then OnHit.</summary>
        internal void Hit(Cast cast, Unit target, float damage)
        {
            if (damage > 0.0f)
            {
                cast.Caster.Damage(target, damage, DamageKind, tag: this);
            }

            OnHit(cast, target);
        }

        /// <summary>Hits every unit the cast can hit within a radius of a place.</summary>
        internal void HitAround(Cast cast, float x, float y, float radius, float damage)
        {
            var found = Find.InRange<Unit>(x, y, radius, unit => CanHit(cast, unit));
            for (var i = 0; i < found.Count; i++)
            {
                Hit(cast, found[i], damage);
            }
        }

        /// <summary>The angle from the caster to a place, in degrees.</summary>
        internal static float AngleTo(Unit caster, float x, float y)
        {
            return (float)(System.Math.Atan2(y - caster.Y, x - caster.X) * 180.0 / System.Math.PI);
        }

        /// <summary>The distance from the caster to a place.</summary>
        internal static float DistanceTo(Unit caster, float x, float y)
        {
            var dx = x - caster.X;
            var dy = y - caster.Y;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
