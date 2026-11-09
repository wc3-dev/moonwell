#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>A channel that pulses at once and every Interval while it lasts, hitting every unit it can within Radius, with Damage and OnHit, and playing Model; it stops with the channel. The channel lasts its Follow Through Time, 5 s unless the map sets it. Point target; with AroundCaster, no target, around the caster where it stands at each pulse.</summary>
    public abstract class PulseChannel : SpellTemplate
    {
        /// <summary>Whether it pulses around the caster.</summary>
        internal bool aroundCaster;

        /// <summary>Makes a channel of 5 s cast at a place.</summary>
        protected PulseChannel()
        {
#if !MOONWELL_LUA
            TargetType = ChannelType.PointTarget;
            FollowThroughTime = 5.0f;
#endif
        }

        /// <summary>The seconds between pulses; 1 unless set.</summary>
        public float Interval { get; set; } = 1.0f;

        /// <summary>How far from its middle a pulse hits; 250 unless set.</summary>
        public float Radius { get; set; } = 250.0f;

        /// <summary>The model played at each pulse; none unless set.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Whether it pulses around the caster, with no target, instead of at a place; false unless set.</summary>
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

        /// <summary>Pulses now and on the cast, which ends with the channel.</summary>
        public override void OnCast(Cast cast)
        {
            var damage = Damage.At(cast.Level);
            Pulse(cast, damage);
            cast.Every(Interval, () => Pulse(cast, damage));
        }

        /// <summary>One pulse.</summary>
        private void Pulse(Cast cast, float damage)
        {
            var x = AroundCaster ? cast.Caster.X : cast.X;
            var y = AroundCaster ? cast.Caster.Y : cast.Y;
            if (Model.Length > 0)
            {
                SpecialEffect.PlayOnce(Model, x, y);
            }

            HitAround(cast, x, y, Radius, damage);
        }
    }
}
