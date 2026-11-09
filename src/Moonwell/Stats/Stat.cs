namespace Moonwell
{
    /// <summary>
    /// A number a unit or a player has, made of a base and the modifiers on it (decision 221): (base + flat) × (1 + the
    /// percents added up / 100). The game's stats are built in, units' only, and change the unit in the game: damage, armor
    /// and attack speed show as the card's green bonus; the others change the unit's own number, which shows white. A map's
    /// own stat is made once, as a static readonly field, and is a number only the map reads, on units and players.
    /// </summary>
    public sealed class Stat
    {
        // Made at their first use: a public type has no static initializer (StaticSetupTests).
        private static Stat? damage, armor, attackSpeed, moveSpeed, maxLife, maxMana, lifeRegen, manaRegen, strength, agility, intelligence;

        /// <summary>Makes a stat of the map's own, best kept in a static readonly field: public static readonly Stat SpellPower = new Stat("Spell power").</summary>
        public Stat(string name, float baseValue = 0.0f)
        {
            Name = name;
            Base = baseValue;
        }

        private Stat(string name, StatKind kind)
        {
            Name = name;
            Kind = kind;
        }

        /// <summary>The name, for a tooltip or a board.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>A map's own stat's base, which every unit and player starts from; 0 for the game's stats, whose base is the unit's.</summary>
        [LocalSafe]
        public float Base { get; }

        internal StatKind Kind { get; }

        /// <summary>Attack damage: the middle of the white damage, with modifiers shown as the green bonus. Whole numbers.</summary>
        public static Stat Damage => damage ??= new Stat("Damage", StatKind.Damage);

        /// <summary>Armor, with modifiers shown as the green bonus. Whole numbers.</summary>
        public static Stat Armor => armor ??= new Stat("Armor", StatKind.Armor);

        /// <summary>Attack speed in percent points, as the game's "+15% attack speed": no base, so Modify(Stat.AttackSpeed, 15, ...) attacks 15% faster.</summary>
        public static Stat AttackSpeed => attackSpeed ??= new Stat("Attack speed", StatKind.AttackSpeed);

        /// <summary>Move speed, which the game holds to the map's least and most speed (150 to 400 unless the map's gameplay constants say otherwise).</summary>
        public static Stat MoveSpeed => moveSpeed ??= new Stat("Move speed", StatKind.MoveSpeed);

        /// <summary>The most life; a modifier never takes it below 1.</summary>
        public static Stat MaxLife => maxLife ??= new Stat("Life", StatKind.MaxLife);

        /// <summary>The most mana.</summary>
        public static Stat MaxMana => maxMana ??= new Stat("Mana", StatKind.MaxMana);

        /// <summary>Life regeneration a second, the unit's own, to which a hero's strength adds.</summary>
        public static Stat LifeRegen => lifeRegen ??= new Stat("Life regeneration", StatKind.LifeRegen);

        /// <summary>Mana regeneration a second, the unit's own, to which a hero's intelligence adds.</summary>
        public static Stat ManaRegen => manaRegen ??= new Stat("Mana regeneration", StatKind.ManaRegen);

        /// <summary>A hero's strength. Whole numbers.</summary>
        public static Stat Strength => strength ??= new Stat("Strength", StatKind.Strength);

        /// <summary>A hero's agility. Whole numbers.</summary>
        public static Stat Agility => agility ??= new Stat("Agility", StatKind.Agility);

        /// <summary>A hero's intelligence. Whole numbers.</summary>
        public static Stat Intelligence => intelligence ??= new Stat("Intelligence", StatKind.Intelligence);
    }

    /// <summary>Which of the game's stats a Stat is; Own for a map's own.</summary>
    internal enum StatKind
    {
        Own,
        Damage,
        Armor,
        AttackSpeed,
        MoveSpeed,
        MaxLife,
        MaxMana,
        LifeRegen,
        ManaRegen,
        Strength,
        Agility,
        Intelligence,
    }
}
