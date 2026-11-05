using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// A save's check and its mixing (decision 183): two polynomial hashes, modulo the primes 65521 and 65519, of the map's
    /// key, the save class's id, the player's name and the save's bytes. Each step stays below 2^31, so the game's 32-bit
    /// integers give what .NET and the desktop Lua give.
    /// </summary>
    internal sealed class SaveCheck
    {
        // Above any byte, so that the texts hashed one after another cannot run into each other.
        private const int Between = 256;

        private int first;
        private int second;

        /// <summary>The check of a key, a class's id, a name and a save's bytes, as four bytes.</summary>
        internal static List<int> Of(string key, string id, string name, List<int> body, int start)
        {
            var check = new SaveCheck();
            check.AddText(key);
            check.AddText(id);
            check.AddText(name);
            for (var i = start; i < body.Count; i++)
            {
                check.Add(body[i]);
            }

            return new List<int> { check.first >> 8, check.first & 255, check.second >> 8, check.second & 255 };
        }

        /// <summary>
        /// Mixes bytes from <paramref name="start"/> on with a stream from the key and the check, in place; mixing again undoes
        /// it. As the check changes with every byte of the save, so does every mixed byte, and a code shows no plain numbers.
        /// </summary>
        internal static void Mix(string key, List<int> check, List<int> bytes, int start)
        {
            var seed = new SaveCheck();
            seed.AddText(key);
            for (var i = 0; i < check.Count; i++)
            {
                seed.Add(check[i]);
            }

            for (var i = start; i < bytes.Count; i++)
            {
                seed.Add(i & 255);
                bytes[i] = bytes[i] ^ ((seed.first ^ seed.second) & 255);
            }
        }

        private void AddText(string text)
        {
            var bytes = Utf8.Encode(text);
            for (var i = 0; i < bytes.Count; i++)
            {
                Add(bytes[i]);
            }

            Add(Between);
        }

        private void Add(int value)
        {
            first = (first * 257 + value) % 65521;
            second = (second * 263 + value) % 65519;
        }
    }
}
