#if !MOONWELL_LUA

using System;

namespace Moonwell.Objects
{
    /// <summary>
    /// On a choice field's property: the numbers of the enum's values World Editor lists for the field, when it lists only
    /// some (<c>Moonwell.WeaponSound</c> holds every sound a hit can make, and a unit's weapon takes 13). Any other value is
    /// an error, which <see cref="BeyondLimitsAttribute"/> does not lift.
    /// </summary>
    /// @CSharpLua.Ignore
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class ListedValuesAttribute : Attribute
    {
        /// <summary>The numbers of the values listed.</summary>
        public int[] Numbers { get; }

        /// <summary>Marks a property with the numbers of the values World Editor lists.</summary>
        public ListedValuesAttribute(params int[] numbers)
        {
            Numbers = numbers;
        }
    }
}

#endif
