using System;

namespace Moonwell
{
    /// <summary>
    /// Saves a public field or property of a save class or a part class under a number (decision 180): renaming and reordering
    /// are safe, and a number is never reused for another field, as old saves still hold its value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SavedAttribute : Attribute
    {
        /// <summary>Saves the member under a number from 1 to 9999.</summary>
        public SavedAttribute(int number)
        {
            Number = number;
        }

        /// <summary>The member's number in the save.</summary>
        public int Number { get; }
    }
}
