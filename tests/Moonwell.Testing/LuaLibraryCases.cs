namespace Moonwell.Testing
{
    /// <summary>Cases for the map's Lua libraries (decision 255), called through the bindings the build writes from lua/library_cases.lua.</summary>
    public static class LuaLibraryCases
    {
        /// <summary>
        /// A global function's binding has the function's name, which CSharp.lua makes a local of the binding's class; the call
        /// reaches the Lua global, not the binding, which called itself until the stack overflowed (I65; with a return value, a
        /// tail call, it looped for ever). Only Lua has the library, so .NET runs nothing here.
        /// </summary>
        public static void CallThroughBinding(FakeWorld world)
        {
#if MOONWELL_LUA
            Lua.LibraryCases.LibraryCasesDouble(7);
            Expect.Equal(14, Lua.LibraryCases.LibraryCasesCount, "the library's function, then its global read");
            Lua.LibraryCases.LibraryCasesCount = 3;
            Expect.Equal(3, Lua.LibraryCases.LibraryCasesCount, "the library's global, set and read");
#endif
        }
    }
}
