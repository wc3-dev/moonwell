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

        /// <summary>The bonus field set on each unit's abilities, by ability id.</summary>
        private readonly Dictionary<unit, Dictionary<int, float>> bonuses = new Dictionary<unit, Dictionary<int, float>>();

        /// <summary>The abilities whose bonus field is armor's, Idef.</summary>
        private readonly HashSet<int> armorBonuses = new HashSet<int>();

        /// <summary>The armor a unit has without the bonus abilities' fields.</summary>
        public void SetArmor(unit unit, float armor)
        {
            armors[unit] = armor;
        }

        /// <summary>The middle of a unit's white damage.</summary>
        public void SetAverageDamage(unit unit, float damage)
        {
            averageDamages[unit] = damage;
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
