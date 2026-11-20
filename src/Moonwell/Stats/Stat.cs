using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A number a unit or a player has, made of a base and the modifiers on it (decisions 221 and 385): (base + flat) × (1 + the
    /// percents added up / 100) × each multiplier + late flat, then held to the stat's least and most, and rounded when whole.
    /// The game's stats are built in, units' only, and change the unit in the game: damage, armor and attack speed show as the
    /// card's green bonus; the others change the unit's own number, which shows white. A map's own stat is made once, as a static
    /// readonly field, and is a number only the map reads, on units and players. The ready-made stats (Stat.CritChance and the
    /// others) are bare numbers of the same kind, with names, bases and limits, that the map's code and Moonwell's packages read.
    /// </summary>
    public sealed class Stat
    {
        // Made at their first use: a public type has no static initializer (StaticSetupTests).
        private static Stat? damage, armor, attackSpeed, moveSpeed, maxLife, maxMana, lifeRegen, manaRegen, strength, agility, intelligence;

        private static Stat? critChance, critDamage, hitChance, evasion, parry, blockChance, blockAmount, attackPower, spellPower, armorPenetration, spellPenetration;

        private static Stat? areaDamageReduction, barrier, stagger, staggerTime, lifeSteal, manaLeech, healingBonus, cooldownReduction, manaCostReduction, castRange;

        private static Stat? castSpeed, crowdControlReduction, experienceGain, goldBonus, lootBonus, dropRate, itemLevel;

        private static Dictionary<int, Stat>? resistances;

        /// <summary>Every stat made, by name; the first made with a name is the one kept.</summary>
        private static Dictionary<string, Stat>? made;

        /// <summary>The stat whose total on the same holder is this one's most.</summary>
        private Stat? cappedBy;

        /// <summary>The base for the units of a class, by class.</summary>
        private Dictionary<Type, float>? classBases;

        /// <summary>The base for the units of a type id, by id.</summary>
        private Dictionary<int, float>? typeBases;

        /// <summary>The formulas (From and Formula) whose results join this stat's base.</summary>
        private List<StatFormula>? formulas;

        /// <summary>The stats that read this one, through From, Formula or CappedBy: each is worked out again when this one changes.</summary>
        private List<Stat>? readers;

        /// <summary>The game stats a base was set for, for a type or a class, which the units taken in after it must be given in the game; null until one is.</summary>
        private static List<Stat>? gameWithBases;

        /// <summary>Makes a stat of the map's own, best kept in a static readonly field: public static readonly Stat Rage = new Stat("Rage", 0, least: 0, most: 100, whole: true). Every unit and player starts at the base; the total is held to the least and the most when given, and rounded half up when whole. A second stat with a name in use is reported in a development build, and Stat.Named finds the first.</summary>
        public Stat(string name, float baseValue = 0.0f, float? least = null, float? most = null, bool whole = false)
        {
            Name = name;
            Base = baseValue;
            Least = least;
            Most = most;
            Whole = whole;
            Register(this);
        }

        private Stat(string name, StatKind kind)
        {
            Name = name;
            Kind = kind;
            // Never below 0, and a unit's most life never below 1.
            Least = kind == StatKind.MaxLife ? 1.0f : 0.0f;
            Register(this);
        }

        /// <summary>The name, for a tooltip or a board.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>A map's own stat's base, which every unit and player starts from unless it has a base of its own or its type has one (SetBase); 0 for the game's stats, whose base is the unit's.</summary>
        [LocalSafe]
        public float Base { get; }

        /// <summary>The least the total can be; null for no least. A game stat's is 0, and 1 for the most life. Setting it after units have the stat announces nothing and works nobody out again: a unit takes it at the stat's next change on it.</summary>
        [LocalSafe]
        public float? Least { get; set; }

        /// <summary>The most the total can be; null for no most. Held after the least, and together with a CappedBy stat's total, the smaller. Setting it after units have the stat announces nothing and works nobody out again: a unit takes it at the stat's next change on it.</summary>
        [LocalSafe]
        public float? Most { get; set; }

        /// <summary>Whether the total is rounded half up to a whole number.</summary>
        [LocalSafe]
        public bool Whole { get; set; }

        internal StatKind Kind { get; }

        /// <summary>The stat whose total on the same holder is this one's most; null when none.</summary>
        internal Stat? Cap => cappedBy;

        /// <summary>Makes another stat's total on the same holder this stat's most, as energy out of most energy, and gives this stat back. This stat is worked out again when the other changes. A cap that would loop back to this stat, through any mix of CappedBy, From and Formula, is refused and reported.</summary>
        public Stat CappedBy(Stat other)
        {
            if (other == this || other.ReadsFrom(this))
            {
                Errors.Refuse("Stat.CappedBy", Name + " cannot be capped by " + other.Name + ": that stat reads " + Name + " in the end");
                return this;
            }

            cappedBy = other;
            other.AddReader(this);
            return this;
        }

        /// <summary>Makes this stat's base also hold another stat's total times a number, on every unit and player: Stat.MaxLife.From(Stamina, 10) gives 10 most life for each point of stamina. Percents work on it as on the rest of the base. Several From and Formula add up. It is kept current: when the other stat changes on a holder, this one is worked out again there, put in the game if it is one, and its change reaction runs. A game stat may read a map's stat and a map's stat a game stat. A loop (A from B from A, through any mix of From, Formula and CappedBy) is refused and reported, and that call ignored. Gives this stat back.</summary>
        public Stat From(Stat other, float perPoint)
        {
            return Link("Stat.From", new Stat[] { other }, read => read(other) * perPoint);
        }

        /// <summary>The general form of From: what read gives joins this stat's base, on every unit and player. Read is given a function that reads a stat's total on the same holder, and from lists every stat read, so that this stat is worked out again when one changes: Stat.AttackPower.Formula(read => read(Stat.Strength) * 2 + read(Level), Stat.Strength, Level). Several From and Formula add up. A loop is refused and reported, as for From, and that call ignored. Gives this stat back.</summary>
        public Stat Formula(Func<Func<Stat, float>, float> read, params Stat[] from)
        {
            return Link("Stat.Formula", from, read);
        }

        private Stat Link(string label, Stat[] from, Func<Func<Stat, float>, float> read)
        {
            for (var i = 0; i < from.Length; i++)
            {
                if (from[i] == this || from[i].ReadsFrom(this))
                {
                    Errors.Refuse(label, Name + " cannot read " + from[i].Name + ": that stat reads " + Name + " in the end, so the two would never settle; the call was ignored");
                    return this;
                }
            }

            formulas ??= new List<StatFormula>();
            formulas.Add(new StatFormula(read, from));
            for (var i = 0; i < from.Length; i++)
            {
                from[i].AddReader(this);
            }

            return this;
        }

        /// <summary>Whether this stat reads the other, directly or through the stats it reads.</summary>
        private bool ReadsFrom(Stat other)
        {
            if (cappedBy != null && (cappedBy == other || cappedBy.ReadsFrom(other)))
            {
                return true;
            }

            if (formulas != null)
            {
                for (var i = 0; i < formulas.Count; i++)
                {
                    var from = formulas[i].From;
                    for (var j = 0; j < from.Length; j++)
                    {
                        if (from[j] == other || from[j].ReadsFrom(other))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void AddReader(Stat reader)
        {
            readers ??= new List<Stat>();
            if (!readers.Contains(reader))
            {
                readers.Add(reader);
            }
        }

        /// <summary>The stats that read this one; null when none.</summary>
        internal List<Stat>? Readers => readers;

        /// <summary>Whether it has formulas: something of another stat joins its base.</summary>
        internal bool HasFormulas => formulas != null;

        /// <summary>What its formulas give on a holder, added up; read reads a stat's total on that holder.</summary>
        internal float Given(Func<Stat, float> read)
        {
            var sum = 0.0f;
            if (formulas != null)
            {
                for (var i = 0; i < formulas.Count; i++)
                {
                    sum += formulas[i].Read(read);
                }
            }

            return sum;
        }

        /// <summary>The stat made with the name, or null when none was. The first made with a name is the one found. A ready-made or game stat is found by its display name ("Life", "Crit chance", "Fire resistance") even when nothing has used it yet: it is made then.</summary>
        [LocalSafe]
        public static Stat? Named(string name)
        {
            if (made != null && made.TryGetValue(name, out var found))
            {
                return found;
            }

            return ReadyMade(name);
        }

        private static Stat? ReadyMade(string name)
        {
            switch (name)
            {
                case "Damage": return Damage;
                case "Armor": return Armor;
                case "Attack speed": return AttackSpeed;
                case "Move speed": return MoveSpeed;
                case "Life": return MaxLife;
                case "Mana": return MaxMana;
                case "Life regeneration": return LifeRegen;
                case "Mana regeneration": return ManaRegen;
                case "Strength": return Strength;
                case "Agility": return Agility;
                case "Intelligence": return Intelligence;
                case "Crit chance": return CritChance;
                case "Crit damage": return CritDamage;
                case "Hit chance": return HitChance;
                case "Evasion": return Evasion;
                case "Parry": return Parry;
                case "Block chance": return BlockChance;
                case "Block amount": return BlockAmount;
                case "Attack power": return AttackPower;
                case "Spell power": return SpellPower;
                case "Armor penetration": return ArmorPenetration;
                case "Spell penetration": return SpellPenetration;
                case "Area damage reduction": return AreaDamageReduction;
                case "Barrier": return Barrier;
                case "Stagger": return Stagger;
                case "Stagger time": return StaggerTime;
                case "Life steal": return LifeSteal;
                case "Mana leech": return ManaLeech;
                case "Healing bonus": return HealingBonus;
                case "Cooldown reduction": return CooldownReduction;
                case "Mana cost reduction": return ManaCostReduction;
                case "Cast range": return CastRange;
                case "Cast speed": return CastSpeed;
                case "Crowd control reduction": return CrowdControlReduction;
                case "Experience gain": return ExperienceGain;
                case "Gold bonus": return GoldBonus;
                case "Loot bonus": return LootBonus;
                case "Drop rate": return DropRate;
                case "Item level": return ItemLevel;
            }

            // A damage kind's resistance: the kinds are the game's numbers, so each is tried by name.
            for (var i = 0; i < 32; i++)
            {
                if (((DamageKind)i).ToString() + " resistance" == name)
                {
                    return Resistance((DamageKind)i);
                }
            }

            return null;
        }

        /// <summary>Sets the base of a stat for the units of a class, and of the classes that inherit it unless they have one of their own; a unit's own base (SetBase on the unit) comes first. The class is the one the unit came in as. For a game stat, Moonwell owns that stat on those units from then on: this is the number it works from, and the game gets the total (a unit takes it in the game when it comes in, or at its next change of the stat). Set it before the units exist: units already there announce nothing and are not worked out again until the stat next changes on them.</summary>
        public void SetBase<T>(float value)
            where T : Unit
        {
            NoteGameBase();
            classBases ??= new Dictionary<Type, float>();
            classBases[typeof(T)] = value;
        }

        /// <summary>Sets the base of a stat for the units of a type id (hfoo, or a declared class's id), after a unit's own base and a class's. For a game stat, Moonwell owns that stat on those units from then on, as for the class form; units already there announce nothing and are not worked out again until the stat next changes on them.</summary>
        public void SetBase(int typeId, float value)
        {
            NoteGameBase();
            typeBases ??= new Dictionary<int, float>();
            typeBases[typeId] = value;
        }

        /// <summary>The base a unit's class or type gives this stat: the most derived class set that the unit's class is, then the type id the unit came in as.</summary>
        internal bool TypeBaseOf(Unit unit, out float value)
        {
            if (classBases != null)
            {
                var kind = Names.TypeOf(unit);
                Type? best = null;
                foreach (var entry in classBases)
                {
                    if (entry.Key.IsAssignableFrom(kind) && (best == null || best.IsAssignableFrom(entry.Key)))
                    {
                        best = entry.Key;
                    }
                }

                if (best != null)
                {
                    value = classBases[best];
                    return true;
                }
            }

            if (typeBases != null && typeBases.TryGetValue(unit.firstTypeId != 0 ? unit.firstTypeId : unit.DeclaredTypeId, out value))
            {
                return true;
            }

            value = 0.0f;
            return false;
        }

        private void NoteGameBase()
        {
            if (Kind != StatKind.Own)
            {
                gameWithBases ??= new List<Stat>();
                if (!gameWithBases.Contains(this))
                {
                    gameWithBases.Add(this);
                }
            }
        }

        /// <summary>The game stats that have a base for a unit's class or type, which the unit must be given in the game as it comes in; empty when none does.</summary>
        internal static List<Stat> WithTypeBase(Unit unit)
        {
            var found = new List<Stat>();
            if (gameWithBases != null)
            {
                for (var i = 0; i < gameWithBases.Count; i++)
                {
                    if (gameWithBases[i].TypeBaseOf(unit, out var ignored))
                    {
                        found.Add(gameWithBases[i]);
                    }
                }
            }

            return found;
        }

        private static void Register(Stat stat)
        {
            made ??= new Dictionary<string, Stat>();
            if (made.ContainsKey(stat.Name))
            {
                Dev.Report("Stat", "a stat named " + stat.Name + " was made already: Stat.Named finds the first, so give the stats different names");
                return;
            }

            made[stat.Name] = stat;
        }

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

        /// <summary>The chance, in percent points, that a hit the unit gives is a crit: 0 to 100. A bare number for now: the stats package gives it its effect.</summary>
        public static Stat CritChance => critChance ??= new Stat("Crit chance", 0.0f, 0.0f, 100.0f);

        /// <summary>What a crit does, in percent of the hit: base 200, which doubles it; never below 0. A bare number for now.</summary>
        public static Stat CritDamage => critDamage ??= new Stat("Crit damage", 200.0f, 0.0f);

        /// <summary>The chance, in percent points, that an attack the unit makes hits: base 100; never below 0. A bare number for now.</summary>
        public static Stat HitChance => hitChance ??= new Stat("Hit chance", 100.0f, 0.0f);

        /// <summary>The chance, in percent points, that an attack on the unit misses: 0 to 100. A bare number for now.</summary>
        public static Stat Evasion => evasion ??= new Stat("Evasion", 0.0f, 0.0f, 100.0f);

        /// <summary>The chance, in percent points, that a melee attack on the unit is parried: 0 to 100. A bare number for now.</summary>
        public static Stat Parry => parry ??= new Stat("Parry", 0.0f, 0.0f, 100.0f);

        /// <summary>The chance, in percent points, that an attack on the unit is blocked: 0 to 100. A bare number for now.</summary>
        public static Stat BlockChance => blockChance ??= new Stat("Block chance", 0.0f, 0.0f, 100.0f);

        /// <summary>What a block takes off the attack; never below 0. A bare number for now.</summary>
        public static Stat BlockAmount => blockAmount ??= new Stat("Block amount", 0.0f, 0.0f);

        /// <summary>What is added to each attack the unit makes. A bare number for now.</summary>
        public static Stat AttackPower => attackPower ??= new Stat("Attack power");

        /// <summary>What is added to each spell hit the unit makes. A bare number for now.</summary>
        public static Stat SpellPower => spellPower ??= new Stat("Spell power");

        /// <summary>The armor points a hit the unit gives ignores; never below 0. A bare number for now.</summary>
        public static Stat ArmorPenetration => armorPenetration ??= new Stat("Armor penetration", 0.0f, 0.0f);

        /// <summary>The resistance points a hit the unit gives ignores; never below 0. A bare number for now.</summary>
        public static Stat SpellPenetration => spellPenetration ??= new Stat("Spell penetration", 0.0f, 0.0f);

        /// <summary>The percent off hits that come from an area: 0 to 100. A bare number for now.</summary>
        public static Stat AreaDamageReduction => areaDamageReduction ??= new Stat("Area damage reduction", 0.0f, 0.0f, 100.0f);

        /// <summary>What the unit's barriers have left to absorb; never below 0. A bare number for now.</summary>
        public static Stat Barrier => barrier ??= new Stat("Barrier", 0.0f, 0.0f);

        /// <summary>The percent of each hit taken that is dealt over time instead: 0 to 100. A bare number for now.</summary>
        public static Stat Stagger => stagger ??= new Stat("Stagger", 0.0f, 0.0f, 100.0f);

        /// <summary>The seconds a staggered hit is dealt over: base 4; never below 0. A bare number for now.</summary>
        public static Stat StaggerTime => staggerTime ??= new Stat("Stagger time", 4.0f, 0.0f);

        /// <summary>The percent of an attack's landed amount that comes back to the attacker as life; never below 0. A bare number for now.</summary>
        public static Stat LifeSteal => lifeSteal ??= new Stat("Life steal", 0.0f, 0.0f);

        /// <summary>The percent of an attack's landed amount that comes back to the attacker as mana; never below 0. A bare number for now.</summary>
        public static Stat ManaLeech => manaLeech ??= new Stat("Mana leech", 0.0f, 0.0f);

        /// <summary>The percent more on the heals the unit gives; never below -100. A bare number for now.</summary>
        public static Stat HealingBonus => healingBonus ??= new Stat("Healing bonus", 0.0f, -100.0f);

        /// <summary>The percent off the cooldown of the unit's abilities: 0 to 100. A bare number for now.</summary>
        public static Stat CooldownReduction => cooldownReduction ??= new Stat("Cooldown reduction", 0.0f, 0.0f, 100.0f);

        /// <summary>The percent off the mana cost of the unit's abilities: 0 to 100. A bare number for now.</summary>
        public static Stat ManaCostReduction => manaCostReduction ??= new Stat("Mana cost reduction", 0.0f, 0.0f, 100.0f);

        /// <summary>The percent more cast range on the unit's abilities; never below -100. A bare number for now.</summary>
        public static Stat CastRange => castRange ??= new Stat("Cast range", 0.0f, -100.0f);

        /// <summary>The percent more cast speed; never below -90. A bare number for now.</summary>
        public static Stat CastSpeed => castSpeed ??= new Stat("Cast speed", 0.0f, -90.0f);

        /// <summary>The percent off how long a crowd control buff on the unit lasts: 0 to 100. A bare number for now.</summary>
        public static Stat CrowdControlReduction => crowdControlReduction ??= new Stat("Crowd control reduction", 0.0f, 0.0f, 100.0f);

        /// <summary>The percent more experience the unit gets; never below -100. A bare number for now.</summary>
        public static Stat ExperienceGain => experienceGain ??= new Stat("Experience gain", 0.0f, -100.0f);

        /// <summary>The percent more gold the unit gets; never below -100. A bare number for now.</summary>
        public static Stat GoldBonus => goldBonus ??= new Stat("Gold bonus", 0.0f, -100.0f);

        /// <summary>A number for the map's loot: nothing reads it but the map.</summary>
        public static Stat LootBonus => lootBonus ??= new Stat("Loot bonus");

        /// <summary>A number for the map's drops: nothing reads it but the map.</summary>
        public static Stat DropRate => dropRate ??= new Stat("Drop rate");

        /// <summary>A whole number for the map's items: nothing reads it but the map.</summary>
        public static Stat ItemLevel => itemLevel ??= new Stat("Item level", 0.0f, null, null, true);

        /// <summary>The unit's resistance to a kind of damage, in percent points and never above 100: one stat for each kind, made at the first ask and the same one every time. A bare number for now.</summary>
        public static Stat Resistance(DamageKind kind)
        {
            resistances ??= new Dictionary<int, Stat>();
            if (!resistances.TryGetValue((int)kind, out var found))
            {
                found = new Stat(kind.ToString() + " resistance", 0.0f, null, 100.0f);
                resistances[(int)kind] = found;
            }

            return found;
        }
    }

    /// <summary>One From or Formula of a stat: what it reads, and the stats it lists as read.</summary>
    internal sealed class StatFormula
    {
        internal readonly Func<Func<Stat, float>, float> Read;

        internal readonly Stat[] From;

        internal StatFormula(Func<Func<Stat, float>, float> read, Stat[] from)
        {
            Read = read;
            From = from;
        }
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
