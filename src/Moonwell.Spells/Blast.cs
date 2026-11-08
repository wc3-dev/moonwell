#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>Hits every unit it can within Radius of the place, once, with Damage and OnHit, playing Model there. Point target; with AroundCaster, no target, around the caster.</summary>
    public abstract class Blast : SpellTemplate
    {
        /// <summary>Whether it blasts around the caster.</summary>
        private bool aroundCaster;

        /// <summary>Makes a blast cast at a place.</summary>
        protected Blast()
        {
#if !MOONWELL_LUA
            TargetType = ChannelType.PointTarget;
#endif
        }

        /// <summary>How far from its middle it hits; 250 unless set.</summary>
        public float Radius { get; set; } = 250.0f;

        /// <summary>The model played at its middle; none unless set.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Whether it blasts around the caster, with no target, instead of at a place; false unless set.</summary>
        public bool AroundCaster
        {
            get => aroundCaster;
            set
            {
                aroundCaster = value;
#if !MOONWELL_LUA
                TargetType = value ? ChannelType.InstantNoTarget : ChannelType.PointTarget;
#endif
            }
        }

        /// <summary>Blasts at once.</summary>
        public override void OnCast(Cast cast)
        {
            var x = AroundCaster ? cast.Caster.X : cast.X;
            var y = AroundCaster ? cast.Caster.Y : cast.Y;
            if (Model.Length > 0)
            {
                SpecialEffect.PlayOnce(Model, x, y);
            }

            HitAround(cast, x, y, Radius, Damage.At(cast.Level));
        }
    }
}
