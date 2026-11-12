using System;

namespace Moonwell.Objects
{
    /// <summary>Keeps the build quiet about clashes on a declared unit's command card and build menu: two buttons in one place or with one hotkey, two abilities with one order, more buttons than places.</summary>
    /// @CSharpLua.Ignore
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AllowClashAttribute : Attribute
    {
    }
}
