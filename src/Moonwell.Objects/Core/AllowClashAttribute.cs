using System;

namespace Moonwell.Objects
{
    /// <summary>Keeps the build quiet about clashes on a declared unit's command card: two buttons in one place, two abilities with one hotkey or one order.</summary>
    /// @CSharpLua.Ignore
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AllowClashAttribute : Attribute
    {
    }
}
