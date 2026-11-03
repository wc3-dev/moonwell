namespace Moonwell.Testing
{
    /// <summary>Does to CoreSystem's string type what the game's own load does (game facts, 11-03); in Lua only.</summary>
    public static class LoadDamage
    {
        // Unlinks Lua's string table from Object, and System.String from string, as a load leaves them.
        /// @CSharpLua.Template = "do setmetatable(string, nil); setmetatable(System.String, nil) end"
        public static void UndoStringLinks()
        {
        }
    }
}
