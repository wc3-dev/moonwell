#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>
    /// The caster jumps in an arc towards the place, at most as far as Speed reaches, and lands with a slam that hits every unit
    /// it can within Radius, with Damage and OnHit. The jump goes along the ground as a knockback does, slowing evenly to land at
    /// the place, and up with Physics' gravity at the low arc for Speed; the slam comes at the end of that time, where the caster
    /// then is (trees, buildings, cliffs and walls stop it short). It lands where Physics leaves it, with no snapping to a free
    /// spot. Point target.
    /// </summary>
    public abstract class Leap : SpellTemplate
    {
        /// <summary>Makes a leap cast at a place.</summary>
        protected Leap()
        {
#if !MOONWELL_LUA
            TargetType = ChannelType.PointTarget;
#endif
        }

        /// <summary>The speed it jumps at, which sets how high and how far it can go (Speed squared over gravity); 800 unless set.</summary>
        public float Speed { get; set; } = 800.0f;

        /// <summary>How near the landing a unit is hit; 250 unless set.</summary>
        public float Radius { get; set; } = 250.0f;

        /// <summary>The model played where it lands; none unless set.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Knocks the caster towards the place and lifts it, and slams when it lands; held by this ability.</summary>
        public override void OnCast(Cast cast)
        {
            var caster = cast.Caster;
            var gravity = Physics.Gravity;
            var distance = DistanceTo(caster, cast.X, cast.Y);
            var reach = Speed * Speed / gravity;
            if (distance > reach)
            {
                distance = reach;
            }

            if (distance <= 0.0f || Speed <= 0.0f)
            {
                return;
            }

            // The low arc for Speed: sin(2 * angle) = distance * gravity / Speed^2, 45 degrees at the farthest.
            var rise = (float)(Speed * System.Math.Sin(0.5 * System.Math.Asin(distance * gravity / (Speed * Speed))));
            var seconds = 2.0f * rise / gravity;
            caster.Knockback(AngleTo(caster, cast.X, cast.Y), distance, seconds);
            caster.Push(0.0f, 0.0f, rise);
            var damage = Damage.At(cast.Level);
            After(seconds, () =>
            {
                if (caster.IsGone)
                {
                    return;
                }

                caster.StopPush();
                if (Model.Length > 0)
                {
                    SpecialEffect.PlayOnce(Model, caster.X, caster.Y);
                }

                HitAround(cast, caster.X, caster.Y, Radius, damage);
            });
        }
    }
}
