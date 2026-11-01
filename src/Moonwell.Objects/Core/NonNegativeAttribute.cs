#if !MOONWELL_LUA

using System;

namespace Moonwell.Objects
{
    /// <summary>
    /// Marks a number field that allows no value below 0: the metadata forbids negatives, no standard object has one
    /// there, and its lowest allowed value, if it has one, is not below 0. <see cref="BeyondLimitsAttribute"/> never lifts it.
    /// </summary>
    /// @CSharpLua.Ignore
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NonNegativeAttribute : Attribute
    {
    }
}

#endif
