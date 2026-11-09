#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>A wave that goes out from the caster towards the place, along the ground, for its Distance, and hits each unit it passes once, with Damage and OnHit. Point target.</summary>
    public abstract class Wave : SpellTemplate
    {
        /// <summary>Makes a wave cast at a place.</summary>
        protected Wave()
        {
#if !MOONWELL_LUA
            TargetType = ChannelType.PointTarget;
#endif
        }

        /// <summary>The wave's model; none unless set.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>The wave's speed; 900 unless set.</summary>
        public float Speed { get; set; } = 900.0f;

        /// <summary>How far it goes, whatever the place's distance; 800 unless set.</summary>
        public float Distance { get; set; } = 800.0f;

        /// <summary>How wide it hits, from its middle; 100 unless set.</summary>
        public float Radius { get; set; } = 100.0f;

        /// <summary>Sends the wave, held by this ability so that it goes on after an instant cast has ended.</summary>
        public override void OnCast(Cast cast)
        {
            var angle = AngleTo(cast.Caster, cast.X, cast.Y) * (float)System.Math.PI / 180.0f;
            var x = cast.Caster.X + Distance * (float)System.Math.Cos(angle);
            var y = cast.Caster.Y + Distance * (float)System.Math.Sin(angle);
            cast.Caster.Fire(new WaveMissile(this, cast), x, y, this);
        }

        /// <summary>The wave's missile: it passes through units and over the ground.</summary>
        private sealed class WaveMissile : Missile
        {
            private readonly Wave wave;
            private readonly Cast cast;

            /// <summary>Makes the wave of a cast.</summary>
            public WaveMissile(Wave wave, Cast cast)
            {
                this.wave = wave;
                this.cast = cast;
                Model = wave.Model;
                Speed = wave.Speed;
                Radius = wave.Radius;
                MaxDistance = wave.Distance;
                FollowsGround = true;
                UnitContact = Contact.Pass;
                GroundContact = Contact.Pass;
            }

            /// <summary>Meets only units the cast can hit.</summary>
            public override bool CanHit(Actor other)
            {
                return other is Unit unit && wave.CanHit(cast, unit);
            }

            /// <summary>Hits each unit it passes, once: a pass reacts to a unit again only after leaving it.</summary>
            public override void OnImpact(Impact impact)
            {
                if (impact.Unit != null)
                {
                    wave.Hit(cast, impact.Unit, wave.Damage.At(cast.Level));
                }
            }
        }
    }
}
