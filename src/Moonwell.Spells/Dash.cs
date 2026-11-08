using System.Collections.Generic;
#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>The caster rushes towards the place, at most its Distance, over Seconds, slowing to a stop as a knockback does, and hits each unit it passes within Radius once, with Damage and OnHit. It stops at what stops a knockback: trees, buildings, cliffs and walls. Point target.</summary>
    public abstract class Dash : SpellTemplate
    {
        /// <summary>Makes a dash cast at a place.</summary>
        protected Dash()
        {
#if !MOONWELL_LUA
            TargetType = ChannelType.PointTarget;
#endif
        }

        /// <summary>The farthest it goes; 600 unless set.</summary>
        public float Distance { get; set; } = 600.0f;

        /// <summary>How long it takes; 0.4 unless set.</summary>
        public float Seconds { get; set; } = 0.4f;

        /// <summary>How near the caster a unit is hit; 100 unless set.</summary>
        public float Radius { get; set; } = 100.0f;

        /// <summary>Pushes the caster and hits along the way, every 1/32 s, until the push stops; held by this ability.</summary>
        public override void OnCast(Cast cast)
        {
            var caster = cast.Caster;
            var distance = DistanceTo(caster, cast.X, cast.Y);
            if (distance > Distance)
            {
                distance = Distance;
            }

            if (distance <= 0.0f)
            {
                return;
            }

            caster.Knockback(AngleTo(caster, cast.X, cast.Y), distance, Seconds);
            var hit = new List<Unit>();
            var damage = Damage.At(cast.Level);
            Timer? along = null;
            along = Every(0.03125f, () =>
            {
                var found = Find.InRange<Unit>(caster.X, caster.Y, Radius, unit => !hit.Contains(unit) && CanHit(cast, unit));
                for (var i = 0; i < found.Count; i++)
                {
                    hit.Add(found[i]);
                    Hit(cast, found[i], damage);
                }

                if (caster.IsGone || !caster.IsPushed())
                {
                    along!.End();
                }
            });
        }
    }
}
