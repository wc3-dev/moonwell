using System;

namespace Moonwell
{
    /// @CSharpLua.Ignore
    /// <summary>The native makes a new handle; the analyzer rejects it inside <c>Local.For</c>.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor)]
    public sealed class CreatesHandleAttribute : Attribute
    {
    }

    /// @CSharpLua.Ignore
    /// <summary>The member changes the game's state; the analyzer rejects it inside <c>Local.For</c>.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor)]
    public sealed class ChangesStateAttribute : Attribute
    {
    }

    /// @CSharpLua.Ignore
    /// <summary>The function changes the game inside its own <c>GetLocalPlayer()</c> block, so it desyncs wherever it
    /// is called; the analyzer rejects every call.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor)]
    public sealed class DesyncsAttribute : Attribute
    {
    }

    /// @CSharpLua.Ignore
    /// <summary>
    /// The member gives a different answer, or runs, on one machine only. The analyzer rejects a call to it outside
    /// <c>Local.For</c>, and checks the body of a map method that carries it as it checks a <c>Local.For</c> block. On a
    /// field or auto-property of the map it says the value is one machine's: code of one machine may write it, and
    /// nothing else may read it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor | AttributeTargets.Field)]
    public sealed class MachineLocalAttribute : Attribute
    {
    }

    /// @CSharpLua.Ignore
    /// <summary>A member of Moonwell's, or of a library a map references, that may be called inside <c>Local.For</c>: it
    /// reads, or changes only what one player sees or hears. On a property it covers the getter; on a class or struct,
    /// every member. The analyzer takes the mark's word: it does not check the member's body.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor)]
    public sealed class LocalSafeAttribute : Attribute
    {
    }

    /// @CSharpLua.Ignore
    /// <summary>Turns the multiplayer analyzer off for one method, its lambdas and local functions included.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor)]
    public sealed class SyncUncheckedAttribute : Attribute
    {
    }

    /// @CSharpLua.Ignore
    /// <summary>A function of one of the map's Lua libraries, which the build's bindings call; the analyzer cannot see into it, so it
    /// rejects it inside <c>Local.For</c>. A function whose comments hold <c>---@localsafe</c> is written without it.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class LuaLibraryAttribute : Attribute
    {
    }
}
