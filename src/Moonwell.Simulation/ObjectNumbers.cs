#if !MOONWELL_LUA
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Moonwell
{
    /// <summary>
    /// A unit type's numbers, and an ability type's, read from its object data, on .NET only: the default object of the map's
    /// class registered for the type (for an ability, a new one), else a new object of the Moonwell.Objects standard class of its
    /// id. Moonwell.Objects is found by name, as this package does not reference it; without it, or for a type with neither,
    /// there are no numbers.
    /// </summary>
    internal static class ObjectNumbers
    {
        /// <summary>The standard unit and hero classes of Moonwell.Objects by id, read once.</summary>
        private static Dictionary<int, Type>? standards;

        /// <summary>The standard ability classes of Moonwell.Objects by id, read once.</summary>
        private static Dictionary<int, Type>? abilityStandards;

        /// <summary>The numbers of a unit type; null when its object data gives none.</summary>
        internal static UnitNumbers? Of(int typeId)
        {
            var standardUnit = Type.GetType("Moonwell.Objects.StandardUnit, Moonwell.Objects");
            var standardHero = standardUnit?.Assembly.GetType("Moonwell.Objects.StandardHero");
            if (standardUnit == null || standardHero == null)
            {
                return null;
            }

            object? declared = null;
            if (ObjectTypes.UnitMaker(typeId) != null)
            {
                declared = ObjectTypes.DefaultOf((Unit)null!, typeId);
            }

            if (declared == null || !(standardUnit.IsInstanceOfType(declared) || standardHero.IsInstanceOfType(declared)))
            {
                standards ??= StandardsOf(standardUnit, standardHero);
                if (!standards.TryGetValue(typeId, out var standard))
                {
                    return null;
                }

                declared = Activator.CreateInstance(standard, true)!;
            }

            return new UnitNumbers
            {
                Life = Convert.ToSingle(Value(declared, "MaxLife")),
                MoveSpeed = Convert.ToSingle(Value(declared, "Speed")),
                Armor = Convert.ToSingle(Value(declared, "Armor")),
                AttackRange = Convert.ToSingle(Value(declared, "AttackRange")),
                AttackCooldown = Convert.ToSingle(Value(declared, "AttackCooldown")),
                AcquisitionRange = Convert.ToSingle(Value(declared, "AcquisitionRange")),
                Damage = Convert.ToInt32(Value(declared, "Damage")),
                DamageDice = Convert.ToInt32(Value(declared, "DamageDice")),
                DamageSides = Convert.ToInt32(Value(declared, "DamageSides")),
            };
        }

        /// <summary>An ability type's order string, and its cooldown and mana cost at each level, read from its object data as a unit type's numbers are; null when it has none.</summary>
        internal static SpellNumbers? SpellOf(int abilityId)
        {
            var standardAbility = Type.GetType("Moonwell.Objects.StandardAbility, Moonwell.Objects");
            if (standardAbility == null)
            {
                return null;
            }

            object? declared = ObjectTypes.AbilityMaker(abilityId)?.Invoke();
            if (declared == null || !standardAbility.IsInstanceOfType(declared))
            {
                abilityStandards ??= StandardsOf(standardAbility, standardAbility);
                if (!abilityStandards.TryGetValue(abilityId, out var standard))
                {
                    return null;
                }

                declared = Activator.CreateInstance(standard, true)!;
            }

            var levels = Math.Max(1, Convert.ToInt32(Value(declared, "LevelCount")));
            var cooldown = Value(declared, "Cooldown");
            var manaCost = Value(declared, "ManaCost");
            var numbers = new SpellNumbers { Order = Value(declared, "Order") as string ?? "", Cooldowns = new float[levels], ManaCosts = new int[levels] };
            for (var level = 1; level <= levels; level++)
            {
                numbers.Cooldowns[level - 1] = Convert.ToSingle(At(cooldown, level));
                numbers.ManaCosts[level - 1] = Convert.ToInt32(At(manaCost, level));
            }

            return numbers;
        }

        /// <summary>A Levels value at a level counted from 1, a counted one's last level past its count; null for none.</summary>
        private static object? At(object? levels, int level)
        {
            if (levels == null)
            {
                return null;
            }

            var type = levels.GetType();
            var count = (int)type.GetProperty("Count")!.GetValue(levels)!;
            return type.GetProperty("Item")!.GetValue(levels, new object[] { count > 0 && level > count ? count : level });
        }

        /// <summary>The classes of Moonwell.Objects under either base marked [Standard], by the id; the attribute is read by name.</summary>
        private static Dictionary<int, Type> StandardsOf(Type first, Type second)
        {
            var found = new Dictionary<int, Type>();
            foreach (var type in first.Assembly.GetTypes())
            {
                if (type.IsAbstract || !(first.IsAssignableFrom(type) || second.IsAssignableFrom(type)))
                {
                    continue;
                }

                foreach (var data in type.GetCustomAttributesData())
                {
                    if (data.AttributeType.FullName == "Moonwell.Objects.StandardAttribute" && data.ConstructorArguments.Count == 1 && data.ConstructorArguments[0].Value is string id && id.Length == 4)
                    {
                        found.TryAdd(ObjectTypes.FourCC(id), type);
                    }
                }
            }

            return found;
        }

        /// <summary>A property's value by name, from the most derived class that declares it; null when none does.</summary>
        private static object? Value(object declared, string name)
        {
            for (var type = declared.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property.GetValue(declared);
                }
            }

            return null;
        }
    }

    /// <summary>An ability type's numbers from its object data, on .NET only: its order string, and its cooldown and mana cost at each level.</summary>
    internal sealed class SpellNumbers
    {
        /// <summary>The order string it is cast with.</summary>
        internal string Order = "";

        /// <summary>The full cooldown at each level, from level 1.</summary>
        internal float[] Cooldowns = Array.Empty<float>();

        /// <summary>The mana cost at each level, from level 1.</summary>
        internal int[] ManaCosts = Array.Empty<int>();

        /// <summary>The index a level index reads, kept within the levels: the last past them.</summary>
        internal static int Index(int count, int levelIndex)
        {
            return levelIndex < 0 ? 0 : levelIndex >= count ? count - 1 : levelIndex;
        }
    }
}
#endif
