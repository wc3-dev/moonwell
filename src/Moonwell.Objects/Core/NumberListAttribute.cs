#if !MOONWELL_LUA

using System;

namespace Moonwell.Objects
{
    /// <summary>On a field's property: the field holds a list of numbers as text, so each part between its commas is a number.</summary>
    /// @CSharpLua.Ignore
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NumberListAttribute : Attribute
    {
    }
}

#endif
