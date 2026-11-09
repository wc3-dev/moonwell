#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>A missile at a unit, which it follows, or at a place: it hits the first unit it can hit, with Damage and OnHit, and ends there or at the place. Unit target unless the map sets TargetType.</summary>
    public abstract class Bolt : SpellTemplate
    {
        /// <summary>Makes a bolt cast at a unit.</summary>
        protected Bolt()
        {
#if !MOONWELL_LUA
            TargetType = ChannelType.UnitTarget;
#endif
        }

        /// <summary>The missile's model; none unless set.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>The missile's speed; 900 unless set.</summary>
        public float Speed { get; set; } = 900.0f;

        /// <summary>The missile's arc, as the game's Missile Arc; 0, straight, unless set.</summary>
        public float Arc { get; set; }

        /// <summary>The most a missile at a unit turns a second, in degrees; 0, the default, turns it at the unit at once.</summary>
        public float TurnRate { get; set; }

        /// <summary>Fires the missile, held by this ability so that it lands after an instant cast has ended.</summary>
        public override void OnCast(Cast cast)
        {
            var target = cast.Target;
            var missile = new BoltMissile(this, cast, target != null);
            if (target != null)
            {
                cast.Caster.Fire(missile, target, this);
                return;
            }

            missile.MaxDistance = DistanceTo(cast.Caster, cast.X, cast.Y);
            cast.Caster.Fire(missile, cast.X, cast.Y, this);
        }

        /// <summary>The bolt's missile.</summary>
        private sealed class BoltMissile : Missile
        {
            private readonly Bolt bolt;
            private readonly Cast cast;

            /// <summary>Makes the missile of a cast, following its target when it has one.</summary>
            public BoltMissile(Bolt bolt, Cast cast, bool follows)
            {
                this.bolt = bolt;
                this.cast = cast;
                Model = bolt.Model;
                Speed = bolt.Speed;
                Arc = bolt.Arc;
                if (follows)
                {
                    Components.Add(new Homing { TurnRate = bolt.TurnRate });
                }
            }

            /// <summary>Meets only units the cast can hit.</summary>
            public override bool CanHit(Actor other)
            {
                return other is Unit unit && bolt.CanHit(cast, unit);
            }

            /// <summary>Hits the unit it meets.</summary>
            public override void OnImpact(Impact impact)
            {
                if (impact.Unit != null)
                {
                    bolt.Hit(cast, impact.Unit, bolt.Damage.At(cast.Level));
                }
            }
        }
    }
}
