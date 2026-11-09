#if !MOONWELL_LUA
using Moonwell.Objects;
#endif

namespace Moonwell.Spells
{
    /// <summary>Summons Count units of class T (decision 227) in front of the caster, for Lifetime seconds, held by this ability, with Limit and DiesWithSummoner as unit.Summon takes them. No target; Damage is not used.</summary>
    /// <typeparam name="T">The class of the units summoned.</typeparam>
    public abstract class Summon<T> : SpellTemplate
        where T : Unit
    {
        /// <summary>Makes a summon with no target.</summary>
        protected Summon()
        {
#if !MOONWELL_LUA
            TargetType = ChannelType.InstantNoTarget;
#endif
        }

        /// <summary>How many each cast summons; 1 unless set.</summary>
        public int Count { get; set; } = 1;

        /// <summary>How long each lives; 60 unless set, 0 for no time limit.</summary>
        public float Lifetime { get; set; } = 60.0f;

        /// <summary>The most of class T one caster keeps, the oldest dying past it; 0, no limit, unless set.</summary>
        public int Limit { get; set; }

        /// <summary>Whether they die with the caster; false unless set.</summary>
        public bool DiesWithSummoner { get; set; }

        /// <summary>Summons them 150 in front of the caster, side by side, facing as it faces.</summary>
        public override void OnCast(Cast cast)
        {
            var caster = cast.Caster;
            var facing = caster.Facing * (float)System.Math.PI / 180.0f;
            var aheadX = caster.X + 150.0f * (float)System.Math.Cos(facing);
            var aheadY = caster.Y + 150.0f * (float)System.Math.Sin(facing);
            for (var i = 0; i < Count; i++)
            {
                var side = (i - (Count - 1) / 2.0f) * 80.0f;
                var x = aheadX - side * (float)System.Math.Sin(facing);
                var y = aheadY + side * (float)System.Math.Cos(facing);
                caster.Summon<T>(x, y, Lifetime, this, Limit, DiesWithSummoner, caster.Facing);
            }
        }
    }
}
