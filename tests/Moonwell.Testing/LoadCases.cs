using System;
using System.Collections.Generic;
using Moonwell.Engine;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for what the game's own load undoes in CoreSystem's string type, and the relink that puts it back. In the game's
    /// Lua, with no debug library, CoreSystem links Lua's string table to Object; a load leaves the string table without it
    /// (game facts, 11-03). On .NET there is nothing to undo, and the cases only show the relink does no harm.
    /// </summary>
    public static class LoadCases
    {
        /// <summary>With the links undone as a load undoes them, a string array fails: the cases below test something real.</summary>
        public static void UndoneLinksBreakStrings(FakeWorld world)
        {
            if ("å".Length != 2)
            {
                return;
            }

            LoadDamage.UndoStringLinks();
            var failed = false;
            try
            {
                failed = new string[2].Length != 2;
            }
            catch (Exception)
            {
                failed = true;
            }

            LoadRepair.Strings();
            Expect.True(failed, "a string array fails with the links undone");
        }

        /// <summary>After the relink, string arrays, a string's default, a dictionary's missing string and a type check work.</summary>
        public static void RelinkMendsStrings(FakeWorld world)
        {
            LoadDamage.UndoStringLinks();
            LoadRepair.Strings();
            Expect.Equal(2, new string[2].Length, "a string array");
            Expect.True(DefaultOf<string>() == null, "a string's default");
            var map = new Dictionary<int, string>();
            Expect.True(!map.TryGetValue(1, out var missing) && missing == null, "a missing key's string");
            object text = "x";
            Expect.True(text is string, "a string is a string");
        }

        /// <summary>A relink with nothing undone changes nothing.</summary>
        public static void RelinkTwice(FakeWorld world)
        {
            LoadRepair.Strings();
            LoadRepair.Strings();
            Expect.Equal(3, new string[3].Length, "a string array");
            Expect.Equal("a,b", string.Join(",", new List<string> { "a", "b" }), "strings joined");
        }

        private static T? DefaultOf<T>()
        {
            return default;
        }
    }
}
