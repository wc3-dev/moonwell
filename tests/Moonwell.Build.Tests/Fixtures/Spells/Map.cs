using Moonwell;
using Moonwell.Objects;
using Moonwell.Spells;

namespace SpellsFixture
{
    /// <summary>A map that declares one ability of each spell template.</summary>
    public sealed class SpellsMap : Map
    {
        public SpellsMap()
        {
            Name = "Spells";
        }
    }

    [ObjectId("A0B1")]
    public sealed class Firebolt : Bolt
    {
        public Firebolt()
        {
            Name = "Firebolt";
            Model = @"Abilities\Weapons\RedDragonBreath\RedDragonMissile.mdl";
            Damage = new PerLevel(75.0f, 150.0f);
        }

        public override void OnHit(Cast cast, Unit target)
        {
            target.Owner.Print("Burnt");
        }
    }

    [ObjectId("A0B2")]
    public sealed class Quake : Blast
    {
        public Quake()
        {
            Name = "Quake";
            AroundCaster = true;
            Damage = 60.0f;
        }
    }

    [ObjectId("A0B3")]
    public sealed class Storm : PulseChannel
    {
        public Storm()
        {
            Name = "Storm";
            Damage = 20.0f;
        }
    }

    [ObjectId("A0B4")]
    public sealed class Shockwave : Wave
    {
        public Shockwave()
        {
            Name = "Shockwave";
        }
    }

    [ObjectId("A0B5")]
    public sealed class Rush : Dash
    {
        public Rush()
        {
            Name = "Rush";
        }
    }

    [ObjectId("A0B6")]
    public sealed class Pounce : Leap
    {
        public Pounce()
        {
            Name = "Pounce";
        }
    }

    [ObjectId("A0B7")]
    public sealed class Zap : Chain
    {
        public Zap()
        {
            Name = "Zap";
        }
    }

    [ObjectId("h0B1")]
    public sealed class Squire : Human.Units.Footman
    {
        public Squire()
        {
            Name = "Squire";
        }
    }

    [ObjectId("A0B8")]
    public sealed class CallSquires : Summon<Squire>
    {
        public CallSquires()
        {
            Name = "Call Squires";
            Count = 2;
        }
    }
}
