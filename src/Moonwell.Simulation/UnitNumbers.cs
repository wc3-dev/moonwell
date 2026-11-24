namespace Moonwell
{
    /// <summary>The numbers a Simulation plays a unit type by, as its object data holds them. A unit takes its range, cooldown, damage, dice and acquisition range when it is made; its life, move speed and armor start from them and are read as they are at each step.</summary>
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

        /// <summary>The mana a unit starts with, at most its most mana.</summary>
        public float Mana;

        /// <summary>The most mana.</summary>
        public float MaxMana;

        /// <summary>The abilities a unit starts with at level 1, by id: on .NET its object data's, a hero's hero abilities left out; none for numbers a test gives.</summary>
        internal int[] Abilities = new int[0];

        /// <summary>The kinds a unit of the type is, from its object data's IsABuilding and classification on .NET; none for numbers a test gives.</summary>
        internal UnitKind[] Kinds = new UnitKind[0];
    }
}
