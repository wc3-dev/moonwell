using System.Collections.Generic;
#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>Hits the target, then jumps to the nearest unit it can hit within JumpRange that it has not hit, Jumps times, every Delay, each jump's damage Falloff times the last's, with a lightning of the Look between; Damage and OnHit at each. Unit target.</summary>
    public abstract class Chain : SpellTemplate
    {
        /// <summary>Makes a chain cast at a unit.</summary>
        protected Chain()
        {
#if !MOONWELL_LUA
            TargetType = ChannelType.UnitTarget;
#endif
        }

        /// <summary>How many jumps after the first hit; 3 unless set.</summary>
        public int Jumps { get; set; } = 3;

        /// <summary>How far a jump reaches; 500 unless set.</summary>
        public float JumpRange { get; set; } = 500.0f;

        /// <summary>Each jump's damage as a share of the last's; 0.85 unless set.</summary>
        public float Falloff { get; set; } = 0.85f;

        /// <summary>The seconds between jumps; 0.2 unless set.</summary>
        public float Delay { get; set; } = 0.2f;

        /// <summary>The lightning between two units, a LightningKind; chain lightning unless set, none when empty.</summary>
        public string Look { get; set; } = LightningKind.ChainLightningPrimary;

        /// <summary>Hits the target at once and jumps on, held by this ability so that it goes on after the cast has ended.</summary>
        public override void OnCast(Cast cast)
        {
            if (cast.Target != null)
            {
                Jump(cast, cast.Caster, cast.Target, 0, Damage.At(cast.Level), new List<Unit>());
            }
        }

        /// <summary>One jump: the lightning, the hit, and the next jump after Delay.</summary>
        private void Jump(Cast cast, Unit from, Unit to, int jumped, float damage, List<Unit> hit)
        {
            if (Look.Length > 0 && !from.IsGone)
            {
                Lightning.PlayOnce(Look, from, to);
            }

            hit.Add(to);
            Hit(cast, to, damage);
            if (jumped >= Jumps || to.IsGone)
            {
                return;
            }

            var next = Nearest(cast, to, hit);
            if (next != null)
            {
                After(Delay, () => Jump(cast, to, next, jumped + 1, damage * Falloff, hit));
            }
        }

        /// <summary>The nearest unit to a unit within JumpRange that the cast can hit and has not hit; null when none.</summary>
        private Unit? Nearest(Cast cast, Unit around, List<Unit> hit)
        {
            var found = Find.InRange<Unit>(around.X, around.Y, JumpRange, unit => !hit.Contains(unit) && CanHit(cast, unit));
            Unit? nearest = null;
            var best = 0.0f;
            for (var i = 0; i < found.Count; i++)
            {
                var dx = found[i].X - around.X;
                var dy = found[i].Y - around.Y;
                var distance = dx * dx + dy * dy;
                if (nearest == null || distance < best)
                {
                    nearest = found[i];
                    best = distance;
                }
            }

            return nearest;
        }
    }
}
