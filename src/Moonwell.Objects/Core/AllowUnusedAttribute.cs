using System;

namespace Moonwell.Objects
{
    /// <summary>Keeps the build quiet about a declared class that no other object lists, no code names and the map places none of: kept for later, or used in a way the build doesn't see.</summary>
    /// @CSharpLua.Ignore
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AllowUnusedAttribute : Attribute
    {
    }
}
