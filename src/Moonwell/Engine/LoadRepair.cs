namespace Moonwell.Engine
{
    /// <summary>Puts back what the game's own load undoes in CoreSystem; the world runs it when a saved game is loaded.</summary>
    internal static class LoadRepair
    {
        // In the game's Lua, with no debug library, CoreSystem makes the string metatable System.String and links it to Lua's
        // string table, and string to Object. A load keeps their fields but neither link, and gives strings a new metatable
        // (game facts, 11-03): this links string to Object, System.String to string, and the new metatable to System.String,
        // which type checks walk. With debug, System.String is string itself and nothing was undone. A do block, as a statement
        // that starts with a parenthesis would be read as a call of the line before it.
        /// @CSharpLua.Template = "do if not System.debugsetmetatable then local S, M = System.String, getmetatable(''); if getmetatable(string) ~= System.Object then setmetatable(string, System.Object) end if getmetatable(S) ~= string then setmetatable(S, string) end if M ~= S and getmetatable(M) ~= S then setmetatable(M, S) end end end"
        internal static void Strings()
        {
        }
    }
}
