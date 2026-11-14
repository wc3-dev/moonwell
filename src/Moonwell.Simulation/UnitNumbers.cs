namespace Moonwell
{
    /// <summary>The numbers a Simulation plays a unit type by, as its object data holds them.</summary>
    public sealed class UnitNumbers
    {
        /// <summary>The most life, which a unit starts with.</summary>
        public float Life;

        /// <summary>The move speed a second; 0 or less never moves.</summary>
        public float MoveSpeed;

        /// <summary>The armor.</summary>
        public float Armor;

        /// <summary>How far the attack reaches, centre to centre.</summary>
        public float AttackRange;

        /// <summary>The seconds from one swing to the next, rounded to whole steps.</summary>
        public float AttackCooldown;

        /// <summary>How far an idle unit looks for an enemy to attack; 0 looks for none.</summary>
        public float AcquisitionRange;

        /// <summary>The damage of a swing before the dice.</summary>
        public int Damage;

        /// <summary>How many dice a swing adds.</summary>
        public int DamageDice;

        /// <summary>The sides of each die.</summary>
        public int DamageSides;
    }
}
