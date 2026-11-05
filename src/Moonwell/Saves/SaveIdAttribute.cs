using System;

namespace Moonwell
{
    /// <summary>
    /// Names a save class, so a code of another class fails its check (decision 183). One save class may go without; every
    /// other has an id of its own, never changed, as it is part of each save's check.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SaveIdAttribute : Attribute
    {
        /// <summary>Names the save class.</summary>
        public SaveIdAttribute(string id)
        {
            Id = id;
        }

        /// <summary>The class's id.</summary>
        public string Id { get; }
    }
}
