#if !MOONWELL_LUA
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Moonwell
{
    /// <summary>
    /// A unit type's numbers read from its object data, on .NET only: the default object of the map's class registered for
    /// the type, else a new object of the Moonwell.Objects standard class of its id. Moonwell.Objects is found by name, as
    /// this package does not reference it; without it, or for a type with neither, there are no numbers.
    /// </summary>
    internal static class ObjectNumbers
    {
        /// <summary>The standard unit and hero classes of Moonwell.Objects by id, read once.</summary>
        private static Dictionary<int, Type>? standards;

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
                if (!StandardsOf(standardUnit, standardHero).TryGetValue(typeId, out var standard))
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

        /// <summary>The unit and hero classes of Moonwell.Objects marked [Standard], by the id; the attribute is read by name.</summary>
        private static Dictionary<int, Type> StandardsOf(Type standardUnit, Type standardHero)
        {
            if (standards != null)
            {
                return standards;
            }

            var found = new Dictionary<int, Type>();
            foreach (var type in standardUnit.Assembly.GetTypes())
            {
                if (type.IsAbstract || !(standardUnit.IsAssignableFrom(type) || standardHero.IsAssignableFrom(type)))
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

            standards = found;
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
}
#endif
