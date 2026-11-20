using System;
using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>The fake world's stats: armor, damage, move speed, regeneration and the bonus abilities' fields, as the probe of 11-08 saw them.</summary>
    public sealed partial class FakeWorld
    {
        private readonly Dictionary<unit, float> armors = new Dictionary<unit, float>();

        private readonly Dictionary<unit, float> averageDamages = new Dictionary<unit, float>();

        private readonly Dictionary<unit, float> moveSpeeds = new Dictionary<unit, float>();

        private readonly Dictionary<unit, float> lifeRegens = new Dictionary<unit, float>();

        private readonly Dictionary<unit, float> manaRegens = new Dictionary<unit, float>();

        private readonly Dictionary<unit, float> attackCooldowns = new Dictionary<unit, float>();

        private readonly Dictionary<unit, float> acquireRanges = new Dictionary<unit, float>();

        /// <summary>Each unit's first weapon's attack range, which SetAttackRange and AddAttackRange keep with the play's own.</summary>
        private readonly Dictionary<unit, float> attackRanges = new Dictionary<unit, float>();

        /// <summary>The ids of the abilities each unit was ever given a level of, in the order they were first given: the order AbilityIdsOf answers in, which a dictionary does not keep in Lua.</summary>
        private readonly Dictionary<unit, List<int>> givenOrders = new Dictionary<unit, List<int>>();

        /// <summary>The cast ranges set, by unit, ability and level index; 0 when none was set.</summary>
        private readonly Dictionary<unit, Dictionary<long, float>> abilityRanges = new Dictionary<unit, Dictionary<long, float>>();

        /// <summary>The units made structures.</summary>
        private readonly HashSet<unit> structures = new HashSet<unit>();

        /// <summary>The bonus field set on each unit's abilities, by ability id.</summary>
        private readonly Dictionary<unit, Dictionary<int, float>> bonuses = new Dictionary<unit, Dictionary<int, float>>();

        /// <summary>The abilities whose bonus field is armor's, Idef.</summary>
        private readonly HashSet<int> armorBonuses = new HashSet<int>();

        /// <summary>The armor a unit has without the bonus abilities' fields.</summary>
        public void SetArmor(unit unit, float armor)
        {
            armors[unit] = armor;
        }

        /// <summary>The armor a unit has without the bonus abilities' fields: what BlzSetUnitArmor sets.</summary>
        public void SetBaseArmor(unit unit, float armor)
        {
            NoteGone(unit);
            armors[unit] = armor;
        }

        /// <summary>The middle of a unit's white damage, as the number it was set to (the real world keeps it to a whole number less its dice).</summary>
        public void SetAverageDamage(unit unit, float damage)
        {
            averageDamages[unit] = damage;
        }

        /// <summary>The seconds between a unit's attacks.</summary>
        public void SetAttackCooldown(unit unit, float seconds)
        {
            attackCooldowns[unit] = seconds;
        }

        /// <summary>A unit's acquisition range.</summary>
        public void SetAcquireRange(unit unit, float range)
        {
            acquireRanges[unit] = range;
            if (played.TryGetValue(unit, out var unitPlay))
            {
                unitPlay.AcquisitionRange = range;
            }
        }

        /// <summary>A unit's first weapon's attack range; 0 until set.</summary>
        public float AttackRangeOf(unit unit)
        {
            return Kept(attackRanges, unit);
        }

        /// <summary>Adds to a unit's first weapon's attack range, as the game's set through weapon index 1 does, and a played unit's attack follows.</summary>
        public void AddAttackRange(unit unit, float delta)
        {
            SetAttackRange(unit, AttackRangeOf(unit) + delta);
        }

        /// <summary>The cast range set for a unit's ability at a level; 0 until set.</summary>
        public float AbilityRange(unit unit, int abilityId, int levelIndex)
        {
            return abilityRanges.TryGetValue(unit, out var set) && set.TryGetValue(LevelKey(abilityId, levelIndex), out var range) ? range : 0.0f;
        }

        /// <summary>Sets the cast range of a unit's ability at a level.</summary>
        public void SetAbilityRange(unit unit, int abilityId, int levelIndex, float range)
        {
            if (!abilityRanges.TryGetValue(unit, out var set))
            {
                set = new Dictionary<long, float>();
                abilityRanges[unit] = set;
            }

            set[LevelKey(abilityId, levelIndex)] = range;
        }

        /// <summary>The ids of the abilities a unit has now, buffs, carriers and bonus abilities with them, in the order they were first given.</summary>
        public List<int> AbilityIdsOf(unit unit)
        {
            var ids = new List<int>();
            if (givenOrders.TryGetValue(unit, out var order))
            {
                for (var i = 0; i < order.Count; i++)
                {
                    if (AbilityLevel(unit, order[i]) > 0)
                    {
                        ids.Add(order[i]);
                    }
                }
            }

            return ids;
        }

        /// <summary>Makes a unit a structure.</summary>
        public void MakeStructure(unit unit)
        {
            structures.Add(unit);
        }

        /// <summary>Whether a unit was made a structure.</summary>
        public bool IsStructure(unit unit)
        {
            return structures.Contains(unit);
        }

        /// <summary>The bonus field a unit's ability holds; 0 until set.</summary>
        public float BonusOf(unit unit, int abilityId)
        {
            return bonuses.TryGetValue(unit, out var held) && held.TryGetValue(abilityId, out var value) ? value : 0.0f;
        }

        /// <summary>The armor of a unit: what the world gave it and every Idef bonus field of an ability it still has.</summary>
        public float ArmorOf(unit unit)
        {
            var armor = Kept(armors, unit);
            foreach (var id in armorBonuses)
            {
                if (AbilityLevel(unit, id) > 0)
                {
                    armor += BonusOf(unit, id);
                }
            }

            return armor;
        }

        /// <summary>The middle of a unit's white damage; 0 until set.</summary>
        public float AverageDamageOf(unit unit)
        {
            return Kept(averageDamages, unit);
        }

        /// <summary>The seconds between a unit's attacks; 0 until set.</summary>
        public float AttackCooldownOf(unit unit)
        {
            return Kept(attackCooldowns, unit);
        }

        /// <summary>A unit's acquisition range; 0 until set.</summary>
        public float AcquireRangeOf(unit unit)
        {
            return Kept(acquireRanges, unit);
        }

        /// <summary>The move speed of a unit; 0 until set.</summary>
        public float MoveSpeedOf(unit unit)
        {
            return Kept(moveSpeeds, unit);
        }

        /// <summary>Sets the move speed of a unit, held to 150 to 400 as the standard gameplay constants hold it (seen 10-21).</summary>
        public void SetMoveSpeed(unit unit, float speed)
        {
            NoteGone(unit);
            moveSpeeds[unit] = Math.Max(150.0f, Math.Min(400.0f, speed));
        }

        /// <summary>A unit's own regeneration a second; 0 until set.</summary>
        public float RegenOf(unit unit, bool mana)
        {
            return Kept(mana ? manaRegens : lifeRegens, unit);
        }

        /// <summary>Sets a unit's own regeneration a second.</summary>
        public void SetRegen(unit unit, bool mana, float perSecond)
        {
            NoteGone(unit);
            (mana ? manaRegens : lifeRegens)[unit] = perSecond;
        }

        /// <summary>Keeps a bonus field of an ability the unit has, a whole number unless real.</summary>
        public void PutBonus(unit unit, int abilityId, int fieldId, bool real, float value)
        {
            NoteGone(unit);
            if (AbilityLevel(unit, abilityId) == 0)
            {
                return;
            }

            if (!bonuses.TryGetValue(unit, out var held))
            {
                held = new Dictionary<int, float>();
                bonuses[unit] = held;
            }

            held[abilityId] = real ? value : (int)value;
            if (fieldId == ObjectTypes.FourCC("Idef"))
            {
                armorBonuses.Add(abilityId);
            }
        }
    }
}
